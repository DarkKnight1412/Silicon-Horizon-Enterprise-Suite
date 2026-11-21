/* =============================================================================
   03. ĐƠN HÀNG VÀ THANH TOÁN
   Chạy SAU 02_sp_foundation_catalog_cart.sql.

   Vòng đời đơn (Orders.Status):
     0 PendingPayment, 1 Pending, 2 Confirmed, 3 Packing, 4 ReadyForPickup, 5 Shipping,
     6 DeliveryFailed, 7 Completed, 8 Cancelled, 9 PartiallyReturned, 10 Returned

   Tác động kho (xem BRD mục 6.5):
     Tạo đơn   : Reserved + SL
     Hủy       : Reserved - SL, serial Allocated -> InStock
     Hoàn tất  : Physical - SL, Reserved - SL, serial Allocated -> Sold (bắt đầu bảo hành)

   Lưu ý khi gọi từ .NET: các proc ghi nhiều dòng tồn kho có thể gặp deadlock (lỗi 1205)
   khi nhiều đơn đặt cùng lúc; nên retry 2-3 lần ở tầng API với lỗi 1205.
   ============================================================================= */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =============================================================================
   PROC NỘI BỘ
   ============================================================================= */

/* Đổi trạng thái đơn có kiểm tra chuyển hợp lệ (ORD-07) và ghi lịch sử (ORD-08). */
CREATE OR ALTER PROCEDURE dbo.usp_Internal_SetOrderStatus
    @OrderId bigint, @ToStatus tinyint, @UserId bigint = NULL, @Reason nvarchar(300) = NULL, @FromStatus tinyint OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @FromStatus = NULL;
    SELECT @FromStatus = Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId;
    IF @FromStatus IS NULL THROW 50001, N'Đơn hàng không tồn tại.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM (VALUES (0,1),(0,8),(1,2),(1,8),(2,3),(2,8),(3,4),(3,5),(3,8),(4,7),(4,8),
                              (5,7),(5,6),(6,5),(6,8),(7,9),(7,10),(9,10)) t (FromS, ToS)
         WHERE t.FromS = @FromStatus AND t.ToS = @ToStatus)
    BEGIN
        DECLARE @m nvarchar(200) = CONCAT(N'Không thể chuyển đơn từ trạng thái ', @FromStatus, N' sang ', @ToStatus, N'.');
        THROW 50002, @m, 1;
    END

    UPDATE dbo.Orders SET Status = @ToStatus, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @OrderId;
    INSERT dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, Reason, ChangedBy) VALUES (@OrderId, @FromStatus, @ToStatus, @Reason, @UserId);
END
GO

/* Hủy đơn: nhả tồn, gỡ serial, trả lượt voucher, đảo công nợ. KHÔNG tự mở giao dịch (caller mở). */
CREATE OR ALTER PROCEDURE dbo.usp_Internal_CancelOrder
    @OrderId bigint, @UserId bigint = NULL, @Reason nvarchar(300)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @From tinyint;
    EXEC dbo.usp_Internal_SetOrderStatus @OrderId, 8, @UserId, @Reason, @From OUTPUT;   -- chỉ cho phép từ 0,1,2,3,4,6

    DECLARE @BranchId int, @VoucherId bigint, @Method tinyint, @Grand decimal(18,0), @Paid decimal(18,0), @CustomerId bigint;
    SELECT @BranchId = BranchId, @VoucherId = VoucherId, @Method = PaymentMethod, @Grand = GrandTotal, @Paid = PaidAmount, @CustomerId = CustomerId
      FROM dbo.Orders WHERE Id = @OrderId;

    -- nhả tồn đang giữ
    DECLARE @items int = (SELECT COUNT(*) FROM dbo.OrderItems WHERE OrderId = @OrderId);
    UPDATE sl SET ReservedQty = sl.ReservedQty - oi.Quantity, UpdatedAt = SYSUTCDATETIME()
      FROM dbo.StockLevels sl JOIN dbo.OrderItems oi ON oi.ProductId = sl.ProductId
     WHERE sl.BranchId = @BranchId AND oi.OrderId = @OrderId;
    IF @@ROWCOUNT <> @items THROW 50002, N'Dữ liệu tồn kho không khớp khi nhả hàng giữ.', 1;

    INSERT dbo.StockMovements (BranchId, ProductId, MovementType, PhysicalDelta, ReservedDelta, RefType, RefId, Note, CreatedBy)
    SELECT @BranchId, oi.ProductId, 2, 0, -oi.Quantity, 'Order', @OrderId, @Reason, @UserId
      FROM dbo.OrderItems oi WHERE oi.OrderId = @OrderId;

    -- gỡ serial đã gán
    DECLARE @released TABLE (SerialId bigint);
    UPDATE si SET Status = 0, OrderItemId = NULL, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
    OUTPUT inserted.Id INTO @released (SerialId)
      FROM dbo.SerialItems si JOIN dbo.OrderItems oi ON oi.Id = si.OrderItemId
     WHERE oi.OrderId = @OrderId AND si.Status = 1;
    INSERT dbo.SerialStatusHistory (SerialItemId, FromStatus, ToStatus, RefType, RefId, Note, ChangedBy)
    SELECT SerialId, 1, 0, 'Order', @OrderId, @Reason, @UserId FROM @released;

    -- trả lượt voucher
    IF @VoucherId IS NOT NULL
    BEGIN
        UPDATE dbo.Vouchers SET UsedCount = UsedCount - 1 WHERE Id = @VoucherId AND UsedCount > 0;
        DELETE dbo.VoucherUsages WHERE OrderId = @OrderId;
    END

    -- đảo công nợ (đơn mua chịu): phần chưa thanh toán
    IF @Method = 4 AND @Grand - @Paid > 0
        INSERT dbo.CustomerLedger (CustomerId, OrderId, EntryType, Amount, CreatedBy) VALUES (@CustomerId, @OrderId, 2, -(@Grand - @Paid), @UserId);

    DECLARE @new nvarchar(20) = N'Cancelled';
    EXEC dbo.usp_Audit_Write @UserId = @UserId, @BranchId = @BranchId, @Action = 'OrderCancelled', @EntityType = 'Order',
         @EntityId = @OrderId, @NewValues = @new, @Reason = @Reason;
