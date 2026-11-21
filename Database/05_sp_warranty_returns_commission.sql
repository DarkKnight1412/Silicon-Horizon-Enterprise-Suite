/* =============================================================================
   05. BẢO HÀNH, ĐỔI TRẢ, HOA HỒNG
   Chạy SAU 02, 03, 04. (usp_Order_Complete ở file 03 gọi usp_Internal_AccrueCommission ở file này.)

   Vòng đời phiếu bảo hành (WarrantyTickets.Status):
     0 Received, 1 Diagnosing, 2 SentToVendor, 3 Repaired, 4 Replaced, 5 Rejected, 6 ReturnedToCustomer
   Vòng đời yêu cầu đổi trả (ReturnRequests.Status):
     0 Requested, 1 Approved, 2 Rejected, 3 GoodsReceived, 4 Refunded, 5 Closed
   ============================================================================= */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =============================================================================
   HOA HỒNG (KPI-01..06) - proc nội bộ
   ============================================================================= */

/* Lấy kỳ hoa hồng đang mở cho một ngày. Kỳ đã chốt thì chuyển sang tháng kế tiếp còn mở (KPI-06). */
CREATE OR ALTER PROCEDURE dbo.usp_Internal_GetOpenPeriod
    @Date date, @PeriodId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ym char(7) = CONVERT(char(7), @Date, 126), @status tinyint, @guard int = 0;
    WHILE @guard < 24
    BEGIN
        SELECT @PeriodId = Id, @status = Status FROM dbo.CommissionPeriods WHERE YearMonth = @ym;
        IF @PeriodId IS NULL
        BEGIN
            INSERT dbo.CommissionPeriods (YearMonth, Status) VALUES (@ym, 0);
            SET @PeriodId = SCOPE_IDENTITY();
            RETURN;
        END
        IF @status = 0 RETURN;
        SET @PeriodId = NULL;
        SET @Date = DATEADD(MONTH, 1, DATEFROMPARTS(YEAR(@Date), MONTH(@Date), 1));
        SET @ym = CONVERT(char(7), @Date, 126);
        SET @guard += 1;
    END;
    THROW 50002, N'Không tìm được kỳ hoa hồng đang mở.', 1;
END
GO

/* Tính hoa hồng khi đơn hoàn tất, chia theo tỷ lệ nhân sự tư vấn (KPI-03).
   Đơn online không có nhân viên tư vấn thì không phát sinh (KPI-02).
   Dùng quy tắc phạm vi "All" (ScopeType = 0) đang hiệu lực, mới nhất; chưa có quy tắc thì bỏ qua.
   BasisType 0: doanh thu = tổng tiền hàng sau giảm giá (không tính phí vận chuyển);
   BasisType 1: lợi nhuận gộp = doanh thu - giá vốn (serial: UnitCost; hàng không serial: giá nhập gần nhất). */
CREATE OR ALTER PROCEDURE dbo.usp_Internal_AccrueCommission
    @OrderId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.OrderSalesStaff WHERE OrderId = @OrderId) RETURN;
    IF EXISTS (SELECT 1 FROM dbo.CommissionEntries WHERE OrderId = @OrderId AND EntryType = 0) RETURN;   -- đã tính

    DECLARE @Date date = dbo.fn_VnDate(SYSUTCDATETIME()), @RuleId int, @Basis tinyint, @Rate decimal(5,2);
    SELECT TOP 1 @RuleId = Id, @Basis = BasisType, @Rate = RatePercent
      FROM dbo.CommissionRules
     WHERE ScopeType = 0 AND EffectiveFrom <= @Date AND (EffectiveTo IS NULL OR EffectiveTo >= @Date)
     ORDER BY EffectiveFrom DESC, Id DESC;
    IF @RuleId IS NULL RETURN;

    DECLARE @Revenue decimal(18,0), @Cost decimal(18,0) = 0, @Base decimal(18,0);
    SELECT @Revenue = SubTotal - DiscountTotal FROM dbo.Orders WHERE Id = @OrderId;

    IF @Basis = 1
    BEGIN
        SELECT @Cost = ISNULL(SUM(x.Cost), 0)
          FROM (SELECT ISNULL((SELECT SUM(si.UnitCost) FROM dbo.SerialItems si WHERE si.OrderItemId = oi.Id), 0)
                       + CASE WHEN oi.RequiresSerial = 0 THEN oi.Quantity * ISNULL(p.LastImportPrice, 0) ELSE 0 END AS Cost
                  FROM dbo.OrderItems oi JOIN dbo.Products p ON p.Id = oi.ProductId WHERE oi.OrderId = @OrderId) x;
        SET @Base = CASE WHEN @Revenue - @Cost > 0 THEN @Revenue - @Cost ELSE 0 END;
    END
    ELSE SET @Base = CASE WHEN @Revenue > 0 THEN @Revenue ELSE 0 END;

    DECLARE @PeriodId int;
    EXEC dbo.usp_Internal_GetOpenPeriod @Date, @PeriodId OUTPUT;

    INSERT dbo.CommissionEntries (PeriodId, EmployeeId, OrderId, RuleId, EntryType, BaseAmount, RatePercent, Amount)
    SELECT @PeriodId, s.EmployeeId, @OrderId, @RuleId, 0,
           CAST(ROUND(@Base * s.ShareRate / 100, 0) AS decimal(18,0)), @Rate,
           CAST(ROUND(@Base * s.ShareRate / 100 * @Rate / 100, 0) AS decimal(18,0))
      FROM dbo.OrderSalesStaff s WHERE s.OrderId = @OrderId;
END
GO

