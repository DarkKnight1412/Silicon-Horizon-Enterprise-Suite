/* =============================================================================
   04. KHO, SERIAL, NHÀ CUNG CẤP
   Chạy SAU 02 và 03.

   Quy ước tồn kho (nhắc lại):
     Physical  = số đơn vị đang ở kho (serial: trạng thái InStock + Allocated)
     Available = Physical - Reserved
     Serial ở RMA, Returned, Defective, InTransfer, Sold KHÔNG nằm trong Physical.

   Dấu của sổ công nợ nhà cung cấp (SupplierLedger.Amount):
     dương = tăng nợ phải trả (Payable); âm = giảm nợ (Payment, ReturnCredit).
   ============================================================================= */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =============================================================================
   TỒN KHO
   ============================================================================= */

/* GET /api/stock  - tồn theo chi nhánh/sản phẩm. @BelowMinOnly = 1 dùng cho cảnh báo tồn tối thiểu (UC-34, INV-02). */
CREATE OR ALTER PROCEDURE dbo.usp_Stock_GetLevels
    @BranchId int = NULL, @ProductId bigint = NULL, @CategoryId int = NULL, @Keyword nvarchar(100) = NULL,
    @BelowMinOnly bit = 0, @PageNumber int = 1, @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 50;
    IF @PageSize > 200 SET @PageSize = 200;

    SELECT sl.BranchId, br.Name AS BranchName, sl.ProductId, p.Sku, p.Name AS ProductName, p.CategoryId,
           sl.PhysicalQty, sl.ReservedQty, sl.AvailableQty,
           ISNULL(sl.MinStock, p.MinStockDefault) AS MinStock,
           CAST(CASE WHEN sl.AvailableQty < ISNULL(sl.MinStock, p.MinStockDefault) THEN 1 ELSE 0 END AS bit) AS IsBelowMin,
           COUNT(*) OVER () AS TotalCount
      FROM dbo.StockLevels sl
      JOIN dbo.Products p ON p.Id = sl.ProductId
      JOIN dbo.Branches br ON br.Id = sl.BranchId
     WHERE (@BranchId IS NULL OR sl.BranchId = @BranchId)
       AND (@ProductId IS NULL OR sl.ProductId = @ProductId)
       AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
       AND (@Keyword IS NULL OR p.Name LIKE N'%' + @Keyword + N'%' OR p.Sku LIKE @Keyword + N'%')
       AND (@BelowMinOnly = 0 OR sl.AvailableQty < ISNULL(sl.MinStock, p.MinStockDefault))
     ORDER BY br.Name, p.Name
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* PUT /api/stock/min  - đặt mức tồn tối thiểu theo chi nhánh và sản phẩm (NULL = dùng mặc định của sản phẩm) */
CREATE OR ALTER PROCEDURE dbo.usp_Stock_SetMin
    @BranchId int, @ProductId bigint, @MinStock int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @MinStock IS NOT NULL AND @MinStock < 0 THROW 50004, N'Mức tồn tối thiểu không hợp lệ.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.StockLevels WHERE BranchId = @BranchId AND ProductId = @ProductId)
        INSERT dbo.StockLevels (BranchId, ProductId, PhysicalQty, ReservedQty, MinStock) VALUES (@BranchId, @ProductId, 0, 0, @MinStock);
    ELSE
        UPDATE dbo.StockLevels SET MinStock = @MinStock, UpdatedAt = SYSUTCDATETIME() WHERE BranchId = @BranchId AND ProductId = @ProductId;
END
GO

/* GET /api/stock/movements  - thẻ kho (INV-03) */
CREATE OR ALTER PROCEDURE dbo.usp_Stock_GetMovements
    @BranchId int, @ProductId bigint, @FromDate datetime2(0) = NULL, @ToDate datetime2(0) = NULL, @PageNumber int = 1, @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 50;
    IF @PageSize > 200 SET @PageSize = 200;

    SELECT m.Id, m.MovementType, m.PhysicalDelta, m.ReservedDelta, m.RefType, m.RefId, m.Note, m.CreatedAt, m.CreatedBy,
           COUNT(*) OVER () AS TotalCount
      FROM dbo.StockMovements m
     WHERE m.BranchId = @BranchId AND m.ProductId = @ProductId
       AND (@FromDate IS NULL OR m.CreatedAt >= @FromDate) AND (@ToDate IS NULL OR m.CreatedAt < @ToDate)
     ORDER BY m.CreatedAt DESC, m.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* =============================================================================
   NHẬP KHO (UC-13)
   @ItemsJson: [{"productId":1,"quantity":2,"unitCost":1500000,"serials":["SN001","SN002"]}]
   Sản phẩm RequiresSerial: bắt buộc đủ serial bằng số lượng, không trùng (INV-07, INV-08).
   Tạo phiếu ở trạng thái hoàn tất trong một giao dịch.
   ============================================================================= */
