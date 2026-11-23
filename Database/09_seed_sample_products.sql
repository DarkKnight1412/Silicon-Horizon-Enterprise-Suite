/* =============================================================================
   09 — SEED DỮ LIỆU MẪU SẢN PHẨM (môi trường dev — DB BRD_DEV)
   -----------------------------------------------------------------------------
   Bơm 60 sản phẩm mẫu = 10 sản phẩm × 6 danh mục:
   CPU · RAM · STORAGE · MONITOR · MAINBOARD · PSU (trang bán hàng công khai /shop).

   Dựa đúng schema thật (table_and_data.sql) và điều kiện lọc của dbo.usp_Product_Search:
     • p.Status = 1 (Đang bán)          • JOIN dbo.Brands (bắt buộc có thương hiệu)
     • CROSS APPLY dbo.fn_ProductPrice  • SUM(StockLevels.AvailableQty) ở chi nhánh IsActive = 1

   Chạy lại nhiều lần đều an toàn (IF NOT EXISTS theo Sku / Name).
   Chạy:  sqlcmd -S localhost -d BRD_DEV -E -I -f 65001 -i "Database\09_seed_sample_products.sql"
   ============================================================================= */
SET NOCOUNT ON;
GO

/* ── PHẦN 1/6 — THƯƠNG HIỆU + SẢN PHẨM: CPU (10) & RAM (10) ── */
INSERT INTO dbo.Brands (Name)
SELECT v.Name
FROM (VALUES
    (N'AMD'),(N'Intel'),(N'ASUS'),(N'MSI'),(N'Gigabyte'),(N'Corsair'),
    (N'Kingston'),(N'Samsung'),(N'Crucial'),(N'G.Skill'),(N'Western Digital'),
    (N'Seagate'),(N'LG'),(N'Dell'),(N'Acer'),(N'AOC'),(N'Cooler Master'),
    (N'Seasonic'),(N'Thermaltake')
) v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Brands b WHERE b.Name = v.Name);
GO

INSERT INTO dbo.Products
    (Sku, Name, Slug, BrandId, CategoryId, ShortDescription, ListPrice, RequiresSerial, MinStockDefault, WarrantyMonths, Status)
SELECT v.Sku, v.Name, LOWER(REPLACE(v.Name,' ','-')), b.Id, c.Id,
       v.Name + N' — hàng chính hãng, bảo hành ' + CAST(v.WarrantyMonths AS nvarchar(4)) + N' tháng.',
       v.ListPrice, 1, 5, v.WarrantyMonths, 1