/* Thu hồi hoa hồng theo tỷ lệ tiền hoàn / doanh thu đơn (KPI-04). Ghi vào kỳ đang mở gần nhất (có thể làm số dư âm). */
CREATE OR ALTER PROCEDURE dbo.usp_Internal_ClawbackCommission
    @ReturnRequestId bigint, @RefundAmount decimal(18,0)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OrderId bigint = (SELECT OrderId FROM dbo.ReturnRequests WHERE Id = @ReturnRequestId);
    DECLARE @Net decimal(18,0) = (SELECT SubTotal - DiscountTotal FROM dbo.Orders WHERE Id = @OrderId);
    IF @Net IS NULL OR @Net <= 0 OR @RefundAmount <= 0 RETURN;
    IF NOT EXISTS (SELECT 1 FROM dbo.CommissionEntries WHERE OrderId = @OrderId AND EntryType = 0) RETURN;

    DECLARE @Today date = dbo.fn_VnDate(SYSUTCDATETIME()), @PeriodId int;
    EXEC dbo.usp_Internal_GetOpenPeriod @Today, @PeriodId OUTPUT;

    DECLARE @Ratio decimal(18,10) = CASE WHEN @RefundAmount > @Net THEN 1 ELSE CAST(@RefundAmount AS decimal(18,10)) / @Net END;

    INSERT dbo.CommissionEntries (PeriodId, EmployeeId, OrderId, ReturnRequestId, RuleId, EntryType, BaseAmount, RatePercent, Amount)
    SELECT @PeriodId, e.EmployeeId, e.OrderId, @ReturnRequestId, e.RuleId, 1,
           -CAST(ROUND(e.BaseAmount * @Ratio, 0) AS decimal(18,0)), e.RatePercent,
           -CAST(ROUND(e.Amount * @Ratio, 0) AS decimal(18,0))
      FROM dbo.CommissionEntries e
     WHERE e.OrderId = @OrderId AND e.EntryType = 0;
END
GO

/* POST /api/admin/commission-periods/{id}/close  (UC-28)  chốt kỳ; sau đó chỉ điều chỉnh qua kỳ sau */
CREATE OR ALTER PROCEDURE dbo.usp_Commission_ClosePeriod @PeriodId int, @UserId bigint
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
            DECLARE @Status tinyint = (SELECT Status FROM dbo.CommissionPeriods WITH (UPDLOCK) WHERE Id = @PeriodId);
            IF @Status IS NULL THROW 50001, N'Kỳ hoa hồng không tồn tại.', 1;
            IF @Status = 1 THROW 50002, N'Kỳ hoa hồng đã chốt.', 1;
            UPDATE dbo.CommissionPeriods SET Status = 1, ClosedBy = @UserId, ClosedAt = SYSUTCDATETIME() WHERE Id = @PeriodId;
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @Action = 'CommissionPeriodClosed', @EntityType = 'CommissionPeriod', @EntityId = @PeriodId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* GET /api/commissions?yearMonth=2026-11  - tổng hợp theo nhân viên và chi tiết (nhân viên chỉ xem của mình: truyền EmployeeId) */
CREATE OR ALTER PROCEDURE dbo.usp_Commission_Summary
    @YearMonth char(7), @EmployeeId bigint = NULL, @BranchId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.Id AS PeriodId, p.YearMonth, p.Status FROM dbo.CommissionPeriods p WHERE p.YearMonth = @YearMonth;

    SELECT e.EmployeeId, emp.FullName, emp.BranchId,
           SUM(CASE WHEN ce.EntryType = 0 THEN ce.Amount ELSE 0 END) AS Earned,
           SUM(CASE WHEN ce.EntryType = 1 THEN ce.Amount ELSE 0 END) AS Clawback,
           SUM(CASE WHEN ce.EntryType = 2 THEN ce.Amount ELSE 0 END) AS Adjustment,
           SUM(ce.Amount) AS Total
      FROM dbo.CommissionEntries ce
      JOIN dbo.CommissionPeriods p ON p.Id = ce.PeriodId AND p.YearMonth = @YearMonth
      JOIN dbo.Employees emp ON emp.Id = ce.EmployeeId
      CROSS APPLY (SELECT ce.EmployeeId) e
     WHERE (@EmployeeId IS NULL OR ce.EmployeeId = @EmployeeId) AND (@BranchId IS NULL OR emp.BranchId = @BranchId)
     GROUP BY e.EmployeeId, emp.FullName, emp.BranchId
     ORDER BY Total DESC;

    SELECT ce.Id, ce.EmployeeId, ce.OrderId, o.OrderCode, ce.ReturnRequestId, ce.EntryType, ce.BaseAmount, ce.RatePercent, ce.Amount, ce.CreatedAt
      FROM dbo.CommissionEntries ce
      JOIN dbo.CommissionPeriods p ON p.Id = ce.PeriodId AND p.YearMonth = @YearMonth
      JOIN dbo.Employees emp ON emp.Id = ce.EmployeeId
      JOIN dbo.Orders o ON o.Id = ce.OrderId
     WHERE (@EmployeeId IS NULL OR ce.EmployeeId = @EmployeeId) AND (@BranchId IS NULL OR emp.BranchId = @BranchId)
     ORDER BY ce.CreatedAt DESC;
END
GO

/* POST /api/admin/commissions/adjustments  - bút toán điều chỉnh thủ công vào kỳ đang mở (có Audit) */
CREATE OR ALTER PROCEDURE dbo.usp_Commission_Adjust
    @EmployeeId bigint, @OrderId bigint, @Amount decimal(18,0), @Reason nvarchar(300), @UserId bigint
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Amount = 0 THROW 50004, N'Số tiền điều chỉnh phải khác 0.', 1;
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N'' THROW 50004, N'Phải nhập lý do điều chỉnh.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE Id = @EmployeeId) THROW 50001, N'Nhân viên không tồn tại.', 1;
        DECLARE @RuleId int = (SELECT TOP 1 RuleId FROM dbo.CommissionEntries WHERE OrderId = @OrderId ORDER BY Id);
        IF @RuleId IS NULL SELECT TOP 1 @RuleId = Id FROM dbo.CommissionRules ORDER BY Id;
        IF @RuleId IS NULL THROW 50004, N'Chưa có quy tắc hoa hồng nào.', 1;

        DECLARE @Today date = dbo.fn_VnDate(SYSUTCDATETIME()), @PeriodId int, @EntryId bigint;
        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_GetOpenPeriod @Today, @PeriodId OUTPUT;
            INSERT dbo.CommissionEntries (PeriodId, EmployeeId, OrderId, RuleId, EntryType, BaseAmount, RatePercent, Amount, CreatedBy)
            VALUES (@PeriodId, @EmployeeId, @OrderId, @RuleId, 2, 0, 0, @Amount, @UserId);
            SET @EntryId = SCOPE_IDENTITY();
            DECLARE @n nvarchar(30) = CAST(@Amount AS nvarchar(30));
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @Action = 'CommissionAdjusted', @EntityType = 'CommissionEntry', @EntityId = @EntryId, @NewValues = @n, @Reason = @Reason;
        COMMIT TRANSACTION;
        SELECT @EntryId AS EntryId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* =============================================================================
   BẢO HÀNH (UC-16, UC-17)
   ============================================================================= */