CREATE OR ALTER PROCEDURE dbo.usp_PurchaseReceipt_Create
    @SupplierId bigint, @BranchId int, @SupplierInvoiceNo varchar(50) = NULL, @ItemsJson nvarchar(max), @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @Today date = dbo.fn_VnDate(SYSUTCDATETIME()), @Term int;
        SELECT @Term = PaymentTermDays FROM dbo.Suppliers WHERE Id = @SupplierId AND IsActive = 1;
        IF @Term IS NULL THROW 50001, N'Nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Id = @BranchId AND IsActive = 1) THROW 50001, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
        IF @ItemsJson IS NULL OR ISJSON(@ItemsJson) = 0 THROW 50004, N'Danh sách hàng nhập không hợp lệ.', 1;

        CREATE TABLE #Raw (ProductId bigint, Quantity int, UnitCost decimal(18,0));
        INSERT #Raw SELECT productId, quantity, unitCost FROM OPENJSON(@ItemsJson) WITH (productId bigint '$.productId', quantity int '$.quantity', unitCost decimal(18,0) '$.unitCost');
        IF NOT EXISTS (SELECT 1 FROM #Raw) THROW 50004, N'Phiếu nhập không có dòng hàng.', 1;
        IF EXISTS (SELECT 1 FROM #Raw GROUP BY ProductId HAVING COUNT(*) > 1) THROW 50004, N'Một sản phẩm xuất hiện nhiều lần trong phiếu nhập.', 1;
        IF EXISTS (SELECT 1 FROM #Raw WHERE Quantity IS NULL OR Quantity <= 0 OR UnitCost IS NULL OR UnitCost < 0) THROW 50004, N'Số lượng phải lớn hơn 0 và đơn giá nhập không âm.', 1;
        IF EXISTS (SELECT 1 FROM #Raw r LEFT JOIN dbo.Products p ON p.Id = r.ProductId WHERE p.Id IS NULL) THROW 50001, N'Có sản phẩm không tồn tại.', 1;

        CREATE TABLE #It (ProductId bigint PRIMARY KEY, Quantity int, UnitCost decimal(18,0), RequiresSerial bit);
        INSERT #It SELECT r.ProductId, r.Quantity, r.UnitCost, p.RequiresSerial FROM #Raw r JOIN dbo.Products p ON p.Id = r.ProductId;

        CREATE TABLE #Se (ProductId bigint, SerialNumber nvarchar(200));
        INSERT #Se (ProductId, SerialNumber)
        SELECT j.productId, LTRIM(RTRIM(s.[value]))
          FROM OPENJSON(@ItemsJson) WITH (productId bigint '$.productId', serials nvarchar(max) '$.serials' AS JSON) j
          CROSS APPLY OPENJSON(j.serials) s
         WHERE j.serials IS NOT NULL;

        IF EXISTS (SELECT 1 FROM #Se WHERE SerialNumber = N'' OR LEN(SerialNumber) > 100) THROW 50004, N'Serial rỗng hoặc dài quá 100 ký tự.', 1;
        IF EXISTS (SELECT 1 FROM #Se GROUP BY ProductId, SerialNumber HAVING COUNT(*) > 1) THROW 50005, N'Có serial bị trùng trong phiếu nhập.', 1;
        IF EXISTS (SELECT 1 FROM #It i WHERE i.RequiresSerial = 1 AND (SELECT COUNT(*) FROM #Se s WHERE s.ProductId = i.ProductId) <> i.Quantity)
            THROW 50004, N'Sản phẩm quản lý theo serial phải nhập đủ danh sách serial bằng số lượng.', 1;
        IF EXISTS (SELECT 1 FROM #It i WHERE i.RequiresSerial = 0 AND EXISTS (SELECT 1 FROM #Se s WHERE s.ProductId = i.ProductId))
            THROW 50004, N'Sản phẩm không quản lý theo serial không được kèm serial.', 1;
        IF EXISTS (SELECT 1 FROM #Se s JOIN dbo.SerialItems si ON si.ProductId = s.ProductId AND si.SerialNumber = s.SerialNumber)
            THROW 50005, N'Có serial đã tồn tại trong hệ thống.', 1;

        DECLARE @Total decimal(18,0) = (SELECT SUM(Quantity * UnitCost) FROM #It);
        DECLARE @Seq bigint = NEXT VALUE FOR dbo.seq_PurchaseReceipt;
        DECLARE @Code varchar(30) = dbo.fn_FormatCode('PN', @Seq, ABS(CHECKSUM(NEWID())));
        DECLARE @ReceiptId bigint;

        BEGIN TRANSACTION;
            INSERT dbo.PurchaseReceipts (ReceiptCode, SupplierId, BranchId, SupplierInvoiceNo, Status, TotalAmount, ReceivedAt, CreatedBy)
            VALUES (@Code, @SupplierId, @BranchId, @SupplierInvoiceNo, 1, @Total, @Now, @UserId);
            SET @ReceiptId = SCOPE_IDENTITY();

            INSERT dbo.PurchaseReceiptItems (ReceiptId, ProductId, Quantity, UnitCost) SELECT @ReceiptId, ProductId, Quantity, UnitCost FROM #It;

            DECLARE @newSerials TABLE (SerialId bigint);
            INSERT dbo.SerialItems (ProductId, SerialNumber, Status, BranchId, PurchaseReceiptItemId, UnitCost, CreatedBy)
            OUTPUT inserted.Id INTO @newSerials (SerialId)
            SELECT s.ProductId, CAST(s.SerialNumber AS varchar(100)), 0, @BranchId, pri.Id, i.UnitCost, @UserId
              FROM #Se s JOIN #It i ON i.ProductId = s.ProductId
              JOIN dbo.PurchaseReceiptItems pri ON pri.ReceiptId = @ReceiptId AND pri.ProductId = s.ProductId;

            INSERT dbo.SerialStatusHistory (SerialItemId, FromStatus, ToStatus, RefType, RefId, ChangedBy)
            SELECT SerialId, NULL, 0, 'PurchaseReceipt', @ReceiptId, @UserId FROM @newSerials;

            -- tồn kho
            INSERT dbo.StockLevels (BranchId, ProductId, PhysicalQty, ReservedQty)
            SELECT @BranchId, i.ProductId, 0, 0 FROM #It i
             WHERE NOT EXISTS (SELECT 1 FROM dbo.StockLevels x WITH (UPDLOCK, HOLDLOCK) WHERE x.BranchId = @BranchId AND x.ProductId = i.ProductId);

            UPDATE sl SET PhysicalQty = sl.PhysicalQty + i.Quantity, UpdatedAt = @Now
              FROM dbo.StockLevels sl JOIN #It i ON i.ProductId = sl.ProductId WHERE sl.BranchId = @BranchId;

            INSERT dbo.StockMovements (BranchId, ProductId, MovementType, PhysicalDelta, ReservedDelta, RefType, RefId, CreatedBy)
            SELECT @BranchId, ProductId, 0, Quantity, 0, 'PurchaseReceipt', @ReceiptId, @UserId FROM #It;

            UPDATE p SET LastImportPrice = i.UnitCost, UpdatedAt = @Now FROM dbo.Products p JOIN #It i ON i.ProductId = p.Id;

            -- công nợ phải trả
            IF @Total > 0
                INSERT dbo.SupplierLedger (SupplierId, PurchaseReceiptId, EntryType, Amount, DueDate, CreatedBy)
                VALUES (@SupplierId, @ReceiptId, 0, @Total, DATEADD(DAY, @Term, @Today), @UserId);
        COMMIT TRANSACTION;

        SELECT @ReceiptId AS ReceiptId, @Code AS ReceiptCode, @Total AS TotalAmount;
    END TRY
    BEGIN CATCH
        DECLARE @en int = ERROR_NUMBER();
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        IF @en IN (2601, 2627) THROW 50005, N'Dữ liệu trùng (ví dụ serial đã tồn tại).', 1;
        THROW;
    END CATCH
END
GO

/* GET /api/purchase-receipts/{id} */
CREATE OR ALTER PROCEDURE dbo.usp_PurchaseReceipt_Get @ReceiptId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseReceipts WHERE Id = @ReceiptId) THROW 50001, N'Phiếu nhập không tồn tại.', 1;
    SELECT r.*, s.Name AS SupplierName, br.Name AS BranchName FROM dbo.PurchaseReceipts r JOIN dbo.Suppliers s ON s.Id = r.SupplierId JOIN dbo.Branches br ON br.Id = r.BranchId WHERE r.Id = @ReceiptId;
    SELECT i.Id, i.ProductId, p.Sku, p.Name, i.Quantity, i.UnitCost, i.Quantity * i.UnitCost AS LineTotal FROM dbo.PurchaseReceiptItems i JOIN dbo.Products p ON p.Id = i.ProductId WHERE i.ReceiptId = @ReceiptId;
    SELECT si.Id AS SerialItemId, si.ProductId, si.SerialNumber, si.Status FROM dbo.SerialItems si JOIN dbo.PurchaseReceiptItems i ON i.Id = si.PurchaseReceiptItemId WHERE i.ReceiptId = @ReceiptId;
END
GO

/* GET /api/purchase-receipts */
CREATE OR ALTER PROCEDURE dbo.usp_PurchaseReceipt_List
    @SupplierId bigint = NULL, @BranchId int = NULL, @FromDate datetime2(0) = NULL, @ToDate datetime2(0) = NULL, @PageNumber int = 1, @PageSize int = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    IF @PageSize > 100 SET @PageSize = 100;
    SELECT r.Id, r.ReceiptCode, r.SupplierId, s.Name AS SupplierName, r.BranchId, r.Status, r.TotalAmount, r.ReceivedAt, COUNT(*) OVER () AS TotalCount
      FROM dbo.PurchaseReceipts r JOIN dbo.Suppliers s ON s.Id = r.SupplierId
     WHERE (@SupplierId IS NULL OR r.SupplierId = @SupplierId) AND (@BranchId IS NULL OR r.BranchId = @BranchId)
       AND (@FromDate IS NULL OR r.ReceivedAt >= @FromDate) AND (@ToDate IS NULL OR r.ReceivedAt < @ToDate)
     ORDER BY r.ReceivedAt DESC, r.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* =============================================================================
   CHUYỂN KHO NỘI BỘ (UC-14, INV-06): Draft -> InTransit (Ship) -> Received
   @ItemsJson: [{"productId":1,"quantity":2,"serials":["SN001","SN002"]}]
   Hàng đang chuyển không tính vào tồn của cả hai chi nhánh.
   ============================================================================= */
CREATE OR ALTER PROCEDURE dbo.usp_StockTransfer_Create
    @FromBranchId int, @ToBranchId int, @ItemsJson nvarchar(max), @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @FromBranchId = @ToBranchId THROW 50004, N'Chi nhánh đi và đến phải khác nhau.', 1;
        IF (SELECT COUNT(*) FROM dbo.Branches WHERE Id IN (@FromBranchId, @ToBranchId) AND IsActive = 1) <> 2 THROW 50001, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
        IF @ItemsJson IS NULL OR ISJSON(@ItemsJson) = 0 THROW 50004, N'Danh sách hàng chuyển không hợp lệ.', 1;

        CREATE TABLE #Raw (ProductId bigint, Quantity int);
        INSERT #Raw SELECT productId, quantity FROM OPENJSON(@ItemsJson) WITH (productId bigint '$.productId', quantity int '$.quantity');
        IF NOT EXISTS (SELECT 1 FROM #Raw) THROW 50004, N'Phiếu chuyển không có dòng hàng.', 1;
        IF EXISTS (SELECT 1 FROM #Raw GROUP BY ProductId HAVING COUNT(*) > 1) THROW 50004, N'Một sản phẩm xuất hiện nhiều lần trong phiếu chuyển.', 1;
        IF EXISTS (SELECT 1 FROM #Raw WHERE Quantity IS NULL OR Quantity <= 0) THROW 50004, N'Số lượng phải lớn hơn 0.', 1;
        IF EXISTS (SELECT 1 FROM #Raw r LEFT JOIN dbo.Products p ON p.Id = r.ProductId WHERE p.Id IS NULL) THROW 50001, N'Có sản phẩm không tồn tại.', 1;

        CREATE TABLE #It (ProductId bigint PRIMARY KEY, Quantity int, RequiresSerial bit);
        INSERT #It SELECT r.ProductId, r.Quantity, p.RequiresSerial FROM #Raw r JOIN dbo.Products p ON p.Id = r.ProductId;

        CREATE TABLE #Se (ProductId bigint, SerialNumber nvarchar(200));
        INSERT #Se SELECT j.productId, LTRIM(RTRIM(s.[value]))
          FROM OPENJSON(@ItemsJson) WITH (productId bigint '$.productId', serials nvarchar(max) '$.serials' AS JSON) j
          CROSS APPLY OPENJSON(j.serials) s WHERE j.serials IS NOT NULL;

        IF EXISTS (SELECT 1 FROM #Se GROUP BY ProductId, SerialNumber HAVING COUNT(*) > 1) THROW 50004, N'Có serial bị trùng trong phiếu chuyển.', 1;
        IF EXISTS (SELECT 1 FROM #It i WHERE i.RequiresSerial = 1 AND (SELECT COUNT(*) FROM #Se s WHERE s.ProductId = i.ProductId) <> i.Quantity)
            THROW 50004, N'Sản phẩm quản lý theo serial phải liệt kê đủ serial bằng số lượng.', 1;
        IF EXISTS (SELECT 1 FROM #It i WHERE i.RequiresSerial = 0 AND EXISTS (SELECT 1 FROM #Se s WHERE s.ProductId = i.ProductId))
            THROW 50004, N'Sản phẩm không quản lý theo serial không được kèm serial.', 1;
        IF EXISTS (SELECT 1 FROM #Se s LEFT JOIN dbo.SerialItems si ON si.ProductId = s.ProductId AND si.SerialNumber = CAST(s.SerialNumber AS varchar(100))
                                                                   AND si.Status = 0 AND si.BranchId = @FromBranchId
                    WHERE si.Id IS NULL)
            THROW 50002, N'Có serial không tồn tại hoặc không ở trạng thái trong kho tại chi nhánh đi.', 1;

        DECLARE @Seq bigint = NEXT VALUE FOR dbo.seq_StockTransfer;
        DECLARE @Code varchar(30) = dbo.fn_FormatCode('CK', @Seq, ABS(CHECKSUM(NEWID())));
        DECLARE @TransferId bigint;

        BEGIN TRANSACTION;
            INSERT dbo.StockTransfers (TransferCode, FromBranchId, ToBranchId, Status, CreatedBy) VALUES (@Code, @FromBranchId, @ToBranchId, 0, @UserId);
            SET @TransferId = SCOPE_IDENTITY();
            INSERT dbo.StockTransferItems (TransferId, ProductId, Quantity) SELECT @TransferId, ProductId, Quantity FROM #It;
            INSERT dbo.StockTransferSerials (TransferItemId, SerialItemId)
            SELECT ti.Id, si.Id FROM #Se s
              JOIN dbo.StockTransferItems ti ON ti.TransferId = @TransferId AND ti.ProductId = s.ProductId
              JOIN dbo.SerialItems si ON si.ProductId = s.ProductId AND si.SerialNumber = CAST(s.SerialNumber AS varchar(100));
        COMMIT TRANSACTION;

        SELECT @TransferId AS TransferId, @Code AS TransferCode;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/stock-transfers/{id}/ship   Draft -> InTransit: trừ tồn chi nhánh đi, serial InStock -> InTransfer */
CREATE OR ALTER PROCEDURE dbo.usp_StockTransfer_Ship @TransferId bigint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Status tinyint, @From int, @To int;
        BEGIN TRANSACTION;
            SELECT @Status = Status, @From = FromBranchId, @To = ToBranchId FROM dbo.StockTransfers WITH (UPDLOCK) WHERE Id = @TransferId;
            IF @Status IS NULL THROW 50001, N'Phiếu chuyển kho không tồn tại.', 1;
            IF @Status <> 0 THROW 50002, N'Phiếu không ở trạng thái nháp.', 1;

            DECLARE @items int = (SELECT COUNT(*) FROM dbo.StockTransferItems WHERE TransferId = @TransferId);
            UPDATE sl SET PhysicalQty = sl.PhysicalQty - ti.Quantity, UpdatedAt = SYSUTCDATETIME()
              FROM dbo.StockLevels sl JOIN dbo.StockTransferItems ti ON ti.ProductId = sl.ProductId
             WHERE sl.BranchId = @From AND ti.TransferId = @TransferId AND sl.PhysicalQty - sl.ReservedQty >= ti.Quantity;
            IF @@ROWCOUNT <> @items THROW 50003, N'Tồn khả dụng tại chi nhánh đi không đủ để chuyển.', 1;

            INSERT dbo.StockMovements (BranchId, ProductId, MovementType, PhysicalDelta, ReservedDelta, RefType, RefId, CreatedBy)
            SELECT @From, ProductId, 4, -Quantity, 0, 'StockTransfer', @TransferId, @UserId FROM dbo.StockTransferItems WHERE TransferId = @TransferId;

            DECLARE @moved TABLE (SerialId bigint);
            DECLARE @expected int = (SELECT COUNT(*) FROM dbo.StockTransferSerials ts JOIN dbo.StockTransferItems ti ON ti.Id = ts.TransferItemId WHERE ti.TransferId = @TransferId);
            UPDATE si SET Status = 2, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
            OUTPUT inserted.Id INTO @moved (SerialId)
              FROM dbo.SerialItems si
              JOIN dbo.StockTransferSerials ts ON ts.SerialItemId = si.Id
              JOIN dbo.StockTransferItems ti ON ti.Id = ts.TransferItemId
             WHERE ti.TransferId = @TransferId AND si.Status = 0 AND si.BranchId = @From;
            IF (SELECT COUNT(*) FROM @moved) <> @expected THROW 50002, N'Có serial không còn ở trạng thái trong kho tại chi nhánh đi.', 1;
            INSERT dbo.SerialStatusHistory (SerialItemId, FromStatus, ToStatus, RefType, RefId, ChangedBy) SELECT SerialId, 0, 2, 'StockTransfer', @TransferId, @UserId FROM @moved;

            UPDATE dbo.StockTransfers SET Status = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @TransferId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/stock-transfers/{id}/receive   InTransit -> Received: cộng tồn chi nhánh đến, serial InTransfer -> InStock */
CREATE OR ALTER PROCEDURE dbo.usp_StockTransfer_Receive @TransferId bigint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Status tinyint, @From int, @To int;
        BEGIN TRANSACTION;
            SELECT @Status = Status, @From = FromBranchId, @To = ToBranchId FROM dbo.StockTransfers WITH (UPDLOCK) WHERE Id = @TransferId;
            IF @Status IS NULL THROW 50001, N'Phiếu chuyển kho không tồn tại.', 1;
            IF @Status <> 1 THROW 50002, N'Phiếu không ở trạng thái đang chuyển.', 1;

            INSERT dbo.StockLevels (BranchId, ProductId, PhysicalQty, ReservedQty)
            SELECT @To, ti.ProductId, 0, 0 FROM dbo.StockTransferItems ti
             WHERE ti.TransferId = @TransferId
               AND NOT EXISTS (SELECT 1 FROM dbo.StockLevels x WITH (UPDLOCK, HOLDLOCK) WHERE x.BranchId = @To AND x.ProductId = ti.ProductId);

            UPDATE sl SET PhysicalQty = sl.PhysicalQty + ti.Quantity, UpdatedAt = SYSUTCDATETIME()
              FROM dbo.StockLevels sl JOIN dbo.StockTransferItems ti ON ti.ProductId = sl.ProductId
             WHERE sl.BranchId = @To AND ti.TransferId = @TransferId;

            INSERT dbo.StockMovements (BranchId, ProductId, MovementType, PhysicalDelta, ReservedDelta, RefType, RefId, CreatedBy)
            SELECT @To, ProductId, 5, Quantity, 0, 'StockTransfer', @TransferId, @UserId FROM dbo.StockTransferItems WHERE TransferId = @TransferId;

            DECLARE @moved TABLE (SerialId bigint);
            UPDATE si SET Status = 0, BranchId = @To, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
            OUTPUT inserted.Id INTO @moved (SerialId)
              FROM dbo.SerialItems si
              JOIN dbo.StockTransferSerials ts ON ts.SerialItemId = si.Id
              JOIN dbo.StockTransferItems ti ON ti.Id = ts.TransferItemId
             WHERE ti.TransferId = @TransferId AND si.Status = 2;
            INSERT dbo.SerialStatusHistory (SerialItemId, FromStatus, ToStatus, RefType, RefId, ChangedBy) SELECT SerialId, 2, 0, 'StockTransfer', @TransferId, @UserId FROM @moved;

            UPDATE dbo.StockTransfers SET Status = 2, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @TransferId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/stock-transfers/{id}/cancel   chỉ hủy được phiếu nháp */
CREATE OR ALTER PROCEDURE dbo.usp_StockTransfer_Cancel @TransferId bigint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.StockTransfers SET Status = 3, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @TransferId AND Status = 0;
    IF @@ROWCOUNT = 0 THROW 50002, N'Phiếu không tồn tại hoặc không còn ở trạng thái nháp.', 1;
END
GO

/* GET /api/stock-transfers/{id} */
CREATE OR ALTER PROCEDURE dbo.usp_StockTransfer_Get @TransferId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.StockTransfers WHERE Id = @TransferId) THROW 50001, N'Phiếu chuyển kho không tồn tại.', 1;
    SELECT t.*, f.Name AS FromBranchName, tb.Name AS ToBranchName FROM dbo.StockTransfers t JOIN dbo.Branches f ON f.Id = t.FromBranchId JOIN dbo.Branches tb ON tb.Id = t.ToBranchId WHERE t.Id = @TransferId;
    SELECT ti.Id, ti.ProductId, p.Sku, p.Name, ti.Quantity FROM dbo.StockTransferItems ti JOIN dbo.Products p ON p.Id = ti.ProductId WHERE ti.TransferId = @TransferId;
    SELECT ts.TransferItemId, si.Id AS SerialItemId, si.SerialNumber, si.Status FROM dbo.StockTransferSerials ts JOIN dbo.SerialItems si ON si.Id = ts.SerialItemId JOIN dbo.StockTransferItems ti ON ti.Id = ts.TransferItemId WHERE ti.TransferId = @TransferId;
END
GO

/* =============================================================================
   KIỂM KÊ (UC-15, UC-21, INV-04, INV-05)
   Kiểm kê theo số lượng. Sản phẩm quản lý theo serial: chỉ duyệt khi không lệch;
   lệch thì xử lý từng serial bằng usp_Serial_ChangeStatus.
   Lưu ý: chênh lệch được áp lên tồn thực tế hiện tại, nên nên kiểm kê vào lúc ít phát sinh xuất/nhập.
   ============================================================================= */
CREATE OR ALTER PROCEDURE dbo.usp_StockTake_Create @BranchId int, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Id = @BranchId AND IsActive = 1) THROW 50001, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
    DECLARE @Seq bigint = NEXT VALUE FOR dbo.seq_StockTake;
    DECLARE @Code varchar(30) = dbo.fn_FormatCode('KK', @Seq, ABS(CHECKSUM(NEWID())));
    INSERT dbo.StockTakes (StockTakeCode, BranchId, Status, CreatedBy) VALUES (@Code, @BranchId, 1, @UserId);
    SELECT SCOPE_IDENTITY() AS StockTakeId, @Code AS StockTakeCode;
END
GO

/* POST /api/stock-takes/{id}/counts
   @CountsJson: [{"productId":1,"countedQty":10,"reason":"..."}]; @Finalize = 1: gửi duyệt (Counting -> PendingApproval) */
CREATE OR ALTER PROCEDURE dbo.usp_StockTake_SubmitCounts
    @StockTakeId bigint, @CountsJson nvarchar(max), @Finalize bit = 0, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Status tinyint, @BranchId int;
        SELECT @Status = Status, @BranchId = BranchId FROM dbo.StockTakes WHERE Id = @StockTakeId;
        IF @Status IS NULL THROW 50001, N'Phiếu kiểm kê không tồn tại.', 1;
        IF @Status <> 1 THROW 50002, N'Phiếu kiểm kê không ở trạng thái đang kiểm.', 1;

        CREATE TABLE #C (ProductId bigint PRIMARY KEY, CountedQty int, Reason nvarchar(300));
        IF @CountsJson IS NOT NULL
        BEGIN
            IF ISJSON(@CountsJson) = 0 THROW 50004, N'Dữ liệu kiểm kê không hợp lệ.', 1;
            IF EXISTS (SELECT 1 FROM OPENJSON(@CountsJson) WITH (productId bigint '$.productId') GROUP BY productId HAVING COUNT(*) > 1) THROW 50004, N'Một sản phẩm xuất hiện nhiều lần.', 1;
            INSERT #C SELECT productId, countedQty, reason FROM OPENJSON(@CountsJson) WITH (productId bigint '$.productId', countedQty int '$.countedQty', reason nvarchar(300) '$.reason');
            IF EXISTS (SELECT 1 FROM #C WHERE CountedQty IS NULL OR CountedQty < 0) THROW 50004, N'Số lượng đếm không hợp lệ.', 1;
            IF EXISTS (SELECT 1 FROM #C c LEFT JOIN dbo.Products p ON p.Id = c.ProductId WHERE p.Id IS NULL) THROW 50001, N'Có sản phẩm không tồn tại.', 1;
        END

        BEGIN TRANSACTION;
            UPDATE sti SET CountedQty = c.CountedQty, Reason = c.Reason, SystemQty = ISNULL(sl.PhysicalQty, 0)
              FROM dbo.StockTakeItems sti JOIN #C c ON c.ProductId = sti.ProductId
              LEFT JOIN dbo.StockLevels sl ON sl.BranchId = @BranchId AND sl.ProductId = sti.ProductId
             WHERE sti.StockTakeId = @StockTakeId;

            INSERT dbo.StockTakeItems (StockTakeId, ProductId, SystemQty, CountedQty, Reason)
            SELECT @StockTakeId, c.ProductId, ISNULL(sl.PhysicalQty, 0), c.CountedQty, c.Reason
              FROM #C c LEFT JOIN dbo.StockLevels sl ON sl.BranchId = @BranchId AND sl.ProductId = c.ProductId
             WHERE NOT EXISTS (SELECT 1 FROM dbo.StockTakeItems x WHERE x.StockTakeId = @StockTakeId AND x.ProductId = c.ProductId);

            IF @Finalize = 1
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.StockTakeItems WHERE StockTakeId = @StockTakeId) THROW 50004, N'Phiếu chưa có dòng kiểm kê nào.', 1;
                UPDATE dbo.StockTakes SET Status = 2, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @StockTakeId;
            END
        COMMIT TRANSACTION;

        SELECT sti.ProductId, p.Sku, p.Name, sti.SystemQty, sti.CountedQty, sti.DiffQty, sti.Reason
          FROM dbo.StockTakeItems sti JOIN dbo.Products p ON p.Id = sti.ProductId WHERE sti.StockTakeId = @StockTakeId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/stock-takes/{id}/approve  (Quản lý)   @Approve = 1: áp chênh lệch vào tồn; 0: từ chối */
CREATE OR ALTER PROCEDURE dbo.usp_StockTake_Approve
    @StockTakeId bigint, @Approve bit, @UserId bigint, @Note nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Status tinyint, @BranchId int;
        BEGIN TRANSACTION;
            SELECT @Status = Status, @BranchId = BranchId FROM dbo.StockTakes WITH (UPDLOCK) WHERE Id = @StockTakeId;
            IF @Status IS NULL THROW 50001, N'Phiếu kiểm kê không tồn tại.', 1;
            IF @Status <> 2 THROW 50002, N'Phiếu kiểm kê không ở trạng thái chờ duyệt.', 1;

            IF @Approve = 1
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.StockTakeItems i JOIN dbo.Products p ON p.Id = i.ProductId
                            WHERE i.StockTakeId = @StockTakeId AND i.DiffQty <> 0 AND p.RequiresSerial = 1)
                    THROW 50004, N'Sản phẩm quản lý theo serial bị lệch, hãy xử lý từng serial (usp_Serial_ChangeStatus) rồi kiểm kê lại.', 1;

                DECLARE @ProductId bigint, @Diff int;
                DECLARE c CURSOR LOCAL FAST_FORWARD FOR
                    SELECT ProductId, DiffQty FROM dbo.StockTakeItems WHERE StockTakeId = @StockTakeId AND DiffQty <> 0;
                OPEN c;
                FETCH NEXT FROM c INTO @ProductId, @Diff;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    EXEC dbo.usp_Internal_StockMove @BranchId, @ProductId, @Diff, 0, 6, 'StockTake', @StockTakeId, @UserId, @Note;
                    FETCH NEXT FROM c INTO @ProductId, @Diff;
                END
                CLOSE c; DEALLOCATE c;
            END

            UPDATE dbo.StockTakes SET Status = CASE WHEN @Approve = 1 THEN 3 ELSE 4 END, ApprovedBy = @UserId, ApprovedAt = SYSUTCDATETIME(),
                                      UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @StockTakeId;

            DECLARE @act varchar(60) = CASE WHEN @Approve = 1 THEN 'StockTakeApproved' ELSE 'StockTakeRejected' END;
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @BranchId = @BranchId, @Action = @act, @EntityType = 'StockTake', @EntityId = @StockTakeId, @Reason = @Note;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* GET /api/stock-takes/{id} */
CREATE OR ALTER PROCEDURE dbo.usp_StockTake_Get @StockTakeId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.StockTakes WHERE Id = @StockTakeId) THROW 50001, N'Phiếu kiểm kê không tồn tại.', 1;
    SELECT * FROM dbo.StockTakes WHERE Id = @StockTakeId;
    SELECT sti.ProductId, p.Sku, p.Name, p.RequiresSerial, sti.SystemQty, sti.CountedQty, sti.DiffQty, sti.Reason
      FROM dbo.StockTakeItems sti JOIN dbo.Products p ON p.Id = sti.ProductId WHERE sti.StockTakeId = @StockTakeId;
END
GO

/* =============================================================================
   SERIAL
   ============================================================================= */

/* GET /api/serials/{serialNumber}?productId=   tra cứu bảo hành bằng serial (UC-07, SER-04, WAR-02)
   Trả: thông tin serial, lịch sử trạng thái, phiếu bảo hành. */
CREATE OR ALTER PROCEDURE dbo.usp_Serial_Lookup
    @SerialNumber varchar(100), @ProductId bigint = NULL, @CustomerId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @SerialId bigint, @rc int;
    SELECT @SerialId = si.Id FROM dbo.SerialItems si
      LEFT JOIN dbo.OrderItems oi ON oi.Id = si.OrderItemId LEFT JOIN dbo.Orders o ON o.Id = oi.OrderId
     WHERE si.SerialNumber = @SerialNumber AND (@ProductId IS NULL OR si.ProductId = @ProductId)
       AND (@CustomerId IS NULL OR o.CustomerId = @CustomerId);     -- khách chỉ tra cứu hàng của mình
    SET @rc = @@ROWCOUNT;
    IF @rc = 0 THROW 50001, N'Không tìm thấy serial.', 1;
    IF @rc > 1 THROW 50004, N'Serial trùng nhiều sản phẩm, hãy truyền ProductId.', 1;

    DECLARE @Today date = dbo.fn_VnDate(SYSUTCDATETIME());
    SELECT si.Id AS SerialItemId, si.SerialNumber, si.Status, si.BranchId, si.ProductId, p.Sku, p.Name AS ProductName,
           si.SoldAt, si.WarrantyStartDate, si.WarrantyEndDate,
           CASE WHEN si.WarrantyEndDate IS NULL THEN NULL ELSE DATEDIFF(DAY, @Today, si.WarrantyEndDate) END AS WarrantyDaysLeft,
           CAST(CASE WHEN si.WarrantyEndDate IS NOT NULL AND si.WarrantyEndDate >= @Today THEN 1 ELSE 0 END AS bit) AS InWarranty,
           o.Id AS OrderId, o.OrderCode, o.CompletedAt AS PurchaseDate, c.Id AS CustomerId, c.FullName AS CustomerName, c.Phone AS CustomerPhone,
           si.ReplacedBySerialId
      FROM dbo.SerialItems si
      JOIN dbo.Products p ON p.Id = si.ProductId
      LEFT JOIN dbo.OrderItems oi ON oi.Id = si.OrderItemId
      LEFT JOIN dbo.Orders o ON o.Id = oi.OrderId
      LEFT JOIN dbo.Customers c ON c.Id = o.CustomerId
     WHERE si.Id = @SerialId;

    SELECT h.FromStatus, h.ToStatus, h.RefType, h.RefId, h.Note, h.ChangedAt FROM dbo.SerialStatusHistory h WHERE h.SerialItemId = @SerialId ORDER BY h.ChangedAt, h.Id;
    SELECT t.Id, t.TicketCode, t.Status, t.IssueDescription, t.ReceivedAt, t.ReturnedAt FROM dbo.WarrantyTickets t WHERE t.SerialItemId = @SerialId ORDER BY t.ReceivedAt DESC;
END
GO

/* POST /api/serials/{id}/status   đổi trạng thái thủ công (cần Quản lý duyệt, có Audit)
   Cho phép: InStock(0) -> Defective(7), Defective(7) -> Scrapped(9), Defective(7) -> InStock(0).
   Rời/vào InStock thì tồn thực tế của chi nhánh giảm/tăng 1. */
CREATE OR ALTER PROCEDURE dbo.usp_Serial_ChangeStatus
    @SerialItemId bigint, @ToStatus tinyint, @Reason nvarchar(300), @UserId bigint
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N'' THROW 50004, N'Phải nhập lý do.', 1;
        DECLARE @Status tinyint, @ProductId bigint, @BranchId int;
        BEGIN TRANSACTION;
            SELECT @Status = Status, @ProductId = ProductId, @BranchId = BranchId FROM dbo.SerialItems WITH (UPDLOCK) WHERE Id = @SerialItemId;
            IF @Status IS NULL THROW 50001, N'Serial không tồn tại.', 1;
            IF NOT EXISTS (SELECT 1 FROM (VALUES (0,7),(7,9),(7,0)) t (FromS, ToS) WHERE t.FromS = @Status AND t.ToS = @ToStatus)
                THROW 50002, N'Chuyển trạng thái serial này không được phép thủ công.', 1;

            IF @Status = 0 AND @ToStatus = 7
                EXEC dbo.usp_Internal_StockMove @BranchId, @ProductId, -1, 0, 7, 'SerialItem', @SerialItemId, @UserId, @Reason;
            IF @Status = 7 AND @ToStatus = 0
                EXEC dbo.usp_Internal_StockMove @BranchId, @ProductId, 1, 0, 7, 'SerialItem', @SerialItemId, @UserId, @Reason;

            UPDATE dbo.SerialItems SET Status = @ToStatus, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @SerialItemId;
            EXEC dbo.usp_Internal_LogSerial @SerialItemId, @Status, @ToStatus, 'Manual', NULL, @Reason, @UserId;

            DECLARE @o nvarchar(10) = CAST(@Status AS nvarchar(10)), @n nvarchar(10) = CAST(@ToStatus AS nvarchar(10));
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @BranchId = @BranchId, @Action = 'SerialStatusChanged', @EntityType = 'SerialItem',
                 @EntityId = @SerialItemId, @OldValues = @o, @NewValues = @n, @Reason = @Reason;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* =============================================================================
   NHÀ CUNG CẤP: trả hàng và công nợ (SUP-02, SUP-03)
   ============================================================================= */

/* POST /api/supplier-returns
   @ItemsJson: [{"productId":1,"quantity":2,"serials":["SN001","SN002"]}]
   Serial trả phải đang ở trạng thái Defective tại chi nhánh. Hàng không serial thì trừ tồn thực tế.
   @CreditAmount > 0: ghi giảm công nợ phải trả. */
CREATE OR ALTER PROCEDURE dbo.usp_SupplierReturn_Create
    @SupplierId bigint, @BranchId int, @ItemsJson nvarchar(max), @CreditAmount decimal(18,0) = 0, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Id = @SupplierId) THROW 50001, N'Nhà cung cấp không tồn tại.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Id = @BranchId AND IsActive = 1) THROW 50001, N'Chi nhánh không tồn tại hoặc đã ngừng hoạt động.', 1;
        IF @CreditAmount < 0 THROW 50004, N'Số tiền ghi giảm công nợ không hợp lệ.', 1;
        IF @ItemsJson IS NULL OR ISJSON(@ItemsJson) = 0 THROW 50004, N'Danh sách hàng trả không hợp lệ.', 1;

        CREATE TABLE #Raw (ProductId bigint, Quantity int);
        INSERT #Raw SELECT productId, quantity FROM OPENJSON(@ItemsJson) WITH (productId bigint '$.productId', quantity int '$.quantity');
        IF NOT EXISTS (SELECT 1 FROM #Raw) THROW 50004, N'Phiếu trả không có dòng hàng.', 1;
        IF EXISTS (SELECT 1 FROM #Raw GROUP BY ProductId HAVING COUNT(*) > 1) THROW 50004, N'Một sản phẩm xuất hiện nhiều lần.', 1;
        IF EXISTS (SELECT 1 FROM #Raw WHERE Quantity IS NULL OR Quantity <= 0) THROW 50004, N'Số lượng phải lớn hơn 0.', 1;
        IF EXISTS (SELECT 1 FROM #Raw r LEFT JOIN dbo.Products p ON p.Id = r.ProductId WHERE p.Id IS NULL) THROW 50001, N'Có sản phẩm không tồn tại.', 1;

        CREATE TABLE #It (ProductId bigint PRIMARY KEY, Quantity int, RequiresSerial bit);
        INSERT #It SELECT r.ProductId, r.Quantity, p.RequiresSerial FROM #Raw r JOIN dbo.Products p ON p.Id = r.ProductId;

        CREATE TABLE #Se (ProductId bigint, SerialNumber nvarchar(200));
        INSERT #Se SELECT j.productId, LTRIM(RTRIM(s.[value]))
          FROM OPENJSON(@ItemsJson) WITH (productId bigint '$.productId', serials nvarchar(max) '$.serials' AS JSON) j
          CROSS APPLY OPENJSON(j.serials) s WHERE j.serials IS NOT NULL;

        IF EXISTS (SELECT 1 FROM #Se GROUP BY ProductId, SerialNumber HAVING COUNT(*) > 1) THROW 50004, N'Có serial bị trùng.', 1;
        IF EXISTS (SELECT 1 FROM #It i WHERE i.RequiresSerial = 1 AND (SELECT COUNT(*) FROM #Se s WHERE s.ProductId = i.ProductId) <> i.Quantity)
            THROW 50004, N'Sản phẩm quản lý theo serial phải liệt kê đủ serial bằng số lượng.', 1;
        IF EXISTS (SELECT 1 FROM #It i WHERE i.RequiresSerial = 0 AND EXISTS (SELECT 1 FROM #Se s WHERE s.ProductId = i.ProductId))
            THROW 50004, N'Sản phẩm không quản lý theo serial không được kèm serial.', 1;
        IF EXISTS (SELECT 1 FROM #Se s LEFT JOIN dbo.SerialItems si ON si.ProductId = s.ProductId AND si.SerialNumber = CAST(s.SerialNumber AS varchar(100))
                                                                   AND si.Status = 7 AND si.BranchId = @BranchId
                    WHERE si.Id IS NULL)
            THROW 50002, N'Có serial không ở trạng thái lỗi (Defective) tại chi nhánh này.', 1;

        DECLARE @Seq bigint = NEXT VALUE FOR dbo.seq_SupplierReturn;
        DECLARE @Code varchar(30) = dbo.fn_FormatCode('TN', @Seq, ABS(CHECKSUM(NEWID())));
        DECLARE @ReturnId bigint, @ProductId bigint, @Qty int, @NegQty int;

        BEGIN TRANSACTION;
            INSERT dbo.SupplierReturns (ReturnCode, SupplierId, BranchId, Status, CreatedBy) VALUES (@Code, @SupplierId, @BranchId, 1, @UserId);
            SET @ReturnId = SCOPE_IDENTITY();

            -- hàng có serial: mỗi serial một dòng
            INSERT dbo.SupplierReturnItems (SupplierReturnId, ProductId, SerialItemId, Quantity)
            SELECT @ReturnId, si.ProductId, si.Id, 1
              FROM #Se s JOIN dbo.SerialItems si ON si.ProductId = s.ProductId AND si.SerialNumber = CAST(s.SerialNumber AS varchar(100));

            DECLARE @moved TABLE (SerialId bigint);
            UPDATE si SET Status = 8, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
            OUTPUT inserted.Id INTO @moved (SerialId)
              FROM dbo.SerialItems si JOIN dbo.SupplierReturnItems ri ON ri.SerialItemId = si.Id
             WHERE ri.SupplierReturnId = @ReturnId AND si.Status = 7;
            INSERT dbo.SerialStatusHistory (SerialItemId, FromStatus, ToStatus, RefType, RefId, ChangedBy) SELECT SerialId, 7, 8, 'SupplierReturn', @ReturnId, @UserId FROM @moved;

            -- hàng không serial: trừ tồn thực tế
            DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT ProductId, Quantity FROM #It WHERE RequiresSerial = 0;
            OPEN c;
            FETCH NEXT FROM c INTO @ProductId, @Qty;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                INSERT dbo.SupplierReturnItems (SupplierReturnId, ProductId, SerialItemId, Quantity) VALUES (@ReturnId, @ProductId, NULL, @Qty);
                SET @NegQty = -@Qty;
                EXEC dbo.usp_Internal_StockMove @BranchId, @ProductId, @NegQty, 0, 9, 'SupplierReturn', @ReturnId, @UserId, NULL;
                FETCH NEXT FROM c INTO @ProductId, @Qty;
            END
            CLOSE c; DEALLOCATE c;

            IF @CreditAmount > 0
                INSERT dbo.SupplierLedger (SupplierId, SupplierReturnId, EntryType, Amount, CreatedBy) VALUES (@SupplierId, @ReturnId, 2, -@CreditAmount, @UserId);
        COMMIT TRANSACTION;

        SELECT @ReturnId AS SupplierReturnId, @Code AS ReturnCode;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/suppliers/{id}/payments   ghi nhận thanh toán cho nhà cung cấp */
CREATE OR ALTER PROCEDURE dbo.usp_SupplierPayment_Record
    @SupplierId bigint, @Amount decimal(18,0), @PurchaseReceiptId bigint = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Amount <= 0 THROW 50004, N'Số tiền phải lớn hơn 0.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Id = @SupplierId) THROW 50001, N'Nhà cung cấp không tồn tại.', 1;
        IF @PurchaseReceiptId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.PurchaseReceipts WHERE Id = @PurchaseReceiptId AND SupplierId = @SupplierId)
            THROW 50001, N'Phiếu nhập không tồn tại hoặc không thuộc nhà cung cấp này.', 1;

        BEGIN TRANSACTION;
            DECLARE @Balance decimal(18,0) = (SELECT ISNULL(SUM(Amount), 0) FROM dbo.SupplierLedger WITH (UPDLOCK, HOLDLOCK) WHERE SupplierId = @SupplierId);
            IF @Amount > @Balance THROW 50006, N'Số tiền thanh toán vượt quá công nợ phải trả hiện tại.', 1;
            INSERT dbo.SupplierLedger (SupplierId, PurchaseReceiptId, EntryType, Amount, CreatedBy) VALUES (@SupplierId, @PurchaseReceiptId, 1, -@Amount, @UserId);
        COMMIT TRANSACTION;

        SELECT @Balance - @Amount AS BalanceAfter;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* GET /api/suppliers/{id}/ledger   sao kê công nợ phải trả */
CREATE OR ALTER PROCEDURE dbo.usp_Supplier_GetLedger @SupplierId bigint, @PageNumber int = 1, @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 50;
    IF @PageSize > 200 SET @PageSize = 200;
    SELECT (SELECT ISNULL(SUM(Amount), 0) FROM dbo.SupplierLedger WHERE SupplierId = @SupplierId) AS Balance;
    SELECT l.Id, l.EntryType, l.Amount, l.DueDate, l.PurchaseReceiptId, l.SupplierReturnId, l.CreatedAt, COUNT(*) OVER () AS TotalCount
      FROM dbo.SupplierLedger l WHERE l.SupplierId = @SupplierId
     ORDER BY l.CreatedAt DESC, l.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO
