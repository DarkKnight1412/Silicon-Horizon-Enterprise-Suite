/* =============================================================================
   02. NỀN TẢNG, TÀI KHOẢN, SẢN PHẨM, GIỎ HÀNG
   Chạy SAU 01_create_tables.sql, trong đúng database.

   QUY ƯỚC CHUNG CHO TẤT CẢ STORED PROCEDURE
   - Tên: usp_<Module>_<Hành động>. Proc nội bộ (chỉ proc khác gọi): usp_Internal_*.
   - Lỗi nghiệp vụ dùng THROW với mã riêng, API ánh xạ sang HTTP:
       50001 NOT_FOUND        -> 404
       50002 INVALID_STATE    -> 409 (sai trạng thái, chuyển trạng thái không hợp lệ)
       50003 OUT_OF_STOCK     -> 409 (không đủ tồn kho)
       50004 VALIDATION       -> 400
       50005 DUPLICATE        -> 409 (trùng dữ liệu)
       50006 LIMIT_EXCEEDED   -> 422 (vượt hạn mức, quá hạn đổi trả...)
     Lỗi hệ thống khác (số khác 5000x) -> 500.
   - Proc ghi dữ liệu tự quản lý giao dịch (XACT_ABORT ON, TRY/CATCH, ROLLBACK, THROW lại).
   - Phân quyền theo vai trò/chi nhánh do tầng API kiểm tra; proc nhận @UserId để ghi lịch sử.
   - IP/UserAgent cho Audit: API gọi sp_set_session_context 'ip' và 'user_agent'
     trên mỗi request trước khi gọi proc (proc Audit tự đọc).
   - Tồn kho: Physical = số đơn vị đang ở kho (serial: InStock + Allocated);
              Available = Physical - Reserved. Hàng RMA/Returned/Defective/InTransfer/Sold không nằm trong Physical.
   ============================================================================= */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- Sequence sinh mã chứng từ ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'seq_Order')          CREATE SEQUENCE dbo.seq_Order          AS bigint START WITH 1 INCREMENT BY 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'seq_PurchaseReceipt') CREATE SEQUENCE dbo.seq_PurchaseReceipt AS bigint START WITH 1 INCREMENT BY 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'seq_StockTransfer')  CREATE SEQUENCE dbo.seq_StockTransfer  AS bigint START WITH 1 INCREMENT BY 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'seq_StockTake')      CREATE SEQUENCE dbo.seq_StockTake      AS bigint START WITH 1 INCREMENT BY 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'seq_Warranty')       CREATE SEQUENCE dbo.seq_Warranty       AS bigint START WITH 1 INCREMENT BY 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'seq_Return')        CREATE SEQUENCE dbo.seq_Return         AS bigint START WITH 1 INCREMENT BY 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'seq_SupplierReturn') CREATE SEQUENCE dbo.seq_SupplierReturn AS bigint START WITH 1 INCREMENT BY 1;
GO

/* Mã chứng từ: <tiền tố><yyMMdd giờ VN><số thứ tự 8 chữ số><2 số ngẫu nhiên>, ví dụ DH26112000000042-37 (bỏ dấu '-') */
CREATE OR ALTER FUNCTION dbo.fn_FormatCode (@Prefix varchar(5), @Seq bigint, @Rnd int)
RETURNS varchar(30)
AS
BEGIN
    RETURN @Prefix
         + CONVERT(char(6), DATEADD(HOUR, 7, SYSUTCDATETIME()), 12)
         + RIGHT('00000000' + CAST(@Seq AS varchar(20)), 8)
         + RIGHT('00' + CAST(ABS(@Rnd) % 100 AS varchar(2)), 2);
END
GO

/* Ngày theo giờ Việt Nam từ thời điểm UTC */
CREATE OR ALTER FUNCTION dbo.fn_VnDate (@Utc datetime2(0))
RETURNS date
AS
BEGIN
    RETURN CAST(DATEADD(HOUR, 7, @Utc) AS date);
END
GO

/* =============================================================================
   GIÁ: giá nhóm khách -> bậc thang theo số lượng -> khuyến mãi tốt nhất (PRC-01, 02, 03, 05)
   Trả về 1 dòng. UnitPrice dùng để lưu snapshot; DiscountPerUnit là phần khuyến mãi mỗi đơn vị.
   ============================================================================= */
CREATE OR ALTER FUNCTION dbo.fn_ProductPrice (@ProductId bigint, @CustomerGroupId int, @Quantity int, @Now datetime2(0))
RETURNS TABLE
AS
RETURN
(
    WITH Base AS (
        SELECT p.Id, p.BrandId, p.CategoryId,
               COALESCE(
                   (SELECT TOP 1 t.Price FROM dbo.PriceTiers t
                     WHERE t.ProductId = p.Id AND t.CustomerGroupId = @CustomerGroupId AND t.MinQuantity <= @Quantity
                     ORDER BY t.MinQuantity DESC),
                   (SELECT pp.Price FROM dbo.ProductPrices pp
                     WHERE pp.ProductId = p.Id AND pp.CustomerGroupId = @CustomerGroupId),
                   p.ListPrice) AS BasePrice
        FROM dbo.Products p
        WHERE p.Id = @ProductId
    )
    SELECT b.Id AS ProductId,
           b.BasePrice,
           pr.PromotionId,
           ISNULL(pr.D, 0) AS DiscountPerUnit,
           b.BasePrice - ISNULL(pr.D, 0) AS FinalUnitPrice
    FROM Base b
    OUTER APPLY (
        SELECT TOP 1 x.Id AS PromotionId, x.D
        FROM (
            SELECT pm.Id,
                   CASE WHEN pm.DiscountType = 0
                        THEN CAST(ROUND(b.BasePrice * pm.DiscountValue / 100, 0) AS decimal(18,0))
                        WHEN pm.DiscountValue > b.BasePrice THEN b.BasePrice
                        ELSE CAST(pm.DiscountValue AS decimal(18,0)) END AS D
            FROM dbo.Promotions pm
            WHERE pm.IsActive = 1 AND @Now >= pm.StartAt AND @Now < pm.EndAt
              AND ((pm.ScopeType = 0 AND pm.ScopeId = b.Id)
                OR (pm.ScopeType = 1 AND pm.ScopeId = b.BrandId)
                OR (pm.ScopeType = 2 AND pm.ScopeId = b.CategoryId))
        ) x
        ORDER BY x.D DESC
    ) pr
);
GO

/* =============================================================================
   VOUCHER: kiểm tra và tính giảm giá (PRC-04). ErrorMessage NULL nghĩa là hợp lệ.
   ============================================================================= */