/* POST /api/warranty-tickets   tiếp nhận (WAR-04, WAR-05)
   - Hàng có serial: truyền @SerialNumber (+ @ProductId nếu trùng). Serial phải đang ở trạng thái Sold.
   - Hàng không serial: truyền @OrderItemId (đơn đã hoàn tất, bảo hành tính từ ngày hoàn tất đơn - WAR-03).
   - Hết hạn hoặc có @RejectReason: phiếu vào thẳng trạng thái Rejected (có lý do), serial giữ nguyên Sold.
   - Hợp lệ: phiếu Received, serial Sold -> RMA. */
CREATE OR ALTER PROCEDURE dbo.usp_Warranty_Receive
    @BranchId int, @IssueDescription nvarchar(1000), @SerialNumber varchar(100) = NULL, @ProductId bigint = NULL,
    @OrderItemId bigint = NULL, @ConditionNotes nvarchar(1000) = NULL, @RejectReason nvarchar(500) = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @SerialNumber IS NULL AND @OrderItemId IS NULL THROW 50004, N'Phải truyền serial hoặc dòng đơn hàng.', 1;
        IF @IssueDescription IS NULL OR LTRIM(RTRIM(@IssueDescription)) = N'' THROW 50004, N'Phải mô tả lỗi.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Id = @BranchId AND IsActive = 1) THROW 50001, N'Chi nhánh không tồn tại.', 1;

        DECLARE @Today date = dbo.fn_VnDate(SYSUTCDATETIME()), @SerialId bigint, @SStatus tinyint, @OIId bigint, @WEnd date, @rc int;

        IF @SerialNumber IS NOT NULL
        BEGIN
            SELECT @SerialId = Id, @SStatus = Status, @OIId = OrderItemId, @WEnd = WarrantyEndDate
              FROM dbo.SerialItems WHERE SerialNumber = @SerialNumber AND (@ProductId IS NULL OR ProductId = @ProductId);
            SET @rc = @@ROWCOUNT;
            IF @rc = 0 THROW 50001, N'Không tìm thấy serial.', 1;
            IF @rc > 1 THROW 50004, N'Serial trùng nhiều sản phẩm, hãy truyền ProductId.', 1;
            IF @SStatus = 4 THROW 50002, N'Serial đang trong quá trình bảo hành.', 1;
            IF @SStatus <> 3 THROW 50002, N'Serial chưa bán hoặc không ở trạng thái đã bán nên không thể tiếp nhận bảo hành.', 1;
        END
        ELSE
        BEGIN
            DECLARE @RequiresSerial bit, @OStatus tinyint, @Completed datetime2(0), @Months int;
            SELECT @RequiresSerial = oi.RequiresSerial, @OStatus = o.Status, @Completed = o.CompletedAt, @Months = oi.WarrantyMonths
              FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.Id = oi.OrderId WHERE oi.Id = @OrderItemId;
            IF @RequiresSerial IS NULL THROW 50001, N'Dòng đơn hàng không tồn tại.', 1;
            IF @RequiresSerial = 1 THROW 50004, N'Hàng quản lý theo serial phải tiếp nhận bằng serial.', 1;
            IF @OStatus NOT IN (7, 9, 10) OR @Completed IS NULL THROW 50002, N'Đơn hàng chưa hoàn tất.', 1;
            SET @OIId = @OrderItemId;
            SET @WEnd = DATEADD(MONTH, @Months, dbo.fn_VnDate(@Completed));
        END

        DECLARE @CustomerId bigint = (SELECT o.CustomerId FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.Id = oi.OrderId WHERE oi.Id = @OIId);
        IF @CustomerId IS NULL THROW 50001, N'Không xác định được khách hàng của hàng này.', 1;

        DECLARE @Reject nvarchar(500) = @RejectReason;
        IF @Reject IS NULL AND (@WEnd IS NULL OR @WEnd < @Today) SET @Reject = N'Hết thời hạn bảo hành.';
        DECLARE @Status tinyint = CASE WHEN @Reject IS NOT NULL THEN 5 ELSE 0 END;

        DECLARE @Seq bigint = NEXT VALUE FOR dbo.seq_Warranty;
        DECLARE @Code varchar(30) = dbo.fn_FormatCode('BH', @Seq, ABS(CHECKSUM(NEWID())));
        DECLARE @TicketId bigint;

        BEGIN TRANSACTION;
            INSERT dbo.WarrantyTickets (TicketCode, SerialItemId, OrderItemId, CustomerId, BranchId, Status, IssueDescription, ConditionNotes, RejectionReason, CreatedBy)
            VALUES (@Code, @SerialId, CASE WHEN @SerialId IS NULL THEN @OIId END, @CustomerId, @BranchId, @Status, @IssueDescription, @ConditionNotes, @Reject, @UserId);
            SET @TicketId = SCOPE_IDENTITY();

            INSERT dbo.WarrantyTicketHistory (TicketId, FromStatus, ToStatus, Note, ChangedBy) VALUES (@TicketId, NULL, @Status, ISNULL(@Reject, N'Tiếp nhận bảo hành'), @UserId);

            IF @Status = 0 AND @SerialId IS NOT NULL
            BEGIN
                UPDATE dbo.SerialItems SET Status = 4, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @SerialId AND Status = 3;
                IF @@ROWCOUNT = 0 THROW 50002, N'Serial vừa thay đổi trạng thái bởi thao tác khác.', 1;
                EXEC dbo.usp_Internal_LogSerial @SerialId, 3, 4, 'WarrantyTicket', @TicketId, N'Tiếp nhận bảo hành', @UserId;
            END
        COMMIT TRANSACTION;

        SELECT @TicketId AS TicketId, @Code AS TicketCode, @Status AS Status, @Reject AS RejectionReason;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/warranty-tickets/{id}/status   chuyển trạng thái (WAR-06, WAR-07)
   Hợp lệ: 0->1,5 | 1->2,3,4,5 | 2->3,4,5 | 3,4,5->6
   - Replaced (4): bắt buộc @ReplacementSerialNumber (cùng sản phẩm, đang InStock tại chi nhánh nhận). Serial cũ -> Replaced,
     serial mới -> Sold kế thừa hạn bảo hành còn lại, trừ tồn thực tế 1.
   - Rejected (5): bắt buộc @RejectionReason.
   - ReturnedToCustomer (6): serial đang RMA quay về Sold (đã sửa xong hoặc bị từ chối). */