FROM (VALUES
    ('CPU-AMD-R5-7600','AMD Ryzen 5 7600','AMD','CPU',5190000,36),
    ('CPU-AMD-R5-7600X','AMD Ryzen 5 7600X','AMD','CPU',5690000,36),
    ('CPU-AMD-R7-7700','AMD Ryzen 7 7700','AMD','CPU',7290000,36),
    ('CPU-AMD-R7-7800X3D','AMD Ryzen 7 7800X3D','AMD','CPU',9490000,36),
    ('CPU-AMD-R9-7950X','AMD Ryzen 9 7950X','AMD','CPU',14990000,36),
    ('CPU-INTEL-I3-14100','Intel Core i3-14100','Intel','CPU',3290000,36),
    ('CPU-INTEL-I5-14400F','Intel Core i5-14400F','Intel','CPU',4590000,36),
    ('CPU-INTEL-I5-14600K','Intel Core i5-14600K','Intel','CPU',7190000,36),
    ('CPU-INTEL-I7-14700K','Intel Core i7-14700K','Intel','CPU',10490000,36),
    ('CPU-INTEL-I9-14900K','Intel Core i9-14900K','Intel','CPU',14490000,36),
    ('RAM-KG-FB-16-D5-5600','Kingston Fury Beast 16GB Kit 2x8 DDR5 5600','Kingston','RAM',1490000,36),
    ('RAM-KG-FB-32-D5-6000','Kingston Fury Beast 32GB Kit 2x16 DDR5 6000','Kingston','RAM',2790000,36),
    ('RAM-COR-VG-32-D5-6000-RGB','Corsair Vengeance RGB 32GB Kit 2x16 DDR5 6000','Corsair','RAM',3190000,36),
    ('RAM-GSK-TZ5N-32-D5-6000','G.Skill Trident Z5 Neo 32GB Kit 2x16 DDR5 6000','G.Skill','RAM',3590000,36),
    ('RAM-COR-VG-16-D4-3200','Corsair Vengeance 16GB Kit 2x8 DDR4 3200','Corsair','RAM',1250000,36),
    ('RAM-KG-FB-32-D4-3600','Kingston Fury Beast 32GB Kit 2x16 DDR4 3600','Kingston','RAM',2190000,36),
    ('RAM-CRU-8-D4-3200-SODIMM','Crucial 8GB DDR4 3200 SODIMM','Crucial','RAM',590000,36),
    ('RAM-KG-16-D4-3200-SODIMM','Kingston 16GB DDR4 3200 SODIMM Laptop','Kingston','RAM',1090000,36),
    ('RAM-CRU-16-D5-4800-SODIMM','Crucial 16GB DDR5 4800 SODIMM Laptop','Crucial','RAM',1390000,36),
    ('RAM-GSK-RJS5-64-D5-6000','G.Skill Ripjaws S5 64GB Kit 2x32 DDR5 6000','G.Skill','RAM',5990000,36)
) v(Sku, Name, Brand, Category, ListPrice, WarrantyMonths)
JOIN dbo.Brands b ON b.Name = v.Brand
JOIN dbo.Categories c ON c.Code = v.Category
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Sku = v.Sku);
GO
/* ── PHẦN 2/6 — SẢN PHẨM: STORAGE (10) & MONITOR (10) ── */
INSERT INTO dbo.Products
    (Sku, Name, Slug, BrandId, CategoryId, ShortDescription, ListPrice, RequiresSerial, MinStockDefault, WarrantyMonths, Status)
SELECT v.Sku, v.Name, LOWER(REPLACE(v.Name,' ','-')), b.Id, c.Id,
       v.Name + N' — hàng chính hãng, bảo hành ' + CAST(v.WarrantyMonths AS nvarchar(4)) + N' tháng.',
       v.ListPrice, 1, 5, v.WarrantyMonths, 1
FROM (VALUES
    ('SSD-SAM-990PRO-1TB','Samsung 990 PRO 1TB NVMe PCIe Gen4','Samsung','STORAGE',2890000,60),
    ('SSD-SAM-990PRO-2TB','Samsung 990 PRO 2TB NVMe PCIe Gen4','Samsung','STORAGE',5290000,60),
    ('SSD-WD-SN850X-1TB','WD Black SN850X 1TB NVMe Gen4','Western Digital','STORAGE',2690000,60),
    ('SSD-KG-NV2-500GB','Kingston NV2 500GB NVMe Gen4','Kingston','STORAGE',890000,60),
    ('SSD-CRU-P3PLUS-1TB','Crucial P3 Plus 1TB NVMe Gen4','Crucial','STORAGE',1690000,60),
    ('SSD-SAM-870EVO-500GB','Samsung 870 EVO 500GB SATA 2.5in','Samsung','STORAGE',1190000,60),
    ('SSD-SAM-870EVO-1TB','Samsung 870 EVO 1TB SATA 2.5in','Samsung','STORAGE',2190000,60),
    ('HDD-SEA-BARRA-2TB','Seagate Barracuda 2TB 7200rpm','Seagate','STORAGE',1390000,60),
    ('HDD-WD-BLUE-1TB','WD Blue 1TB 7200rpm','Western Digital','STORAGE',890000,60),
    ('HDD-SEA-IRONWOLF-4TB','Seagate IronWolf 4TB NAS','Seagate','STORAGE',3190000,60),
    ('MON-DELL-U2422H','Dell UltraSharp U2422H 24in FHD IPS','Dell','MONITOR',4290000,36),
    ('MON-LG-24MP400','LG 24MP400 24in FHD IPS','LG','MONITOR',2590000,36),
    ('MON-SAM-ODYG5-27','Samsung Odyssey G5 27in 2K VA 144Hz','Samsung','MONITOR',6490000,36),
    ('MON-LG-27GN800','LG UltraGear 27GN800 27in 2K IPS 144Hz','LG','MONITOR',7290000,36),
    ('MON-ACER-VG270','Acer Nitro VG270 27in FHD IPS 165Hz','Acer','MONITOR',4390000,36),
    ('MON-ASUS-VG27AQ','ASUS TUF Gaming VG27AQ 27in 2K IPS 165Hz','ASUS','MONITOR',7990000,36),
    ('MON-DELL-P2723D','Dell P2723D 27in 2K IPS','Dell','MONITOR',6190000,36),
    ('MON-LG-27UP850','LG 27UP850 27in 4K IPS USB-C','LG','MONITOR',9990000,36),
    ('MON-SAM-ODYG7-32','Samsung Odyssey G7 32in 2K VA 240Hz','Samsung','MONITOR',12990000,36),
    ('MON-AOC-24G2SP','AOC 24G2SP 24in FHD IPS 165Hz','AOC','MONITOR',3390000,36)
) v(Sku, Name, Brand, Category, ListPrice, WarrantyMonths)
JOIN dbo.Brands b ON b.Name = v.Brand
JOIN dbo.Categories c ON c.Code = v.Category
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Sku = v.Sku);
GO
/* ── PHẦN 3/6 — SẢN PHẨM: MAINBOARD (10) & PSU (10) ── */
INSERT INTO dbo.Products
    (Sku, Name, Slug, BrandId, CategoryId, ShortDescription, ListPrice, RequiresSerial, MinStockDefault, WarrantyMonths, Status)
SELECT v.Sku, v.Name, LOWER(REPLACE(v.Name,' ','-')), b.Id, c.Id,
       v.Name + N' — hàng chính hãng, bảo hành ' + CAST(v.WarrantyMonths AS nvarchar(4)) + N' tháng.',
       v.ListPrice, 1, 5, v.WarrantyMonths, 1
FROM (VALUES
    ('MB-ASUS-TUF-B650M-PLUS-WIFI','ASUS TUF Gaming B650M-PLUS WIFI','ASUS','MAINBOARD',4790000,36),
    ('MB-MSI-MAG-B650-TOMAHAWK-WIFI','MSI MAG B650 Tomahawk WIFI','MSI','MAINBOARD',5990000,36),
    ('MB-GIGA-B650-AORUS-ELITE-AX','Gigabyte B650 AORUS Elite AX','Gigabyte','MAINBOARD',6290000,36),
    ('MB-ASUS-ROG-X670E-A','ASUS ROG Strix X670E-A Gaming','ASUS','MAINBOARD',9490000,36),
    ('MB-ASUS-PRIME-B760M-A-WIFI-D4','ASUS Prime B760M-A WIFI D4','ASUS','MAINBOARD',3690000,36),
    ('MB-MSI-PRO-B760M-A-DDR4','MSI PRO B760M-A DDR4','MSI','MAINBOARD',3190000,36),
    ('MB-GIGA-B760M-DS3H-DDR4','Gigabyte B760M DS3H DDR4','Gigabyte','MAINBOARD',2790000,36),
    ('MB-ASUS-TUF-B760-PLUS-WIFI-D5','ASUS TUF Gaming B760-PLUS WIFI D5','ASUS','MAINBOARD',5190000,36),
    ('MB-MSI-MAG-Z790-TOMAHAWK-WIFI','MSI MAG Z790 Tomahawk WIFI','MSI','MAINBOARD',7990000,36),
    ('MB-ASUS-ROG-MAXIMUS-Z790-HERO','ASUS ROG Maximus Z790 Hero','ASUS','MAINBOARD',15990000,36),
    ('PSU-COR-CV550','Corsair CV550 550W 80 Plus Bronze','Corsair','PSU',1190000,60),
    ('PSU-COR-CX650','Corsair CX650 650W 80 Plus Bronze','Corsair','PSU',1590000,60),
    ('PSU-CM-MWE650-V2','Cooler Master MWE 650 V2 Bronze','Cooler Master','PSU',1490000,60),
    ('PSU-SEA-FOCUS-GX750','Seasonic Focus GX-750 750W 80 Plus Gold','Seasonic','PSU',2690000,60),
    ('PSU-COR-RM750E','Corsair RM750e 750W 80 Plus Gold','Corsair','PSU',2890000,60),
    ('PSU-MSI-MAG-A650BN','MSI MAG A650BN 650W 80 Plus Bronze','MSI','PSU',1390000,60),
    ('PSU-SEA-PRIME-GX850','Seasonic Prime GX-850 850W 80 Plus Gold','Seasonic','PSU',3690000,60),
    ('PSU-COR-HX1000I','Corsair HX1000i 1000W 80 Plus Platinum','Corsair','PSU',6290000,60),
    ('PSU-CM-MWE850-GOLD','Cooler Master MWE Gold 850 V2','Cooler Master','PSU',2890000,60),
    ('PSU-TT-GF1-1000W','Thermaltake Toughpower GF1 1000W Gold','Thermaltake','PSU',5490000,60)
) v(Sku, Name, Brand, Category, ListPrice, WarrantyMonths)
JOIN dbo.Brands b ON b.Name = v.Brand
JOIN dbo.Categories c ON c.Code = v.Category
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Sku = v.Sku);
GO
/* ── PHẦN 4/6 — THÔNG SỐ KỸ THUẬT: Specs_CPU · Specs_RAM · Specs_Storage ──
   Mỗi sản phẩm đúng 1 dòng (khoá chính = ProductId). */
INSERT INTO dbo.Specs_CPU (ProductId, Socket, Cores, Threads, BaseClockGhz, BoostClockGhz, CacheMb, TdpW, HasCooler, HasIntegratedGpu)
SELECT p.Id, v.Socket, v.Cores, v.Threads, v.BaseGhz, v.BoostGhz, v.CacheMb, v.TdpW, v.HasCooler, v.HasIgpu
FROM (VALUES
    ('CPU-AMD-R5-7600','AM5',6,12,3.80,5.10,32,65,1,1),
    ('CPU-AMD-R5-7600X','AM5',6,12,4.70,5.30,32,105,0,1),
    ('CPU-AMD-R7-7700','AM5',8,16,3.80,5.30,32,65,1,1),
    ('CPU-AMD-R7-7800X3D','AM5',8,16,4.20,5.00,96,120,0,1),
    ('CPU-AMD-R9-7950X','AM5',16,32,4.50,5.70,64,170,0,1),
    ('CPU-INTEL-I3-14100','LGA1700',4,8,3.50,4.70,12,60,1,1),
    ('CPU-INTEL-I5-14400F','LGA1700',10,16,2.50,4.70,20,65,1,0),
    ('CPU-INTEL-I5-14600K','LGA1700',14,20,3.50,5.30,24,125,0,1),
    ('CPU-INTEL-I7-14700K','LGA1700',20,28,3.40,5.60,33,125,0,1),
    ('CPU-INTEL-I9-14900K','LGA1700',24,32,3.20,6.00,36,125,0,1)
) v(Sku, Socket, Cores, Threads, BaseGhz, BoostGhz, CacheMb, TdpW, HasCooler, HasIgpu)
JOIN dbo.Products p ON p.Sku = v.Sku
WHERE NOT EXISTS (SELECT 1 FROM dbo.Specs_CPU x WHERE x.ProductId = p.Id);
GO

INSERT INTO dbo.Specs_RAM (ProductId, MemoryType, CapacityGbPerKit, ModulesPerKit, BusMhz, CasLatency, FormFactor, HasRgb)
SELECT p.Id, v.MemoryType, v.CapacityGbPerKit, v.ModulesPerKit, v.BusMhz, v.CasLatency, v.FormFactor, v.HasRgb
FROM (VALUES
    ('RAM-KG-FB-16-D5-5600','DDR5',16,2,5600,36,'DIMM',0),
    ('RAM-KG-FB-32-D5-6000','DDR5',32,2,6000,36,'DIMM',0),
    ('RAM-COR-VG-32-D5-6000-RGB','DDR5',32,2,6000,36,'DIMM',1),
    ('RAM-GSK-TZ5N-32-D5-6000','DDR5',32,2,6000,30,'DIMM',1),
    ('RAM-COR-VG-16-D4-3200','DDR4',16,2,3200,16,'DIMM',0),
    ('RAM-KG-FB-32-D4-3600','DDR4',32,2,3600,18,'DIMM',0),
    ('RAM-CRU-8-D4-3200-SODIMM','DDR4',8,1,3200,22,'SODIMM',0),
    ('RAM-KG-16-D4-3200-SODIMM','DDR4',16,1,3200,22,'SODIMM',0),
    ('RAM-CRU-16-D5-4800-SODIMM','DDR5',16,1,4800,40,'SODIMM',0),
    ('RAM-GSK-RJS5-64-D5-6000','DDR5',64,2,6000,30,'DIMM',0)
) v(Sku, MemoryType, CapacityGbPerKit, ModulesPerKit, BusMhz, CasLatency, FormFactor, HasRgb)
JOIN dbo.Products p ON p.Sku = v.Sku
WHERE NOT EXISTS (SELECT 1 FROM dbo.Specs_RAM x WHERE x.ProductId = p.Id);
GO

INSERT INTO dbo.Specs_Storage (ProductId, StorageType, [Interface], FormFactor, CapacityGb, ReadMbps, WriteMbps)
SELECT p.Id, v.StorageType, v.Ifc, v.FormFactor, v.CapacityGb, v.ReadMbps, v.WriteMbps
FROM (VALUES
    ('SSD-SAM-990PRO-1TB','SSD NVMe','PCIe Gen4 x4','M.2 2280',1000,7450,6900),
    ('SSD-SAM-990PRO-2TB','SSD NVMe','PCIe Gen4 x4','M.2 2280',2000,7450,6900),
    ('SSD-WD-SN850X-1TB','SSD NVMe','PCIe Gen4 x4','M.2 2280',1000,7300,6300),
    ('SSD-KG-NV2-500GB','SSD NVMe','PCIe Gen4 x4','M.2 2280',500,3500,2100),
    ('SSD-CRU-P3PLUS-1TB','SSD NVMe','PCIe Gen4 x4','M.2 2280',1000,5000,3600),
    ('SSD-SAM-870EVO-500GB','SSD SATA','SATA III','2.5in',500,560,530),
    ('SSD-SAM-870EVO-1TB','SSD SATA','SATA III','2.5in',1000,560,530),
    ('HDD-SEA-BARRA-2TB','HDD','SATA III','3.5in',2000,190,190),
    ('HDD-WD-BLUE-1TB','HDD','SATA III','3.5in',1000,150,150),
    ('HDD-SEA-IRONWOLF-4TB','HDD','SATA III','3.5in',4000,180,180)
) v(Sku, StorageType, Ifc, FormFactor, CapacityGb, ReadMbps, WriteMbps)
JOIN dbo.Products p ON p.Sku = v.Sku
WHERE NOT EXISTS (SELECT 1 FROM dbo.Specs_Storage x WHERE x.ProductId = p.Id);
GO
/* ── PHẦN 5/6 — THÔNG SỐ KỸ THUẬT: Specs_Monitor (+ Ports) · Specs_Mainboard · Specs_PSU ── */
INSERT INTO dbo.Specs_Monitor (ProductId, SizeInch, Resolution, PanelType, RefreshRateHz, ResponseTimeMs, SrgbPercent)
SELECT p.Id, v.SizeInch, v.Resolution, v.PanelType, v.RefreshRateHz, v.ResponseTimeMs, v.SrgbPercent
FROM (VALUES
    ('MON-DELL-U2422H',23.8,'FHD','IPS',60,5.0,99),
    ('MON-LG-24MP400',23.8,'FHD','IPS',75,5.0,99),
    ('MON-SAM-ODYG5-27',27.0,'2K','VA',144,1.0,88),
    ('MON-LG-27GN800',27.0,'2K','IPS',144,1.0,99),
    ('MON-ACER-VG270',27.0,'FHD','IPS',165,1.0,99),
    ('MON-ASUS-VG27AQ',27.0,'2K','IPS',165,1.0,99),
    ('MON-DELL-P2723D',27.0,'2K','IPS',60,5.0,99),
    ('MON-LG-27UP850',27.0,'4K','IPS',60,5.0,95),
    ('MON-SAM-ODYG7-32',31.5,'2K','VA',240,1.0,95),
    ('MON-AOC-24G2SP',23.8,'FHD','IPS',165,1.0,99)
) v(Sku, SizeInch, Resolution, PanelType, RefreshRateHz, ResponseTimeMs, SrgbPercent)
JOIN dbo.Products p ON p.Sku = v.Sku
WHERE NOT EXISTS (SELECT 1 FROM dbo.Specs_Monitor x WHERE x.ProductId = p.Id);
GO