END
GO

/* =============================================================================
   TẠO ĐƠN (UC-04, UC-09)
   - Online: @ItemsJson = NULL, lấy các dòng được tick trong giỏ của khách; xóa khỏi giỏ sau khi đặt.
   - Tại quầy (Channel = 1): bắt buộc @ItemsJson = [{"productId":1,"quantity":2}]
   - @SalesStaffJson: [{"employeeId":5,"shareRate":70},{"employeeId":6,"shareRate":30}] (tổng 100).
     Đơn tại quầy không truyền thì mặc định gán nhân viên tạo đơn 100%. Đơn online không gán (KPI-02).
   - Giá chốt tại đây và snapshot vào OrderItems (ORD-04, CART-10).
   - Giữ hàng an toàn khi đặt đồng thời (ORD-10): khóa dòng tồn kho rồi mới tăng Reserved.
   Trạng thái đầu: PendingPayment (online, chuyển khoản/cổng) có ExpiresAt; còn lại Pending.
   ============================================================================= */
CREATE OR ALTER PROCEDURE dbo.usp_Order_Create
    @CustomerId bigint, @BranchId int, @Channel tinyint, @FulfillmentType tinyint,
    @RecipientName nvarchar(150) = NULL, @RecipientPhone varchar(20) = NULL, @ShippingAddress nvarchar(500) = NULL,
    @ShippingFee decimal(18,0) = 0, @PaymentMethod tinyint, @VoucherCode varchar(40) = NULL,
    @ItemsJson nvarchar(max) = NULL, @SalesStaffJson nvarchar(max) = NULL, @Note nvarchar(500) = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @Today date = dbo.fn_VnDate(SYSUTCDATETIME());
        DECLARE @GroupId int, @CustActive bit;
        SELECT @GroupId = CustomerGroupId, @CustActive = IsActive FROM dbo.Customers WHERE Id = @CustomerId;
        IF @GroupId IS NULL THROW 50001, N'Khách hàng không tồn tại.', 1;
        IF @CustActive = 0 THROW 50002, N'Tài khoản khách hàng đã bị khóa.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Id = @BranchId AND IsActive = 1) THROW 50001, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
        IF @Channel NOT IN (0, 1) OR @FulfillmentType NOT IN (0, 1) OR @PaymentMethod NOT BETWEEN 0 AND 4 THROW 50004, N'Tham số kênh bán, hình thức nhận hàng hoặc phương thức thanh toán không hợp lệ.', 1;
        IF @ShippingFee < 0 THROW 50004, N'Phí vận chuyển không hợp lệ.', 1;
        IF @FulfillmentType = 0 AND (@ShippingAddress IS NULL OR @RecipientName IS NULL OR @RecipientPhone IS NULL)
            THROW 50004, N'Giao tận nơi cần đủ tên, số điện thoại và địa chỉ người nhận.', 1;
        IF @Channel = 0 AND @PaymentMethod = 3 THROW 50004, N'Tiền mặt/POS chỉ dùng cho đơn tại quầy.', 1;
        IF @Channel = 1 AND @ItemsJson IS NULL THROW 50004, N'Đơn tại quầy phải truyền danh sách sản phẩm.', 1;

        -- 1. Danh sách hàng
        CREATE TABLE #Items (ProductId bigint PRIMARY KEY, Quantity int NOT NULL);
        DECLARE @FromCart bit = CASE WHEN @ItemsJson IS NULL THEN 1 ELSE 0 END;
        IF @FromCart = 1
            INSERT #Items (ProductId, Quantity)
            SELECT ci.ProductId, ci.Quantity FROM dbo.Carts c JOIN dbo.CartItems ci ON ci.CartId = c.Id
             WHERE c.CustomerId = @CustomerId AND ci.IsSelected = 1;
        ELSE
            INSERT #Items (ProductId, Quantity)
            SELECT productId, SUM(quantity) FROM OPENJSON(@ItemsJson) WITH (productId bigint '$.productId', quantity int '$.quantity') GROUP BY productId;

        IF NOT EXISTS (SELECT 1 FROM #Items) THROW 50004, N'Không có sản phẩm nào để đặt hàng.', 1;
        IF EXISTS (SELECT 1 FROM #Items WHERE Quantity IS NULL OR Quantity <= 0) THROW 50004, N'Số lượng phải lớn hơn 0.', 1;
        IF EXISTS (SELECT 1 FROM #Items i LEFT JOIN dbo.Products p ON p.Id = i.ProductId AND p.Status = 1 WHERE p.Id IS NULL)
            THROW 50004, N'Có sản phẩm không tồn tại hoặc đã ngừng kinh doanh.', 1;

        -- 2. Tính giá chốt
        SELECT i.ProductId, i.Quantity, p.Sku, p.Name AS ProductName, p.WarrantyMonths, p.RequiresSerial,
               pr.BasePrice AS UnitPrice, pr.PromotionId, pr.DiscountPerUnit * i.Quantity AS LineDiscount,
               i.Quantity * pr.BasePrice AS LineGross
          INTO #P
          FROM #Items i JOIN dbo.Products p ON p.Id = i.ProductId
          CROSS APPLY dbo.fn_ProductPrice(i.ProductId, @GroupId, i.Quantity, @Now) pr;

        DECLARE @Sub decimal(18,0), @PromoDisc decimal(18,0), @HasPromo bit;
        SELECT @Sub = SUM(LineGross), @PromoDisc = SUM(LineDiscount), @HasPromo = MAX(CASE WHEN PromotionId IS NOT NULL THEN 1 ELSE 0 END) FROM #P;

        -- 3. Voucher
        DECLARE @VoucherId bigint, @VoucherDisc decimal(18,0) = 0, @Err nvarchar(200);
        IF @VoucherCode IS NOT NULL
        BEGIN
            SELECT @VoucherId = VoucherId, @Err = ErrorMessage, @VoucherDisc = DiscountAmount
              FROM dbo.fn_VoucherCheck(@VoucherCode, @GroupId, @Sub - @PromoDisc, @HasPromo, @Now);
            IF @Err IS NOT NULL THROW 50004, @Err, 1;
        END

        DECLARE @Discount decimal(18,0) = @PromoDisc + @VoucherDisc;
        DECLARE @Grand decimal(18,0) = @Sub - @Discount + @ShippingFee;

        -- 4. Mua chịu: hạn mức và nợ quá hạn (PRC-08)
        IF @PaymentMethod = 4
        BEGIN
            DECLARE @Limit decimal(18,0), @Term int, @Owed decimal(18,0), @Overdue decimal(18,0);
            SELECT @Limit = CreditLimit, @Term = PaymentTermDays FROM dbo.Customers WHERE Id = @CustomerId;
            SELECT @Owed = ISNULL(SUM(Amount), 0) FROM dbo.CustomerLedger WHERE CustomerId = @CustomerId;
            SELECT @Overdue = CASE WHEN x > 0 THEN x ELSE 0 END
              FROM (SELECT ISNULL(SUM(CASE WHEN EntryType = 0 AND DueDate < @Today THEN Amount ELSE 0 END), 0)
                         + ISNULL(SUM(CASE WHEN Amount < 0 THEN Amount ELSE 0 END), 0) AS x
                      FROM dbo.CustomerLedger WHERE CustomerId = @CustomerId) t;   -- giả định khoản thanh toán trừ vào nợ cũ nhất trước
            IF @Limit <= 0 THROW 50006, N'Khách hàng chưa được cấp hạn mức công nợ.', 1;
            IF @Overdue > 0 THROW 50006, N'Khách hàng đang có công nợ quá hạn.', 1;
            IF @Owed + @Grand > @Limit THROW 50006, N'Vượt hạn mức công nợ được cấp.', 1;
        END

        -- 5. Trạng thái đầu và hạn giữ hàng (ORD-11)
        DECLARE @Status tinyint = CASE WHEN @Channel = 0 AND @PaymentMethod IN (1, 2) THEN 0 ELSE 1 END;
        DECLARE @GwMin int = ISNULL(TRY_CAST((SELECT [Value] FROM dbo.SystemSettings WHERE [Key] = 'hold.gateway.minutes') AS int), 30);
        DECLARE @TrHrs int = ISNULL(TRY_CAST((SELECT [Value] FROM dbo.SystemSettings WHERE [Key] = 'hold.transfer.hours') AS int), 24);
        DECLARE @Expires datetime2(0) = CASE WHEN @Status <> 0 THEN NULL
                                              WHEN @PaymentMethod = 2 THEN DATEADD(MINUTE, @GwMin, @Now)
                                              ELSE DATEADD(HOUR, @TrHrs, @Now) END;

        -- 6. Nhân viên tư vấn
        CREATE TABLE #Staff (EmployeeId bigint PRIMARY KEY, ShareRate decimal(5,2) NOT NULL);
        IF @SalesStaffJson IS NOT NULL
            INSERT #Staff (EmployeeId, ShareRate) SELECT employeeId, shareRate FROM OPENJSON(@SalesStaffJson) WITH (employeeId bigint '$.employeeId', shareRate decimal(5,2) '$.shareRate');
        ELSE IF @Channel = 1 AND @UserId IS NOT NULL
            INSERT #Staff (EmployeeId, ShareRate) SELECT Id, 100 FROM dbo.Employees WHERE UserId = @UserId AND Status = 0;
        IF EXISTS (SELECT 1 FROM #Staff)
        BEGIN
            IF (SELECT SUM(ShareRate) FROM #Staff) <> 100 THROW 50004, N'Tổng tỷ lệ chia của nhân viên tư vấn phải bằng 100.', 1;
            IF EXISTS (SELECT 1 FROM #Staff s LEFT JOIN dbo.Employees e ON e.Id = s.EmployeeId AND e.Status = 0 WHERE e.Id IS NULL)
                THROW 50004, N'Nhân viên tư vấn không tồn tại hoặc đã nghỉ việc.', 1;
        END

        DECLARE @OrderId bigint, @Code varchar(30);
        BEGIN TRANSACTION;

            -- 7. Khóa và kiểm tra tồn, sau đó giữ hàng
            DECLARE @Short nvarchar(250);
            SELECT TOP 1 @Short = p.Name
              FROM #Items i JOIN dbo.Products p ON p.Id = i.ProductId
              LEFT JOIN dbo.StockLevels sl WITH (UPDLOCK, HOLDLOCK) ON sl.BranchId = @BranchId AND sl.ProductId = i.ProductId
             WHERE ISNULL(sl.PhysicalQty - sl.ReservedQty, 0) < i.Quantity;
            IF @Short IS NOT NULL
            BEGIN
                DECLARE @m nvarchar(400) = CONCAT(N'Không đủ hàng tại chi nhánh cho sản phẩm: ', @Short, N'.');
                THROW 50003, @m, 1;
            END

            UPDATE sl SET ReservedQty = sl.ReservedQty + i.Quantity, UpdatedAt = @Now
              FROM dbo.StockLevels sl JOIN #Items i ON i.ProductId = sl.ProductId
             WHERE sl.BranchId = @BranchId AND sl.PhysicalQty - sl.ReservedQty >= i.Quantity;
            IF @@ROWCOUNT <> (SELECT COUNT(*) FROM #Items) THROW 50003, N'Không đủ hàng để giữ cho đơn.', 1;

            -- 8. Đơn và chi tiết
            DECLARE @Seq bigint = NEXT VALUE FOR dbo.seq_Order;
            SET @Code = dbo.fn_FormatCode('DH', @Seq, ABS(CHECKSUM(NEWID())));

            INSERT dbo.Orders (OrderCode, CustomerId, BranchId, Channel, FulfillmentType, Status, RecipientName, RecipientPhone, ShippingAddress,
                               SubTotal, DiscountTotal, ShippingFee, GrandTotal, PaidAmount, PaymentMethod, PaymentStatus, VoucherId, ExpiresAt, Note, CreatedBy)
            VALUES (@Code, @CustomerId, @BranchId, @Channel, @FulfillmentType, @Status, @RecipientName, @RecipientPhone, @ShippingAddress,
                    @Sub, @Discount, @ShippingFee, @Grand, 0, @PaymentMethod, 0, @VoucherId, @Expires, @Note, @UserId);
            SET @OrderId = SCOPE_IDENTITY();

            INSERT dbo.OrderItems (OrderId, ProductId, Sku, ProductName, UnitPrice, DiscountAmount, Quantity, LineTotal, WarrantyMonths, RequiresSerial, PromotionId)
            SELECT @OrderId, ProductId, Sku, ProductName, UnitPrice, LineDiscount, Quantity, LineGross - LineDiscount, WarrantyMonths, RequiresSerial, PromotionId FROM #P;

            INSERT dbo.StockMovements (BranchId, ProductId, MovementType, PhysicalDelta, ReservedDelta, RefType, RefId, CreatedBy)
            SELECT @BranchId, ProductId, 1, 0, Quantity, 'Order', @OrderId, @UserId FROM #Items;

            INSERT dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, Reason, ChangedBy) VALUES (@OrderId, NULL, @Status, N'Tạo đơn', @UserId);

            INSERT dbo.OrderSalesStaff (OrderId, EmployeeId, ShareRate, IsPrimary)
            SELECT @OrderId, EmployeeId, ShareRate, CASE WHEN ROW_NUMBER() OVER (ORDER BY ShareRate DESC, EmployeeId) = 1 THEN 1 ELSE 0 END FROM #Staff;

            -- 9. Voucher
            IF @VoucherId IS NOT NULL
            BEGIN
                UPDATE dbo.Vouchers SET UsedCount = UsedCount + 1 WHERE Id = @VoucherId AND (MaxUses IS NULL OR UsedCount < MaxUses);
                IF @@ROWCOUNT = 0 THROW 50006, N'Mã giảm giá đã hết lượt dùng.', 1;
                INSERT dbo.VoucherUsages (VoucherId, OrderId, CustomerId, DiscountAmount) VALUES (@VoucherId, @OrderId, @CustomerId, @VoucherDisc);
            END

            -- 10. Công nợ khách (mua chịu)
            IF @PaymentMethod = 4
                INSERT dbo.CustomerLedger (CustomerId, OrderId, EntryType, Amount, DueDate, CreatedBy)
                VALUES (@CustomerId, @OrderId, 0, @Grand, DATEADD(DAY, @Term, @Today), @UserId);

            -- 11. Xóa các dòng đã đặt khỏi giỏ (CART-05)
            IF @FromCart = 1
                DELETE ci FROM dbo.CartItems ci JOIN dbo.Carts c ON c.Id = ci.CartId JOIN #Items i ON i.ProductId = ci.ProductId
                 WHERE c.CustomerId = @CustomerId AND ci.IsSelected = 1;
        COMMIT TRANSACTION;

        SELECT @OrderId AS OrderId, @Code AS OrderCode, @Status AS Status, @Grand AS GrandTotal, @Expires AS ExpiresAt;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* GET /api/orders/{id}  hoặc  /api/orders/by-code/{code}
   Trả: header, dòng hàng (kèm số serial đã gán), serial, thanh toán, lịch sử trạng thái, nhân viên tư vấn */
CREATE OR ALTER PROCEDURE dbo.usp_Order_Get
    @OrderId bigint = NULL, @OrderCode varchar(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @OrderId IS NULL SELECT @OrderId = Id FROM dbo.Orders WHERE OrderCode = @OrderCode;
    IF @OrderId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE Id = @OrderId) THROW 50001, N'Đơn hàng không tồn tại.', 1;

    SELECT o.*, br.Name AS BranchName, c.FullName AS CustomerName, c.Phone AS CustomerPhone
      FROM dbo.Orders o JOIN dbo.Branches br ON br.Id = o.BranchId JOIN dbo.Customers c ON c.Id = o.CustomerId
     WHERE o.Id = @OrderId;

    SELECT oi.*, (SELECT COUNT(*) FROM dbo.SerialItems s WHERE s.OrderItemId = oi.Id AND s.Status IN (1, 3)) AS SerialAssignedCount
      FROM dbo.OrderItems oi WHERE oi.OrderId = @OrderId;

    SELECT s.Id AS SerialItemId, s.OrderItemId, s.SerialNumber, s.Status, s.WarrantyStartDate, s.WarrantyEndDate
      FROM dbo.SerialItems s JOIN dbo.OrderItems oi ON oi.Id = s.OrderItemId WHERE oi.OrderId = @OrderId;

    SELECT * FROM dbo.Payments WHERE OrderId = @OrderId ORDER BY CreatedAt;
    SELECT h.FromStatus, h.ToStatus, h.Reason, h.ChangedBy, h.ChangedAt FROM dbo.OrderStatusHistory h WHERE h.OrderId = @OrderId ORDER BY h.ChangedAt, h.Id;
    SELECT ss.EmployeeId, e.FullName, ss.ShareRate, ss.IsPrimary FROM dbo.OrderSalesStaff ss JOIN dbo.Employees e ON e.Id = ss.EmployeeId WHERE ss.OrderId = @OrderId;
END
GO

/* GET /api/orders  - danh sách có lọc (khách: truyền CustomerId; nhân viên: BranchId, Status...) */
CREATE OR ALTER PROCEDURE dbo.usp_Order_List
    @CustomerId bigint = NULL, @BranchId int = NULL, @Status tinyint = NULL,
    @FromDate datetime2(0) = NULL, @ToDate datetime2(0) = NULL, @Keyword nvarchar(50) = NULL,
    @PageNumber int = 1, @PageSize int = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    IF @PageSize > 100 SET @PageSize = 100;

    SELECT o.Id, o.OrderCode, o.CustomerId, c.FullName AS CustomerName, c.Phone AS CustomerPhone, o.BranchId, o.Channel, o.FulfillmentType,
           o.Status, o.GrandTotal, o.PaidAmount, o.PaymentMethod, o.PaymentStatus, o.CreatedAt, o.ExpiresAt,
           COUNT(*) OVER () AS TotalCount
      FROM dbo.Orders o JOIN dbo.Customers c ON c.Id = o.CustomerId
     WHERE (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
       AND (@BranchId IS NULL OR o.BranchId = @BranchId)
       AND (@Status IS NULL OR o.Status = @Status)
       AND (@FromDate IS NULL OR o.CreatedAt >= @FromDate)
       AND (@ToDate IS NULL OR o.CreatedAt < @ToDate)
       AND (@Keyword IS NULL OR o.OrderCode LIKE @Keyword + N'%' OR c.Phone LIKE @Keyword + N'%' OR c.FullName LIKE N'%' + @Keyword + N'%')
     ORDER BY o.CreatedAt DESC, o.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* =============================================================================
   XỬ LÝ ĐƠN: xác nhận, đóng gói, giao, hoàn tất, hủy
   ============================================================================= */

/* POST /api/orders/{id}/confirm   (UC-10)  Pending -> Confirmed */
CREATE OR ALTER PROCEDURE dbo.usp_Order_Confirm @OrderId bigint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @From tinyint;
        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_SetOrderStatus @OrderId, 2, @UserId, N'Xác nhận đơn', @From OUTPUT;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/orders/{id}/start-packing   Confirmed -> Packing */
CREATE OR ALTER PROCEDURE dbo.usp_Order_StartPacking @OrderId bigint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @From tinyint;
        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_SetOrderStatus @OrderId, 3, @UserId, N'Bắt đầu đóng gói', @From OUTPUT;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/orders/{id}/serials   (UC-11)  quét serial gán vào đơn: InStock -> Allocated
   Tiến độ quét trả về theo từng dòng hàng. */
CREATE OR ALTER PROCEDURE dbo.usp_Order_AssignSerial
    @OrderId bigint, @SerialNumber varchar(100), @ProductId bigint = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @OStatus tinyint, @BranchId int;
        SELECT @OStatus = Status, @BranchId = BranchId FROM dbo.Orders WHERE Id = @OrderId;
        IF @OStatus IS NULL THROW 50001, N'Đơn hàng không tồn tại.', 1;
        IF @OStatus <> 3 THROW 50002, N'Chỉ quét serial khi đơn đang ở trạng thái đóng gói.', 1;

        DECLARE @SerialId bigint, @SStatus tinyint, @SBranch int, @OrderItemId bigint, @rc int;
        SELECT @SerialId = si.Id, @SStatus = si.Status, @SBranch = si.BranchId, @OrderItemId = oi.Id
          FROM dbo.SerialItems si
          JOIN dbo.OrderItems oi ON oi.ProductId = si.ProductId AND oi.OrderId = @OrderId AND oi.RequiresSerial = 1
         WHERE si.SerialNumber = @SerialNumber AND (@ProductId IS NULL OR si.ProductId = @ProductId);
        SET @rc = @@ROWCOUNT;

        IF @rc = 0 THROW 50001, N'Serial không tồn tại hoặc không thuộc sản phẩm nào trong đơn.', 1;
        IF @rc > 1 THROW 50004, N'Serial trùng nhiều sản phẩm trong đơn, hãy truyền ProductId.', 1;
        IF @SStatus <> 0 THROW 50002, N'Serial không ở trạng thái trong kho (InStock).', 1;
        IF @SBranch <> @BranchId THROW 50002, N'Serial không thuộc chi nhánh xuất hàng của đơn.', 1;
        IF (SELECT COUNT(*) FROM dbo.SerialItems WHERE OrderItemId = @OrderItemId AND Status IN (1, 3)) >= (SELECT Quantity FROM dbo.OrderItems WHERE Id = @OrderItemId)
            THROW 50006, N'Đã quét đủ serial cho dòng hàng này.', 1;

        BEGIN TRANSACTION;
            UPDATE dbo.SerialItems SET Status = 1, OrderItemId = @OrderItemId, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
             WHERE Id = @SerialId AND Status = 0;
            IF @@ROWCOUNT = 0 THROW 50002, N'Serial vừa được sử dụng bởi thao tác khác.', 1;
            EXEC dbo.usp_Internal_LogSerial @SerialId, 0, 1, 'Order', @OrderId, NULL, @UserId;
        COMMIT TRANSACTION;

        SELECT oi.Id AS OrderItemId, oi.ProductId, oi.Quantity,
               (SELECT COUNT(*) FROM dbo.SerialItems s WHERE s.OrderItemId = oi.Id AND s.Status IN (1, 3)) AS Assigned
          FROM dbo.OrderItems oi WHERE oi.OrderId = @OrderId AND oi.RequiresSerial = 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* DELETE /api/orders/{id}/serials/{serialNumber}   gỡ gán nhầm (SER-02), có lý do và Audit */
CREATE OR ALTER PROCEDURE dbo.usp_Order_UnassignSerial
    @OrderId bigint, @SerialNumber varchar(100), @Reason nvarchar(300), @ProductId bigint = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N'' THROW 50004, N'Phải nhập lý do gỡ serial.', 1;
        DECLARE @OStatus tinyint, @BranchId int;
        SELECT @OStatus = Status, @BranchId = BranchId FROM dbo.Orders WHERE Id = @OrderId;
        IF @OStatus IS NULL THROW 50001, N'Đơn hàng không tồn tại.', 1;
        IF @OStatus <> 3 THROW 50002, N'Chỉ gỡ serial khi đơn đang ở trạng thái đóng gói.', 1;

        DECLARE @SerialId bigint, @rc int;
        SELECT @SerialId = si.Id
          FROM dbo.SerialItems si JOIN dbo.OrderItems oi ON oi.Id = si.OrderItemId
         WHERE oi.OrderId = @OrderId AND si.SerialNumber = @SerialNumber AND si.Status = 1 AND (@ProductId IS NULL OR si.ProductId = @ProductId);
        SET @rc = @@ROWCOUNT;
        IF @rc = 0 THROW 50001, N'Serial không được gán vào đơn này.', 1;
        IF @rc > 1 THROW 50004, N'Serial trùng nhiều sản phẩm, hãy truyền ProductId.', 1;

        BEGIN TRANSACTION;
            UPDATE dbo.SerialItems SET Status = 0, OrderItemId = NULL, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @SerialId;
            EXEC dbo.usp_Internal_LogSerial @SerialId, 1, 0, 'Order', @OrderId, @Reason, @UserId;
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @BranchId = @BranchId, @Action = 'SerialUnassigned', @EntityType = 'SerialItem',
                 @EntityId = @SerialId, @Reason = @Reason;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/orders/{id}/ready   Packing -> ReadyForPickup (nhận tại cửa hàng) hoặc Shipping (giao tận nơi)
   Yêu cầu đã quét đủ serial cho mọi dòng cần serial. */
CREATE OR ALTER PROCEDURE dbo.usp_Order_MarkReady @OrderId bigint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Fulfillment tinyint = (SELECT FulfillmentType FROM dbo.Orders WHERE Id = @OrderId);
        IF @Fulfillment IS NULL THROW 50001, N'Đơn hàng không tồn tại.', 1;

        IF EXISTS (SELECT 1 FROM dbo.OrderItems oi
                    WHERE oi.OrderId = @OrderId AND oi.RequiresSerial = 1
                      AND (SELECT COUNT(*) FROM dbo.SerialItems s WHERE s.OrderItemId = oi.Id AND s.Status = 1) <> oi.Quantity)
            THROW 50002, N'Chưa quét đủ serial cho các dòng hàng cần serial.', 1;

        DECLARE @To tinyint = CASE WHEN @Fulfillment = 1 THEN 4 ELSE 5 END, @From tinyint;
        DECLARE @Days int = ISNULL(TRY_CAST((SELECT [Value] FROM dbo.SystemSettings WHERE [Key] = 'pickup.hold.days') AS int), 3);

        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_SetOrderStatus @OrderId, @To, @UserId, NULL, @From OUTPUT;
            IF @To = 4 UPDATE dbo.Orders SET ExpiresAt = DATEADD(DAY, @Days, SYSUTCDATETIME()) WHERE Id = @OrderId;   -- hạn khách đến nhận (ORD-02)
        COMMIT TRANSACTION;

        SELECT @To AS Status;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/orders/{id}/delivery-failed   Shipping -> DeliveryFailed (bắt buộc lý do) */
CREATE OR ALTER PROCEDURE dbo.usp_Order_MarkDeliveryFailed @OrderId bigint, @Reason nvarchar(300), @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N'' THROW 50004, N'Phải nhập lý do giao thất bại.', 1;
        DECLARE @From tinyint;
        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_SetOrderStatus @OrderId, 6, @UserId, @Reason, @From OUTPUT;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/orders/{id}/retry-delivery   DeliveryFailed -> Shipping */
CREATE OR ALTER PROCEDURE dbo.usp_Order_RetryDelivery @OrderId bigint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @From tinyint;
        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_SetOrderStatus @OrderId, 5, @UserId, N'Giao lại', @From OUTPUT;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/orders/{id}/complete   (UC-12)  ReadyForPickup/Shipping -> Completed
   Trừ tồn thực tế, serial Allocated -> Sold và bắt đầu bảo hành (WAR-01), ghi nhận thanh toán COD/tiền mặt,
   tính hoa hồng (KPI-01). Đơn chuyển khoản/cổng thanh toán phải đã thanh toán đủ. */
CREATE OR ALTER PROCEDURE dbo.usp_Order_Complete @OrderId bigint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @Today date = dbo.fn_VnDate(SYSUTCDATETIME()), @From tinyint;
        DECLARE @BranchId int, @Method tinyint, @Grand decimal(18,0), @Paid decimal(18,0);

        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_SetOrderStatus @OrderId, 7, @UserId, NULL, @From OUTPUT;
            SELECT @BranchId = BranchId, @Method = PaymentMethod, @Grand = GrandTotal, @Paid = PaidAmount FROM dbo.Orders WHERE Id = @OrderId;

            IF EXISTS (SELECT 1 FROM dbo.OrderItems oi
                        WHERE oi.OrderId = @OrderId AND oi.RequiresSerial = 1
                          AND (SELECT COUNT(*) FROM dbo.SerialItems s WHERE s.OrderItemId = oi.Id AND s.Status = 1) <> oi.Quantity)
                THROW 50002, N'Chưa gán đủ serial cho các dòng hàng cần serial.', 1;

            IF @Method IN (1, 2) AND @Paid < @Grand THROW 50002, N'Đơn chưa được thanh toán đủ.', 1;
            IF @Method IN (0, 3) AND @Paid < @Grand
            BEGIN
                INSERT dbo.Payments (OrderId, PaymentType, Method, Amount, Status, PaidAt, CreatedBy) VALUES (@OrderId, 0, @Method, @Grand - @Paid, 1, @Now, @UserId);
                SET @Paid = @Grand;
            END

            -- trừ tồn thực tế và tồn đang giữ
            DECLARE @items int = (SELECT COUNT(*) FROM dbo.OrderItems WHERE OrderId = @OrderId);
            UPDATE sl SET PhysicalQty = sl.PhysicalQty - oi.Quantity, ReservedQty = sl.ReservedQty - oi.Quantity, UpdatedAt = @Now
              FROM dbo.StockLevels sl JOIN dbo.OrderItems oi ON oi.ProductId = sl.ProductId
             WHERE sl.BranchId = @BranchId AND oi.OrderId = @OrderId;
            IF @@ROWCOUNT <> @items THROW 50002, N'Dữ liệu tồn kho không khớp khi xuất hàng.', 1;

            INSERT dbo.StockMovements (BranchId, ProductId, MovementType, PhysicalDelta, ReservedDelta, RefType, RefId, CreatedBy)
            SELECT @BranchId, oi.ProductId, 3, -oi.Quantity, -oi.Quantity, 'Order', @OrderId, @UserId FROM dbo.OrderItems oi WHERE oi.OrderId = @OrderId;

            -- serial thành đã bán, kích hoạt bảo hành
            DECLARE @sold TABLE (SerialId bigint);
            UPDATE si SET Status = 3, SoldAt = @Now, WarrantyStartDate = @Today,
                          WarrantyEndDate = DATEADD(MONTH, oi.WarrantyMonths, @Today), UpdatedAt = @Now, UpdatedBy = @UserId
            OUTPUT inserted.Id INTO @sold (SerialId)
              FROM dbo.SerialItems si JOIN dbo.OrderItems oi ON oi.Id = si.OrderItemId
             WHERE oi.OrderId = @OrderId AND si.Status = 1;
            INSERT dbo.SerialStatusHistory (SerialItemId, FromStatus, ToStatus, RefType, RefId, ChangedBy)
            SELECT SerialId, 1, 3, 'Order', @OrderId, @UserId FROM @sold;

            UPDATE dbo.Orders
               SET PaidAmount = @Paid,
                   PaymentStatus = CASE WHEN @Paid >= @Grand THEN 2 WHEN @Paid > 0 THEN 1 ELSE 0 END,
                   CompletedAt = @Now, ExpiresAt = NULL
             WHERE Id = @OrderId;

            EXEC dbo.usp_Internal_AccrueCommission @OrderId;      -- định nghĩa ở 05_sp_warranty_returns_commission.sql
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/orders/{id}/cancel   (UC-06, UC-20)
   Khách chỉ được hủy khi Pending/PendingPayment; hủy ở trạng thái sau cần Quản lý duyệt (API kiểm tra quyền).
   RefundRequired = 1 nghĩa là đơn đã thu tiền, cần hoàn tiền (usp_Payment_Refund). */
CREATE OR ALTER PROCEDURE dbo.usp_Order_Cancel @OrderId bigint, @Reason nvarchar(300), @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N'' THROW 50004, N'Phải nhập lý do hủy đơn.', 1;
        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_CancelOrder @OrderId, @UserId, @Reason;
        COMMIT TRANSACTION;

        SELECT Id AS OrderId, Status, PaidAmount, CAST(CASE WHEN PaidAmount > 0 THEN 1 ELSE 0 END AS bit) AS RefundRequired
          FROM dbo.Orders WHERE Id = @OrderId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* JOB mỗi phút (UC-33): hủy đơn PendingPayment quá hạn thanh toán và ReadyForPickup quá hạn nhận.
   Idempotent: kiểm tra lại điều kiện trong giao dịch, mỗi đơn một giao dịch riêng, lỗi 1 đơn không dừng cả lô. */
CREATE OR ALTER PROCEDURE dbo.usp_Order_ExpireOverdue @MaxBatch int = 200
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @Id bigint, @done int = 0, @failed int = 0;

    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT TOP (@MaxBatch) Id FROM dbo.Orders
         WHERE ExpiresAt IS NOT NULL AND ExpiresAt <= @Now AND Status IN (0, 4) ORDER BY ExpiresAt;
    OPEN c;
    FETCH NEXT FROM c INTO @Id;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
                IF EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK) WHERE Id = @Id AND Status IN (0, 4) AND ExpiresAt <= @Now)
                BEGIN
                    EXEC dbo.usp_Internal_CancelOrder @Id, NULL, N'Quá hạn giữ hàng / hạn nhận hàng';
                    SET @done += 1;
                END
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            SET @failed += 1;
        END CATCH
        FETCH NEXT FROM c INTO @Id;
    END
    CLOSE c; DEALLOCATE c;

    SELECT @done AS Cancelled, @failed AS Failed;
END
GO

/* =============================================================================
   THANH TOÁN (PAY-01..05)
   ============================================================================= */

/* POST /api/payments/callback  (cổng thanh toán) và POST /api/orders/{id}/payments (thu ngân xác nhận chuyển khoản)
   Idempotent theo (Provider, ProviderTransactionId): gọi lại cùng giao dịch chỉ ghi nhận một lần (PAY-02).
   NeedsReview = 1: tiền về cho đơn đã hủy hoặc thanh toán thừa, cần xử lý thủ công/hoàn tiền. */
CREATE OR ALTER PROCEDURE dbo.usp_Payment_Record
    @OrderId bigint, @Method tinyint, @Amount decimal(18,0),
    @Provider varchar(30) = NULL, @ProviderTransactionId varchar(100) = NULL, @PaidAt datetime2(0) = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Amount <= 0 THROW 50004, N'Số tiền phải lớn hơn 0.', 1;

        IF @ProviderTransactionId IS NOT NULL
        BEGIN
            DECLARE @ExistingId bigint = (SELECT Id FROM dbo.Payments WHERE Provider = @Provider AND ProviderTransactionId = @ProviderTransactionId);
            IF @ExistingId IS NOT NULL
            BEGIN
                SELECT @ExistingId AS PaymentId, CAST(1 AS bit) AS IsDuplicate;
                RETURN;
            END
        END

        DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @Status tinyint, @Grand decimal(18,0), @Paid decimal(18,0), @OMethod tinyint,
                @CustomerId bigint, @NewPaid decimal(18,0), @PaymentId bigint, @From tinyint;

        BEGIN TRANSACTION;
            SELECT @Status = Status, @Grand = GrandTotal, @Paid = PaidAmount, @OMethod = PaymentMethod, @CustomerId = CustomerId
              FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId;
            IF @Status IS NULL THROW 50001, N'Đơn hàng không tồn tại.', 1;

            INSERT dbo.Payments (OrderId, PaymentType, Method, Amount, Status, Provider, ProviderTransactionId, PaidAt, CreatedBy)
            VALUES (@OrderId, 0, @Method, @Amount, 1, @Provider, @ProviderTransactionId, ISNULL(@PaidAt, @Now), @UserId);
            SET @PaymentId = SCOPE_IDENTITY();

            SET @NewPaid = @Paid + @Amount;
            UPDATE dbo.Orders SET PaidAmount = @NewPaid, PaymentStatus = CASE WHEN @NewPaid >= @Grand THEN 2 ELSE 1 END,
                                  UpdatedAt = @Now, UpdatedBy = @UserId
             WHERE Id = @OrderId;

            IF @Status = 0 AND @NewPaid >= @Grand
            BEGIN
                EXEC dbo.usp_Internal_SetOrderStatus @OrderId, 1, @UserId, N'Đã nhận thanh toán', @From OUTPUT;
                UPDATE dbo.Orders SET ExpiresAt = NULL WHERE Id = @OrderId;
                SET @Status = 1;
            END

            IF @OMethod = 4
                INSERT dbo.CustomerLedger (CustomerId, OrderId, PaymentId, EntryType, Amount, CreatedBy) VALUES (@CustomerId, @OrderId, @PaymentId, 1, -@Amount, @UserId);
        COMMIT TRANSACTION;

        SELECT @PaymentId AS PaymentId, CAST(0 AS bit) AS IsDuplicate, @NewPaid AS PaidAmount, @Status AS OrderStatus,
               CAST(CASE WHEN @Status = 8 OR @NewPaid > @Grand THEN 1 ELSE 0 END AS bit) AS NeedsReview;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() IN (2601, 2627) AND @ProviderTransactionId IS NOT NULL
        BEGIN
            SELECT Id AS PaymentId, CAST(1 AS bit) AS IsDuplicate FROM dbo.Payments WHERE Provider = @Provider AND ProviderTransactionId = @ProviderTransactionId;
            RETURN;
        END;
        THROW;
    END CATCH
END
GO

/* POST /api/orders/{id}/refunds   hoàn tiền thủ công cho đơn đã hủy (hoàn tiền do đổi trả dùng usp_Return_Refund).
   Giao dịch hoàn là bản ghi riêng, không sửa giao dịch gốc (PAY-04). Cần Quản lý duyệt (API kiểm tra quyền). */
CREATE OR ALTER PROCEDURE dbo.usp_Payment_Refund
    @OrderId bigint, @Amount decimal(18,0), @Method tinyint, @Reason nvarchar(300), @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Amount <= 0 THROW 50004, N'Số tiền hoàn phải lớn hơn 0.', 1;
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N'' THROW 50004, N'Phải nhập lý do hoàn tiền.', 1;

        DECLARE @Paid decimal(18,0), @Status tinyint, @BranchId int, @PaymentId bigint, @Parent bigint;
        BEGIN TRANSACTION;
            SELECT @Paid = PaidAmount, @Status = Status, @BranchId = BranchId FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId;
            IF @Status IS NULL THROW 50001, N'Đơn hàng không tồn tại.', 1;
            IF @Status NOT IN (8, 6) THROW 50002, N'Chỉ hoàn tiền thủ công cho đơn đã hủy hoặc giao thất bại; đơn đã giao hoàn tiền qua quy trình đổi trả.', 1;
            IF @Amount > @Paid THROW 50006, N'Số tiền hoàn vượt quá số đã thanh toán.', 1;

            SELECT TOP 1 @Parent = Id FROM dbo.Payments WHERE OrderId = @OrderId AND PaymentType = 0 AND Status = 1 ORDER BY PaidAt DESC, Id DESC;
            INSERT dbo.Payments (OrderId, PaymentType, Method, Amount, Status, ParentPaymentId, PaidAt, CreatedBy)
            VALUES (@OrderId, 1, @Method, @Amount, 1, @Parent, SYSUTCDATETIME(), @UserId);
            SET @PaymentId = SCOPE_IDENTITY();

            UPDATE dbo.Orders SET PaidAmount = PaidAmount - @Amount,
                                  PaymentStatus = CASE WHEN PaidAmount - @Amount <= 0 THEN 3 ELSE PaymentStatus END,
                                  UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
             WHERE Id = @OrderId;

            DECLARE @new nvarchar(30) = CAST(@Amount AS nvarchar(30));
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @BranchId = @BranchId, @Action = 'RefundIssued', @EntityType = 'Order',
                 @EntityId = @OrderId, @NewValues = @new, @Reason = @Reason;
        COMMIT TRANSACTION;

        SELECT @PaymentId AS PaymentId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