CREATE OR ALTER PROCEDURE dbo.usp_Warranty_ChangeStatus
    @TicketId bigint, @ToStatus tinyint, @Note nvarchar(500) = NULL, @ReplacementSerialNumber varchar(100) = NULL,
    @RejectionReason nvarchar(500) = NULL, @Fee decimal(18,0) = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @Today date = dbo.fn_VnDate(SYSUTCDATETIME());
        DECLARE @Status tinyint, @SerialId bigint, @BranchId int, @OIId bigint;

        BEGIN TRANSACTION;
            SELECT @Status = Status, @SerialId = SerialItemId, @BranchId = BranchId FROM dbo.WarrantyTickets WITH (UPDLOCK) WHERE Id = @TicketId;
            IF @Status IS NULL THROW 50001, N'Phiếu bảo hành không tồn tại.', 1;

            IF NOT EXISTS (SELECT 1 FROM (VALUES (0,1),(0,5),(1,2),(1,3),(1,4),(1,5),(2,3),(2,4),(2,5),(3,6),(4,6),(5,6)) t (FromS, ToS)
                            WHERE t.FromS = @Status AND t.ToS = @ToStatus)
            BEGIN
                DECLARE @m nvarchar(200) = CONCAT(N'Không thể chuyển phiếu từ trạng thái ', @Status, N' sang ', @ToStatus, N'.');
                THROW 50002, @m, 1;
            END
            IF @Fee IS NOT NULL AND @Fee < 0 THROW 50004, N'Phí không hợp lệ.', 1;

            IF @ToStatus = 5 AND (@RejectionReason IS NULL OR LTRIM(RTRIM(@RejectionReason)) = N'') THROW 50004, N'Phải nhập lý do từ chối bảo hành.', 1;

            -- Đổi mới: thay serial
            IF @ToStatus = 4 AND @SerialId IS NOT NULL
            BEGIN
                IF @ReplacementSerialNumber IS NULL THROW 50004, N'Đổi mới phải chỉ định serial thay thế.', 1;
                DECLARE @OldProduct bigint, @OldEnd date, @NewId bigint;
                SELECT @OldProduct = ProductId, @OldEnd = WarrantyEndDate, @OIId = OrderItemId FROM dbo.SerialItems WHERE Id = @SerialId;
                SELECT @NewId = Id FROM dbo.SerialItems WHERE ProductId = @OldProduct AND SerialNumber = @ReplacementSerialNumber AND Status = 0 AND BranchId = @BranchId;
                IF @NewId IS NULL THROW 50002, N'Serial thay thế không tồn tại, không cùng sản phẩm hoặc không ở trạng thái trong kho tại chi nhánh.', 1;

                -- hàng mới xuất khỏi kho
                EXEC dbo.usp_Internal_StockMove @BranchId, @OldProduct, -1, 0, 3, 'WarrantyTicket', @TicketId, @UserId, N'Đổi mới bảo hành';

                UPDATE dbo.SerialItems SET Status = 6, ReplacedBySerialId = @NewId, UpdatedAt = @Now, UpdatedBy = @UserId WHERE Id = @SerialId AND Status = 4;
                IF @@ROWCOUNT = 0 THROW 50002, N'Serial cũ không ở trạng thái bảo hành (RMA).', 1;
                EXEC dbo.usp_Internal_LogSerial @SerialId, 4, 6, 'WarrantyTicket', @TicketId, N'Đổi mới', @UserId;

                UPDATE dbo.SerialItems SET Status = 3, OrderItemId = @OIId, SoldAt = @Now, WarrantyStartDate = @Today,
                                           WarrantyEndDate = @OldEnd, UpdatedAt = @Now, UpdatedBy = @UserId
                 WHERE Id = @NewId AND Status = 0;
                IF @@ROWCOUNT = 0 THROW 50002, N'Serial thay thế vừa được dùng bởi thao tác khác.', 1;
                EXEC dbo.usp_Internal_LogSerial @NewId, 0, 3, 'WarrantyTicket', @TicketId, N'Hàng đổi bảo hành, kế thừa hạn bảo hành', @UserId;

                UPDATE dbo.WarrantyTickets SET ReplacementSerialId = @NewId WHERE Id = @TicketId;
            END

            -- Trả khách: serial RMA quay về Sold
            IF @ToStatus = 6 AND @SerialId IS NOT NULL
            BEGIN
                UPDATE dbo.SerialItems SET Status = 3, UpdatedAt = @Now, UpdatedBy = @UserId WHERE Id = @SerialId AND Status = 4;
                IF @@ROWCOUNT > 0 EXEC dbo.usp_Internal_LogSerial @SerialId, 4, 3, 'WarrantyTicket', @TicketId, N'Trả khách sau bảo hành', @UserId;
            END

            UPDATE dbo.WarrantyTickets
               SET Status = @ToStatus,
                   RejectionReason = CASE WHEN @ToStatus = 5 THEN @RejectionReason ELSE RejectionReason END,
                   VendorSentAt = CASE WHEN @ToStatus = 2 THEN @Now ELSE VendorSentAt END,
                   VendorReturnedAt = CASE WHEN @Status = 2 AND @ToStatus IN (3, 4) THEN @Now ELSE VendorReturnedAt END,
                   ReturnedAt = CASE WHEN @ToStatus = 6 THEN @Now ELSE ReturnedAt END,
                   Fee = ISNULL(@Fee, Fee), UpdatedAt = @Now, UpdatedBy = @UserId
             WHERE Id = @TicketId;

            INSERT dbo.WarrantyTicketHistory (TicketId, FromStatus, ToStatus, Note, ChangedBy) VALUES (@TicketId, @Status, @ToStatus, ISNULL(@Note, @RejectionReason), @UserId);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/warranty-tickets/{id}/attachments */