INSERT INTO dbo.Specs_Monitor_Ports (ProductId, PortType, Quantity)
SELECT p.Id, v.PortType, v.Quantity
FROM (VALUES
    ('MON-DELL-U2422H','HDMI',1),('MON-DELL-U2422H','DP',1),
    ('MON-LG-24MP400','HDMI',1),
    ('MON-SAM-ODYG5-27','HDMI',2),('MON-SAM-ODYG5-27','DP',1),
    ('MON-LG-27GN800','HDMI',2),('MON-LG-27GN800','DP',1),
    ('MON-ACER-VG270','HDMI',2),('MON-ACER-VG270','DP',1),
    ('MON-ASUS-VG27AQ','HDMI',2),('MON-ASUS-VG27AQ','DP',1),
    ('MON-DELL-P2723D','HDMI',1),('MON-DELL-P2723D','DP',1),
    ('MON-LG-27UP850','HDMI',2),('MON-LG-27UP850','DP',1),('MON-LG-27UP850','TypeC',1),
    ('MON-SAM-ODYG7-32','HDMI',2),('MON-SAM-ODYG7-32','DP',1),
    ('MON-AOC-24G2SP','HDMI',1),('MON-AOC-24G2SP','DP',1)
) v(Sku, PortType, Quantity)
JOIN dbo.Products p ON p.Sku = v.Sku
WHERE NOT EXISTS (SELECT 1 FROM dbo.Specs_Monitor_Ports x WHERE x.ProductId = p.Id);
GO

INSERT INTO dbo.Specs_Mainboard (ProductId, Socket, Chipset, MemoryType, RamSlots, MaxBusMhz, FormFactor, M2Slots, SataPorts)
SELECT p.Id, v.Socket, v.Chipset, v.MemoryType, v.RamSlots, v.MaxBusMhz, v.FormFactor, v.M2Slots, v.SataPorts
FROM (VALUES
    ('MB-ASUS-TUF-B650M-PLUS-WIFI','AM5','B650','DDR5',4,6200,'mATX',2,4),
    ('MB-MSI-MAG-B650-TOMAHAWK-WIFI','AM5','B650','DDR5',4,6400,'ATX',3,6),
    ('MB-GIGA-B650-AORUS-ELITE-AX','AM5','B650','DDR5',4,6400,'ATX',3,4),
    ('MB-ASUS-ROG-X670E-A','AM5','X670E','DDR5',4,6400,'ATX',4,6),
    ('MB-ASUS-PRIME-B760M-A-WIFI-D4','LGA1700','B760','DDR4',4,5333,'mATX',2,4),
    ('MB-MSI-PRO-B760M-A-DDR4','LGA1700','B760','DDR4',4,4800,'mATX',2,4),
    ('MB-GIGA-B760M-DS3H-DDR4','LGA1700','B760','DDR4',2,4800,'mATX',2,4),
    ('MB-ASUS-TUF-B760-PLUS-WIFI-D5','LGA1700','B760','DDR5',4,6200,'ATX',3,4),
    ('MB-MSI-MAG-Z790-TOMAHAWK-WIFI','LGA1700','Z790','DDR5',4,6600,'ATX',4,6),
    ('MB-ASUS-ROG-MAXIMUS-Z790-HERO','LGA1700','Z790','DDR5',4,7600,'ATX',4,6)
) v(Sku, Socket, Chipset, MemoryType, RamSlots, MaxBusMhz, FormFactor, M2Slots, SataPorts)
JOIN dbo.Products p ON p.Sku = v.Sku
WHERE NOT EXISTS (SELECT 1 FROM dbo.Specs_Mainboard x WHERE x.ProductId = p.Id);
GO

INSERT INTO dbo.Specs_PSU (ProductId, PowerW, EfficiencyRating, ModularType)
SELECT p.Id, v.PowerW, v.EfficiencyRating, v.ModularType
FROM (VALUES
    ('PSU-COR-CV550',550,'80 Plus Bronze','Non'),
    ('PSU-COR-CX650',650,'80 Plus Bronze','Non'),
    ('PSU-CM-MWE650-V2',650,'80 Plus Bronze','Non'),
    ('PSU-SEA-FOCUS-GX750',750,'80 Plus Gold','Full'),
    ('PSU-COR-RM750E',750,'80 Plus Gold','Full'),
    ('PSU-MSI-MAG-A650BN',650,'80 Plus Bronze','Non'),
    ('PSU-SEA-PRIME-GX850',850,'80 Plus Gold','Full'),
    ('PSU-COR-HX1000I',1000,'80 Plus Platinum','Full'),
    ('PSU-CM-MWE850-GOLD',850,'80 Plus Gold','Full'),
    ('PSU-TT-GF1-1000W',1000,'80 Plus Gold','Full')
) v(Sku, PowerW, EfficiencyRating, ModularType)
JOIN dbo.Products p ON p.Sku = v.Sku
WHERE NOT EXISTS (SELECT 1 FROM dbo.Specs_PSU x WHERE x.ProductId = p.Id);
GO
/* ── PHẦN 6/6 — ẢNH · BẢNG GIÁ · TỒN KHO · KHUYẾN MÃI + KIỂM TRA ──
   Mọi lệnh đều giới hạn trong nhóm SKU mẫu của script này. */
DECLARE @Sample TABLE (Sku varchar(50) PRIMARY KEY);
INSERT INTO @Sample (Sku)
SELECT Sku FROM dbo.Products
WHERE Sku LIKE 'CPU-%' OR Sku LIKE 'RAM-%' OR Sku LIKE 'SSD-%' OR Sku LIKE 'HDD-%'
   OR Sku LIKE 'MON-%' OR Sku LIKE 'MB-%'  OR Sku LIKE 'PSU-%';

/* Ảnh: dùng ảnh placeholder SVG có sẵn trong frontend/public/images/products/ */
INSERT INTO dbo.ProductImages (ProductId, Url, SortOrder, IsPrimary)
SELECT p.Id, '/images/products/' + LOWER(c.Code) + '.svg', 0, 1
FROM dbo.Products p
JOIN dbo.Categories c ON c.Id = p.CategoryId
JOIN @Sample s ON s.Sku = p.Sku
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductImages i WHERE i.ProductId = p.Id);

/* Bảng giá: RETAIL = giá niêm yết; WHOLESALE = 92% giá niêm yết (làm tròn 1.000đ) */
INSERT INTO dbo.ProductPrices (ProductId, CustomerGroupId, Price)
SELECT p.Id, g.Id, p.ListPrice
FROM dbo.Products p
JOIN @Sample s ON s.Sku = p.Sku
JOIN dbo.CustomerGroups g ON g.Code = 'RETAIL'
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductPrices pp WHERE pp.ProductId = p.Id AND pp.CustomerGroupId = g.Id);

INSERT INTO dbo.ProductPrices (ProductId, CustomerGroupId, Price)
SELECT p.Id, g.Id, CAST(ROUND(p.ListPrice * 0.92, -3) AS decimal(18,0))
FROM dbo.Products p
JOIN @Sample s ON s.Sku = p.Sku
JOIN dbo.CustomerGroups g ON g.Code = 'WHOLESALE'
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductPrices pp WHERE pp.ProductId = p.Id AND pp.CustomerGroupId = g.Id);

/* Tồn kho tại chi nhánh đang hoạt động — tạo đa dạng trạng thái Còn hàng / Sắp hết / Hết hàng */
INSERT INTO dbo.StockLevels (BranchId, ProductId, PhysicalQty, ReservedQty, UpdatedAt)
SELECT b.Id, p.Id,
       CASE WHEN p.Id % 13 = 0 THEN 0
            WHEN p.Id % 7  = 0 THEN 3
            ELSE 6 + (p.Id % 20) END,
       0, SYSUTCDATETIME()
FROM dbo.Products p
JOIN @Sample s ON s.Sku = p.Sku
JOIN dbo.Branches b ON b.Code = 'CN01' AND b.IsActive = 1
WHERE NOT EXISTS (SELECT 1 FROM dbo.StockLevels sl WHERE sl.BranchId = b.Id AND sl.ProductId = p.Id);

/* Khuyến mãi theo danh mục (để thẻ hiện nhãn "Khuyến mãi" và sắp xếp theo giảm giá) */
INSERT INTO dbo.Promotions (Name, ScopeType, ScopeId, DiscountType, DiscountValue, StartAt, EndAt, IsActive)
SELECT v.Name, 2, c.Id, 0, v.Pct, SYSUTCDATETIME(), DATEADD(DAY, 30, SYSUTCDATETIME()), 1
FROM (VALUES
    (N'Giảm giá CPU -10%', 'CPU', 10.00),
    (N'Giảm giá Màn hình -8%', 'MONITOR', 8.00)
) v(Name, Cat, Pct)
JOIN dbo.Categories c ON c.Code = v.Cat
WHERE NOT EXISTS (SELECT 1 FROM dbo.Promotions pm WHERE pm.Name = v.Name);
GO

PRINT N'--- KIỂM TRA KẾT QUẢ ---';
SELECT (SELECT COUNT(*) FROM dbo.Brands)        AS SoBrands,
       (SELECT COUNT(*) FROM dbo.Products WHERE Status = 1) AS SoSanPhamDangBan,
       (SELECT COUNT(*) FROM dbo.ProductPrices) AS SoDongBangGia,
       (SELECT COUNT(*) FROM dbo.StockLevels)   AS SoDongTonKho,
       (SELECT COUNT(*) FROM dbo.ProductImages) AS SoAnh;

SELECT c.Code AS DanhMuc, COUNT(*) AS SoSanPham,
       SUM(CASE WHEN ISNULL(sl.AvailableQty, 0) > 0 THEN 1 ELSE 0 END) AS ConHang
FROM dbo.Products p
JOIN dbo.Categories c ON c.Id = p.CategoryId
LEFT JOIN dbo.StockLevels sl ON sl.ProductId = p.Id
WHERE p.Status = 1
GROUP BY c.Code
ORDER BY c.Code;
GO
