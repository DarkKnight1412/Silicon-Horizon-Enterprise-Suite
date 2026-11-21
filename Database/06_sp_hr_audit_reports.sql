/* =============================================================================
   06. NHÂN SỰ, CHẤM CÔNG, NHẬT KÝ, BÁO CÁO
   Chạy SAU 02..05.
   Ngày/giờ làm việc tính theo giờ Việt Nam (UTC+7), dữ liệu lưu UTC.
   ============================================================================= */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =============================================================================
   XẾP CA VÀ CHẤM CÔNG (UC-19, UC-22, UC-23, HR-04..06)
   ============================================================================= */

/* POST /api/schedules   Quản lý xếp ca */
CREATE OR ALTER PROCEDURE dbo.usp_Schedule_Assign
    @EmployeeId bigint, @ShiftId int, @BranchId int, @WorkDate date, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE Id = @EmployeeId AND Status = 0) THROW 50001, N'Nhân viên không tồn tại hoặc đã nghỉ việc.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Shifts WHERE Id = @ShiftId) THROW 50001, N'Ca làm việc không tồn tại.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Id = @BranchId AND IsActive = 1) THROW 50001, N'Chi nhánh không tồn tại.', 1;
    IF EXISTS (SELECT 1 FROM dbo.EmployeeSchedules WHERE EmployeeId = @EmployeeId AND WorkDate = @WorkDate AND ShiftId = @ShiftId) THROW 50005, N'Nhân viên đã được xếp ca này.', 1;

    INSERT dbo.EmployeeSchedules (EmployeeId, ShiftId, BranchId, WorkDate) VALUES (@EmployeeId, @ShiftId, @BranchId, @WorkDate);
    SELECT SCOPE_IDENTITY() AS ScheduleId;
END
GO

/* DELETE /api/schedules/{id}   chỉ xóa được ca chưa có chấm công */
CREATE OR ALTER PROCEDURE dbo.usp_Schedule_Remove @ScheduleId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Attendances WHERE ScheduleId = @ScheduleId) THROW 50002, N'Ca đã có dữ liệu chấm công, không thể xóa.', 1;
    DELETE dbo.EmployeeSchedules WHERE Id = @ScheduleId;
    IF @@ROWCOUNT = 0 THROW 50001, N'Ca làm việc không tồn tại.', 1;
END
GO

/* GET /api/schedules */
CREATE OR ALTER PROCEDURE dbo.usp_Schedule_List
    @BranchId int = NULL, @EmployeeId bigint = NULL, @FromDate date, @ToDate date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.Id AS ScheduleId, s.WorkDate, s.BranchId, s.EmployeeId, e.FullName, sh.Code AS ShiftCode, sh.Name AS ShiftName, sh.StartTime, sh.EndTime
      FROM dbo.EmployeeSchedules s
      JOIN dbo.Employees e ON e.Id = s.EmployeeId
      JOIN dbo.Shifts sh ON sh.Id = s.ShiftId
     WHERE s.WorkDate BETWEEN @FromDate AND @ToDate
       AND (@BranchId IS NULL OR s.BranchId = @BranchId) AND (@EmployeeId IS NULL OR s.EmployeeId = @EmployeeId)
     ORDER BY s.WorkDate, sh.StartTime, e.FullName;
END
GO

/* POST /api/attendance/check-in   chấm công vào ca: lấy ca hôm nay chưa chấm công, theo giờ bắt đầu sớm nhất */
CREATE OR ALTER PROCEDURE dbo.usp_Attendance_CheckIn @EmployeeId bigint
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @Today date = dbo.fn_VnDate(SYSUTCDATETIME()), @ScheduleId bigint;
        IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE Id = @EmployeeId AND Status = 0) THROW 50001, N'Nhân viên không tồn tại hoặc đã nghỉ việc.', 1;

        BEGIN TRANSACTION;
            SELECT TOP 1 @ScheduleId = s.Id
              FROM dbo.EmployeeSchedules s JOIN dbo.Shifts sh ON sh.Id = s.ShiftId
             WHERE s.EmployeeId = @EmployeeId AND s.WorkDate = @Today
               AND NOT EXISTS (SELECT 1 FROM dbo.Attendances a WHERE a.ScheduleId = s.Id)
             ORDER BY sh.StartTime;
            IF @ScheduleId IS NULL THROW 50004, N'Hôm nay bạn chưa được xếp ca hoặc đã chấm công vào đủ các ca.', 1;

            INSERT dbo.Attendances (EmployeeId, ScheduleId, WorkDate, CheckInAt, Source) VALUES (@EmployeeId, @ScheduleId, @Today, @Now, 0);
        COMMIT TRANSACTION;

        SELECT SCOPE_IDENTITY() AS AttendanceId, @ScheduleId AS ScheduleId, @Now AS CheckInAt;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/attendance/check-out   chấm công ra ca: ca hôm nay đã vào mà chưa ra */
CREATE OR ALTER PROCEDURE dbo.usp_Attendance_CheckOut @EmployeeId bigint
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @Today date = dbo.fn_VnDate(SYSUTCDATETIME()), @Id bigint;
    SELECT TOP 1 @Id = Id FROM dbo.Attendances
     WHERE EmployeeId = @EmployeeId AND WorkDate = @Today AND CheckInAt IS NOT NULL AND CheckOutAt IS NULL
     ORDER BY CheckInAt DESC;
    IF @Id IS NULL THROW 50002, N'Không có ca nào đang chờ chấm công ra.', 1;

    UPDATE dbo.Attendances SET CheckOutAt = @Now, UpdatedAt = @Now WHERE Id = @Id;
    SELECT @Id AS AttendanceId, @Now AS CheckOutAt;
END
GO

/* POST /api/attendance/adjust   điều chỉnh công (quên chấm, sai giờ) - Quản lý duyệt, bắt buộc lý do, có Audit (HR-06) */
CREATE OR ALTER PROCEDURE dbo.usp_Attendance_Adjust
    @EmployeeId bigint, @WorkDate date, @ScheduleId bigint = NULL, @CheckInAt datetime2(0) = NULL, @CheckOutAt datetime2(0) = NULL,
    @Reason nvarchar(300), @UserId bigint
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N'' THROW 50004, N'Phải nhập lý do điều chỉnh công.', 1;
        IF @CheckInAt IS NULL AND @CheckOutAt IS NULL THROW 50004, N'Phải có giờ vào hoặc giờ ra.', 1;
        IF @CheckInAt IS NOT NULL AND @CheckOutAt IS NOT NULL AND @CheckOutAt < @CheckInAt THROW 50004, N'Giờ ra phải sau giờ vào.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE Id = @EmployeeId) THROW 50001, N'Nhân viên không tồn tại.', 1;
        IF @ScheduleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.EmployeeSchedules WHERE Id = @ScheduleId AND EmployeeId = @EmployeeId AND WorkDate = @WorkDate)
            THROW 50001, N'Ca không thuộc nhân viên và ngày đã chọn.', 1;

        DECLARE @Id bigint, @OldIn datetime2(0), @OldOut datetime2(0);
        BEGIN TRANSACTION;
            SELECT @Id = Id, @OldIn = CheckInAt, @OldOut = CheckOutAt FROM dbo.Attendances WITH (UPDLOCK)
             WHERE EmployeeId = @EmployeeId AND WorkDate = @WorkDate AND ((@ScheduleId IS NULL AND ScheduleId IS NULL) OR ScheduleId = @ScheduleId);

            IF @Id IS NULL
            BEGIN
                INSERT dbo.Attendances (EmployeeId, ScheduleId, WorkDate, CheckInAt, CheckOutAt, Source, AdjustedBy, AdjustReason, CreatedBy)
                VALUES (@EmployeeId, @ScheduleId, @WorkDate, @CheckInAt, @CheckOutAt, 1, @UserId, @Reason, @UserId);
                SET @Id = SCOPE_IDENTITY();
            END
            ELSE
                UPDATE dbo.Attendances
                   SET CheckInAt = ISNULL(@CheckInAt, CheckInAt), CheckOutAt = ISNULL(@CheckOutAt, CheckOutAt),
                       Source = 1, AdjustedBy = @UserId, AdjustReason = @Reason, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
                 WHERE Id = @Id;

            DECLARE @old nvarchar(100) = CONCAT(CONVERT(varchar(19), @OldIn, 126), N' | ', CONVERT(varchar(19), @OldOut, 126));
            DECLARE @new nvarchar(100) = CONCAT(CONVERT(varchar(19), @CheckInAt, 126), N' | ', CONVERT(varchar(19), @CheckOutAt, 126));
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @Action = 'AttendanceAdjusted', @EntityType = 'Attendance', @EntityId = @Id,
                 @OldValues = @old, @NewValues = @new, @Reason = @Reason;
        COMMIT TRANSACTION;

        SELECT @Id AS AttendanceId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* GET /api/attendance?yearMonth=2026-11   bảng công tháng: chi tiết từng ngày và tổng hợp từng nhân viên */
CREATE OR ALTER PROCEDURE dbo.usp_Attendance_GetMonthly
    @YearMonth char(7), @EmployeeId bigint = NULL, @BranchId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @From date = TRY_CAST(@YearMonth + '-01' AS date);
    IF @From IS NULL THROW 50004, N'Tháng không hợp lệ (định dạng yyyy-MM).', 1;
    DECLARE @To date = EOMONTH(@From);

    SELECT a.EmployeeId, e.FullName, a.WorkDate, sh.Code AS ShiftCode, sh.StartTime, a.CheckInAt, a.CheckOutAt, a.Source, a.AdjustReason,
           CASE WHEN a.CheckInAt IS NOT NULL AND a.CheckOutAt IS NOT NULL THEN DATEDIFF(MINUTE, a.CheckInAt, a.CheckOutAt) END AS WorkedMinutes,
           CAST(CASE WHEN a.CheckInAt IS NOT NULL AND sh.StartTime IS NOT NULL
                      AND CAST(DATEADD(HOUR, 7, a.CheckInAt) AS time(0)) > DATEADD(MINUTE, 5, sh.StartTime) THEN 1 ELSE 0 END AS bit) AS IsLate
      FROM dbo.Attendances a
      JOIN dbo.Employees e ON e.Id = a.EmployeeId
      LEFT JOIN dbo.EmployeeSchedules s ON s.Id = a.ScheduleId
      LEFT JOIN dbo.Shifts sh ON sh.Id = s.ShiftId
     WHERE a.WorkDate BETWEEN @From AND @To
       AND (@EmployeeId IS NULL OR a.EmployeeId = @EmployeeId) AND (@BranchId IS NULL OR e.BranchId = @BranchId)
     ORDER BY e.FullName, a.WorkDate;

    SELECT e.Id AS EmployeeId, e.FullName,
           COUNT(DISTINCT a.WorkDate) AS DaysWorked,
           CAST(ISNULL(SUM(CASE WHEN a.CheckInAt IS NOT NULL AND a.CheckOutAt IS NOT NULL THEN DATEDIFF(MINUTE, a.CheckInAt, a.CheckOutAt) END), 0) / 60.0 AS decimal(9,2)) AS TotalHours,
           (SELECT COUNT(*) FROM dbo.EmployeeSchedules s2 WHERE s2.EmployeeId = e.Id AND s2.WorkDate BETWEEN @From AND @To) AS ScheduledShifts,
           SUM(CASE WHEN a.CheckInAt IS NOT NULL AND a.CheckOutAt IS NULL THEN 1 ELSE 0 END) AS MissingCheckOut
      FROM dbo.Employees e
      LEFT JOIN dbo.Attendances a ON a.EmployeeId = e.Id AND a.WorkDate BETWEEN @From AND @To
     WHERE (@EmployeeId IS NULL OR e.Id = @EmployeeId) AND (@BranchId IS NULL OR e.BranchId = @BranchId) AND e.Status = 0
     GROUP BY e.Id, e.FullName
     ORDER BY e.FullName;
END
GO

/* =============================================================================
   NHẬT KÝ HỆ THỐNG (UC-29, AUD-04)
   ============================================================================= */

/* GET /api/audit-logs   Admin xem toàn bộ; Quản lý chỉ chi nhánh mình (API truyền @BranchId) */
CREATE OR ALTER PROCEDURE dbo.usp_Audit_Search
    @EntityType varchar(50) = NULL, @EntityId bigint = NULL, @UserId bigint = NULL, @BranchId int = NULL,
    @Action varchar(60) = NULL, @FromDate datetime2(0) = NULL, @ToDate datetime2(0) = NULL, @PageNumber int = 1, @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 50;
    IF @PageSize > 200 SET @PageSize = 200;

    SELECT l.Id, l.UserId, u.Username, l.BranchId, l.[Action], l.EntityType, l.EntityId, l.OldValues, l.NewValues, l.Reason, l.IpAddress, l.CreatedAt,
           COUNT(*) OVER () AS TotalCount
      FROM dbo.AuditLogs l LEFT JOIN dbo.Users u ON u.Id = l.UserId
     WHERE (@EntityType IS NULL OR l.EntityType = @EntityType) AND (@EntityId IS NULL OR l.EntityId = @EntityId)
       AND (@UserId IS NULL OR l.UserId = @UserId) AND (@BranchId IS NULL OR l.BranchId = @BranchId)
       AND (@Action IS NULL OR l.[Action] = @Action)
       AND (@FromDate IS NULL OR l.CreatedAt >= @FromDate) AND (@ToDate IS NULL OR l.CreatedAt < @ToDate)
     ORDER BY l.CreatedAt DESC, l.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* =============================================================================
   BÁO CÁO (mục 11 BRD)
   ============================================================================= */

