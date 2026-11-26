/* =============================================================================
   10 — PC BUILD SẴN (combo linh kiện) — DB BRD_DEV
   -----------------------------------------------------------------------------
   Mục đích: khách hàng KHÔNG có chuyên môn không phải tự chọn từng linh kiện.
   Cửa hàng bán sẵn các bộ "PC build sẵn" — mỗi bộ là một danh sách linh kiện có
   trong kho. Khách bấm "Thêm vào giỏ" ⇒ hệ thống tự thêm TẤT CẢ linh kiện của bộ
   đó vào giỏ hàng (một giao dịch, kiểm tra tồn kho của từng linh kiện).

    ⚠ GIÁ CỦA BỘ: bộ KHÔNG có giá riêng — giá bộ = TỔNG GIÁ LINH KIỆN theo giá
    hiện hành (bảng giá theo nhóm khách + khuyến mãi đang chạy, hàm fn_ProductPrice).
    Nhờ vậy số tiền hiển thị luôn khớp số tiền trong giỏ và trong đơn hàng, không
    phát sinh chênh lệch. Muốn bán combo rẻ hơn mua lẻ thì phải bổ sung cơ chế giảm
    giá ở tầng giỏ hàng/đơn hàng (CartItems/OrderItems hiện chưa có cột giảm giá).

   Nội dung file:
     1/3 — Bảng dbo.PcBuilds, dbo.PcBuildItems (tạo an toàn nếu chưa có)
     2/3 — Proc: usp_PcBuild_Search · usp_PcBuild_GetById · usp_PcBuild_AddToCart
     3/3 — Dữ liệu mẫu: 5 bộ PC build sẵn (dùng SKU của Database/09_seed_sample_products.sql)
     BỔ SUNG — tab "TỰ BUILD PC": usp_PcBuild_BuilderCatalog (danh mục linh kiện + thông số tương thích)

   Chạy lại nhiều lần đều an toàn (IF OBJECT_ID · CREATE OR ALTER · IF NOT EXISTS).
   Chạy:  sqlcmd -S localhost -d BRD_DEV -E -I -f 65001 -i "Database\10_pc_builds.sql"
   ============================================================================= */
SET NOCOUNT ON;
GO

/* =============================================================================
   1/3 — BẢNG
   ============================================================================= */

IF OBJECT_ID('dbo.PcBuilds', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PcBuilds (
        Id          bigint IDENTITY(1,1) NOT NULL,
        Code        varchar(30)   NOT NULL,            -- mã bộ, VD: PC-GAMING-01
        Name        nvarchar(200) NOT NULL,
        Slug        varchar(250)  NOT NULL,
        Tier        tinyint       NOT NULL DEFAULT 0,  -- 0 Văn phòng, 1 Gaming, 2 Đồ hoạ/Render, 3 Cao cấp
        Summary     nvarchar(500) NULL,                -- mô tả ngắn hiển thị trên thẻ bộ
        Description nvarchar(max) NULL,                -- mô tả đầy đủ ở trang chi tiết
        ImageUrl    varchar(500)  NULL,
        IsFeatured  bit           NOT NULL DEFAULT 0,  -- ghim lên khối "PC build sẵn" ở trang chủ
        SortOrder   int           NOT NULL DEFAULT 0,
        IsActive    bit           NOT NULL DEFAULT 1,
        CreatedAt   datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy   bigint        NULL,
        UpdatedAt   datetime2(0)  NULL,
        UpdatedBy   bigint        NULL,
        CONSTRAINT PK_PcBuilds PRIMARY KEY (Id),
        CONSTRAINT UQ_PcBuilds_Code UNIQUE (Code),
        CONSTRAINT UQ_PcBuilds_Slug UNIQUE (Slug),
        CONSTRAINT CK_PcBuilds_Tier CHECK (Tier BETWEEN 0 AND 3)
    );
END
GO

IF OBJECT_ID('dbo.PcBuildItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PcBuildItems (
        Id        bigint        IDENTITY(1,1) NOT NULL,
        PcBuildId bigint        NOT NULL,
        ProductId bigint        NOT NULL,
        Quantity  int           NOT NULL DEFAULT 1,
        Note      nvarchar(200) NULL,                  -- ghi chú vai trò linh kiện trong bộ
        SortOrder int           NOT NULL DEFAULT 0,    -- thứ tự hiển thị (CPU → Mainboard → RAM → ...)
        CONSTRAINT PK_PcBuildItems PRIMARY KEY (Id),
        CONSTRAINT UQ_PcBuildItems UNIQUE (PcBuildId, ProductId),
        CONSTRAINT FK_PcBuildItems_PcBuilds FOREIGN KEY (PcBuildId) REFERENCES dbo.PcBuilds (Id) ON DELETE CASCADE,
        CONSTRAINT FK_PcBuildItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
        CONSTRAINT CK_PcBuildItems_Qty CHECK (Quantity > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PcBuildItems_PcBuildId' AND object_id = OBJECT_ID('dbo.PcBuildItems'))
    CREATE INDEX IX_PcBuildItems_PcBuildId ON dbo.PcBuildItems (PcBuildId, SortOrder);
GO

/* =============================================================================
   2/3 — STORED PROCEDURE
   ============================================================================= */

/* GET /api/v1/pc-builds — danh sách bộ PC build sẵn (phân trang).
   Giá bộ (PartsPrice) = tổng giá linh kiện theo fn_ProductPrice ⇒ luôn khớp giá trong giỏ hàng.
   AvailableBuilds = số bộ còn lắp được = MIN(tồn khả dụng của linh kiện / số lượng cần). */
CREATE OR ALTER PROCEDURE dbo.usp_PcBuild_Search
    @Keyword nvarchar(100) = NULL,
    @Tier tinyint = NULL,
    @CustomerGroupId int = NULL,
    @MinPrice decimal(18,0) = NULL,
    @MaxPrice decimal(18,0) = NULL,
    @InStockOnly bit = 0,
    @FeaturedOnly bit = 0,
    @SortBy varchar(20) = 'order',
    @PageNumber int = 1,
    @PageSize int = 20
AS
BEGIN
    SET NOCOUNT ON;

    SET @Keyword = NULLIF(LTRIM(RTRIM(ISNULL(@Keyword, N''))), N'');
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    IF @PageSize > 200 SET @PageSize = 200;

    ;WITH Comp AS (
        SELECT bi.PcBuildId, bi.Quantity, pr.FinalUnitPrice,
               (SELECT ISNULL(SUM(sl.AvailableQty), 0)
                  FROM dbo.StockLevels sl
                  JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1
                 WHERE sl.ProductId = bi.ProductId) AS Avail
          FROM dbo.PcBuildItems bi
          CROSS APPLY dbo.fn_ProductPrice(bi.ProductId, @CustomerGroupId, bi.Quantity, SYSUTCDATETIME()) pr
    ),
    Agg AS (
        SELECT c.PcBuildId,
               COUNT(*) AS ComponentCount,
               SUM(c.FinalUnitPrice * c.Quantity) AS PartsPrice,
               MIN(c.Avail / c.Quantity) AS AvailableBuilds
          FROM Comp c
         GROUP BY c.PcBuildId
    )
    SELECT b.Id, b.Code, b.Name, b.Slug, b.Tier, b.Summary, b.ImageUrl, b.IsFeatured,
           ISNULL(a.ComponentCount, 0) AS ComponentCount,
           ISNULL(a.PartsPrice, 0) AS PartsPrice,
           ISNULL(a.AvailableBuilds, 0) AS AvailableBuilds,
           STUFF((SELECT N' · ' + p.Name
                    FROM dbo.PcBuildItems x
                    JOIN dbo.Products p ON p.Id = x.ProductId
                   WHERE x.PcBuildId = b.Id
                   ORDER BY x.SortOrder, p.Name
                     FOR XML PATH('')), 1, 3, N'') AS ComponentPreview,
           COUNT(*) OVER () AS TotalCount
      FROM dbo.PcBuilds b
      LEFT JOIN Agg a ON a.PcBuildId = b.Id
     WHERE b.IsActive = 1
       AND (@Tier IS NULL OR b.Tier = @Tier)
       AND (@FeaturedOnly = 0 OR b.IsFeatured = 1)
       AND (@InStockOnly = 0 OR ISNULL(a.AvailableBuilds, 0) > 0)
       AND (@MinPrice IS NULL OR ISNULL(a.PartsPrice, 0) >= @MinPrice)
       AND (@MaxPrice IS NULL OR ISNULL(a.PartsPrice, 0) <= @MaxPrice)
       AND (@Keyword IS NULL
            OR b.Name LIKE N'%' + @Keyword + N'%'
            OR b.Code LIKE N'%' + @Keyword + N'%'
            OR b.Summary LIKE N'%' + @Keyword + N'%')
     ORDER BY CASE WHEN @SortBy = 'price_asc' THEN ISNULL(a.PartsPrice, 0) END ASC,
              CASE WHEN @SortBy = 'price_desc' THEN ISNULL(a.PartsPrice, 0) END DESC,
              CASE WHEN @SortBy = 'name' THEN b.Name END ASC,
              CASE WHEN @SortBy NOT IN ('price_asc', 'price_desc', 'name') THEN b.SortOrder END ASC,
              b.Id
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* GET /api/v1/pc-builds/{id} — chi tiết bộ.
   Result set 1: header bộ · Result set 2: linh kiện trong bộ (kèm giá và tồn hiện tại). */
CREATE OR ALTER PROCEDURE dbo.usp_PcBuild_GetById
    @PcBuildId bigint,
    @CustomerGroupId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.PcBuilds WHERE Id = @PcBuildId AND IsActive = 1)
        THROW 50001, N'Bộ PC build sẵn không tồn tại hoặc đã ngừng bán.', 1;

    ;WITH Comp AS (
        SELECT bi.PcBuildId, bi.Quantity, pr.FinalUnitPrice,
               (SELECT ISNULL(SUM(sl.AvailableQty), 0)
                  FROM dbo.StockLevels sl
                  JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1
                 WHERE sl.ProductId = bi.ProductId) AS Avail
          FROM dbo.PcBuildItems bi
          CROSS APPLY dbo.fn_ProductPrice(bi.ProductId, @CustomerGroupId, bi.Quantity, SYSUTCDATETIME()) pr
         WHERE bi.PcBuildId = @PcBuildId
    )
    SELECT b.Id, b.Code, b.Name, b.Slug, b.Tier, b.Summary, b.Description, b.ImageUrl, b.IsFeatured,
           ISNULL(a.ComponentCount, 0) AS ComponentCount,
           ISNULL(a.PartsPrice, 0) AS PartsPrice,
           ISNULL(a.AvailableBuilds, 0) AS AvailableBuilds
      FROM dbo.PcBuilds b
      LEFT JOIN (SELECT c.PcBuildId,
                        COUNT(*) AS ComponentCount,
                        SUM(c.FinalUnitPrice * c.Quantity) AS PartsPrice,
                        MIN(c.Avail / c.Quantity) AS AvailableBuilds
                   FROM Comp c
                  GROUP BY c.PcBuildId) a ON a.PcBuildId = b.Id
     WHERE b.Id = @PcBuildId;

    SELECT bi.ProductId, p.Sku, p.Name, p.Slug,
           c.Code AS CategoryCode, c.Name AS CategoryName, c.SpecType,
           br.Name AS BrandName,
           bi.Quantity,
           pr.FinalUnitPrice AS UnitPrice,
           pr.FinalUnitPrice * bi.Quantity AS LineTotal,
           (SELECT ISNULL(SUM(sl.AvailableQty), 0)
              FROM dbo.StockLevels sl
              JOIN dbo.Branches b2 ON b2.Id = sl.BranchId AND b2.IsActive = 1
             WHERE sl.ProductId = bi.ProductId) AS AvailableQty,
           (SELECT TOP 1 i.Url FROM dbo.ProductImages i
             WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) AS ImageUrl,
           p.WarrantyMonths, p.RequiresSerial, p.Status,
           bi.Note, bi.SortOrder
      FROM dbo.PcBuildItems bi
      JOIN dbo.Products p ON p.Id = bi.ProductId
      JOIN dbo.Categories c ON c.Id = p.CategoryId
      JOIN dbo.Brands br ON br.Id = p.BrandId
      CROSS APPLY dbo.fn_ProductPrice(bi.ProductId, @CustomerGroupId, bi.Quantity, SYSUTCDATETIME()) pr
     WHERE bi.PcBuildId = @PcBuildId
     ORDER BY bi.SortOrder, c.Name, p.Name;
END
GO

/* POST /api/v1/pc-builds/{id}/add-to-cart — thêm TẤT CẢ linh kiện của bộ vào giỏ hàng.
   Một giao dịch duy nhất: nếu BẤT KỲ linh kiện nào không đủ tồn (hoặc đã ngừng bán) thì KHÔNG thêm gì cả
   và báo rõ tên linh kiện thiếu (50003/50001). Result set: từng linh kiện đã thêm + số lượng hiện có trong giỏ.
   Tham số chủ giỏ giống usp_Cart_AddItem: @CustomerId (đăng nhập) hoặc @SessionToken (khách vãng lai). */
CREATE OR ALTER PROCEDURE dbo.usp_PcBuild_AddToCart
    @CustomerId bigint = NULL,
    @SessionToken varchar(64) = NULL,
    @PcBuildId bigint,
    @Quantity int = 1
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        IF @Quantity <= 0 THROW 50004, N'Số bộ cần thêm phải lớn hơn 0.', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.PcBuilds WHERE Id = @PcBuildId AND IsActive = 1)
            THROW 50001, N'Bộ PC build sẵn không tồn tại hoặc đã ngừng bán.', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.PcBuildItems WHERE PcBuildId = @PcBuildId)
            THROW 50004, N'Bộ PC build sẵn chưa có linh kiện nào.', 1;

        DECLARE @CartId bigint, @Stop nvarchar(260);

        -- Linh kiện trong bộ đã ngừng kinh doanh ⇒ không bán bộ nữa (cần cập nhật lại dữ liệu bộ).
        SELECT TOP 1 @Stop = N'Linh kiện "' + p.Name + N'" đã ngừng kinh doanh, không thể thêm bộ vào giỏ.'
          FROM dbo.PcBuildItems bi
          JOIN dbo.Products p ON p.Id = bi.ProductId
         WHERE bi.PcBuildId = @PcBuildId AND p.Status <> 1
         ORDER BY bi.SortOrder, p.Name;
        IF @Stop IS NOT NULL THROW 50001, @Stop, 1;

        BEGIN TRANSACTION;
            EXEC dbo.usp_Internal_ResolveCart @CustomerId, @SessionToken, 1, @CartId OUTPUT;

            -- Thiếu tồn ở bất kỳ linh kiện nào ⇒ dừng, báo tên linh kiện thiếu, rollback toàn bộ.
            SELECT TOP 1 @Stop = N'Linh kiện "' + p.Name + N'" chỉ còn ' + CAST(st.Avail AS nvarchar(10))
                               + N' trong kho, không đủ cho ' + CAST(bi.Quantity * @Quantity AS nvarchar(10)) + N' bộ.'
              FROM dbo.PcBuildItems bi
              JOIN dbo.Products p ON p.Id = bi.ProductId
              LEFT JOIN dbo.CartItems ci ON ci.CartId = @CartId AND ci.ProductId = bi.ProductId
              CROSS APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS Avail
                             FROM dbo.StockLevels sl
                             JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1
                            WHERE sl.ProductId = bi.ProductId) st
             WHERE bi.PcBuildId = @PcBuildId
               AND ISNULL(ci.Quantity, 0) + bi.Quantity * @Quantity > st.Avail
             ORDER BY bi.SortOrder, p.Name;
            IF @Stop IS NOT NULL THROW 50003, @Stop, 1;

            UPDATE ci
               SET Quantity = ci.Quantity + bi.Quantity * @Quantity
              FROM dbo.CartItems ci
              JOIN dbo.PcBuildItems bi ON bi.ProductId = ci.ProductId AND bi.PcBuildId = @PcBuildId
             WHERE ci.CartId = @CartId;

            INSERT dbo.CartItems (CartId, ProductId, Quantity)
            SELECT @CartId, bi.ProductId, bi.Quantity * @Quantity
              FROM dbo.PcBuildItems bi
             WHERE bi.PcBuildId = @PcBuildId
               AND NOT EXISTS (SELECT 1 FROM dbo.CartItems x
                                WHERE x.CartId = @CartId AND x.ProductId = bi.ProductId);

            UPDATE dbo.Carts SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @CartId;
        COMMIT TRANSACTION;

        SELECT bi.ProductId, p.Name, bi.Quantity * @Quantity AS AddedQuantity, ci.Quantity AS CartQuantity
          FROM dbo.PcBuildItems bi
          JOIN dbo.Products p ON p.Id = bi.ProductId
          JOIN dbo.CartItems ci ON ci.CartId = @CartId AND ci.ProductId = bi.ProductId
         WHERE bi.PcBuildId = @PcBuildId
         ORDER BY bi.SortOrder, p.Name;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* =============================================================================
   3/3 — DỮ LIỆU MẪU (5 bộ, dùng SKU của Database/09_seed_sample_products.sql)
   -----------------------------------------------------------------------------
   An toàn khi chạy lại: bộ được thêm theo Code, linh kiện theo (bộ, sản phẩm).
   Cảnh báo: danh mục hiện CHƯA có card đồ hoạ rời nên các bộ dùng CPU có đồ hoạ
   tích hợp để máy xuất được hình. Khi cửa hàng nhập VGA, chỉ cần thêm dòng vào bộ.
   ============================================================================= */

DECLARE @Builds TABLE (
    Code varchar(30), Name nvarchar(200), Slug varchar(250), Tier tinyint,
    Summary nvarchar(500), Description nvarchar(max), ImageUrl varchar(500),
    IsFeatured bit, SortOrder int
);

INSERT @Builds (Code, Name, Slug, Tier, Summary, Description, ImageUrl, IsFeatured, SortOrder) VALUES
('PC-OFFICE-01', N'PC Văn phòng cơ bản (Core i3)', 'pc-van-phong-co-ban-core-i3', 0,
 N'Core i3-14100 · 16GB DDR4 · SSD NVMe 500GB · màn hình 24 inch FHD',
 N'Bộ máy dành cho văn phòng, học tập, kế toán, bán hàng: CPU Intel Core i3-14100 có đồ hoạ tích hợp, 16GB RAM DDR4, SSD NVMe Gen4 500GB và màn hình 24 inch Full HD. Máy chạy êm, khởi động nhanh, đủ dùng Office, trình duyệt nhiều tab, phần mềm kế toán. Bảo hành theo từng linh kiện (CPU/Bo mạch/RAM/SSD 36 tháng, nguồn 60 tháng).',
 '/images/builds/pc-tower.svg', 1, 10),

('PC-OFFICE-PLUS-02', N'PC Văn phòng nâng cao (Ryzen 5)', 'pc-van-phong-nang-cao-ryzen-5', 0,
 N'Ryzen 5 7600 · 16GB DDR5 · SSD NVMe 1TB · màn hình 24 inch IPS có USB-C',
 N'Nâng cấp lên nền tảng AM5 với DDR5: CPU AMD Ryzen 5 7600 (6 nhân), bo mạch B650 có WiFi, SSD NVMe Gen4 1TB và màn hình Dell UltraSharp 24 inch có USB-C (cắm trực tiếp laptop/màn hình phụ, sạc ngược). Phù hợp công việc văn phòng nặng, xử lý nhiều file Excel lớn, họp online. Bảo hành theo từng linh kiện.',
 '/images/builds/pc-tower.svg', 0, 20),

('PC-GAMING-01', N'PC Gaming tầm trung (Ryzen 7)', 'pc-gaming-tam-trung-ryzen-7', 1,
 N'Ryzen 7 7700 · 32GB DDR5 6000 RGB · SSD Gen4 1TB · màn hình 27 inch 2K 144Hz',
 N'Cấu hình chơi game phổ thông và giải trí: CPU AMD Ryzen 7 7700 (8 nhân), 32GB RAM DDR5 6000 có đèn RGB, SSD NVMe Gen4 1TB và màn hình 27 inch 2K 144Hz. Nền tảng AM5 còn dài hạn, sẵn sàng nâng cấp card đồ hoạ rời khi cửa hàng có hàng. Bảo hành theo từng linh kiện.',
 '/images/builds/pc-tower.svg', 1, 30),

('PC-CREATOR-01', N'PC Đồ hoạ - Render (Core i5)', 'pc-do-hoa-render-core-i5', 2,
 N'Core i5-14600K · 32GB DDR5 · 2 ổ NVMe Gen4 · nguồn 850W · màn hình 27 inch 2K',
 N'Bộ máy cho dựng hình - render - thiết kế: CPU Intel Core i5-14600K (14 nhân), 32GB RAM DDR5, hai ổ NVMe Gen4 (1TB cài hệ điều hành + 2TB chứa dự án), nguồn 850W Gold dư công suất để gắn thêm card đồ hoạ rời, màn hình 27 inch 2K IPS màu chuẩn. Bảo hành theo từng linh kiện.',
 '/images/builds/pc-tower.svg', 1, 40),

('PC-ULTRA-01', N'PC cao cấp (Core i9)', 'pc-cao-cap-core-i9', 3,
 N'Core i9-14900K · 64GB DDR5 · NVMe Gen4 2TB + HDD 4TB · nguồn 1000W Platinum · màn hình 4K',
 N'Cấu hình cao cấp cho ảo hoá, dựng phim 4K, chạy nhiều máy ảo hoặc huấn luyện mô hình nhỏ: CPU Intel Core i9-14900K (24 nhân), 64GB RAM DDR5, ổ NVMe Gen4 2TB + HDD 4TB lưu trữ, nguồn 1000W Platinum và màn hình 27 inch 4K USB-C. Linh kiện tuyển chọn, bảo hành theo từng món.',
 '/images/builds/pc-tower.svg', 0, 50);

INSERT INTO dbo.PcBuilds (Code, Name, Slug, Tier, Summary, Description, ImageUrl, IsFeatured, SortOrder)
SELECT v.Code, v.Name, v.Slug, v.Tier, v.Summary, v.Description, v.ImageUrl, v.IsFeatured, v.SortOrder
  FROM @Builds v
 WHERE NOT EXISTS (SELECT 1 FROM dbo.PcBuilds b WHERE b.Code = v.Code);

DECLARE @Items TABLE (
    BuildCode varchar(30), Sku varchar(50), Quantity int, Note nvarchar(200), SortOrder int
);

INSERT @Items (BuildCode, Sku, Quantity, Note, SortOrder) VALUES
('PC-OFFICE-01', 'CPU-INTEL-I3-14100', 1, N'CPU Intel Core i3-14100 — có đồ hoạ tích hợp', 10),
('PC-OFFICE-01', 'MB-GIGA-B760M-DS3H-DDR4', 1, N'Bo mạch chủ LGA1700, DDR4, 2 khe RAM', 20),
('PC-OFFICE-01', 'RAM-COR-VG-16-D4-3200', 1, N'RAM 16GB (2 thanh x 8GB) DDR4 3200', 30),
('PC-OFFICE-01', 'SSD-KG-NV2-500GB', 1, N'Ổ cứng SSD NVMe Gen4 500GB', 40),
('PC-OFFICE-01', 'PSU-COR-CV550', 1, N'Nguồn 550W 80 Plus Bronze', 50),
('PC-OFFICE-01', 'MON-LG-24MP400', 1, N'Màn hình 24 inch Full HD IPS', 60),