CREATE OR ALTER FUNCTION dbo.fn_VoucherCheck (@Code varchar(40), @CustomerGroupId int, @OrderAmount decimal(18,0), @HasPromotion bit, @Now datetime2(0))
RETURNS TABLE
AS
RETURN
(
    SELECT s.VoucherId, s.ErrorMessage,
           CASE WHEN s.ErrorMessage IS NOT NULL THEN CAST(0 AS decimal(18,0))
                ELSE CASE WHEN s.MaxDiscountAmount IS NOT NULL AND s.RawDiscount > s.MaxDiscountAmount
                          THEN s.MaxDiscountAmount ELSE s.RawDiscount END END AS DiscountAmount
    FROM (
        SELECT v.Id AS VoucherId, v.MaxDiscountAmount,
               CASE WHEN v.Id IS NULL THEN N'Mã giảm giá không tồn tại'
                    WHEN v.IsActive = 0 THEN N'Mã giảm giá đã bị khóa'
                    WHEN @Now < v.StartAt OR @Now >= v.EndAt THEN N'Mã giảm giá ngoài thời hạn sử dụng'
                    WHEN v.MaxUses IS NOT NULL AND v.UsedCount >= v.MaxUses THEN N'Mã giảm giá đã hết lượt dùng'
                    WHEN v.CustomerGroupId IS NOT NULL AND v.CustomerGroupId <> @CustomerGroupId THEN N'Mã không áp dụng cho nhóm khách này'
                    WHEN v.MinOrderAmount IS NOT NULL AND @OrderAmount < v.MinOrderAmount THEN N'Đơn hàng chưa đạt giá trị tối thiểu'
                    WHEN v.IsStackable = 0 AND @HasPromotion = 1 THEN N'Mã không áp dụng cùng sản phẩm đang khuyến mãi'
               END AS ErrorMessage,
               CASE WHEN v.DiscountType = 0
                    THEN CAST(ROUND(@OrderAmount * v.DiscountValue / 100, 0) AS decimal(18,0))
                    WHEN v.DiscountValue > @OrderAmount THEN @OrderAmount
                    ELSE CAST(v.DiscountValue AS decimal(18,0)) END AS RawDiscount
        FROM (SELECT 1 AS One) d
        LEFT JOIN dbo.Vouchers v ON v.Code = @Code
    ) s
);
GO

/* =============================================================================
   PROC NỘI BỘ DÙNG CHUNG
   ============================================================================= */

/* Ghi nhật ký hệ thống (AUD-01..03). Chỉ INSERT. */
CREATE OR ALTER PROCEDURE dbo.usp_Audit_Write
    @UserId bigint = NULL, @BranchId int = NULL,
    @Action varchar(60), @EntityType varchar(50), @EntityId bigint,
    @OldValues nvarchar(max) = NULL, @NewValues nvarchar(max) = NULL, @Reason nvarchar(300) = NULL,
    @IpAddress varchar(45) = NULL, @UserAgent nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.AuditLogs (UserId, BranchId, [Action], EntityType, EntityId, OldValues, NewValues, Reason, IpAddress, UserAgent)
    VALUES (@UserId, @BranchId, @Action, @EntityType, @EntityId, @OldValues, @NewValues, @Reason,
            COALESCE(@IpAddress, CONVERT(varchar(45), SESSION_CONTEXT(N'ip'))),
            COALESCE(@UserAgent, CONVERT(nvarchar(300), SESSION_CONTEXT(N'user_agent'))));
END
GO

/* Ghi lịch sử trạng thái serial (SER-03). Không tự đổi trạng thái. */
CREATE OR ALTER PROCEDURE dbo.usp_Internal_LogSerial
    @SerialItemId bigint, @FromStatus tinyint = NULL, @ToStatus tinyint,
    @RefType varchar(30) = NULL, @RefId bigint = NULL, @Note nvarchar(300) = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.SerialStatusHistory (SerialItemId, FromStatus, ToStatus, RefType, RefId, Note, ChangedBy)
    VALUES (@SerialItemId, @FromStatus, @ToStatus, @RefType, @RefId, @Note, @UserId);
END
GO

/* Thay đổi tồn kho của 1 sản phẩm tại 1 chi nhánh và ghi bút toán (INV-03).
   Không bao giờ để Physical/Reserved âm hoặc Reserved > Physical. */
CREATE OR ALTER PROCEDURE dbo.usp_Internal_StockMove
    @BranchId int, @ProductId bigint, @PhysicalDelta int, @ReservedDelta int,
    @MovementType tinyint, @RefType varchar(30), @RefId bigint,
    @UserId bigint = NULL, @Note nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.StockLevels WITH (UPDLOCK, HOLDLOCK) WHERE BranchId = @BranchId AND ProductId = @ProductId)
        INSERT dbo.StockLevels (BranchId, ProductId, PhysicalQty, ReservedQty) VALUES (@BranchId, @ProductId, 0, 0);

    UPDATE dbo.StockLevels
       SET PhysicalQty = PhysicalQty + @PhysicalDelta,
           ReservedQty = ReservedQty + @ReservedDelta,
           UpdatedAt   = SYSUTCDATETIME()
     WHERE BranchId = @BranchId AND ProductId = @ProductId
       AND PhysicalQty + @PhysicalDelta >= 0
       AND ReservedQty + @ReservedDelta >= 0
       AND ReservedQty + @ReservedDelta <= PhysicalQty + @PhysicalDelta;

    IF @@ROWCOUNT = 0
    BEGIN
        DECLARE @m nvarchar(300) = CONCAT(N'Tồn kho không đủ (chi nhánh ', @BranchId, N', sản phẩm ', @ProductId, N').');
        THROW 50003, @m, 1;
    END

    INSERT dbo.StockMovements (BranchId, ProductId, MovementType, PhysicalDelta, ReservedDelta, RefType, RefId, Note, CreatedBy)
    VALUES (@BranchId, @ProductId, @MovementType, @PhysicalDelta, @ReservedDelta, @RefType, @RefId, @Note, @UserId);
END
GO

/* =============================================================================
   TÀI KHOẢN
   ============================================================================= */

/* POST /api/auth/login (API so khớp hash, proc chỉ trả dữ liệu) */
CREATE OR ALTER PROCEDURE dbo.usp_Auth_GetUserByUsername
    @Username varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId bigint;
    SELECT @UserId = Id FROM dbo.Users WHERE Username = @Username OR Email = @Username;

    SELECT Id, Username, Email, Phone, PasswordHash, UserType, IsActive FROM dbo.Users WHERE Id = @UserId;
    SELECT r.Code AS RoleCode FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id = ur.RoleId WHERE ur.UserId = @UserId;
    SELECT DISTINCT pm.Code AS PermissionCode
      FROM dbo.UserRoles ur
      JOIN dbo.RolePermissions rp ON rp.RoleId = ur.RoleId
      JOIN dbo.Permissions pm ON pm.Id = rp.PermissionId
     WHERE ur.UserId = @UserId;
    SELECT e.Id AS EmployeeId, e.BranchId, e.Position, e.Status FROM dbo.Employees e WHERE e.UserId = @UserId;
    SELECT c.Id AS CustomerId, c.CustomerGroupId FROM dbo.Customers c WHERE c.UserId = @UserId;
END
GO

/* POST /api/auth/register */
CREATE OR ALTER PROCEDURE dbo.usp_Auth_RegisterCustomer
    @Username varchar(50), @PasswordHash varchar(255), @Email varchar(150) = NULL,
    @Phone varchar(20), @FullName nvarchar(150)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username) THROW 50005, N'Tên đăng nhập đã tồn tại.', 1;
        IF @Email IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email) THROW 50005, N'Email đã được sử dụng.', 1;

        DECLARE @GroupId int = (SELECT Id FROM dbo.CustomerGroups WHERE IsDefault = 1);
        DECLARE @RoleId int = (SELECT Id FROM dbo.Roles WHERE Code = 'Customer');
        DECLARE @UserId bigint, @CustomerId bigint;

        BEGIN TRANSACTION;
            INSERT dbo.Users (Username, Email, Phone, PasswordHash, UserType) VALUES (@Username, @Email, @Phone, @PasswordHash, 0);
            SET @UserId = SCOPE_IDENTITY();
            INSERT dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);
            INSERT dbo.Customers (UserId, FullName, Phone, Email, CustomerGroupId) VALUES (@UserId, @FullName, @Phone, @Email, @GroupId);
            SET @CustomerId = SCOPE_IDENTITY();
        COMMIT TRANSACTION;

        SELECT @UserId AS UserId, @CustomerId AS CustomerId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_UpdateLastLogin @UserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET LastLoginAt = SYSUTCDATETIME() WHERE Id = @UserId;
END
GO

/* =============================================================================
   SẢN PHẨM
   ============================================================================= */

/* Lưu thông số vào đúng bảng con theo SpecType của Category (PRD-01, PRD-02, PRD-04).
   JSON dùng camelCase. Monitor có "ports": [{"portType":"HDMI","quantity":2}] */
CREATE OR ALTER PROCEDURE dbo.usp_Internal_SaveSpecs
    @ProductId bigint, @SpecType tinyint, @SpecsJson nvarchar(max), @Replace bit
AS
BEGIN
    SET NOCOUNT ON;
    IF @SpecType = 0 RETURN;
    IF @SpecsJson IS NULL OR ISJSON(@SpecsJson) = 0 THROW 50004, N'Thiếu hoặc sai định dạng JSON thông số kỹ thuật.', 1;

    IF @Replace = 1
    BEGIN
        IF @SpecType = 1 DELETE dbo.Specs_CPU       WHERE ProductId = @ProductId;
        IF @SpecType = 2 DELETE dbo.Specs_RAM       WHERE ProductId = @ProductId;
        IF @SpecType = 3 DELETE dbo.Specs_Storage   WHERE ProductId = @ProductId;
        IF @SpecType = 4 DELETE dbo.Specs_Monitor   WHERE ProductId = @ProductId;   -- cổng kết nối xóa theo (CASCADE)
        IF @SpecType = 5 DELETE dbo.Specs_Mainboard WHERE ProductId = @ProductId;
        IF @SpecType = 6 DELETE dbo.Specs_PSU       WHERE ProductId = @ProductId;
    END

    IF @SpecType = 1
        INSERT dbo.Specs_CPU (ProductId, Socket, Cores, Threads, BaseClockGhz, BoostClockGhz, CacheMb, TdpW, HasCooler, HasIntegratedGpu)
        SELECT @ProductId, socket, cores, threads, baseClockGhz, boostClockGhz, cacheMb, tdpW, ISNULL(hasCooler, 0), ISNULL(hasIntegratedGpu, 0)
        FROM OPENJSON(@SpecsJson) WITH (
            socket varchar(20) '$.socket', cores smallint '$.cores', threads smallint '$.threads',
            baseClockGhz decimal(4,2) '$.baseClockGhz', boostClockGhz decimal(4,2) '$.boostClockGhz',
            cacheMb smallint '$.cacheMb', tdpW smallint '$.tdpW', hasCooler bit '$.hasCooler', hasIntegratedGpu bit '$.hasIntegratedGpu');

    ELSE IF @SpecType = 2
        INSERT dbo.Specs_RAM (ProductId, MemoryType, CapacityGbPerKit, ModulesPerKit, BusMhz, CasLatency, FormFactor, HasRgb)
        SELECT @ProductId, memoryType, capacityGbPerKit, modulesPerKit, busMhz, casLatency, ISNULL(formFactor, 'DIMM'), ISNULL(hasRgb, 0)
        FROM OPENJSON(@SpecsJson) WITH (
            memoryType varchar(10) '$.memoryType', capacityGbPerKit smallint '$.capacityGbPerKit', modulesPerKit tinyint '$.modulesPerKit',
            busMhz int '$.busMhz', casLatency tinyint '$.casLatency', formFactor varchar(10) '$.formFactor', hasRgb bit '$.hasRgb');

    ELSE IF @SpecType = 3
        INSERT dbo.Specs_Storage (ProductId, StorageType, [Interface], FormFactor, CapacityGb, ReadMbps, WriteMbps)
        SELECT @ProductId, storageType, interfaceType, formFactor, capacityGb, readMbps, writeMbps
        FROM OPENJSON(@SpecsJson) WITH (
            storageType varchar(20) '$.storageType', interfaceType varchar(30) '$.interface', formFactor varchar(20) '$.formFactor',
            capacityGb int '$.capacityGb', readMbps int '$.readMbps', writeMbps int '$.writeMbps');

    ELSE IF @SpecType = 4
    BEGIN
        INSERT dbo.Specs_Monitor (ProductId, SizeInch, Resolution, PanelType, RefreshRateHz, ResponseTimeMs, SrgbPercent)
        SELECT @ProductId, sizeInch, resolution, panelType, refreshRateHz, responseTimeMs, srgbPercent
        FROM OPENJSON(@SpecsJson) WITH (
            sizeInch decimal(4,1) '$.sizeInch', resolution varchar(20) '$.resolution', panelType varchar(10) '$.panelType',
            refreshRateHz smallint '$.refreshRateHz', responseTimeMs decimal(4,1) '$.responseTimeMs', srgbPercent tinyint '$.srgbPercent');

        INSERT dbo.Specs_Monitor_Ports (ProductId, PortType, Quantity)
        SELECT @ProductId, portType, quantity
        FROM OPENJSON(@SpecsJson, '$.ports') WITH (portType varchar(20) '$.portType', quantity tinyint '$.quantity');
    END

    ELSE IF @SpecType = 5
        INSERT dbo.Specs_Mainboard (ProductId, Socket, Chipset, MemoryType, RamSlots, MaxBusMhz, FormFactor, M2Slots, SataPorts)
        SELECT @ProductId, socket, chipset, memoryType, ramSlots, maxBusMhz, formFactor, ISNULL(m2Slots, 0), ISNULL(sataPorts, 0)
        FROM OPENJSON(@SpecsJson) WITH (
            socket varchar(20) '$.socket', chipset varchar(30) '$.chipset', memoryType varchar(10) '$.memoryType',
            ramSlots tinyint '$.ramSlots', maxBusMhz int '$.maxBusMhz', formFactor varchar(10) '$.formFactor',
            m2Slots tinyint '$.m2Slots', sataPorts tinyint '$.sataPorts');

    ELSE IF @SpecType = 6
        INSERT dbo.Specs_PSU (ProductId, PowerW, EfficiencyRating, ModularType)
        SELECT @ProductId, powerW, efficiencyRating, modularType
        FROM OPENJSON(@SpecsJson) WITH (
            powerW smallint '$.powerW', efficiencyRating varchar(20) '$.efficiencyRating', modularType varchar(10) '$.modularType');
END
GO

/* POST /api/admin/products  - Admin (UC-25) */
CREATE OR ALTER PROCEDURE dbo.usp_Product_Create
    @Sku varchar(50), @Name nvarchar(250), @Slug varchar(300), @BrandId int, @CategoryId int,
    @ShortDescription nvarchar(500) = NULL, @Description nvarchar(max) = NULL,
    @ListPrice decimal(18,0), @LastImportPrice decimal(18,0) = NULL,
    @RequiresSerial bit = 1, @MinStockDefault int = 0, @WarrantyMonths int = 0, @WarrantyTerms nvarchar(1000) = NULL,
    @Status tinyint = 0, @SpecsJson nvarchar(max) = NULL, @ImagesJson nvarchar(max) = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @SpecType tinyint = (SELECT SpecType FROM dbo.Categories WHERE Id = @CategoryId AND IsActive = 1);
        IF @SpecType IS NULL THROW 50001, N'Danh mục không tồn tại hoặc đã ngừng dùng.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Id = @BrandId AND IsActive = 1) THROW 50001, N'Thương hiệu không tồn tại.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = @Sku) THROW 50005, N'SKU đã tồn tại.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Products WHERE Slug = @Slug) THROW 50005, N'Slug đã tồn tại.', 1;

        DECLARE @ProductId bigint;
        BEGIN TRANSACTION;
            INSERT dbo.Products (Sku, Name, Slug, BrandId, CategoryId, ShortDescription, Description, ListPrice, LastImportPrice,
                                 RequiresSerial, MinStockDefault, WarrantyMonths, WarrantyTerms, Status, CreatedBy)
            VALUES (@Sku, @Name, @Slug, @BrandId, @CategoryId, @ShortDescription, @Description, @ListPrice, @LastImportPrice,
                    @RequiresSerial, @MinStockDefault, @WarrantyMonths, @WarrantyTerms, @Status, @UserId);
            SET @ProductId = SCOPE_IDENTITY();

            IF @ImagesJson IS NOT NULL
                INSERT dbo.ProductImages (ProductId, Url, SortOrder, IsPrimary)
                SELECT @ProductId, url, ISNULL(sortOrder, 0), ISNULL(isPrimary, 0)
                FROM OPENJSON(@ImagesJson) WITH (url varchar(500) '$.url', sortOrder int '$.sortOrder', isPrimary bit '$.isPrimary');

            EXEC dbo.usp_Internal_SaveSpecs @ProductId, @SpecType, @SpecsJson, 0;
        COMMIT TRANSACTION;

        SELECT @ProductId AS ProductId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* PUT /api/admin/products/{id}  - không đổi Category (PRD-03). Thông số được thay toàn bộ. Đổi giá niêm yết ghi Audit (PRC-07). */
CREATE OR ALTER PROCEDURE dbo.usp_Product_Update
    @ProductId bigint, @Name nvarchar(250), @Slug varchar(300), @BrandId int,
    @ShortDescription nvarchar(500) = NULL, @Description nvarchar(max) = NULL,
    @ListPrice decimal(18,0), @RequiresSerial bit, @MinStockDefault int, @WarrantyMonths int, @WarrantyTerms nvarchar(1000) = NULL,
    @SpecsJson nvarchar(max) = NULL, @ImagesJson nvarchar(max) = NULL, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @CategoryId int, @OldPrice decimal(18,0), @OldRequiresSerial bit;
        SELECT @CategoryId = CategoryId, @OldPrice = ListPrice, @OldRequiresSerial = RequiresSerial FROM dbo.Products WHERE Id = @ProductId;
        IF @CategoryId IS NULL THROW 50001, N'Sản phẩm không tồn tại.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Products WHERE Slug = @Slug AND Id <> @ProductId) THROW 50005, N'Slug đã tồn tại.', 1;
        IF @OldRequiresSerial <> @RequiresSerial
           AND (EXISTS (SELECT 1 FROM dbo.SerialItems WHERE ProductId = @ProductId) OR EXISTS (SELECT 1 FROM dbo.StockLevels WHERE ProductId = @ProductId AND PhysicalQty > 0))
            THROW 50002, N'Không đổi cờ RequiresSerial khi sản phẩm đã có tồn kho hoặc serial.', 1;
        DECLARE @SpecType tinyint = (SELECT SpecType FROM dbo.Categories WHERE Id = @CategoryId);

        BEGIN TRANSACTION;
            UPDATE dbo.Products
               SET Name = @Name, Slug = @Slug, BrandId = @BrandId, ShortDescription = @ShortDescription, Description = @Description,
                   ListPrice = @ListPrice, RequiresSerial = @RequiresSerial, MinStockDefault = @MinStockDefault,
                   WarrantyMonths = @WarrantyMonths, WarrantyTerms = @WarrantyTerms, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
             WHERE Id = @ProductId;

            IF @ImagesJson IS NOT NULL
            BEGIN
                DELETE dbo.ProductImages WHERE ProductId = @ProductId;
                INSERT dbo.ProductImages (ProductId, Url, SortOrder, IsPrimary)
                SELECT @ProductId, url, ISNULL(sortOrder, 0), ISNULL(isPrimary, 0)
                FROM OPENJSON(@ImagesJson) WITH (url varchar(500) '$.url', sortOrder int '$.sortOrder', isPrimary bit '$.isPrimary');
            END

            IF @SpecsJson IS NOT NULL EXEC dbo.usp_Internal_SaveSpecs @ProductId, @SpecType, @SpecsJson, 1;

            IF @OldPrice <> @ListPrice
                EXEC dbo.usp_Audit_Write @UserId = @UserId, @Action = 'PriceChanged', @EntityType = 'Product', @EntityId = @ProductId,
                     @OldValues = @OldPrice, @NewValues = @ListPrice, @Reason = N'Đổi giá niêm yết';
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* PATCH /api/admin/products/{id}/status  - 0 Draft, 1 Active, 2 Discontinued (PRD-07, PRD-09) */
CREATE OR ALTER PROCEDURE dbo.usp_Product_SetStatus
    @ProductId bigint, @Status tinyint, @UserId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN (0, 1, 2) THROW 50004, N'Trạng thái không hợp lệ.', 1;
    DECLARE @Old tinyint = (SELECT Status FROM dbo.Products WHERE Id = @ProductId);
    IF @Old IS NULL THROW 50001, N'Sản phẩm không tồn tại.', 1;
    IF @Old = @Status RETURN;

    UPDATE dbo.Products SET Status = @Status, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId WHERE Id = @ProductId;
    DECLARE @o nvarchar(10) = CAST(@Old AS nvarchar(10)), @n nvarchar(10) = CAST(@Status AS nvarchar(10));
    EXEC dbo.usp_Audit_Write @UserId = @UserId, @Action = 'ProductStatusChanged', @EntityType = 'Product', @EntityId = @ProductId,
         @OldValues = @o, @NewValues = @n;
END
GO

/* GET /api/products/{id}  - chi tiết sản phẩm: header, ảnh, thông số, tồn từng chi nhánh */
CREATE OR ALTER PROCEDURE dbo.usp_Product_GetById
    @ProductId bigint, @CustomerGroupId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now datetime2(0) = SYSUTCDATETIME();
    IF @CustomerGroupId IS NULL SELECT @CustomerGroupId = Id FROM dbo.CustomerGroups WHERE IsDefault = 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId) THROW 50001, N'Sản phẩm không tồn tại.', 1;
    DECLARE @SpecType tinyint = (SELECT c.SpecType FROM dbo.Products p JOIN dbo.Categories c ON c.Id = p.CategoryId WHERE p.Id = @ProductId);

    SELECT p.Id, p.Sku, p.Name, p.Slug, p.BrandId, b.Name AS BrandName, p.CategoryId, c.Code AS CategoryCode, c.Name AS CategoryName, c.SpecType,
           p.ShortDescription, p.Description, p.RequiresSerial, p.WarrantyMonths, p.WarrantyTerms, p.Status,
           p.ListPrice, pr.BasePrice, pr.FinalUnitPrice AS Price, pr.PromotionId,
           (SELECT ISNULL(SUM(sl.AvailableQty), 0) FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = p.Id) AS AvailableQty
      FROM dbo.Products p
      JOIN dbo.Brands b ON b.Id = p.BrandId
      JOIN dbo.Categories c ON c.Id = p.CategoryId
      CROSS APPLY dbo.fn_ProductPrice(p.Id, @CustomerGroupId, 1, @Now) pr
     WHERE p.Id = @ProductId;

    SELECT Id, Url, SortOrder, IsPrimary FROM dbo.ProductImages WHERE ProductId = @ProductId ORDER BY IsPrimary DESC, SortOrder;

    IF @SpecType = 1 SELECT * FROM dbo.Specs_CPU WHERE ProductId = @ProductId;
    ELSE IF @SpecType = 2 SELECT * FROM dbo.Specs_RAM WHERE ProductId = @ProductId;
    ELSE IF @SpecType = 3 SELECT * FROM dbo.Specs_Storage WHERE ProductId = @ProductId;
    ELSE IF @SpecType = 4
    BEGIN
        SELECT * FROM dbo.Specs_Monitor WHERE ProductId = @ProductId;
        SELECT PortType, Quantity FROM dbo.Specs_Monitor_Ports WHERE ProductId = @ProductId;
    END
    ELSE IF @SpecType = 5 SELECT * FROM dbo.Specs_Mainboard WHERE ProductId = @ProductId;
    ELSE IF @SpecType = 6 SELECT * FROM dbo.Specs_PSU WHERE ProductId = @ProductId;

    SELECT sl.BranchId, br.Name AS BranchName, sl.AvailableQty
      FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1
     WHERE sl.ProductId = @ProductId AND sl.AvailableQty > 0;
END
GO

/* GET /api/products  - tìm kiếm, lọc chung + lọc theo thông số (PRD-12, 13, 14)
   @SpecFiltersJson: [{"field":"memoryType","value":"DDR5"},{"field":"capacityGbPerKit","value":"32"}]
   Trường hợp lệ: socket, memoryType, capacityGbPerKit, busMhz, storageType, interface, capacityGb, resolution,
                  panelType, refreshRateHz, sizeInch, chipset, formFactor, powerW, cores (so sánh bằng, AND giữa các trường)
   @SortBy: newest | price_asc | price_desc | name | discount */
CREATE OR ALTER PROCEDURE dbo.usp_Product_Search
    @CustomerGroupId int = NULL, @CategoryCode varchar(30) = NULL, @BrandId int = NULL, @Keyword nvarchar(100) = NULL,
    @MinPrice decimal(18,0) = NULL, @MaxPrice decimal(18,0) = NULL, @InStockOnly bit = 0, @OnPromotion bit = 0,
    @SpecFiltersJson nvarchar(max) = NULL, @SortBy varchar(20) = 'newest', @PageNumber int = 1, @PageSize int = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    IF @PageSize > 100 SET @PageSize = 100;

    DECLARE @Now datetime2(0) = SYSUTCDATETIME();
    IF @CustomerGroupId IS NULL SELECT @CustomerGroupId = Id FROM dbo.CustomerGroups WHERE IsDefault = 1;

    CREATE TABLE #Cat (Id int PRIMARY KEY);
    IF @CategoryCode IS NOT NULL
    BEGIN
        ;WITH tree AS (
            SELECT Id FROM dbo.Categories WHERE Code = @CategoryCode
            UNION ALL
            SELECT c.Id FROM dbo.Categories c JOIN tree t ON c.ParentId = t.Id
        )
        INSERT #Cat (Id) SELECT Id FROM tree;
    END

    CREATE TABLE #F (Field varchar(30), Value nvarchar(50));
    IF @SpecFiltersJson IS NOT NULL
        INSERT #F (Field, Value) SELECT field, value FROM OPENJSON(@SpecFiltersJson) WITH (field varchar(30) '$.field', value nvarchar(50) '$.value');

    SELECT p.Id, p.Sku, p.Name, p.Slug, p.BrandId, b.Name AS BrandName, p.CategoryId, p.RequiresSerial, p.WarrantyMonths,
           pr.BasePrice, pr.FinalUnitPrice AS Price, pr.PromotionId, ISNULL(st.AvailableQty, 0) AS AvailableQty,
           img.Url AS ImageUrl, p.CreatedAt
    INTO #R
    FROM dbo.Products p
    JOIN dbo.Brands b ON b.Id = p.BrandId
    CROSS APPLY dbo.fn_ProductPrice(p.Id, @CustomerGroupId, 1, @Now) pr
    OUTER APPLY (SELECT SUM(sl.AvailableQty) AS AvailableQty
                   FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1
                  WHERE sl.ProductId = p.Id) st
    OUTER APPLY (SELECT TOP 1 i.Url FROM dbo.ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) img
    LEFT JOIN dbo.Specs_CPU c        ON c.ProductId   = p.Id
    LEFT JOIN dbo.Specs_RAM r        ON r.ProductId   = p.Id
    LEFT JOIN dbo.Specs_Storage s    ON s.ProductId   = p.Id
    LEFT JOIN dbo.Specs_Monitor m    ON m.ProductId   = p.Id
    LEFT JOIN dbo.Specs_Mainboard mb ON mb.ProductId  = p.Id
    LEFT JOIN dbo.Specs_PSU psu      ON psu.ProductId = p.Id
    WHERE p.Status = 1
      AND (@CategoryCode IS NULL OR p.CategoryId IN (SELECT Id FROM #Cat))
      AND (@BrandId IS NULL OR p.BrandId = @BrandId)
      AND (@Keyword IS NULL OR p.Name LIKE N'%' + @Keyword + N'%' OR p.Sku LIKE @Keyword + N'%')
      AND (@MinPrice IS NULL OR pr.FinalUnitPrice >= @MinPrice)
      AND (@MaxPrice IS NULL OR pr.FinalUnitPrice <= @MaxPrice)
      AND (@InStockOnly = 0 OR ISNULL(st.AvailableQty, 0) > 0)
      AND (@OnPromotion = 0 OR pr.PromotionId IS NOT NULL)
      AND NOT EXISTS (
            SELECT 1 FROM #F f
            WHERE NOT (
                (f.Field = 'socket'           AND (c.Socket = f.Value OR mb.Socket = f.Value)) OR
                (f.Field = 'memoryType'       AND (r.MemoryType = f.Value OR mb.MemoryType = f.Value)) OR
                (f.Field = 'capacityGbPerKit' AND r.CapacityGbPerKit = TRY_CAST(f.Value AS int)) OR
                (f.Field = 'busMhz'           AND r.BusMhz = TRY_CAST(f.Value AS int)) OR
                (f.Field = 'storageType'      AND s.StorageType = f.Value) OR
                (f.Field = 'interface'        AND s.[Interface] = f.Value) OR
                (f.Field = 'capacityGb'       AND s.CapacityGb = TRY_CAST(f.Value AS int)) OR
                (f.Field = 'resolution'       AND m.Resolution = f.Value) OR
                (f.Field = 'panelType'        AND m.PanelType = f.Value) OR
                (f.Field = 'refreshRateHz'    AND m.RefreshRateHz = TRY_CAST(f.Value AS int)) OR
                (f.Field = 'sizeInch'         AND m.SizeInch = TRY_CAST(f.Value AS decimal(4,1))) OR
                (f.Field = 'chipset'          AND mb.Chipset = f.Value) OR
                (f.Field = 'formFactor'       AND (r.FormFactor = f.Value OR s.FormFactor = f.Value OR mb.FormFactor = f.Value)) OR
                (f.Field = 'powerW'           AND psu.PowerW = TRY_CAST(f.Value AS int)) OR
                (f.Field = 'cores'            AND c.Cores = TRY_CAST(f.Value AS int))
            ));

    SELECT *, COUNT(*) OVER () AS TotalCount
      FROM #R
     ORDER BY CASE WHEN @SortBy = 'price_asc'  THEN Price END ASC,
              CASE WHEN @SortBy = 'price_desc' THEN Price END DESC,
              CASE WHEN @SortBy = 'name'       THEN Name  END ASC,
              CASE WHEN @SortBy = 'discount'   THEN BasePrice - Price END DESC,
              CreatedAt DESC, Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* PUT /api/admin/products/{id}/prices/{groupId}  - đặt giá theo nhóm khách (PRC-01, PRC-06, PRC-07)
   Giá thấp hơn giá nhập gần nhất chỉ được lưu khi có @ApprovedBy (Quản lý/Admin duyệt). */
CREATE OR ALTER PROCEDURE dbo.usp_ProductPrice_Upsert
    @ProductId bigint, @CustomerGroupId int, @Price decimal(18,0), @UserId bigint = NULL, @ApprovedBy bigint = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Price < 0 THROW 50004, N'Giá không hợp lệ.', 1;
        DECLARE @Cost decimal(18,0);
        SELECT @Cost = LastImportPrice FROM dbo.Products WHERE Id = @ProductId;
        IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId) THROW 50001, N'Sản phẩm không tồn tại.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.CustomerGroups WHERE Id = @CustomerGroupId) THROW 50001, N'Nhóm khách không tồn tại.', 1;
        IF @Cost IS NOT NULL AND @Price < @Cost AND @ApprovedBy IS NULL
            THROW 50004, N'Giá bán thấp hơn giá vốn, cần Quản lý/Admin duyệt.', 1;

        DECLARE @Old decimal(18,0) = (SELECT Price FROM dbo.ProductPrices WHERE ProductId = @ProductId AND CustomerGroupId = @CustomerGroupId);
        BEGIN TRANSACTION;
            IF @Old IS NULL
                INSERT dbo.ProductPrices (ProductId, CustomerGroupId, Price, CreatedBy) VALUES (@ProductId, @CustomerGroupId, @Price, @UserId);
            ELSE
                UPDATE dbo.ProductPrices SET Price = @Price, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
                 WHERE ProductId = @ProductId AND CustomerGroupId = @CustomerGroupId;

            DECLARE @o nvarchar(30) = CAST(@Old AS nvarchar(30)), @n nvarchar(30) = CAST(@Price AS nvarchar(30));
            DECLARE @why nvarchar(300) = CASE WHEN @ApprovedBy IS NOT NULL THEN CONCAT(N'Duyệt bởi user ', @ApprovedBy) END;
            EXEC dbo.usp_Audit_Write @UserId = @UserId, @Action = 'PriceChanged', @EntityType = 'ProductPrice', @EntityId = @ProductId,
                 @OldValues = @o, @NewValues = @n, @Reason = @why;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/compatibility/check  - cảnh báo tương thích (PRD-10), không chặn đặt hàng
   @ItemsJson: [{"productId":1,"quantity":1},{"productId":2,"quantity":2}]
   Kiểm tra: CPU-Mainboard (socket), RAM-Mainboard (chuẩn RAM, số khe), tổng TDP CPU so với công suất nguồn. */
CREATE OR ALTER PROCEDURE dbo.usp_Compat_Check
    @ItemsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    CREATE TABLE #W (Severity tinyint, Code varchar(30), Message nvarchar(300));
    CREATE TABLE #I (ProductId bigint PRIMARY KEY, Quantity int);
    INSERT #I SELECT productId, SUM(quantity) FROM OPENJSON(@ItemsJson) WITH (productId bigint '$.productId', quantity int '$.quantity') GROUP BY productId;

    INSERT #W
    SELECT 1, 'SOCKET_MISMATCH', CONCAT(N'CPU "', pc.Name, N'" (', c.Socket, N') không cùng socket với bo mạch "', pm.Name, N'" (', mb.Socket, N').')
      FROM #I ic JOIN dbo.Specs_CPU c ON c.ProductId = ic.ProductId JOIN dbo.Products pc ON pc.Id = c.ProductId
      CROSS JOIN #I im JOIN dbo.Specs_Mainboard mb ON mb.ProductId = im.ProductId JOIN dbo.Products pm ON pm.Id = mb.ProductId
     WHERE c.Socket <> mb.Socket;

    INSERT #W
    SELECT 1, 'MEMORY_TYPE_MISMATCH', CONCAT(N'RAM "', pr.Name, N'" (', r.MemoryType, N') không tương thích bo mạch "', pm.Name, N'" (', mb.MemoryType, N').')
      FROM #I ir JOIN dbo.Specs_RAM r ON r.ProductId = ir.ProductId JOIN dbo.Products pr ON pr.Id = r.ProductId
      CROSS JOIN #I im JOIN dbo.Specs_Mainboard mb ON mb.ProductId = im.ProductId JOIN dbo.Products pm ON pm.Id = mb.ProductId
     WHERE r.MemoryType <> mb.MemoryType;

    INSERT #W
    SELECT 1, 'RAM_SLOTS_EXCEEDED', CONCAT(N'Tổng số thanh RAM (', rs.Modules, N') vượt số khe của bo mạch "', pm.Name, N'" (', mb.RamSlots, N' khe).')
      FROM (SELECT SUM(CAST(r.ModulesPerKit AS int) * ir.Quantity) AS Modules FROM #I ir JOIN dbo.Specs_RAM r ON r.ProductId = ir.ProductId) rs
      CROSS JOIN #I im JOIN dbo.Specs_Mainboard mb ON mb.ProductId = im.ProductId JOIN dbo.Products pm ON pm.Id = mb.ProductId
     WHERE rs.Modules > mb.RamSlots;

    DECLARE @Ratio decimal(4,2) = ISNULL(TRY_CAST((SELECT [Value] FROM dbo.SystemSettings WHERE [Key] = 'compat.psu.tdp.ratio') AS decimal(4,2)), 0.8);
    INSERT #W
    SELECT 1, 'PSU_POWER_LOW', CONCAT(N'Tổng TDP CPU (', t.Tdp, N'W) vượt ', CAST(@Ratio * 100 AS int), N'% công suất nguồn "', pp.Name, N'" (', psu.PowerW, N'W).')
      FROM (SELECT SUM(CAST(c.TdpW AS int) * ic.Quantity) AS Tdp FROM #I ic JOIN dbo.Specs_CPU c ON c.ProductId = ic.ProductId) t
      CROSS JOIN #I ip JOIN dbo.Specs_PSU psu ON psu.ProductId = ip.ProductId JOIN dbo.Products pp ON pp.Id = psu.ProductId
     WHERE t.Tdp > psu.PowerW * @Ratio;

    SELECT Severity, Code, Message FROM #W;
END
GO

/* =============================================================================
   GIỎ HÀNG (CART-01..11)
   Chủ giỏ: @CustomerId (đã đăng nhập) hoặc @SessionToken (khách vãng lai).
   ============================================================================= */

CREATE OR ALTER PROCEDURE dbo.usp_Internal_ResolveCart
    @CustomerId bigint = NULL, @SessionToken varchar(64) = NULL, @Create bit = 1, @CartId bigint OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @CustomerId IS NULL AND @SessionToken IS NULL THROW 50004, N'Thiếu thông tin chủ giỏ hàng.', 1;
    SET @CartId = NULL;
    IF @CustomerId IS NOT NULL SELECT @CartId = Id FROM dbo.Carts WHERE CustomerId = @CustomerId;
    ELSE SELECT @CartId = Id FROM dbo.Carts WHERE SessionToken = @SessionToken;

    IF @CartId IS NULL AND @Create = 1
    BEGIN
        INSERT dbo.Carts (CustomerId, SessionToken) VALUES (@CustomerId, CASE WHEN @CustomerId IS NULL THEN @SessionToken END);
        SET @CartId = SCOPE_IDENTITY();
    END
END
GO

/* POST /api/cart/items  - thêm sản phẩm; @Replace = 1: đặt đúng số lượng thay vì cộng thêm */
CREATE OR ALTER PROCEDURE dbo.usp_Cart_AddItem
    @CustomerId bigint = NULL, @SessionToken varchar(64) = NULL, @ProductId bigint, @Quantity int, @Replace bit = 0
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Quantity <= 0 THROW 50004, N'Số lượng phải lớn hơn 0.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND Status = 1) THROW 50001, N'Sản phẩm không tồn tại hoặc ngừng kinh doanh.', 1;

        DECLARE @Avail int = (SELECT ISNULL(SUM(sl.AvailableQty), 0) FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = @ProductId);
        DECLARE @CartId bigint, @Existing int, @NewQty int;

        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_ResolveCart @CustomerId, @SessionToken, 1, @CartId OUTPUT;
            SELECT @Existing = Quantity FROM dbo.CartItems WHERE CartId = @CartId AND ProductId = @ProductId;
            SET @NewQty = CASE WHEN @Replace = 1 THEN @Quantity ELSE ISNULL(@Existing, 0) + @Quantity END;

            IF @NewQty > @Avail THROW 50003, N'Số lượng vượt quá tồn kho khả dụng.', 1;

            IF @Existing IS NULL INSERT dbo.CartItems (CartId, ProductId, Quantity) VALUES (@CartId, @ProductId, @NewQty);
            ELSE UPDATE dbo.CartItems SET Quantity = @NewQty WHERE CartId = @CartId AND ProductId = @ProductId;

            UPDATE dbo.Carts SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @CartId;
        COMMIT TRANSACTION;

        SELECT @CartId AS CartId, @ProductId AS ProductId, @NewQty AS Quantity;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* PATCH /api/cart/items/{productId}  - đổi số lượng và/hoặc tick chọn. Số lượng <= 0 thì xóa dòng. */
CREATE OR ALTER PROCEDURE dbo.usp_Cart_UpdateItem
    @CustomerId bigint = NULL, @SessionToken varchar(64) = NULL, @ProductId bigint,
    @Quantity int = NULL, @IsSelected bit = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @CartId bigint;
        EXEC dbo.usp_Internal_ResolveCart @CustomerId, @SessionToken, 0, @CartId OUTPUT;
        IF @CartId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.CartItems WHERE CartId = @CartId AND ProductId = @ProductId)
            THROW 50001, N'Sản phẩm không có trong giỏ hàng.', 1;

        DECLARE @Avail int = (SELECT ISNULL(SUM(sl.AvailableQty), 0) FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = @ProductId);

        IF @Quantity IS NOT NULL AND @Quantity <= 0
        BEGIN
            DELETE dbo.CartItems WHERE CartId = @CartId AND ProductId = @ProductId;
            RETURN;
        END
        IF @Quantity IS NOT NULL AND @Quantity > @Avail THROW 50003, N'Số lượng vượt quá tồn kho khả dụng.', 1;
        IF @IsSelected = 1 AND @Avail <= 0 THROW 50003, N'Sản phẩm đã hết hàng, không thể chọn.', 1;

        UPDATE dbo.CartItems
           SET Quantity = ISNULL(@Quantity, Quantity), IsSelected = ISNULL(@IsSelected, IsSelected)
         WHERE CartId = @CartId AND ProductId = @ProductId;
        UPDATE dbo.Carts SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @CartId;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END
GO

/* DELETE /api/cart/items/{productId} */
CREATE OR ALTER PROCEDURE dbo.usp_Cart_RemoveItem
    @CustomerId bigint = NULL, @SessionToken varchar(64) = NULL, @ProductId bigint
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @CartId bigint;
    EXEC dbo.usp_Internal_ResolveCart @CustomerId, @SessionToken, 0, @CartId OUTPUT;
    IF @CartId IS NOT NULL DELETE dbo.CartItems WHERE CartId = @CartId AND ProductId = @ProductId;
END
GO

/* POST /api/cart/select-all  - chọn / bỏ chọn tất cả (chỉ chọn được món còn hàng) */
CREATE OR ALTER PROCEDURE dbo.usp_Cart_SelectAll
    @CustomerId bigint = NULL, @SessionToken varchar(64) = NULL, @IsSelected bit
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @CartId bigint;
    EXEC dbo.usp_Internal_ResolveCart @CustomerId, @SessionToken, 0, @CartId OUTPUT;
    IF @CartId IS NULL RETURN;

    UPDATE ci SET IsSelected = @IsSelected
      FROM dbo.CartItems ci
     WHERE ci.CartId = @CartId
       AND (@IsSelected = 0 OR EXISTS (SELECT 1 FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1
                                        WHERE sl.ProductId = ci.ProductId AND sl.AvailableQty > 0));
END
GO

/* GET /api/cart  - kiểm tra tồn tức thời (CART-07): hết hàng thì bỏ tick, ít hơn số lượng giỏ thì giảm số lượng.
   Trả 3 bảng: dòng giỏ hàng, tổng tiền các dòng được tick, cảnh báo. */
CREATE OR ALTER PROCEDURE dbo.usp_Cart_Get
    @CustomerId bigint = NULL, @SessionToken varchar(64) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Now datetime2(0) = SYSUTCDATETIME(), @CartId bigint, @GroupId int;
    EXEC dbo.usp_Internal_ResolveCart @CustomerId, @SessionToken, 0, @CartId OUTPUT;

    SELECT @GroupId = CustomerGroupId FROM dbo.Customers WHERE Id = @CustomerId;
    IF @GroupId IS NULL SELECT @GroupId = Id FROM dbo.CustomerGroups WHERE IsDefault = 1;

    DECLARE @W TABLE (ProductId bigint, Code varchar(30), OldQuantity int, NewQuantity int);

    IF @CartId IS NOT NULL
    BEGIN
        ;WITH A AS (
            SELECT ci.Id, (SELECT ISNULL(SUM(sl.AvailableQty), 0) FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1
                            WHERE sl.ProductId = ci.ProductId) AS Avail
              FROM dbo.CartItems ci WHERE ci.CartId = @CartId
        )
        UPDATE ci
           SET IsSelected = CASE WHEN a.Avail = 0 THEN 0 ELSE ci.IsSelected END,
               Quantity   = CASE WHEN a.Avail > 0 AND ci.Quantity > a.Avail THEN a.Avail ELSE ci.Quantity END
        OUTPUT inserted.ProductId, CASE WHEN a.Avail = 0 THEN 'OUT_OF_STOCK' ELSE 'QUANTITY_REDUCED' END, deleted.Quantity, inserted.Quantity
          INTO @W (ProductId, Code, OldQuantity, NewQuantity)
          FROM dbo.CartItems ci JOIN A a ON a.Id = ci.Id
         WHERE (a.Avail = 0 AND ci.IsSelected = 1) OR (a.Avail > 0 AND ci.Quantity > a.Avail);
    END

    SELECT ci.Id AS CartItemId, ci.ProductId, p.Sku, p.Name, img.Url AS ImageUrl, ci.Quantity, ci.IsSelected,
           pr.BasePrice, pr.FinalUnitPrice AS UnitPrice, pr.PromotionId, pr.DiscountPerUnit,
           ci.Quantity * pr.FinalUnitPrice AS LineTotal, st.Avail AS AvailableQty
      FROM dbo.CartItems ci
      JOIN dbo.Products p ON p.Id = ci.ProductId
      CROSS APPLY dbo.fn_ProductPrice(ci.ProductId, @GroupId, ci.Quantity, @Now) pr
      OUTER APPLY (SELECT TOP 1 i.Url FROM dbo.ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) img
      OUTER APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS Avail FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = ci.ProductId) st
     WHERE ci.CartId = @CartId
     ORDER BY ci.AddedAt;

    SELECT ISNULL(SUM(CASE WHEN ci.IsSelected = 1 THEN 1 ELSE 0 END), 0) AS SelectedCount,
           ISNULL(SUM(CASE WHEN ci.IsSelected = 1 THEN ci.Quantity * pr.BasePrice END), 0) AS SubTotal,
           ISNULL(SUM(CASE WHEN ci.IsSelected = 1 THEN ci.Quantity * pr.DiscountPerUnit END), 0) AS DiscountTotal,
           ISNULL(SUM(CASE WHEN ci.IsSelected = 1 THEN ci.Quantity * pr.FinalUnitPrice END), 0) AS Total
      FROM dbo.CartItems ci
      CROSS APPLY dbo.fn_ProductPrice(ci.ProductId, @GroupId, ci.Quantity, @Now) pr
     WHERE ci.CartId = @CartId;

    SELECT w.ProductId, p.Name, w.Code, w.OldQuantity, w.NewQuantity FROM @W w JOIN dbo.Products p ON p.Id = w.ProductId;
END
GO

/* POST /api/cart/merge  - gộp giỏ tạm vào giỏ tài khoản sau khi đăng nhập (CART-02) */
CREATE OR ALTER PROCEDURE dbo.usp_Cart_Merge
    @SessionToken varchar(64), @CustomerId bigint
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        DECLARE @GuestCartId bigint = (SELECT Id FROM dbo.Carts WHERE SessionToken = @SessionToken AND CustomerId IS NULL);
        IF @GuestCartId IS NULL RETURN;

        DECLARE @CartId bigint;
        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_ResolveCart @CustomerId, NULL, 1, @CartId OUTPUT;

            ;WITH G AS (
                SELECT gi.ProductId, gi.Quantity,
                       (SELECT ISNULL(SUM(sl.AvailableQty), 0) FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = gi.ProductId) AS Avail
                  FROM dbo.CartItems gi WHERE gi.CartId = @GuestCartId
            )
            UPDATE ci
               SET Quantity = CASE WHEN ci.Quantity + g.Quantity > g.Avail THEN g.Avail ELSE ci.Quantity + g.Quantity END
              FROM dbo.CartItems ci JOIN G g ON g.ProductId = ci.ProductId
             WHERE ci.CartId = @CartId AND g.Avail > 0;

            INSERT dbo.CartItems (CartId, ProductId, Quantity)
            SELECT @CartId, gi.ProductId,
                   CASE WHEN gi.Quantity > st.Avail THEN st.Avail ELSE gi.Quantity END
              FROM dbo.CartItems gi
              CROSS APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS Avail FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = gi.ProductId) st
             WHERE gi.CartId = @GuestCartId AND st.Avail > 0
               AND NOT EXISTS (SELECT 1 FROM dbo.CartItems x WHERE x.CartId = @CartId AND x.ProductId = gi.ProductId);

            DELETE dbo.Carts WHERE Id = @GuestCartId;     -- CartItems xóa theo (CASCADE)
            UPDATE dbo.Carts SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @CartId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* POST /api/vouchers/validate  - kiểm tra mã trước khi đặt hàng */
CREATE OR ALTER PROCEDURE dbo.usp_Voucher_Validate
    @Code varchar(40), @CustomerId bigint, @OrderAmount decimal(18,0), @HasPromotion bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @GroupId int = (SELECT CustomerGroupId FROM dbo.Customers WHERE Id = @CustomerId);
    IF @GroupId IS NULL SELECT @GroupId = Id FROM dbo.CustomerGroups WHERE IsDefault = 1;

    SELECT CAST(CASE WHEN ErrorMessage IS NULL THEN 1 ELSE 0 END AS bit) AS IsValid, ErrorMessage, DiscountAmount
      FROM dbo.fn_VoucherCheck(@Code, @GroupId, @OrderAmount, @HasPromotion, SYSUTCDATETIME());
END
GO