/* GET /api/reports/revenue?from=&to=&branchId=&groupBy=day|month|branch|category
   Doanh thu ghi nhận theo ngày hoàn tất đơn (PAY-05), không tính phí vận chuyển.
   Giá vốn: serial lấy UnitCost của serial đang gán đơn; hàng không serial lấy giá nhập gần nhất (ước tính). */
CREATE OR ALTER PROCEDURE dbo.usp_Report_Revenue
    @FromDate date, @ToDate date, @BranchId int = NULL, @GroupBy varchar(10) = 'day'
AS
BEGIN
    SET NOCOUNT ON;
    IF @GroupBy NOT IN ('day', 'month', 'branch', 'category') THROW 50004, N'GroupBy phải là day, month, branch hoặc category.', 1;
    DECLARE @FromUtc datetime2(0) = DATEADD(HOUR, -7, CAST(@FromDate AS datetime2(0)));
    DECLARE @ToUtc datetime2(0) = DATEADD(HOUR, -7, CAST(DATEADD(DAY, 1, @ToDate) AS datetime2(0)));

    IF @GroupBy = 'category'
    BEGIN
        SELECT c.Id AS CategoryId, c.Name AS CategoryName, SUM(oi.Quantity) AS Quantity, SUM(oi.LineTotal) AS Revenue
          FROM dbo.Orders o
          JOIN dbo.OrderItems oi ON oi.OrderId = o.Id
          JOIN dbo.Products p ON p.Id = oi.ProductId
          JOIN dbo.Categories c ON c.Id = p.CategoryId
         WHERE o.Status IN (7, 9, 10) AND o.CompletedAt >= @FromUtc AND o.CompletedAt < @ToUtc AND (@BranchId IS NULL OR o.BranchId = @BranchId)
         GROUP BY c.Id, c.Name
         ORDER BY Revenue DESC;
        RETURN;
    END

    ;WITH O AS (
        SELECT o.Id, o.BranchId, DATEADD(HOUR, 7, o.CompletedAt) AS LocalAt, o.SubTotal - o.DiscountTotal AS Net,
               ISNULL((SELECT SUM(rq.RefundAmount) FROM dbo.ReturnRequests rq WHERE rq.OrderId = o.Id AND rq.Status IN (4, 5)), 0) AS Refunded,
               ISNULL((SELECT SUM(si.UnitCost) FROM dbo.SerialItems si JOIN dbo.OrderItems oi ON oi.Id = si.OrderItemId WHERE oi.OrderId = o.Id), 0)
             + ISNULL((SELECT SUM(oi.Quantity * ISNULL(p.LastImportPrice, 0)) FROM dbo.OrderItems oi JOIN dbo.Products p ON p.Id = oi.ProductId
                        WHERE oi.OrderId = o.Id AND oi.RequiresSerial = 0), 0) AS Cost
          FROM dbo.Orders o
         WHERE o.Status IN (7, 9, 10) AND o.CompletedAt >= @FromUtc AND o.CompletedAt < @ToUtc AND (@BranchId IS NULL OR o.BranchId = @BranchId)
    )
    SELECT CASE @GroupBy WHEN 'month'  THEN CONVERT(varchar(7), LocalAt, 23)
                         WHEN 'branch' THEN CAST(BranchId AS varchar(20))
                         ELSE CONVERT(varchar(10), LocalAt, 23) END AS GroupKey,
           COUNT(*) AS Orders, SUM(Net) AS GrossRevenue, SUM(Refunded) AS Refunds, SUM(Net - Refunded) AS NetRevenue,
           SUM(Cost) AS Cost, SUM(Net - Refunded - Cost) AS GrossProfit
      FROM O
     GROUP BY CASE @GroupBy WHEN 'month'  THEN CONVERT(varchar(7), LocalAt, 23)
                            WHEN 'branch' THEN CAST(BranchId AS varchar(20))
                            ELSE CONVERT(varchar(10), LocalAt, 23) END
     ORDER BY GroupKey;
END
GO

/* GET /api/reports/inventory   giá trị tồn theo chi nhánh/danh mục và hàng tồn lâu không xuất (mặc định 90 ngày) */
CREATE OR ALTER PROCEDURE dbo.usp_Report_Inventory
    @BranchId int = NULL,
    @SlowDays int = 90
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Since datetime2(0) =
        DATEADD(DAY, -@SlowDays, SYSUTCDATETIME());

    -- Tính tổng giá trị serial theo sản phẩm và chi nhánh
    ;WITH SerialStockValue AS (
        SELECT
            si.ProductId,
            si.BranchId,
            SUM(si.UnitCost) AS TotalSerialValue
        FROM dbo.SerialItems si
        WHERE si.Status IN (0, 1)
        GROUP BY si.ProductId, si.BranchId
    )
    SELECT
        sl.BranchId,
        br.Name AS BranchName,
        c.Id AS CategoryId,
        c.Name AS CategoryName,
        SUM(sl.PhysicalQty) AS PhysicalQty,
        SUM(sl.ReservedQty) AS ReservedQty,
        SUM(sl.AvailableQty) AS AvailableQty,

        SUM(
            CASE
                WHEN p.RequiresSerial = 1
                    THEN ISNULL(ss.TotalSerialValue, 0)
                ELSE sl.PhysicalQty * ISNULL(p.LastImportPrice, 0)
            END
        ) AS StockValue

    FROM dbo.StockLevels sl
    JOIN dbo.Products p ON p.Id = sl.ProductId
    JOIN dbo.Categories c ON c.Id = p.CategoryId
    JOIN dbo.Branches br ON br.Id = sl.BranchId

    LEFT JOIN SerialStockValue ss
        ON ss.ProductId = sl.ProductId
       AND ss.BranchId = sl.BranchId

    WHERE @BranchId IS NULL OR sl.BranchId = @BranchId

    GROUP BY
        sl.BranchId,
        br.Name,
        c.Id,
        c.Name

    ORDER BY br.Name, c.Name;


    -- Báo cáo hàng tồn lâu không xuất
    SELECT
        sl.BranchId,
        sl.ProductId,
        p.Sku,
        p.Name,
        sl.PhysicalQty,

        (
            SELECT MAX(m.CreatedAt)
            FROM dbo.StockMovements m
            WHERE m.BranchId = sl.BranchId
              AND m.ProductId = sl.ProductId
              AND m.MovementType = 3
        ) AS LastIssueAt

    FROM dbo.StockLevels sl
    JOIN dbo.Products p ON p.Id = sl.ProductId

    WHERE sl.PhysicalQty > 0
      AND (@BranchId IS NULL OR sl.BranchId = @BranchId)
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.StockMovements m
          WHERE m.BranchId = sl.BranchId
            AND m.ProductId = sl.ProductId
            AND m.MovementType = 3
            AND m.CreatedAt >= @Since
      )

    ORDER BY sl.PhysicalQty DESC;
END;
GO

/* GET /api/reports/serial-status   số lượng serial theo trạng thái */
CREATE OR ALTER PROCEDURE dbo.usp_Report_SerialStatus
    @ProductId bigint = NULL, @BranchId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT si.Status, COUNT(*) AS Quantity
      FROM dbo.SerialItems si
     WHERE (@ProductId IS NULL OR si.ProductId = @ProductId) AND (@BranchId IS NULL OR si.BranchId = @BranchId)
     GROUP BY si.Status ORDER BY si.Status;
END
GO

/* GET /api/reports/debt   công nợ phải thu (khách sỉ) và phải trả (nhà cung cấp), kèm phần quá hạn.
   Quy ước: khoản thanh toán/giảm trừ được trừ vào khoản nợ cũ nhất trước (FIFO). */