CREATE OR ALTER PROCEDURE dbo.usp_Warranty_AddAttachment @TicketId bigint, @Url varchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.WarrantyTickets WHERE Id = @TicketId) THROW 50001, N'Phiếu bảo hành không tồn tại.', 1;
    INSERT dbo.WarrantyTicketAttachments (TicketId, Url) VALUES (@TicketId, @Url);
END
GO

/* GET /api/warranty-tickets/{id} */
CREATE OR ALTER PROCEDURE dbo.usp_Warranty_Get @TicketId bigint, @CustomerId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.WarrantyTickets WHERE Id = @TicketId AND (@CustomerId IS NULL OR CustomerId = @CustomerId)) THROW 50001, N'Phiếu bảo hành không tồn tại.', 1;
    SELECT t.*, si.SerialNumber, p.Sku, p.Name AS ProductName, c.FullName AS CustomerName, c.Phone AS CustomerPhone, rs.SerialNumber AS ReplacementSerialNumber
      FROM dbo.WarrantyTickets t
      JOIN dbo.Customers c ON c.Id = t.CustomerId
      LEFT JOIN dbo.SerialItems si ON si.Id = t.SerialItemId
      LEFT JOIN dbo.SerialItems rs ON rs.Id = t.ReplacementSerialId
      LEFT JOIN dbo.OrderItems oi ON oi.Id = t.OrderItemId
      LEFT JOIN dbo.Products p ON p.Id = COALESCE(si.ProductId, oi.ProductId)
     WHERE t.Id = @TicketId;
    SELECT FromStatus, ToStatus, Note, ChangedBy, ChangedAt FROM dbo.WarrantyTicketHistory WHERE TicketId = @TicketId ORDER BY ChangedAt, Id;
    SELECT Id, Url FROM dbo.WarrantyTicketAttachments WHERE TicketId = @TicketId;
END
GO

/* GET /api/warranty-tickets */
CREATE OR ALTER PROCEDURE dbo.usp_Warranty_List
    @BranchId int = NULL, @CustomerId bigint = NULL, @Status tinyint = NULL, @Keyword nvarchar(100) = NULL, @PageNumber int = 1, @PageSize int = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    IF @PageSize > 100 SET @PageSize = 100;
    SELECT t.Id, t.TicketCode, t.Status, t.BranchId, t.CustomerId, c.FullName AS CustomerName, si.SerialNumber, t.ReceivedAt, t.ReturnedAt,
           COUNT(*) OVER () AS TotalCount
      FROM dbo.WarrantyTickets t
      JOIN dbo.Customers c ON c.Id = t.CustomerId
      LEFT JOIN dbo.SerialItems si ON si.Id = t.SerialItemId
     WHERE (@BranchId IS NULL OR t.BranchId = @BranchId) AND (@CustomerId IS NULL OR t.CustomerId = @CustomerId) AND (@Status IS NULL OR t.Status = @Status)
       AND (@Keyword IS NULL OR t.TicketCode LIKE @Keyword + N'%' OR si.SerialNumber LIKE @Keyword + N'%' OR c.Phone LIKE @Keyword + N'%')
     ORDER BY t.ReceivedAt DESC, t.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* =============================================================================
   ĐỔI TRẢ (UC-08, UC-18, UC-20, RET-01..05)
   ============================================================================= */

/* POST /api/orders/{id}/returns   khách tạo yêu cầu
   @ItemsJson: [{"orderItemId":10,"quantity":1,"serialNumbers":["SN001"]}]  (hàng có serial: liệt kê đủ serial)
   Điều kiện: đơn Completed/PartiallyReturned, trong hạn đổi trả (SystemSettings return.window.days, tính từ ngày hoàn tất),
   không trả quá số lượng đã mua trừ phần đã trả ở các yêu cầu chưa bị từ chối.
   Tiền hoàn dự kiến = giá dòng sau khuyến mãi, trừ phần voucher phân bổ theo tỷ lệ; không hoàn phí vận chuyển. */