('PC-OFFICE-PLUS-02', 'CPU-AMD-R5-7600', 1, N'CPU AMD Ryzen 5 7600 — có đồ hoạ tích hợp', 10),
('PC-OFFICE-PLUS-02', 'MB-ASUS-TUF-B650M-PLUS-WIFI', 1, N'Bo mạch chủ AM5, DDR5, WiFi 6', 20),
('PC-OFFICE-PLUS-02', 'RAM-KG-FB-16-D5-5600', 1, N'RAM 16GB (2 thanh x 8GB) DDR5 5600', 30),
('PC-OFFICE-PLUS-02', 'SSD-CRU-P3PLUS-1TB', 1, N'Ổ cứng SSD NVMe Gen4 1TB', 40),
('PC-OFFICE-PLUS-02', 'PSU-CM-MWE650-V2', 1, N'Nguồn 650W 80 Plus Bronze', 50),
('PC-OFFICE-PLUS-02', 'MON-DELL-U2422H', 1, N'Màn hình 24 inch IPS có USB-C', 60);

INSERT @Items (BuildCode, Sku, Quantity, Note, SortOrder) VALUES
('PC-GAMING-01', 'CPU-AMD-R7-7700', 1, N'CPU AMD Ryzen 7 7700 — có đồ hoạ tích hợp', 10),
('PC-GAMING-01', 'MB-MSI-MAG-B650-TOMAHAWK-WIFI', 1, N'Bo mạch chủ AM5, DDR5, WiFi 6E', 20),
('PC-GAMING-01', 'RAM-GSK-TZ5N-32-D5-6000', 1, N'RAM 32GB (2 thanh x 16GB) DDR5 6000 RGB', 30),
('PC-GAMING-01', 'SSD-WD-SN850X-1TB', 1, N'Ổ cứng SSD NVMe Gen4 1TB', 40),
('PC-GAMING-01', 'PSU-SEA-FOCUS-GX750', 1, N'Nguồn 750W 80 Plus Gold', 50),
('PC-GAMING-01', 'MON-LG-27GN800', 1, N'Màn hình 27 inch 2K 144Hz', 60);

INSERT @Items (BuildCode, Sku, Quantity, Note, SortOrder) VALUES
('PC-CREATOR-01', 'CPU-INTEL-I5-14600K', 1, N'CPU Intel Core i5-14600K — 14 nhân', 10),
('PC-CREATOR-01', 'MB-ASUS-TUF-B760-PLUS-WIFI-D5', 1, N'Bo mạch chủ LGA1700, DDR5, WiFi 6', 20),
('PC-CREATOR-01', 'RAM-KG-FB-32-D5-6000', 1, N'RAM 32GB (2 thanh x 16GB) DDR5 6000', 30),
('PC-CREATOR-01', 'SSD-SAM-990PRO-1TB', 1, N'Ổ cài hệ điều hành: SSD NVMe Gen4 1TB', 40),
('PC-CREATOR-01', 'SSD-SAM-990PRO-2TB', 1, N'Ổ chứa dự án: SSD NVMe Gen4 2TB', 50),
('PC-CREATOR-01', 'PSU-SEA-PRIME-GX850', 1, N'Nguồn 850W 80 Plus Gold (dư cho nâng cấp VGA)', 60),
('PC-CREATOR-01', 'MON-DELL-P2723D', 1, N'Màn hình 27 inch 2K IPS', 70),

('PC-ULTRA-01', 'CPU-INTEL-I9-14900K', 1, N'CPU Intel Core i9-14900K — 24 nhân', 10),
('PC-ULTRA-01', 'MB-ASUS-ROG-MAXIMUS-Z790-HERO', 1, N'Bo mạch chủ cao cấp Z790, DDR5', 20),
('PC-ULTRA-01', 'RAM-GSK-RJS5-64-D5-6000', 1, N'RAM 64GB (2 thanh x 32GB) DDR5 6000', 30),
('PC-ULTRA-01', 'SSD-SAM-990PRO-2TB', 1, N'Ổ cài hệ điều hành: SSD NVMe Gen4 2TB', 40),
('PC-ULTRA-01', 'HDD-SEA-IRONWOLF-4TB', 1, N'Ổ lưu trữ: HDD 4TB', 50),
('PC-ULTRA-01', 'PSU-COR-HX1000I', 1, N'Nguồn 1000W 80 Plus Platinum', 60),
('PC-ULTRA-01', 'MON-LG-27UP850', 1, N'Màn hình 27 inch 4K IPS có USB-C', 70);

/* Gắn linh kiện vào bộ theo SKU (bỏ qua nếu bộ/SKU không tồn tại hoặc đã có trong bộ). */
INSERT INTO dbo.PcBuildItems (PcBuildId, ProductId, Quantity, Note, SortOrder)
SELECT b.Id, p.Id, i.Quantity, i.Note, i.SortOrder
  FROM @Items i
  JOIN dbo.PcBuilds b ON b.Code = i.BuildCode
  JOIN dbo.Products p ON p.Sku = i.Sku
 WHERE NOT EXISTS (SELECT 1 FROM dbo.PcBuildItems x WHERE x.PcBuildId = b.Id AND x.ProductId = p.Id);
GO

/* =============================================================================
   BỔ SUNG — TAB "TỰ BUILD PC" (khách tự chọn từng linh kiện theo thứ tự)
   ============================================================================= */

/* GET /api/v1/pc-builds/builder/catalog — danh mục linh kiện cho tab "TỰ BUILD PC".
   Trả MỘT result set gồm 6 nhóm theo đúng THỨ TỰ CHỌN của khách (StepOrder 1..6:
   Bo mạch chủ → CPU → RAM → Ổ cứng → Nguồn → Màn hình), chỉ gồm sản phẩm ĐANG BÁN.

   Mỗi dòng có: giá theo nhóm khách/khuyến mãi (fn_ProductPrice), tồn khả dụng, ảnh chính và
   `SpecsJson` — thông số CẦN THIẾT để gợi ý tương thích (camelCase, khác nhau theo nhóm):
     • MAINBOARD: socket · memoryType · ramSlots · m2Slots · sataPorts · chipset · formFactor
     • CPU      : socket · tdpW · cores · hasIntegratedGpu
     • RAM      : memoryType · modulesPerKit · capacityGbPerKit · busMhz · formFactor
     • STORAGE  : storageType · interface · formFactor · capacityGb
     • PSU      : powerW · efficiencyRating · modularType
     • MONITOR  : sizeInch · resolution · refreshRateHz · panelType
   FE tự lọc theo lựa chọn của khách (4 quy tắc như usp_Compat_Check: socket · chuẩn RAM · số khe RAM ·
   công suất nguồn) nên đổi lựa chọn là gợi ý cập nhật NGAY, không phải gọi lại API. */
CREATE OR ALTER PROCEDURE dbo.usp_PcBuild_BuilderCatalog
    @CustomerGroupId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 1 AS StepOrder, c.Id AS CategoryId, c.Code AS CategoryCode, c.Name AS CategoryName, c.SpecType,
           p.Id AS ProductId, p.Sku, p.Name, p.Slug, b.Name AS BrandName,
           pr.BasePrice, pr.FinalUnitPrice AS Price, st.AvailableQty, img.Url AS ImageUrl,
           p.WarrantyMonths, p.RequiresSerial,
           (SELECT mb.Socket AS socket, mb.MemoryType AS memoryType, mb.RamSlots AS ramSlots,
                   mb.M2Slots AS m2Slots, mb.SataPorts AS sataPorts, mb.Chipset AS chipset,
                   mb.FormFactor AS formFactor
              FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS SpecsJson
      FROM dbo.Products p
      JOIN dbo.Categories c ON c.Id = p.CategoryId AND c.Code = 'MAINBOARD'
      JOIN dbo.Brands b ON b.Id = p.BrandId
      JOIN dbo.Specs_Mainboard mb ON mb.ProductId = p.Id
      CROSS APPLY dbo.fn_ProductPrice(p.Id, @CustomerGroupId, 1, SYSUTCDATETIME()) pr
      CROSS APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS AvailableQty FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = p.Id) st
      OUTER APPLY (SELECT TOP 1 i.Url FROM dbo.ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) img
     WHERE p.Status = 1

    UNION ALL
    SELECT 2, c.Id, c.Code, c.Name, c.SpecType,
           p.Id, p.Sku, p.Name, p.Slug, b.Name,
           pr.BasePrice, pr.FinalUnitPrice, st.AvailableQty, img.Url,
           p.WarrantyMonths, p.RequiresSerial,
           (SELECT cpu.Socket AS socket, cpu.TdpW AS tdpW, cpu.Cores AS cores,
                   cpu.HasIntegratedGpu AS hasIntegratedGpu
              FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM dbo.Products p
      JOIN dbo.Categories c ON c.Id = p.CategoryId AND c.Code = 'CPU'
      JOIN dbo.Brands b ON b.Id = p.BrandId
      JOIN dbo.Specs_CPU cpu ON cpu.ProductId = p.Id
      CROSS APPLY dbo.fn_ProductPrice(p.Id, @CustomerGroupId, 1, SYSUTCDATETIME()) pr
      CROSS APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS AvailableQty FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = p.Id) st
      OUTER APPLY (SELECT TOP 1 i.Url FROM dbo.ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) img
     WHERE p.Status = 1

    UNION ALL
    SELECT 3, c.Id, c.Code, c.Name, c.SpecType,
           p.Id, p.Sku, p.Name, p.Slug, b.Name,
           pr.BasePrice, pr.FinalUnitPrice, st.AvailableQty, img.Url,
           p.WarrantyMonths, p.RequiresSerial,
           (SELECT r.MemoryType AS memoryType, r.ModulesPerKit AS modulesPerKit,
                   r.CapacityGbPerKit AS capacityGbPerKit, r.BusMhz AS busMhz, r.FormFactor AS formFactor
              FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM dbo.Products p
      JOIN dbo.Categories c ON c.Id = p.CategoryId AND c.Code = 'RAM'
      JOIN dbo.Brands b ON b.Id = p.BrandId
      JOIN dbo.Specs_RAM r ON r.ProductId = p.Id
      CROSS APPLY dbo.fn_ProductPrice(p.Id, @CustomerGroupId, 1, SYSUTCDATETIME()) pr
      CROSS APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS AvailableQty FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = p.Id) st
      OUTER APPLY (SELECT TOP 1 i.Url FROM dbo.ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) img
     WHERE p.Status = 1

    UNION ALL
    SELECT 4, c.Id, c.Code, c.Name, c.SpecType,
           p.Id, p.Sku, p.Name, p.Slug, b.Name,
           pr.BasePrice, pr.FinalUnitPrice, st.AvailableQty, img.Url,
           p.WarrantyMonths, p.RequiresSerial,
           (SELECT sd.StorageType AS storageType, sd.Interface AS interface, sd.FormFactor AS formFactor,
                   sd.CapacityGb AS capacityGb
              FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM dbo.Products p
      JOIN dbo.Categories c ON c.Id = p.CategoryId AND c.Code = 'STORAGE'
      JOIN dbo.Brands b ON b.Id = p.BrandId
      JOIN dbo.Specs_Storage sd ON sd.ProductId = p.Id
      CROSS APPLY dbo.fn_ProductPrice(p.Id, @CustomerGroupId, 1, SYSUTCDATETIME()) pr
      CROSS APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS AvailableQty FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = p.Id) st
      OUTER APPLY (SELECT TOP 1 i.Url FROM dbo.ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) img
     WHERE p.Status = 1

    UNION ALL
    SELECT 5, c.Id, c.Code, c.Name, c.SpecType,
           p.Id, p.Sku, p.Name, p.Slug, b.Name,
           pr.BasePrice, pr.FinalUnitPrice, st.AvailableQty, img.Url,
           p.WarrantyMonths, p.RequiresSerial,
           (SELECT psu.PowerW AS powerW, psu.EfficiencyRating AS efficiencyRating,
                   psu.ModularType AS modularType
              FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM dbo.Products p
      JOIN dbo.Categories c ON c.Id = p.CategoryId AND c.Code = 'PSU'
      JOIN dbo.Brands b ON b.Id = p.BrandId
      JOIN dbo.Specs_PSU psu ON psu.ProductId = p.Id
      CROSS APPLY dbo.fn_ProductPrice(p.Id, @CustomerGroupId, 1, SYSUTCDATETIME()) pr
      CROSS APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS AvailableQty FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = p.Id) st
      OUTER APPLY (SELECT TOP 1 i.Url FROM dbo.ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) img
     WHERE p.Status = 1

    UNION ALL
    SELECT 6, c.Id, c.Code, c.Name, c.SpecType,
           p.Id, p.Sku, p.Name, p.Slug, b.Name,
           pr.BasePrice, pr.FinalUnitPrice, st.AvailableQty, img.Url,
           p.WarrantyMonths, p.RequiresSerial,
           (SELECT m.SizeInch AS sizeInch, m.Resolution AS resolution,
                   m.RefreshRateHz AS refreshRateHz, m.PanelType AS panelType
              FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM dbo.Products p
      JOIN dbo.Categories c ON c.Id = p.CategoryId AND c.Code = 'MONITOR'
      JOIN dbo.Brands b ON b.Id = p.BrandId
      JOIN dbo.Specs_Monitor m ON m.ProductId = p.Id
      CROSS APPLY dbo.fn_ProductPrice(p.Id, @CustomerGroupId, 1, SYSUTCDATETIME()) pr
      CROSS APPLY (SELECT ISNULL(SUM(sl.AvailableQty), 0) AS AvailableQty FROM dbo.StockLevels sl JOIN dbo.Branches br ON br.Id = sl.BranchId AND br.IsActive = 1 WHERE sl.ProductId = p.Id) st
      OUTER APPLY (SELECT TOP 1 i.Url FROM dbo.ProductImages i WHERE i.ProductId = p.Id ORDER BY i.IsPrimary DESC, i.SortOrder) img
     WHERE p.Status = 1
     ORDER BY StepOrder, Price, Name;
END
GO