CREATE OR ALTER PROCEDURE dbo.usp_Report_Debt
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Today date = dbo.fn_VnDate(SYSUTCDATETIME());

    SELECT c.Id AS CustomerId, c.FullName, c.Phone, c.CreditLimit, x.Balance,
           CASE WHEN x.OverdueRaw > 0 THEN x.OverdueRaw ELSE 0 END AS Overdue
      FROM dbo.Customers c
      CROSS APPLY (
          SELECT ISNULL(SUM(l.Amount), 0) AS Balance,
                 ISNULL(SUM(CASE WHEN l.EntryType = 0 AND l.DueDate < @Today THEN l.Amount ELSE 0 END), 0)
               + ISNULL(SUM(CASE WHEN l.Amount < 0 THEN l.Amount ELSE 0 END), 0) AS OverdueRaw
            FROM dbo.CustomerLedger l WHERE l.CustomerId = c.Id) x
     WHERE x.Balance <> 0
     ORDER BY x.Balance DESC;

    SELECT s.Id AS SupplierId, s.Name, x.Balance,
           CASE WHEN x.OverdueRaw > 0 THEN x.OverdueRaw ELSE 0 END AS Overdue
      FROM dbo.Suppliers s
      CROSS APPLY (
          SELECT ISNULL(SUM(l.Amount), 0) AS Balance,
                 ISNULL(SUM(CASE WHEN l.EntryType = 0 AND l.DueDate < @Today THEN l.Amount ELSE 0 END), 0)
               + ISNULL(SUM(CASE WHEN l.Amount < 0 THEN l.Amount ELSE 0 END), 0) AS OverdueRaw
            FROM dbo.SupplierLedger l WHERE l.SupplierId = s.Id) x
     WHERE x.Balance <> 0
     ORDER BY x.Balance DESC;
END
GO

/* GET /api/customers/{id}/ledger   sao kê công nợ của khách */
CREATE OR ALTER PROCEDURE dbo.usp_Customer_GetLedger @CustomerId bigint, @PageNumber int = 1, @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 50;
    IF @PageSize > 200 SET @PageSize = 200;
    SELECT (SELECT ISNULL(SUM(Amount), 0) FROM dbo.CustomerLedger WHERE CustomerId = @CustomerId) AS Balance,
           (SELECT CreditLimit FROM dbo.Customers WHERE Id = @CustomerId) AS CreditLimit;
    SELECT l.Id, l.EntryType, l.Amount, l.DueDate, l.OrderId, l.PaymentId, l.CreatedAt, COUNT(*) OVER () AS TotalCount
      FROM dbo.CustomerLedger l WHERE l.CustomerId = @CustomerId
     ORDER BY l.CreatedAt DESC, l.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* GET /api/reports/warranty?from=&to=&branchId=   số phiếu theo trạng thái, sản phẩm lỗi nhiều, thời gian xử lý trung bình */
CREATE OR ALTER PROCEDURE dbo.usp_Report_Warranty
    @FromDate date, @ToDate date, @BranchId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @FromUtc datetime2(0) = DATEADD(HOUR, -7, CAST(@FromDate AS datetime2(0)));
    DECLARE @ToUtc datetime2(0) = DATEADD(HOUR, -7, CAST(DATEADD(DAY, 1, @ToDate) AS datetime2(0)));

    SELECT t.Status, COUNT(*) AS Tickets
      FROM dbo.WarrantyTickets t
     WHERE t.ReceivedAt >= @FromUtc AND t.ReceivedAt < @ToUtc AND (@BranchId IS NULL OR t.BranchId = @BranchId)
     GROUP BY t.Status ORDER BY t.Status;

    SELECT TOP 20 p.Id AS ProductId, p.Sku, p.Name, COUNT(*) AS Tickets,
           (SELECT ISNULL(SUM(oi.Quantity), 0) FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.Id = oi.OrderId
             WHERE oi.ProductId = p.Id AND o.Status IN (7, 9, 10)) AS SoldQuantity
      FROM dbo.WarrantyTickets t
      LEFT JOIN dbo.SerialItems si ON si.Id = t.SerialItemId
      LEFT JOIN dbo.OrderItems oi2 ON oi2.Id = t.OrderItemId
      JOIN dbo.Products p ON p.Id = COALESCE(si.ProductId, oi2.ProductId)
     WHERE t.ReceivedAt >= @FromUtc AND t.ReceivedAt < @ToUtc AND (@BranchId IS NULL OR t.BranchId = @BranchId)
     GROUP BY p.Id, p.Sku, p.Name
     ORDER BY Tickets DESC;

    SELECT CAST(AVG(CAST(DATEDIFF(HOUR, t.ReceivedAt, t.ReturnedAt) AS float)) / 24.0 AS decimal(9,2)) AS AvgHandlingDays
      FROM dbo.WarrantyTickets t
     WHERE t.ReturnedAt IS NOT NULL AND t.ReceivedAt >= @FromUtc AND t.ReceivedAt < @ToUtc AND (@BranchId IS NULL OR t.BranchId = @BranchId);
END
GO