CREATE OR ALTER PROCEDURE dbo.usp_Return_Create
    @OrderId bigint, @CustomerId bigint, @Reason nvarchar(500), @ItemsJson nvarchar(max), @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N'' THROW 50004, N'Phải nhập lý do đổi trả.', 1;
        IF @ItemsJson IS NULL OR ISJSON(@ItemsJson) = 0 THROW 50004, N'Danh sách hàng trả không hợp lệ.', 1;

        DECLARE @Status tinyint, @BranchId int, @Completed datetime2(0), @Sub decimal(18,0), @Disc decimal(18,0);
        SELECT @Status = Status, @BranchId = BranchId, @Completed = CompletedAt, @Sub = SubTotal, @Disc = DiscountTotal
          FROM dbo.Orders WHERE Id = @OrderId AND CustomerId = @CustomerId;
        IF @Status IS NULL THROW 50001, N'Đơn hàng không tồn tại.', 1;
        IF @Status NOT IN (7, 9) OR @Completed IS NULL THROW 50002, N'Chỉ đổi trả được với đơn đã hoàn tất.', 1;

        DECLARE @Days int = ISNULL(TRY_CAST((SELECT [Value] FROM dbo.SystemSettings WHERE [Key] = 'return.window.days') AS int), 7);
        IF DATEDIFF(DAY, @Completed, SYSUTCDATETIME()) > @Days THROW 50006, N'Đã quá thời hạn đổi trả.', 1;

        CREATE TABLE #Raw (OrderItemId bigint, Quantity int);
        INSERT #Raw SELECT orderItemId, quantity FROM OPENJSON(@ItemsJson) WITH (orderItemId bigint '$.orderItemId', quantity int '$.quantity');
        IF NOT EXISTS (SELECT 1 FROM #Raw) THROW 50004, N'Yêu cầu không có dòng hàng.', 1;
        IF EXISTS (SELECT 1 FROM #Raw GROUP BY OrderItemId HAVING COUNT(*) > 1) THROW 50004, N'Một dòng hàng xuất hiện nhiều lần.', 1;
        IF EXISTS (SELECT 1 FROM #Raw WHERE Quantity IS NULL OR Quantity <= 0) THROW 50004, N'Số lượng phải lớn hơn 0.', 1;
        IF EXISTS (SELECT 1 FROM #Raw r LEFT JOIN dbo.OrderItems oi ON oi.Id = r.OrderItemId AND oi.OrderId = @OrderId WHERE oi.Id IS NULL)
            THROW 50001, N'Có dòng hàng không thuộc đơn này.', 1;

        -- không trả vượt số đã mua
        IF EXISTS (
            SELECT 1 FROM #Raw r JOIN dbo.OrderItems oi ON oi.Id = r.OrderItemId
             WHERE r.Quantity + ISNULL((SELECT SUM(ri.Quantity) FROM dbo.ReturnItems ri JOIN dbo.ReturnRequests rq ON rq.Id = ri.ReturnRequestId
                                          WHERE ri.OrderItemId = oi.Id AND rq.Status <> 2), 0) > oi.Quantity)
            THROW 50006, N'Số lượng trả vượt quá số lượng đã mua (kể cả các yêu cầu đang xử lý).', 1;

        CREATE TABLE #Se (OrderItemId bigint, SerialNumber nvarchar(200));
        INSERT #Se SELECT j.orderItemId, LTRIM(RTRIM(s.[value]))
          FROM OPENJSON(@ItemsJson) WITH (orderItemId bigint '$.orderItemId', serialNumbers nvarchar(max) '$.serialNumbers' AS JSON) j
          CROSS APPLY OPENJSON(j.serialNumbers) s WHERE j.serialNumbers IS NOT NULL;

        IF EXISTS (SELECT 1 FROM #Se GROUP BY OrderItemId, SerialNumber HAVING COUNT(*) > 1) THROW 50004, N'Có serial bị trùng.', 1;
        IF EXISTS (SELECT 1 FROM #Raw r JOIN dbo.OrderItems oi ON oi.Id = r.OrderItemId
                    WHERE oi.RequiresSerial = 1 AND (SELECT COUNT(*) FROM #Se s WHERE s.OrderItemId = r.OrderItemId) <> r.Quantity)
            THROW 50004, N'Hàng quản lý theo serial phải liệt kê đủ serial bằng số lượng trả.', 1;
        IF EXISTS (SELECT 1 FROM #Raw r JOIN dbo.OrderItems oi ON oi.Id = r.OrderItemId
                    WHERE oi.RequiresSerial = 0 AND EXISTS (SELECT 1 FROM #Se s WHERE s.OrderItemId = r.OrderItemId))
            THROW 50004, N'Hàng không quản lý theo serial không được kèm serial.', 1;
        -- serial phải thuộc dòng, đang Sold, chưa nằm trong yêu cầu đổi trả khác còn hiệu lực
        IF EXISTS (SELECT 1 FROM #Se s LEFT JOIN dbo.SerialItems si ON si.OrderItemId = s.OrderItemId AND si.SerialNumber = CAST(s.SerialNumber AS varchar(100)) AND si.Status = 3
                    WHERE si.Id IS NULL)
            THROW 50002, N'Có serial không thuộc dòng hàng này hoặc không ở trạng thái đã bán (đang bảo hành, đã đổi trả...).', 1;
        IF EXISTS (SELECT 1 FROM #Se s JOIN dbo.SerialItems si ON si.OrderItemId = s.OrderItemId AND si.SerialNumber = CAST(s.SerialNumber AS varchar(100))
                    JOIN dbo.ReturnItems ri ON ri.SerialItemId = si.Id JOIN dbo.ReturnRequests rq ON rq.Id = ri.ReturnRequestId AND rq.Status <> 2)
            THROW 50005, N'Có serial đã nằm trong một yêu cầu đổi trả khác.', 1;

        -- tiền hoàn dự kiến: (giá dòng - phần voucher phân bổ) * số lượng trả / số lượng mua
        DECLARE @VoucherDisc decimal(18,0) = @Disc - ISNULL((SELECT SUM(DiscountAmount) FROM dbo.OrderItems WHERE OrderId = @OrderId), 0);
        DECLARE @SumLine decimal(18,0) = (SELECT SUM(LineTotal) FROM dbo.OrderItems WHERE OrderId = @OrderId);
        DECLARE @Refund decimal(18,0) = (
            SELECT ISNULL(SUM(ROUND((oi.LineTotal - CASE WHEN @SumLine > 0 THEN @VoucherDisc * oi.LineTotal / @SumLine ELSE 0 END) * r.Quantity / oi.Quantity, 0)), 0)
              FROM #Raw r JOIN dbo.OrderItems oi ON oi.Id = r.OrderItemId);

        DECLARE @Seq bigint = NEXT VALUE FOR dbo.seq_Return;
        DECLARE @Code varchar(30) = dbo.fn_FormatCode('DT', @Seq, ABS(CHECKSUM(NEWID())));
        DECLARE @ReturnId bigint;

        BEGIN TRANSACTION;
            INSERT dbo.ReturnRequests (ReturnCode, OrderId, CustomerId, BranchId, Status, Reason, RefundAmount, CreatedBy)
            VALUES (@Code, @OrderId, @CustomerId, @BranchId, 0, @Reason, @Refund, @UserId);
            SET @ReturnId = SCOPE_IDENTITY();

            -- hàng có serial: mỗi serial một dòng
            INSERT dbo.ReturnItems (ReturnRequestId, OrderItemId, SerialItemId, Quantity)
            SELECT @ReturnId, s.OrderItemId, si.Id, 1
              FROM #Se s JOIN dbo.SerialItems si ON si.OrderItemId = s.OrderItemId AND si.SerialNumber = CAST(s.SerialNumber AS varchar(100));
            -- hàng không serial: một dòng theo số lượng
            INSERT dbo.ReturnItems (ReturnRequestId, OrderItemId, SerialItemId, Quantity)
            SELECT @ReturnId, r.OrderItemId, NULL, r.Quantity FROM #Raw r JOIN dbo.OrderItems oi ON oi.Id = r.OrderItemId WHERE oi.RequiresSerial = 0;
        COMMIT TRANSACTION;

        SELECT @ReturnId AS ReturnRequestId, @Code AS ReturnCode, @Refund AS RefundAmount;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/returns/{id}/review   Quản lý duyệt hoặc từ chối (RET-04) */
CREATE OR ALTER PROCEDURE dbo.usp_Return_Review
    @ReturnRequestId bigint, @Approve bit, @UserId bigint, @Note nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Approve = 0 AND (@Note IS NULL OR LTRIM(RTRIM(@Note)) = N'') THROW 50004, N'Từ chối phải nhập lý do.', 1;
        DECLARE @BranchId int;
        BEGIN TRANSACTION;
            UPDATE dbo.ReturnRequests
               SET Status = CASE WHEN @Approve = 1 THEN 1 ELSE 2 END, ApprovedBy = @UserId, ApprovedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
             WHERE Id = @ReturnRequestId AND Status = 0;
            IF @@ROWCOUNT = 0 THROW 50002, N'Yêu cầu đổi trả không tồn tại hoặc không ở trạng thái chờ duyệt.', 1;
            SELECT @BranchId = BranchId FROM dbo.ReturnRequests WHERE Id = @ReturnRequestId;

            DECLARE @act varchar(60) = CASE WHEN @Approve = 1 THEN 'ReturnApproved' ELSE 'ReturnRejected' END;
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @BranchId = @BranchId, @Action = @act, @EntityType = 'ReturnRequest', @EntityId = @ReturnRequestId, @Reason = @Note;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/returns/{id}/inspect   thủ kho nhận hàng và kiểm tra từng dòng (UC-18, RET-03)
   @ResultsJson: [{"returnItemId":1,"result":1}]   result: 1 đạt, 2 không đạt
   Đạt: serial -> InStock (cộng tồn thực tế), hàng không serial cộng lại tồn.  Không đạt: serial -> Defective (không vào tồn bán được). */
CREATE OR ALTER PROCEDURE dbo.usp_Return_Inspect
    @ReturnRequestId bigint, @ResultsJson nvarchar(max), @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @ResultsJson IS NULL OR ISJSON(@ResultsJson) = 0 THROW 50004, N'Kết quả kiểm tra không hợp lệ.', 1;
        DECLARE @Status tinyint, @BranchId int;
        SELECT @Status = Status, @BranchId = BranchId FROM dbo.ReturnRequests WHERE Id = @ReturnRequestId;
        IF @Status IS NULL THROW 50001, N'Yêu cầu đổi trả không tồn tại.', 1;
        IF @Status <> 1 THROW 50002, N'Yêu cầu chưa được duyệt hoặc đã nhận hàng.', 1;

        CREATE TABLE #R (ReturnItemId bigint PRIMARY KEY, Result tinyint);
        INSERT #R SELECT returnItemId, result FROM OPENJSON(@ResultsJson) WITH (returnItemId bigint '$.returnItemId', result tinyint '$.result');
        IF EXISTS (SELECT 1 FROM #R WHERE Result NOT IN (1, 2) OR Result IS NULL) THROW 50004, N'Kết quả phải là 1 (đạt) hoặc 2 (không đạt).', 1;
        IF (SELECT COUNT(*) FROM #R) <> (SELECT COUNT(*) FROM dbo.ReturnItems WHERE ReturnRequestId = @ReturnRequestId)
           OR EXISTS (SELECT 1 FROM #R r LEFT JOIN dbo.ReturnItems ri ON ri.Id = r.ReturnItemId AND ri.ReturnRequestId = @ReturnRequestId WHERE ri.Id IS NULL)
            THROW 50004, N'Phải nhập kết quả cho đầy đủ tất cả dòng của yêu cầu.', 1;

        DECLARE @ReturnItemId bigint, @OrderItemId bigint, @SerialId bigint, @Qty int, @Result tinyint, @ProductId bigint;
        BEGIN TRANSACTION;
            DECLARE c CURSOR LOCAL FAST_FORWARD FOR
                SELECT ri.Id, ri.OrderItemId, ri.SerialItemId, ri.Quantity, r.Result, oi.ProductId
                  FROM dbo.ReturnItems ri JOIN #R r ON r.ReturnItemId = ri.Id JOIN dbo.OrderItems oi ON oi.Id = ri.OrderItemId
                 WHERE ri.ReturnRequestId = @ReturnRequestId;
            OPEN c;
            FETCH NEXT FROM c INTO @ReturnItemId, @OrderItemId, @SerialId, @Qty, @Result, @ProductId;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                IF @SerialId IS NOT NULL
                BEGIN
                    UPDATE dbo.SerialItems SET Status = 5, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @SerialId AND Status = 3;
                    IF @@ROWCOUNT = 0 THROW 50002, N'Có serial không còn ở trạng thái đã bán.', 1;
                    EXEC dbo.usp_Internal_LogSerial @SerialId, 3, 5, 'ReturnRequest', @ReturnRequestId, N'Khách trả hàng', @UserId;

                    IF @Result = 1
                    BEGIN
                        UPDATE dbo.SerialItems SET Status = 0, BranchId = @BranchId, OrderItemId = NULL, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @SerialId;
                        EXEC dbo.usp_Internal_LogSerial @SerialId, 5, 0, 'ReturnRequest', @ReturnRequestId, N'Kiểm tra đạt, nhập lại kho', @UserId;
                        EXEC dbo.usp_Internal_StockMove @BranchId, @ProductId, 1, 0, 8, 'ReturnRequest', @ReturnRequestId, @UserId, NULL;
                    END
                    ELSE
                    BEGIN
                        UPDATE dbo.SerialItems SET Status = 7, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @SerialId;
                        EXEC dbo.usp_Internal_LogSerial @SerialId, 5, 7, 'ReturnRequest', @ReturnRequestId, N'Kiểm tra không đạt', @UserId;
                    END
                END
                ELSE IF @Result = 1
                    EXEC dbo.usp_Internal_StockMove @BranchId, @ProductId, @Qty, 0, 8, 'ReturnRequest', @ReturnRequestId, @UserId, NULL;

                UPDATE dbo.ReturnItems SET InspectionResult = @Result WHERE Id = @ReturnItemId;
                FETCH NEXT FROM c INTO @ReturnItemId, @OrderItemId, @SerialId, @Qty, @Result, @ProductId;
            END
            CLOSE c; DEALLOCATE c;

            UPDATE dbo.ReturnRequests SET Status = 3, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @ReturnRequestId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/returns/{id}/refund   hoàn tiền sau khi đã nhận hàng (RET-02, RET-05, PAY-04)
   - Hoàn tiền mặt tối đa bằng số đã thanh toán của đơn; phần còn lại (đơn mua chịu) trừ vào công nợ khách.
   - Đơn chuyển PartiallyReturned, hoặc Returned nếu đã trả hết số lượng.
   - Thu hồi hoa hồng theo tỷ lệ (KPI-04). */
CREATE OR ALTER PROCEDURE dbo.usp_Return_Refund
    @ReturnRequestId bigint, @Method tinyint, @Amount decimal(18,0) = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Status tinyint, @OrderId bigint, @Planned decimal(18,0), @BranchId int, @CustomerId bigint;
        SELECT @Status = Status, @OrderId = OrderId, @Planned = RefundAmount, @BranchId = BranchId, @CustomerId = CustomerId
          FROM dbo.ReturnRequests WHERE Id = @ReturnRequestId;
        IF @Status IS NULL THROW 50001, N'Yêu cầu đổi trả không tồn tại.', 1;
        IF @Status <> 3 THROW 50002, N'Chỉ hoàn tiền sau khi đã nhận và kiểm tra hàng.', 1;

        DECLARE @Refund decimal(18,0) = ISNULL(@Amount, @Planned);
        IF @Refund < 0 OR @Refund > @Planned THROW 50006, N'Số tiền hoàn không hợp lệ hoặc vượt số tiền dự kiến của yêu cầu.', 1;

        DECLARE @Paid decimal(18,0), @OMethod tinyint, @OStatus tinyint, @From tinyint, @PaymentId bigint, @Parent bigint;
        BEGIN TRANSACTION;
            SELECT @Paid = PaidAmount, @OMethod = PaymentMethod, @OStatus = Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId;

            DECLARE @Cash decimal(18,0) = CASE WHEN @Refund > @Paid THEN @Paid ELSE @Refund END;
            DECLARE @Debt decimal(18,0) = CASE WHEN @OMethod = 4 THEN @Refund - @Cash ELSE 0 END;

            IF @Cash > 0
            BEGIN
                SELECT TOP 1 @Parent = Id FROM dbo.Payments WHERE OrderId = @OrderId AND PaymentType = 0 AND Status = 1 ORDER BY PaidAt DESC, Id DESC;
                INSERT dbo.Payments (OrderId, PaymentType, Method, Amount, Status, ParentPaymentId, ReturnRequestId, PaidAt, CreatedBy)
                VALUES (@OrderId, 1, @Method, @Cash, 1, @Parent, @ReturnRequestId, SYSUTCDATETIME(), @UserId);
                SET @PaymentId = SCOPE_IDENTITY();
                UPDATE dbo.Orders SET PaidAmount = PaidAmount - @Cash, PaymentStatus = CASE WHEN PaidAmount - @Cash <= 0 THEN 3 ELSE PaymentStatus END WHERE Id = @OrderId;
            END
            IF @Debt > 0
                INSERT dbo.CustomerLedger (CustomerId, OrderId, EntryType, Amount, CreatedBy) VALUES (@CustomerId, @OrderId, 2, -@Debt, @UserId);

            -- trạng thái đơn: trả hết thì Returned, còn lại PartiallyReturned
            DECLARE @TotalQty int = (SELECT SUM(Quantity) FROM dbo.OrderItems WHERE OrderId = @OrderId);
            DECLARE @ReturnedQty int = (SELECT ISNULL(SUM(ri.Quantity), 0) FROM dbo.ReturnItems ri JOIN dbo.ReturnRequests rq ON rq.Id = ri.ReturnRequestId
                                          WHERE rq.OrderId = @OrderId AND (rq.Status IN (3, 4, 5) OR rq.Id = @ReturnRequestId));
            DECLARE @Target tinyint = CASE WHEN @ReturnedQty >= @TotalQty THEN 10 ELSE 9 END;
            IF @OStatus <> @Target
                EXEC dbo.usp_Internal_SetOrderStatus @OrderId, @Target, @UserId, N'Hoàn tiền đổi trả', @From OUTPUT;

            UPDATE dbo.ReturnRequests SET Status = 4, RefundAmount = @Refund, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @ReturnRequestId;

            EXEC dbo.usp_Internal_ClawbackCommission @ReturnRequestId, @Refund;

            DECLARE @n nvarchar(30) = CAST(@Refund AS nvarchar(30));
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @BranchId = @BranchId, @Action = 'ReturnRefunded', @EntityType = 'ReturnRequest', @EntityId = @ReturnRequestId, @NewValues = @n;
        COMMIT TRANSACTION;

        SELECT @ReturnRequestId AS ReturnRequestId, @Refund AS RefundAmount, @Cash AS CashRefunded, @Debt AS DebtReduced, @Target AS OrderStatus;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* GET /api/returns/{id} */
CREATE OR ALTER PROCEDURE dbo.usp_Return_Get @ReturnRequestId bigint, @CustomerId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.ReturnRequests WHERE Id = @ReturnRequestId AND (@CustomerId IS NULL OR CustomerId = @CustomerId)) THROW 50001, N'Yêu cầu đổi trả không tồn tại.', 1;
    SELECT rq.*, o.OrderCode FROM dbo.ReturnRequests rq JOIN dbo.Orders o ON o.Id = rq.OrderId WHERE rq.Id = @ReturnRequestId;
    SELECT ri.Id, ri.OrderItemId, oi.Sku, oi.ProductName, ri.SerialItemId, si.SerialNumber, ri.Quantity, ri.InspectionResult
      FROM dbo.ReturnItems ri JOIN dbo.OrderItems oi ON oi.Id = ri.OrderItemId LEFT JOIN dbo.SerialItems si ON si.Id = ri.SerialItemId
     WHERE ri.ReturnRequestId = @ReturnRequestId;
END
GO

/* GET /api/returns */
CREATE OR ALTER PROCEDURE dbo.usp_Return_List
    @BranchId int = NULL, @CustomerId bigint = NULL, @Status tinyint = NULL, @PageNumber int = 1, @PageSize int = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    IF @PageSize > 100 SET @PageSize = 100;
    SELECT rq.Id, rq.ReturnCode, rq.OrderId, o.OrderCode, rq.CustomerId, rq.BranchId, rq.Status, rq.RefundAmount, rq.CreatedAt, COUNT(*) OVER () AS TotalCount
      FROM dbo.ReturnRequests rq JOIN dbo.Orders o ON o.Id = rq.OrderId
     WHERE (@BranchId IS NULL OR rq.BranchId = @BranchId) AND (@CustomerId IS NULL OR rq.CustomerId = @CustomerId) AND (@Status IS NULL OR rq.Status = @Status)
     ORDER BY rq.CreatedAt DESC, rq.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO
