SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- bắt buộc cho filtered index và computed column
SET ANSI_NULLS ON;

BEGIN TRY
BEGIN TRANSACTION;

/* =============================================================================
   1. MODULE CHUNG: người dùng, phân quyền, chi nhánh, cấu hình
   ============================================================================= */

CREATE TABLE dbo.Users (
    Id            bigint IDENTITY(1,1) NOT NULL,
    Username      varchar(50)   NOT NULL,
    Email         varchar(150)  NULL,
    Phone         varchar(20)   NULL,
    PasswordHash  varchar(255)  NOT NULL,
    UserType      tinyint       NOT NULL,                 -- 0 Customer, 1 Employee
    IsActive      bit           NOT NULL DEFAULT 1,
    LastLoginAt   datetime2(0)  NULL,
    CreatedAt     datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy     bigint        NULL,
    UpdatedAt     datetime2(0)  NULL,
    UpdatedBy     bigint        NULL,
    CONSTRAINT PK_Users PRIMARY KEY (Id),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT CK_Users_UserType CHECK (UserType IN (0, 1))
);

CREATE TABLE dbo.Roles (
    Id    int IDENTITY(1,1) NOT NULL,
    Code  varchar(50)   NOT NULL,        -- Customer, Cashier, TechAdvisor, Storekeeper, Manager, Admin
    Name  nvarchar(100) NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (Id),
    CONSTRAINT UQ_Roles_Code UNIQUE (Code)
);

CREATE TABLE dbo.Permissions (
    Id    int IDENTITY(1,1) NOT NULL,
    Code  varchar(100) NOT NULL,         -- ví dụ: order.cancel.approve, stock.adjust.approve
    CONSTRAINT PK_Permissions PRIMARY KEY (Id),
    CONSTRAINT UQ_Permissions_Code UNIQUE (Code)
);

CREATE TABLE dbo.UserRoles (
    UserId bigint NOT NULL,
    RoleId int    NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id)
);

CREATE TABLE dbo.RolePermissions (
    RoleId       int NOT NULL,
    PermissionId int NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id),
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (Id)
);

CREATE TABLE dbo.Branches (
    Id         int IDENTITY(1,1) NOT NULL,
    Code       varchar(20)   NOT NULL,
    Name       nvarchar(150) NOT NULL,
    Address    nvarchar(300) NOT NULL,
    Phone      varchar(20)   NULL,
    IsActive   bit           NOT NULL DEFAULT 1,
    CreatedAt  datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  bigint        NULL,
    UpdatedAt  datetime2(0)  NULL,
    UpdatedBy  bigint        NULL,
    CONSTRAINT PK_Branches PRIMARY KEY (Id),
    CONSTRAINT UQ_Branches_Code UNIQUE (Code)
);

CREATE TABLE dbo.SystemSettings (
    [Key]       varchar(100)  NOT NULL,
    [Value]     nvarchar(500) NOT NULL,
    Description nvarchar(300) NULL,
    UpdatedAt   datetime2(0)  NULL,
    UpdatedBy   bigint        NULL,
    CONSTRAINT PK_SystemSettings PRIMARY KEY ([Key])
);

/* =============================================================================
   2. MODULE DANH MỤC HÀNG HÓA (bảng cha Products + bảng con Specs_*)
   ============================================================================= */

CREATE TABLE dbo.Categories (
    Id         int IDENTITY(1,1) NOT NULL,
    Code       varchar(30)   NOT NULL,
    Name       nvarchar(100) NOT NULL,
    SpecType   tinyint       NOT NULL DEFAULT 0,   -- 0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU
    ParentId   int           NULL,
    IsActive   bit           NOT NULL DEFAULT 1,
    CreatedAt  datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  bigint        NULL,
    UpdatedAt  datetime2(0)  NULL,
    UpdatedBy  bigint        NULL,
    CONSTRAINT PK_Categories PRIMARY KEY (Id),
    CONSTRAINT UQ_Categories_Code UNIQUE (Code),
    CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentId) REFERENCES dbo.Categories (Id),
    CONSTRAINT CK_Categories_SpecType CHECK (SpecType BETWEEN 0 AND 6)
);

CREATE TABLE dbo.Brands (
    Id         int IDENTITY(1,1) NOT NULL,
    Name       nvarchar(100) NOT NULL,
    IsActive   bit           NOT NULL DEFAULT 1,
    CreatedAt  datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  bigint        NULL,
    UpdatedAt  datetime2(0)  NULL,
    UpdatedBy  bigint        NULL,
    CONSTRAINT PK_Brands PRIMARY KEY (Id),
    CONSTRAINT UQ_Brands_Name UNIQUE (Name)
);

CREATE TABLE dbo.Products (
    Id                bigint IDENTITY(1,1) NOT NULL,
    Sku               varchar(50)    NOT NULL,
    Name              nvarchar(250)  NOT NULL,
    Slug              varchar(300)   NOT NULL,
    BrandId           int            NOT NULL,
    CategoryId        int            NOT NULL,
    ShortDescription  nvarchar(500)  NULL,
    Description       nvarchar(max)  NULL,
    LastImportPrice   decimal(18,0)  NULL,           -- tham khảo; giá vốn thực tế tính từ phiếu nhập
    ListPrice         decimal(18,0)  NOT NULL,
    RequiresSerial    bit            NOT NULL DEFAULT 1,
    MinStockDefault   int            NOT NULL DEFAULT 0,
    WarrantyMonths    int            NOT NULL DEFAULT 0,
    WarrantyTerms     nvarchar(1000) NULL,
    Status            tinyint        NOT NULL DEFAULT 0,  -- 0 Draft, 1 Active, 2 Discontinued (Hết hàng suy ra từ tồn)
    CreatedAt         datetime2(0)   NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy         bigint         NULL,
    UpdatedAt         datetime2(0)   NULL,
    UpdatedBy         bigint         NULL,
    CONSTRAINT PK_Products PRIMARY KEY (Id),
    CONSTRAINT UQ_Products_Sku UNIQUE (Sku),
    CONSTRAINT UQ_Products_Slug UNIQUE (Slug),
    CONSTRAINT FK_Products_Brands FOREIGN KEY (BrandId) REFERENCES dbo.Brands (Id),
    CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (Id),
    CONSTRAINT CK_Products_ListPrice CHECK (ListPrice >= 0),
    CONSTRAINT CK_Products_MinStock CHECK (MinStockDefault >= 0),
    CONSTRAINT CK_Products_Warranty CHECK (WarrantyMonths >= 0),
    CONSTRAINT CK_Products_Status CHECK (Status IN (0, 1, 2))
);

CREATE TABLE dbo.ProductImages (
    Id         bigint IDENTITY(1,1) NOT NULL,
    ProductId  bigint        NOT NULL,
    Url        varchar(500)  NOT NULL,
    SortOrder  int           NOT NULL DEFAULT 0,
    IsPrimary  bit           NOT NULL DEFAULT 0,
    CONSTRAINT PK_ProductImages PRIMARY KEY (Id),
    CONSTRAINT FK_ProductImages_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id) ON DELETE CASCADE
);

CREATE TABLE dbo.Specs_CPU (
    ProductId         bigint       NOT NULL,
    Socket            varchar(20)  NOT NULL,
    Cores             smallint     NOT NULL,
    Threads           smallint     NOT NULL,
    BaseClockGhz      decimal(4,2) NOT NULL,
    BoostClockGhz     decimal(4,2) NULL,
    CacheMb           smallint     NOT NULL,
    TdpW              smallint     NOT NULL,
    HasCooler         bit          NOT NULL DEFAULT 0,
    HasIntegratedGpu  bit          NOT NULL DEFAULT 0,
    CONSTRAINT PK_Specs_CPU PRIMARY KEY (ProductId),
    CONSTRAINT FK_Specs_CPU_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id) ON DELETE CASCADE
);

CREATE TABLE dbo.Specs_RAM (
    ProductId         bigint      NOT NULL,
    MemoryType        varchar(10) NOT NULL,       -- DDR4, DDR5
    CapacityGbPerKit  smallint    NOT NULL,
    ModulesPerKit     tinyint     NOT NULL,
    BusMhz            int         NOT NULL,
    CasLatency        tinyint     NOT NULL,
    FormFactor        varchar(10) NOT NULL,       -- DIMM, SODIMM
    HasRgb            bit         NOT NULL DEFAULT 0,
    CONSTRAINT PK_Specs_RAM PRIMARY KEY (ProductId),
    CONSTRAINT FK_Specs_RAM_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id) ON DELETE CASCADE,
    CONSTRAINT CK_Specs_RAM_Modules CHECK (ModulesPerKit IN (1, 2, 4))
);

CREATE TABLE dbo.Specs_Storage (
    ProductId    bigint      NOT NULL,
    StorageType  varchar(20) NOT NULL,            -- SSD NVMe, SSD SATA, HDD
    [Interface]  varchar(30) NOT NULL,            -- PCIe Gen4 x4, SATA III
    FormFactor   varchar(20) NOT NULL,            -- M.2 2280, 2.5in, 3.5in
    CapacityGb   int         NOT NULL,
    ReadMbps     int         NULL,
    WriteMbps    int         NULL,
    CONSTRAINT PK_Specs_Storage PRIMARY KEY (ProductId),
    CONSTRAINT FK_Specs_Storage_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id) ON DELETE CASCADE
);

CREATE TABLE dbo.Specs_Monitor (
    ProductId       bigint       NOT NULL,
    SizeInch        decimal(4,1) NOT NULL,
    Resolution      varchar(20)  NOT NULL,        -- FHD, 2K, 4K
    PanelType       varchar(10)  NOT NULL,        -- IPS, VA, OLED
    RefreshRateHz   smallint     NOT NULL,
    ResponseTimeMs  decimal(4,1) NOT NULL,
    SrgbPercent     tinyint      NULL,
    CONSTRAINT PK_Specs_Monitor PRIMARY KEY (ProductId),
    CONSTRAINT FK_Specs_Monitor_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id) ON DELETE CASCADE
);

CREATE TABLE dbo.Specs_Monitor_Ports (
    ProductId  bigint      NOT NULL,
    PortType   varchar(20) NOT NULL,              -- HDMI, DP, TypeC
    Quantity   tinyint     NOT NULL,
    CONSTRAINT PK_Specs_Monitor_Ports PRIMARY KEY (ProductId, PortType),
    CONSTRAINT FK_Specs_Monitor_Ports_Monitor FOREIGN KEY (ProductId) REFERENCES dbo.Specs_Monitor (ProductId) ON DELETE CASCADE
);

CREATE TABLE dbo.Specs_Mainboard (
    ProductId   bigint      NOT NULL,
    Socket      varchar(20) NOT NULL,
    Chipset     varchar(30) NOT NULL,
    MemoryType  varchar(10) NOT NULL,
    RamSlots    tinyint     NOT NULL,
    MaxBusMhz   int         NOT NULL,
    FormFactor  varchar(10) NOT NULL,             -- ATX, mATX, ITX
    M2Slots     tinyint     NOT NULL DEFAULT 0,
    SataPorts   tinyint     NOT NULL DEFAULT 0,
    CONSTRAINT PK_Specs_Mainboard PRIMARY KEY (ProductId),
    CONSTRAINT FK_Specs_Mainboard_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id) ON DELETE CASCADE
);

CREATE TABLE dbo.Specs_PSU (
    ProductId         bigint      NOT NULL,
    PowerW            smallint    NOT NULL,
    EfficiencyRating  varchar(20) NULL,
    ModularType       varchar(10) NULL,           -- Full, Semi, Non
    CONSTRAINT PK_Specs_PSU PRIMARY KEY (ProductId),
    CONSTRAINT FK_Specs_PSU_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id) ON DELETE CASCADE
);

CREATE TABLE dbo.CompatibilityRules (
    Id               int IDENTITY(1,1) NOT NULL,
    Name             nvarchar(150) NOT NULL,
    LeftCategoryId   int           NOT NULL,
    RightCategoryId  int           NOT NULL,
    LeftField        varchar(50)   NOT NULL,
    RightField       varchar(50)   NOT NULL,
    Operator         varchar(10)   NOT NULL,      -- EQ, LTE, GTE
    Severity         tinyint       NOT NULL DEFAULT 1,   -- 0 Info, 1 Warning
    IsActive         bit           NOT NULL DEFAULT 1,
    CONSTRAINT PK_CompatibilityRules PRIMARY KEY (Id),
    CONSTRAINT FK_CompatibilityRules_Left FOREIGN KEY (LeftCategoryId) REFERENCES dbo.Categories (Id),
    CONSTRAINT FK_CompatibilityRules_Right FOREIGN KEY (RightCategoryId) REFERENCES dbo.Categories (Id),
    CONSTRAINT CK_CompatibilityRules_Operator CHECK (Operator IN ('EQ', 'LTE', 'GTE'))
);

/* =============================================================================
   3. MODULE GIÁ VÀ KHUYẾN MÃI
   ============================================================================= */

CREATE TABLE dbo.CustomerGroups (
    Id         int IDENTITY(1,1) NOT NULL,
    Code       varchar(30)   NOT NULL,            -- RETAIL, WHOLESALE
    Name       nvarchar(100) NOT NULL,
    IsDefault  bit           NOT NULL DEFAULT 0,
    CONSTRAINT PK_CustomerGroups PRIMARY KEY (Id),
    CONSTRAINT UQ_CustomerGroups_Code UNIQUE (Code)
);

CREATE TABLE dbo.ProductPrices (
    Id               bigint IDENTITY(1,1) NOT NULL,
    ProductId        bigint        NOT NULL,
    CustomerGroupId  int           NOT NULL,
    Price            decimal(18,0) NOT NULL,
    CreatedAt        datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        bigint        NULL,
    UpdatedAt        datetime2(0)  NULL,
    UpdatedBy        bigint        NULL,
    CONSTRAINT PK_ProductPrices PRIMARY KEY (Id),
    CONSTRAINT UQ_ProductPrices UNIQUE (ProductId, CustomerGroupId),
    CONSTRAINT FK_ProductPrices_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT FK_ProductPrices_Groups FOREIGN KEY (CustomerGroupId) REFERENCES dbo.CustomerGroups (Id),
    CONSTRAINT CK_ProductPrices_Price CHECK (Price >= 0)
);

CREATE TABLE dbo.PriceTiers (
    Id               bigint IDENTITY(1,1) NOT NULL,
    ProductId        bigint        NOT NULL,
    CustomerGroupId  int           NOT NULL,
    MinQuantity      int           NOT NULL,
    Price            decimal(18,0) NOT NULL,
    CreatedAt        datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        bigint        NULL,
    UpdatedAt        datetime2(0)  NULL,
    UpdatedBy        bigint        NULL,
    CONSTRAINT PK_PriceTiers PRIMARY KEY (Id),
    CONSTRAINT UQ_PriceTiers UNIQUE (ProductId, CustomerGroupId, MinQuantity),
    CONSTRAINT FK_PriceTiers_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT FK_PriceTiers_Groups FOREIGN KEY (CustomerGroupId) REFERENCES dbo.CustomerGroups (Id),
    CONSTRAINT CK_PriceTiers_MinQty CHECK (MinQuantity > 1),
    CONSTRAINT CK_PriceTiers_Price CHECK (Price >= 0)
);

CREATE TABLE dbo.Promotions (
    Id              bigint IDENTITY(1,1) NOT NULL,
    Name            nvarchar(200) NOT NULL,
    ScopeType       tinyint       NOT NULL,       -- 0 Product, 1 Brand, 2 Category
    ScopeId         bigint        NOT NULL,
    DiscountType    tinyint       NOT NULL,       -- 0 Percent, 1 Amount
    DiscountValue   decimal(18,2) NOT NULL,
    StartAt         datetime2(0)  NOT NULL,
    EndAt           datetime2(0)  NOT NULL,
    MaxQuantity     int           NULL,
    MaxPerCustomer  int           NULL,
    IsActive        bit           NOT NULL DEFAULT 1,
    CreatedAt       datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy       bigint        NULL,
    UpdatedAt       datetime2(0)  NULL,
    UpdatedBy       bigint        NULL,
    CONSTRAINT PK_Promotions PRIMARY KEY (Id),
    CONSTRAINT CK_Promotions_Scope CHECK (ScopeType IN (0, 1, 2)),
    CONSTRAINT CK_Promotions_Type CHECK (DiscountType IN (0, 1)),
    CONSTRAINT CK_Promotions_Value CHECK (DiscountValue > 0),
    CONSTRAINT CK_Promotions_Dates CHECK (EndAt > StartAt)
);

CREATE TABLE dbo.Vouchers (
    Id                 bigint IDENTITY(1,1) NOT NULL,
    Code               varchar(40)   NOT NULL,
    DiscountType       tinyint       NOT NULL,    -- 0 Percent, 1 Amount
    DiscountValue      decimal(18,2) NOT NULL,
    MinOrderAmount     decimal(18,0) NULL,
    MaxDiscountAmount  decimal(18,0) NULL,
    StartAt            datetime2(0)  NOT NULL,
    EndAt              datetime2(0)  NOT NULL,
    MaxUses            int           NULL,
    UsedCount          int           NOT NULL DEFAULT 0,
    CustomerGroupId    int           NULL,
    IsStackable        bit           NOT NULL DEFAULT 0,
    IsActive           bit           NOT NULL DEFAULT 1,
    CreatedAt          datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy          bigint        NULL,
    UpdatedAt          datetime2(0)  NULL,
    UpdatedBy          bigint        NULL,
    CONSTRAINT PK_Vouchers PRIMARY KEY (Id),
    CONSTRAINT UQ_Vouchers_Code UNIQUE (Code),
    CONSTRAINT FK_Vouchers_Groups FOREIGN KEY (CustomerGroupId) REFERENCES dbo.CustomerGroups (Id),
    CONSTRAINT CK_Vouchers_Type CHECK (DiscountType IN (0, 1)),
    CONSTRAINT CK_Vouchers_Value CHECK (DiscountValue > 0),
    CONSTRAINT CK_Vouchers_Dates CHECK (EndAt > StartAt),
    CONSTRAINT CK_Vouchers_Used CHECK (UsedCount >= 0)
);

/* =============================================================================
   4. MODULE KHÁCH HÀNG, GIỎ HÀNG
   ============================================================================= */

CREATE TABLE dbo.Customers (
    Id               bigint IDENTITY(1,1) NOT NULL,
    UserId           bigint        NULL,          -- NULL: khách vãng lai
    FullName         nvarchar(150) NOT NULL,
    Phone            varchar(20)   NOT NULL,
    Email            varchar(150)  NULL,
    CustomerGroupId  int           NOT NULL,
    TaxCode          varchar(20)   NULL,
    CreditLimit      decimal(18,0) NOT NULL DEFAULT 0,
    PaymentTermDays  int           NOT NULL DEFAULT 0,
    IsActive         bit           NOT NULL DEFAULT 1,
    CreatedAt        datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        bigint        NULL,
    UpdatedAt        datetime2(0)  NULL,
    UpdatedBy        bigint        NULL,
    CONSTRAINT PK_Customers PRIMARY KEY (Id),
    CONSTRAINT FK_Customers_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_Customers_Groups FOREIGN KEY (CustomerGroupId) REFERENCES dbo.CustomerGroups (Id),
    CONSTRAINT CK_Customers_Credit CHECK (CreditLimit >= 0)
);

CREATE TABLE dbo.CustomerAddresses (
    Id             bigint IDENTITY(1,1) NOT NULL,
    CustomerId     bigint        NOT NULL,
    RecipientName  nvarchar(150) NOT NULL,
    Phone          varchar(20)   NOT NULL,
    Province       nvarchar(100) NOT NULL,
    District       nvarchar(100) NOT NULL,
    Ward           nvarchar(100) NOT NULL,
    Street         nvarchar(250) NOT NULL,
    IsDefault      bit           NOT NULL DEFAULT 0,
    CONSTRAINT PK_CustomerAddresses PRIMARY KEY (Id),
    CONSTRAINT FK_CustomerAddresses_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id)
);

CREATE TABLE dbo.Carts (
    Id            bigint IDENTITY(1,1) NOT NULL,
    CustomerId    bigint       NULL,
    SessionToken  varchar(64)  NULL,
    CreatedAt     datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt     datetime2(0) NULL,
    CONSTRAINT PK_Carts PRIMARY KEY (Id),
    CONSTRAINT FK_Carts_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
    CONSTRAINT CK_Carts_Owner CHECK (CustomerId IS NOT NULL OR SessionToken IS NOT NULL)
);

CREATE TABLE dbo.CartItems (
    Id          bigint IDENTITY(1,1) NOT NULL,
    CartId      bigint       NOT NULL,
    ProductId   bigint       NOT NULL,
    Quantity    int          NOT NULL,
    IsSelected  bit          NOT NULL DEFAULT 1,
    AddedAt     datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CartItems PRIMARY KEY (Id),
    CONSTRAINT UQ_CartItems UNIQUE (CartId, ProductId),
    CONSTRAINT FK_CartItems_Carts FOREIGN KEY (CartId) REFERENCES dbo.Carts (Id) ON DELETE CASCADE,
    CONSTRAINT FK_CartItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT CK_CartItems_Qty CHECK (Quantity > 0)
);

/* =============================================================================
   5. MODULE NHÂN SỰ (tạo trước vì đơn hàng tham chiếu nhân viên)
   ============================================================================= */

CREATE TABLE dbo.Employees (
    Id              bigint IDENTITY(1,1) NOT NULL,
    EmployeeCode    varchar(20)   NOT NULL,
    UserId          bigint        NOT NULL,
    FullName        nvarchar(150) NOT NULL,
    Phone           varchar(20)   NULL,
    Position        tinyint       NOT NULL,       -- 0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager
    BranchId        int           NOT NULL,
    Status          tinyint       NOT NULL DEFAULT 0,   -- 0 Active, 1 Terminated
    HireDate        date          NOT NULL,
    TerminatedDate  date          NULL,
    CreatedAt       datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy       bigint        NULL,
    UpdatedAt       datetime2(0)  NULL,
    UpdatedBy       bigint        NULL,
    CONSTRAINT PK_Employees PRIMARY KEY (Id),
    CONSTRAINT UQ_Employees_Code UNIQUE (EmployeeCode),
    CONSTRAINT UQ_Employees_User UNIQUE (UserId),
    CONSTRAINT FK_Employees_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_Employees_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT CK_Employees_Position CHECK (Position BETWEEN 0 AND 3),
    CONSTRAINT CK_Employees_Status CHECK (Status IN (0, 1))
);

CREATE TABLE dbo.Shifts (
    Id         int IDENTITY(1,1) NOT NULL,
    Code       varchar(20) NOT NULL,              -- MORNING, AFTERNOON, FULL
    Name       nvarchar(50) NOT NULL,
    StartTime  time(0)     NOT NULL,
    EndTime    time(0)     NOT NULL,
    CONSTRAINT PK_Shifts PRIMARY KEY (Id),
    CONSTRAINT UQ_Shifts_Code UNIQUE (Code)
);

CREATE TABLE dbo.EmployeeSchedules (
    Id          bigint IDENTITY(1,1) NOT NULL,
    EmployeeId  bigint NOT NULL,
    ShiftId     int    NOT NULL,
    BranchId    int    NOT NULL,
    WorkDate    date   NOT NULL,
    CONSTRAINT PK_EmployeeSchedules PRIMARY KEY (Id),
    CONSTRAINT UQ_EmployeeSchedules UNIQUE (EmployeeId, WorkDate, ShiftId),
    CONSTRAINT FK_EmployeeSchedules_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id),
    CONSTRAINT FK_EmployeeSchedules_Shifts FOREIGN KEY (ShiftId) REFERENCES dbo.Shifts (Id),
    CONSTRAINT FK_EmployeeSchedules_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id)
);

CREATE TABLE dbo.Attendances (
    Id            bigint IDENTITY(1,1) NOT NULL,
    EmployeeId    bigint        NOT NULL,
    ScheduleId    bigint        NULL,
    WorkDate      date          NOT NULL,
    CheckInAt     datetime2(0)  NULL,
    CheckOutAt    datetime2(0)  NULL,
    Source        tinyint       NOT NULL DEFAULT 0,     -- 0 Device, 1 ManualAdjust
    AdjustedBy    bigint        NULL,
    AdjustReason  nvarchar(300) NULL,
    CreatedAt     datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy     bigint        NULL,
    UpdatedAt     datetime2(0)  NULL,
    UpdatedBy     bigint        NULL,
    CONSTRAINT PK_Attendances PRIMARY KEY (Id),
    CONSTRAINT FK_Attendances_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id),
    CONSTRAINT FK_Attendances_Schedules FOREIGN KEY (ScheduleId) REFERENCES dbo.EmployeeSchedules (Id),
    CONSTRAINT FK_Attendances_AdjustedBy FOREIGN KEY (AdjustedBy) REFERENCES dbo.Users (Id),
    CONSTRAINT CK_Attendances_Time CHECK (CheckOutAt IS NULL OR CheckInAt IS NULL OR CheckOutAt >= CheckInAt)
);

/* =============================================================================
   6. MODULE ĐƠN HÀNG, THANH TOÁN
   ============================================================================= */

CREATE TABLE dbo.Orders (
    Id               bigint IDENTITY(1,1) NOT NULL,
    OrderCode        varchar(30)   NOT NULL,
    CustomerId       bigint        NOT NULL,
    BranchId         int           NOT NULL,        -- chi nhánh xuất hàng
    Channel          tinyint       NOT NULL,        -- 0 Online, 1 POS
    FulfillmentType  tinyint       NOT NULL,        -- 0 Delivery, 1 Pickup
    Status           tinyint       NOT NULL,        -- 0 PendingPayment, 1 Pending, 2 Confirmed, 3 Packing,
                                                    -- 4 ReadyForPickup, 5 Shipping, 6 DeliveryFailed, 7 Completed,
                                                    -- 8 Cancelled, 9 PartiallyReturned, 10 Returned
    RecipientName    nvarchar(150) NULL,            -- snapshot địa chỉ giao
    RecipientPhone   varchar(20)   NULL,
    ShippingAddress  nvarchar(500) NULL,
    SubTotal         decimal(18,0) NOT NULL,
    DiscountTotal    decimal(18,0) NOT NULL DEFAULT 0,
    ShippingFee      decimal(18,0) NOT NULL DEFAULT 0,
    GrandTotal       decimal(18,0) NOT NULL,
    PaidAmount       decimal(18,0) NOT NULL DEFAULT 0,
    PaymentMethod    tinyint       NOT NULL,        -- 0 COD, 1 BankTransfer, 2 Gateway, 3 Cash/POS, 4 Credit
    PaymentStatus    tinyint       NOT NULL DEFAULT 0,   -- 0 Unpaid, 1 PartiallyPaid, 2 Paid, 3 Refunded
    VoucherId        bigint        NULL,
    ExpiresAt        datetime2(0)  NULL,            -- hạn giữ hàng / hạn nhận hàng
    CompletedAt      datetime2(0)  NULL,
    Note             nvarchar(500) NULL,
    RowVersion       rowversion    NOT NULL,
    CreatedAt        datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        bigint        NULL,
    UpdatedAt        datetime2(0)  NULL,
    UpdatedBy        bigint        NULL,
    CONSTRAINT PK_Orders PRIMARY KEY (Id),
    CONSTRAINT UQ_Orders_Code UNIQUE (OrderCode),
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
    CONSTRAINT FK_Orders_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT FK_Orders_Vouchers FOREIGN KEY (VoucherId) REFERENCES dbo.Vouchers (Id),
    CONSTRAINT CK_Orders_Status CHECK (Status BETWEEN 0 AND 10),
    CONSTRAINT CK_Orders_Channel CHECK (Channel IN (0, 1)),
    CONSTRAINT CK_Orders_Fulfillment CHECK (FulfillmentType IN (0, 1)),
    CONSTRAINT CK_Orders_Total CHECK (GrandTotal = SubTotal - DiscountTotal + ShippingFee),
    CONSTRAINT CK_Orders_Amounts CHECK (SubTotal >= 0 AND DiscountTotal >= 0 AND ShippingFee >= 0 AND PaidAmount >= 0)
);

CREATE TABLE dbo.OrderItems (
    Id              bigint IDENTITY(1,1) NOT NULL,
    OrderId         bigint        NOT NULL,
    ProductId       bigint        NOT NULL,
    Sku             varchar(50)   NOT NULL,          -- snapshot
    ProductName     nvarchar(250) NOT NULL,          -- snapshot
    UnitPrice       decimal(18,0) NOT NULL,          -- snapshot, giá sau bảng giá/bậc thang
    DiscountAmount  decimal(18,0) NOT NULL DEFAULT 0,
    Quantity        int           NOT NULL,
    LineTotal       decimal(18,0) NOT NULL,
    WarrantyMonths  int           NOT NULL,          -- snapshot
    RequiresSerial  bit           NOT NULL,          -- snapshot
    PromotionId     bigint        NULL,
    CONSTRAINT PK_OrderItems PRIMARY KEY (Id),
    CONSTRAINT UQ_OrderItems UNIQUE (OrderId, ProductId),
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT FK_OrderItems_Promotions FOREIGN KEY (PromotionId) REFERENCES dbo.Promotions (Id),
    CONSTRAINT CK_OrderItems_Qty CHECK (Quantity > 0),
    CONSTRAINT CK_OrderItems_Total CHECK (LineTotal = UnitPrice * Quantity - DiscountAmount)
);

CREATE TABLE dbo.OrderStatusHistory (
    Id          bigint IDENTITY(1,1) NOT NULL,
    OrderId     bigint        NOT NULL,
    FromStatus  tinyint       NULL,
    ToStatus    tinyint       NOT NULL,
    Reason      nvarchar(300) NULL,
    ChangedBy   bigint        NULL,                  -- NULL nếu do hệ thống
    ChangedAt   datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OrderStatusHistory PRIMARY KEY (Id),
    CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT FK_OrderStatusHistory_Users FOREIGN KEY (ChangedBy) REFERENCES dbo.Users (Id)
);

CREATE TABLE dbo.OrderSalesStaff (
    OrderId     bigint       NOT NULL,
    EmployeeId  bigint       NOT NULL,
    ShareRate   decimal(5,2) NOT NULL,               -- tổng một đơn = 100, kiểm tra ở tầng ứng dụng
    IsPrimary   bit          NOT NULL DEFAULT 0,
    CONSTRAINT PK_OrderSalesStaff PRIMARY KEY (OrderId, EmployeeId),
    CONSTRAINT FK_OrderSalesStaff_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT FK_OrderSalesStaff_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id),
    CONSTRAINT CK_OrderSalesStaff_Rate CHECK (ShareRate BETWEEN 0 AND 100)
);

CREATE TABLE dbo.VoucherUsages (
    Id              bigint IDENTITY(1,1) NOT NULL,
    VoucherId       bigint        NOT NULL,
    OrderId         bigint        NOT NULL,
    CustomerId      bigint        NULL,
    DiscountAmount  decimal(18,0) NOT NULL,
    CreatedAt       datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_VoucherUsages PRIMARY KEY (Id),
    CONSTRAINT UQ_VoucherUsages UNIQUE (VoucherId, OrderId),
    CONSTRAINT FK_VoucherUsages_Vouchers FOREIGN KEY (VoucherId) REFERENCES dbo.Vouchers (Id),
    CONSTRAINT FK_VoucherUsages_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT FK_VoucherUsages_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id)
);

/* =============================================================================
   7. MODULE ĐỔI TRẢ - phần đầu (Payments tham chiếu ReturnRequests)
   ============================================================================= */

CREATE TABLE dbo.ReturnRequests (
    Id            bigint IDENTITY(1,1) NOT NULL,
    ReturnCode    varchar(30)   NOT NULL,
    OrderId       bigint        NOT NULL,
    CustomerId    bigint        NOT NULL,
    BranchId      int           NOT NULL,
    Status        tinyint       NOT NULL DEFAULT 0,      -- 0 Requested, 1 Approved, 2 Rejected, 3 GoodsReceived, 4 Refunded, 5 Closed
    Reason        nvarchar(500) NOT NULL,
    RefundAmount  decimal(18,0) NOT NULL DEFAULT 0,
    ApprovedBy    bigint        NULL,
    ApprovedAt    datetime2(0)  NULL,
    CreatedAt     datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy     bigint        NULL,
    UpdatedAt     datetime2(0)  NULL,
    UpdatedBy     bigint        NULL,
    CONSTRAINT PK_ReturnRequests PRIMARY KEY (Id),
    CONSTRAINT UQ_ReturnRequests_Code UNIQUE (ReturnCode),
    CONSTRAINT FK_ReturnRequests_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT FK_ReturnRequests_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
    CONSTRAINT FK_ReturnRequests_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT FK_ReturnRequests_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.Users (Id),
    CONSTRAINT CK_ReturnRequests_Status CHECK (Status BETWEEN 0 AND 5),
    CONSTRAINT CK_ReturnRequests_Refund CHECK (RefundAmount >= 0)
);

CREATE TABLE dbo.Payments (
    Id                     bigint IDENTITY(1,1) NOT NULL,
    OrderId                bigint        NOT NULL,
    PaymentType            tinyint       NOT NULL,       -- 0 Payment, 1 Refund
    Method                 tinyint       NOT NULL,
    Amount                 decimal(18,0) NOT NULL,
    Status                 tinyint       NOT NULL DEFAULT 0,   -- 0 Pending, 1 Succeeded, 2 Failed
    Provider               varchar(30)   NULL,
    ProviderTransactionId  varchar(100)  NULL,
    ParentPaymentId        bigint        NULL,               -- giao dịch hoàn trỏ về giao dịch gốc
    ReturnRequestId        bigint        NULL,
    PaidAt                 datetime2(0)  NULL,
    CreatedAt              datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy              bigint        NULL,
    UpdatedAt              datetime2(0)  NULL,
    UpdatedBy              bigint        NULL,
    CONSTRAINT PK_Payments PRIMARY KEY (Id),
    CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT FK_Payments_Parent FOREIGN KEY (ParentPaymentId) REFERENCES dbo.Payments (Id),
    CONSTRAINT FK_Payments_ReturnRequests FOREIGN KEY (ReturnRequestId) REFERENCES dbo.ReturnRequests (Id),
    CONSTRAINT CK_Payments_Type CHECK (PaymentType IN (0, 1)),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Payments_Status CHECK (Status IN (0, 1, 2))
);

CREATE TABLE dbo.CustomerLedger (
    Id          bigint IDENTITY(1,1) NOT NULL,
    CustomerId  bigint        NOT NULL,
    OrderId     bigint        NULL,
    PaymentId   bigint        NULL,
    EntryType   tinyint       NOT NULL,              -- 0 Charge, 1 Payment, 2 Adjustment
    Amount      decimal(18,0) NOT NULL,              -- dương: phát sinh nợ, âm: giảm nợ
    DueDate     date          NULL,
    CreatedAt   datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy   bigint        NULL,
    CONSTRAINT PK_CustomerLedger PRIMARY KEY (Id),
    CONSTRAINT FK_CustomerLedger_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
    CONSTRAINT FK_CustomerLedger_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT FK_CustomerLedger_Payments FOREIGN KEY (PaymentId) REFERENCES dbo.Payments (Id),
    CONSTRAINT CK_CustomerLedger_Type CHECK (EntryType IN (0, 1, 2))
);

/* =============================================================================
   8. MODULE KHO, SERIAL, NHÀ CUNG CẤP
   ============================================================================= */

CREATE TABLE dbo.Suppliers (
    Id               bigint IDENTITY(1,1) NOT NULL,
    Code             varchar(30)   NOT NULL,
    Name             nvarchar(200) NOT NULL,
    TaxCode          varchar(20)   NULL,
    Phone            varchar(20)   NULL,
    Address          nvarchar(300) NULL,
    PaymentTermDays  int           NOT NULL DEFAULT 0,
    IsActive         bit           NOT NULL DEFAULT 1,
    CreatedAt        datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        bigint        NULL,
    UpdatedAt        datetime2(0)  NULL,
    UpdatedBy        bigint        NULL,
    CONSTRAINT PK_Suppliers PRIMARY KEY (Id),
    CONSTRAINT UQ_Suppliers_Code UNIQUE (Code)
);

CREATE TABLE dbo.SupplierProducts (
    SupplierId   bigint      NOT NULL,
    ProductId    bigint      NOT NULL,
    SupplierSku  varchar(50) NULL,
    CONSTRAINT PK_SupplierProducts PRIMARY KEY (SupplierId, ProductId),
    CONSTRAINT FK_SupplierProducts_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers (Id),
    CONSTRAINT FK_SupplierProducts_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id)
);

CREATE TABLE dbo.StockLevels (
    BranchId      int          NOT NULL,
    ProductId     bigint       NOT NULL,
    PhysicalQty   int          NOT NULL DEFAULT 0,
    ReservedQty   int          NOT NULL DEFAULT 0,
    AvailableQty  AS (PhysicalQty - ReservedQty) PERSISTED,
    MinStock      int          NULL,                 -- NULL: dùng Products.MinStockDefault
    RowVersion    rowversion   NOT NULL,
    UpdatedAt     datetime2(0) NULL,
    CONSTRAINT PK_StockLevels PRIMARY KEY (BranchId, ProductId),
    CONSTRAINT FK_StockLevels_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT FK_StockLevels_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT CK_StockLevels_NonNegative CHECK (PhysicalQty >= 0 AND ReservedQty >= 0),
    CONSTRAINT CK_StockLevels_Reserved CHECK (ReservedQty <= PhysicalQty)
);

CREATE TABLE dbo.StockMovements (                    -- bất biến, chỉ INSERT
    Id             bigint IDENTITY(1,1) NOT NULL,
    BranchId       int           NOT NULL,
    ProductId      bigint        NOT NULL,
    MovementType   tinyint       NOT NULL,           -- 0 Receipt, 1 Reserve, 2 Release, 3 Issue, 4 TransferOut, 5 TransferIn,
                                                     -- 6 StockTakeAdjust, 7 ManualAdjust, 8 CustomerReturn, 9 SupplierReturn
    PhysicalDelta  int           NOT NULL,
    ReservedDelta  int           NOT NULL,
    RefType        varchar(30)   NOT NULL,           -- Order, PurchaseReceipt, StockTransfer, StockTake, ReturnRequest...
    RefId          bigint        NOT NULL,
    Note           nvarchar(300) NULL,
    CreatedAt      datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy      bigint        NULL,
    CONSTRAINT PK_StockMovements PRIMARY KEY (Id),
    CONSTRAINT FK_StockMovements_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT FK_StockMovements_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT FK_StockMovements_Users FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (Id),
    CONSTRAINT CK_StockMovements_Type CHECK (MovementType BETWEEN 0 AND 9)
);

CREATE TABLE dbo.PurchaseReceipts (
    Id                 bigint IDENTITY(1,1) NOT NULL,
    ReceiptCode        varchar(30)   NOT NULL,
    SupplierId         bigint        NOT NULL,
    BranchId           int           NOT NULL,
    SupplierInvoiceNo  varchar(50)   NULL,
    Status             tinyint       NOT NULL DEFAULT 0,   -- 0 Draft, 1 Completed, 2 Cancelled
    TotalAmount        decimal(18,0) NOT NULL DEFAULT 0,
    ReceivedAt         datetime2(0)  NULL,
    CreatedAt          datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy          bigint        NULL,
    UpdatedAt          datetime2(0)  NULL,
    UpdatedBy          bigint        NULL,
    CONSTRAINT PK_PurchaseReceipts PRIMARY KEY (Id),
    CONSTRAINT UQ_PurchaseReceipts_Code UNIQUE (ReceiptCode),
    CONSTRAINT FK_PurchaseReceipts_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers (Id),
    CONSTRAINT FK_PurchaseReceipts_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT CK_PurchaseReceipts_Status CHECK (Status IN (0, 1, 2))
);

CREATE TABLE dbo.PurchaseReceiptItems (
    Id         bigint IDENTITY(1,1) NOT NULL,
    ReceiptId  bigint        NOT NULL,
    ProductId  bigint        NOT NULL,
    Quantity   int           NOT NULL,
    UnitCost   decimal(18,0) NOT NULL,
    CONSTRAINT PK_PurchaseReceiptItems PRIMARY KEY (Id),
    CONSTRAINT UQ_PurchaseReceiptItems UNIQUE (ReceiptId, ProductId),
    CONSTRAINT FK_PurchaseReceiptItems_Receipts FOREIGN KEY (ReceiptId) REFERENCES dbo.PurchaseReceipts (Id),
    CONSTRAINT FK_PurchaseReceiptItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT CK_PurchaseReceiptItems_Qty CHECK (Quantity > 0),
    CONSTRAINT CK_PurchaseReceiptItems_Cost CHECK (UnitCost >= 0)
);

CREATE TABLE dbo.SerialItems (
    Id                     bigint IDENTITY(1,1) NOT NULL,
    ProductId              bigint        NOT NULL,
    SerialNumber           varchar(100)  NOT NULL,
    Status                 tinyint       NOT NULL DEFAULT 0,   -- 0 InStock, 1 Allocated, 2 InTransfer, 3 Sold, 4 RMA,
                                                               -- 5 Returned, 6 Replaced, 7 Defective,
                                                               -- 8 ReturnedToSupplier, 9 Scrapped
    BranchId               int           NOT NULL,             -- vị trí hiện tại
    PurchaseReceiptItemId  bigint        NOT NULL,
    UnitCost               decimal(18,0) NOT NULL,
    OrderItemId            bigint        NULL,                 -- đơn đang gán / đã bán
    SoldAt                 datetime2(0)  NULL,
    WarrantyStartDate      date          NULL,
    WarrantyEndDate        date          NULL,
    ReplacedBySerialId     bigint        NULL,
    RowVersion             rowversion    NOT NULL,
    CreatedAt              datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy              bigint        NULL,
    UpdatedAt              datetime2(0)  NULL,
    UpdatedBy              bigint        NULL,
    CONSTRAINT PK_SerialItems PRIMARY KEY (Id),
    -- Muốn serial duy nhất toàn hệ thống: đổi thành UNIQUE (SerialNumber)
    CONSTRAINT UQ_SerialItems UNIQUE (ProductId, SerialNumber),
    CONSTRAINT FK_SerialItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT FK_SerialItems_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT FK_SerialItems_ReceiptItems FOREIGN KEY (PurchaseReceiptItemId) REFERENCES dbo.PurchaseReceiptItems (Id),
    CONSTRAINT FK_SerialItems_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems (Id),
    CONSTRAINT FK_SerialItems_ReplacedBy FOREIGN KEY (ReplacedBySerialId) REFERENCES dbo.SerialItems (Id),
    CONSTRAINT CK_SerialItems_Status CHECK (Status BETWEEN 0 AND 9),
    CONSTRAINT CK_SerialItems_SoldHasOrder CHECK (Status <> 3 OR OrderItemId IS NOT NULL)
);

CREATE TABLE dbo.SerialStatusHistory (
    Id            bigint IDENTITY(1,1) NOT NULL,
    SerialItemId  bigint        NOT NULL,
    FromStatus    tinyint       NULL,
    ToStatus      tinyint       NOT NULL,
    RefType       varchar(30)   NULL,
    RefId         bigint        NULL,
    Note          nvarchar(300) NULL,
    ChangedBy     bigint        NULL,
    ChangedAt     datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_SerialStatusHistory PRIMARY KEY (Id),
    CONSTRAINT FK_SerialStatusHistory_Serials FOREIGN KEY (SerialItemId) REFERENCES dbo.SerialItems (Id),
    CONSTRAINT FK_SerialStatusHistory_Users FOREIGN KEY (ChangedBy) REFERENCES dbo.Users (Id)
);

CREATE TABLE dbo.StockTransfers (
    Id            bigint IDENTITY(1,1) NOT NULL,
    TransferCode  varchar(30) NOT NULL,
    FromBranchId  int         NOT NULL,
    ToBranchId    int         NOT NULL,
    Status        tinyint     NOT NULL DEFAULT 0,        -- 0 Draft, 1 InTransit, 2 Received, 3 Cancelled
    CreatedAt     datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy     bigint      NULL,
    UpdatedAt     datetime2(0) NULL,
    UpdatedBy     bigint      NULL,
    CONSTRAINT PK_StockTransfers PRIMARY KEY (Id),
    CONSTRAINT UQ_StockTransfers_Code UNIQUE (TransferCode),
    CONSTRAINT FK_StockTransfers_From FOREIGN KEY (FromBranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT FK_StockTransfers_To FOREIGN KEY (ToBranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT CK_StockTransfers_Branches CHECK (FromBranchId <> ToBranchId),
    CONSTRAINT CK_StockTransfers_Status CHECK (Status BETWEEN 0 AND 3)
);

CREATE TABLE dbo.StockTransferItems (
    Id          bigint IDENTITY(1,1) NOT NULL,
    TransferId  bigint NOT NULL,
    ProductId   bigint NOT NULL,
    Quantity    int    NOT NULL,
    CONSTRAINT PK_StockTransferItems PRIMARY KEY (Id),
    CONSTRAINT UQ_StockTransferItems UNIQUE (TransferId, ProductId),
    CONSTRAINT FK_StockTransferItems_Transfers FOREIGN KEY (TransferId) REFERENCES dbo.StockTransfers (Id),
    CONSTRAINT FK_StockTransferItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT CK_StockTransferItems_Qty CHECK (Quantity > 0)
);

CREATE TABLE dbo.StockTransferSerials (
    TransferItemId  bigint NOT NULL,
    SerialItemId    bigint NOT NULL,
    CONSTRAINT PK_StockTransferSerials PRIMARY KEY (TransferItemId, SerialItemId),
    CONSTRAINT FK_StockTransferSerials_Items FOREIGN KEY (TransferItemId) REFERENCES dbo.StockTransferItems (Id),
    CONSTRAINT FK_StockTransferSerials_Serials FOREIGN KEY (SerialItemId) REFERENCES dbo.SerialItems (Id)
);

CREATE TABLE dbo.StockTakes (
    Id             bigint IDENTITY(1,1) NOT NULL,
    StockTakeCode  varchar(30)  NOT NULL,
    BranchId       int          NOT NULL,
    Status         tinyint      NOT NULL DEFAULT 0,      -- 0 Draft, 1 Counting, 2 PendingApproval, 3 Approved, 4 Rejected
    ApprovedBy     bigint       NULL,
    ApprovedAt     datetime2(0) NULL,
    CreatedAt      datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy      bigint       NULL,
    UpdatedAt      datetime2(0) NULL,
    UpdatedBy      bigint       NULL,
    CONSTRAINT PK_StockTakes PRIMARY KEY (Id),
    CONSTRAINT UQ_StockTakes_Code UNIQUE (StockTakeCode),
    CONSTRAINT FK_StockTakes_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT FK_StockTakes_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.Users (Id),
    CONSTRAINT CK_StockTakes_Status CHECK (Status BETWEEN 0 AND 4)
);

CREATE TABLE dbo.StockTakeItems (
    Id           bigint IDENTITY(1,1) NOT NULL,
    StockTakeId  bigint        NOT NULL,
    ProductId    bigint        NOT NULL,
    SystemQty    int           NOT NULL,
    CountedQty   int           NOT NULL,
    DiffQty      AS (CountedQty - SystemQty) PERSISTED,
    Reason       nvarchar(300) NULL,
    CONSTRAINT PK_StockTakeItems PRIMARY KEY (Id),
    CONSTRAINT UQ_StockTakeItems UNIQUE (StockTakeId, ProductId),
    CONSTRAINT FK_StockTakeItems_StockTakes FOREIGN KEY (StockTakeId) REFERENCES dbo.StockTakes (Id),
    CONSTRAINT FK_StockTakeItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id)
);

CREATE TABLE dbo.SupplierReturns (
    Id          bigint IDENTITY(1,1) NOT NULL,
    ReturnCode  varchar(30)  NOT NULL,
    SupplierId  bigint       NOT NULL,
    BranchId    int          NOT NULL,
    Status      tinyint      NOT NULL DEFAULT 0,         -- 0 Draft, 1 Completed, 2 Cancelled
    CreatedAt   datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy   bigint       NULL,
    UpdatedAt   datetime2(0) NULL,
    UpdatedBy   bigint       NULL,
    CONSTRAINT PK_SupplierReturns PRIMARY KEY (Id),
    CONSTRAINT UQ_SupplierReturns_Code UNIQUE (ReturnCode),
    CONSTRAINT FK_SupplierReturns_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers (Id),
    CONSTRAINT FK_SupplierReturns_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT CK_SupplierReturns_Status CHECK (Status IN (0, 1, 2))
);

CREATE TABLE dbo.SupplierReturnItems (
    Id                bigint IDENTITY(1,1) NOT NULL,
    SupplierReturnId  bigint NOT NULL,
    ProductId         bigint NOT NULL,
    SerialItemId      bigint NULL,
    Quantity          int    NOT NULL,
    CONSTRAINT PK_SupplierReturnItems PRIMARY KEY (Id),
    CONSTRAINT FK_SupplierReturnItems_Returns FOREIGN KEY (SupplierReturnId) REFERENCES dbo.SupplierReturns (Id),
    CONSTRAINT FK_SupplierReturnItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
    CONSTRAINT FK_SupplierReturnItems_Serials FOREIGN KEY (SerialItemId) REFERENCES dbo.SerialItems (Id),
    CONSTRAINT CK_SupplierReturnItems_Qty CHECK (Quantity > 0)
);

CREATE TABLE dbo.SupplierLedger (
    Id                 bigint IDENTITY(1,1) NOT NULL,
    SupplierId         bigint        NOT NULL,
    PurchaseReceiptId  bigint        NULL,
    SupplierReturnId   bigint        NULL,
    EntryType          tinyint       NOT NULL,           -- 0 Payable, 1 Payment, 2 ReturnCredit
    Amount             decimal(18,0) NOT NULL,
    DueDate            date          NULL,
    CreatedAt          datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy          bigint        NULL,
    CONSTRAINT PK_SupplierLedger PRIMARY KEY (Id),
    CONSTRAINT FK_SupplierLedger_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers (Id),
    CONSTRAINT FK_SupplierLedger_Receipts FOREIGN KEY (PurchaseReceiptId) REFERENCES dbo.PurchaseReceipts (Id),
    CONSTRAINT FK_SupplierLedger_Returns FOREIGN KEY (SupplierReturnId) REFERENCES dbo.SupplierReturns (Id),
    CONSTRAINT CK_SupplierLedger_Type CHECK (EntryType IN (0, 1, 2))
);

/* =============================================================================
   9. MODULE BẢO HÀNH VÀ ĐỔI TRẢ (phần còn lại)
   ============================================================================= */

CREATE TABLE dbo.WarrantyTickets (
    Id                   bigint IDENTITY(1,1) NOT NULL,
    TicketCode           varchar(30)    NOT NULL,
    SerialItemId         bigint         NULL,            -- hàng có serial
    OrderItemId          bigint         NULL,            -- hàng không serial
    CustomerId           bigint         NOT NULL,
    BranchId             int            NOT NULL,
    Status               tinyint        NOT NULL DEFAULT 0,   -- 0 Received, 1 Diagnosing, 2 SentToVendor, 3 Repaired,
                                                              -- 4 Replaced, 5 Rejected, 6 ReturnedToCustomer
    IssueDescription     nvarchar(1000) NOT NULL,
    ConditionNotes       nvarchar(1000) NULL,
    RejectionReason      nvarchar(500)  NULL,
    ReplacementSerialId  bigint         NULL,
    VendorSentAt         datetime2(0)   NULL,
    VendorReturnedAt     datetime2(0)   NULL,
    ReceivedAt           datetime2(0)   NOT NULL DEFAULT SYSUTCDATETIME(),
    ReturnedAt           datetime2(0)   NULL,
    Fee                  decimal(18,0)  NOT NULL DEFAULT 0,
    CreatedAt            datetime2(0)   NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy            bigint         NULL,
    UpdatedAt            datetime2(0)   NULL,
    UpdatedBy            bigint         NULL,
    CONSTRAINT PK_WarrantyTickets PRIMARY KEY (Id),
    CONSTRAINT UQ_WarrantyTickets_Code UNIQUE (TicketCode),
    CONSTRAINT FK_WarrantyTickets_Serials FOREIGN KEY (SerialItemId) REFERENCES dbo.SerialItems (Id),
    CONSTRAINT FK_WarrantyTickets_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems (Id),
    CONSTRAINT FK_WarrantyTickets_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
    CONSTRAINT FK_WarrantyTickets_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id),
    CONSTRAINT FK_WarrantyTickets_Replacement FOREIGN KEY (ReplacementSerialId) REFERENCES dbo.SerialItems (Id),
    CONSTRAINT CK_WarrantyTickets_Status CHECK (Status BETWEEN 0 AND 6),
    CONSTRAINT CK_WarrantyTickets_Target CHECK (SerialItemId IS NOT NULL OR OrderItemId IS NOT NULL)
);

CREATE TABLE dbo.WarrantyTicketHistory (
    Id          bigint IDENTITY(1,1) NOT NULL,
    TicketId    bigint        NOT NULL,
    FromStatus  tinyint       NULL,
    ToStatus    tinyint       NOT NULL,
    Note        nvarchar(500) NULL,
    ChangedBy   bigint        NULL,
    ChangedAt   datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_WarrantyTicketHistory PRIMARY KEY (Id),
    CONSTRAINT FK_WarrantyTicketHistory_Tickets FOREIGN KEY (TicketId) REFERENCES dbo.WarrantyTickets (Id),
    CONSTRAINT FK_WarrantyTicketHistory_Users FOREIGN KEY (ChangedBy) REFERENCES dbo.Users (Id)
);

CREATE TABLE dbo.WarrantyTicketAttachments (
    Id        bigint IDENTITY(1,1) NOT NULL,
    TicketId  bigint       NOT NULL,
    Url       varchar(500) NOT NULL,
    CONSTRAINT PK_WarrantyTicketAttachments PRIMARY KEY (Id),
    CONSTRAINT FK_WarrantyTicketAttachments_Tickets FOREIGN KEY (TicketId) REFERENCES dbo.WarrantyTickets (Id)
);

CREATE TABLE dbo.ReturnItems (
    Id                bigint IDENTITY(1,1) NOT NULL,
    ReturnRequestId   bigint  NOT NULL,
    OrderItemId       bigint  NOT NULL,
    SerialItemId      bigint  NULL,
    Quantity          int     NOT NULL,
    InspectionResult  tinyint NOT NULL DEFAULT 0,        -- 0 Pending, 1 Pass, 2 Fail
    CONSTRAINT PK_ReturnItems PRIMARY KEY (Id),
    CONSTRAINT FK_ReturnItems_Requests FOREIGN KEY (ReturnRequestId) REFERENCES dbo.ReturnRequests (Id),
    CONSTRAINT FK_ReturnItems_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems (Id),
    CONSTRAINT FK_ReturnItems_Serials FOREIGN KEY (SerialItemId) REFERENCES dbo.SerialItems (Id),
    CONSTRAINT CK_ReturnItems_Qty CHECK (Quantity > 0),
    CONSTRAINT CK_ReturnItems_Inspection CHECK (InspectionResult IN (0, 1, 2))
);

/* =============================================================================
   10. MODULE HOA HỒNG, NHẬT KÝ
   ============================================================================= */

CREATE TABLE dbo.CommissionRules (
    Id                int IDENTITY(1,1) NOT NULL,
    Name              nvarchar(150) NOT NULL,
    ScopeType         tinyint       NOT NULL,            -- 0 All, 1 Category, 2 Product
    ScopeId           bigint        NULL,
    BasisType         tinyint       NOT NULL,            -- 0 Revenue, 1 GrossProfit
    RatePercent       decimal(5,2)  NOT NULL,
    TargetAmount      decimal(18,0) NULL,
    BonusRatePercent  decimal(5,2)  NULL,
    EffectiveFrom     date          NOT NULL,
    EffectiveTo       date          NULL,
    CreatedAt         datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy         bigint        NULL,
    UpdatedAt         datetime2(0)  NULL,
    UpdatedBy         bigint        NULL,
    CONSTRAINT PK_CommissionRules PRIMARY KEY (Id),
    CONSTRAINT CK_CommissionRules_Scope CHECK (ScopeType IN (0, 1, 2)),
    CONSTRAINT CK_CommissionRules_Basis CHECK (BasisType IN (0, 1)),
    CONSTRAINT CK_CommissionRules_Rate CHECK (RatePercent BETWEEN 0 AND 100)
);

CREATE TABLE dbo.CommissionPeriods (
    Id          int IDENTITY(1,1) NOT NULL,
    YearMonth   char(7)      NOT NULL,                   -- 2026-11
    Status      tinyint      NOT NULL DEFAULT 0,         -- 0 Open, 1 Closed
    ClosedBy    bigint       NULL,
    ClosedAt    datetime2(0) NULL,
    CONSTRAINT PK_CommissionPeriods PRIMARY KEY (Id),
    CONSTRAINT UQ_CommissionPeriods_YearMonth UNIQUE (YearMonth),
    CONSTRAINT FK_CommissionPeriods_ClosedBy FOREIGN KEY (ClosedBy) REFERENCES dbo.Users (Id),
    CONSTRAINT CK_CommissionPeriods_Status CHECK (Status IN (0, 1))
);

CREATE TABLE dbo.CommissionEntries (
    Id               bigint IDENTITY(1,1) NOT NULL,
    PeriodId         int           NOT NULL,
    EmployeeId       bigint        NOT NULL,
    OrderId          bigint        NOT NULL,
    ReturnRequestId  bigint        NULL,
    RuleId           int           NOT NULL,
    EntryType        tinyint       NOT NULL,             -- 0 Earn, 1 Clawback, 2 Adjustment
    BaseAmount       decimal(18,0) NOT NULL,
    RatePercent      decimal(5,2)  NOT NULL,
    Amount           decimal(18,0) NOT NULL,             -- âm với Clawback
    CreatedAt        datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        bigint        NULL,
    CONSTRAINT PK_CommissionEntries PRIMARY KEY (Id),
    CONSTRAINT FK_CommissionEntries_Periods FOREIGN KEY (PeriodId) REFERENCES dbo.CommissionPeriods (Id),
    CONSTRAINT FK_CommissionEntries_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees (Id),
    CONSTRAINT FK_CommissionEntries_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT FK_CommissionEntries_Returns FOREIGN KEY (ReturnRequestId) REFERENCES dbo.ReturnRequests (Id),
    CONSTRAINT FK_CommissionEntries_Rules FOREIGN KEY (RuleId) REFERENCES dbo.CommissionRules (Id),
    CONSTRAINT CK_CommissionEntries_Type CHECK (EntryType IN (0, 1, 2))
);

CREATE TABLE dbo.AuditLogs (                             -- chỉ INSERT; thu hồi quyền UPDATE/DELETE (xem cuối script)
    Id          bigint IDENTITY(1,1) NOT NULL,
    UserId      bigint        NULL,
    BranchId    int           NULL,
    [Action]    varchar(60)   NOT NULL,                  -- PriceChanged, OrderCancelled, StockAdjusted, SerialUnassigned...
    EntityType  varchar(50)   NOT NULL,
    EntityId    bigint        NOT NULL,
    OldValues   nvarchar(max) NULL,                      -- JSON
    NewValues   nvarchar(max) NULL,                      -- JSON
    Reason      nvarchar(300) NULL,
    IpAddress   varchar(45)   NULL,
    UserAgent   nvarchar(300) NULL,
    CreatedAt   datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLogs PRIMARY KEY (Id),
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_AuditLogs_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (Id)
);

/* =============================================================================
   11. UNIQUE INDEX CÓ ĐIỀU KIỆN (filtered)
   ============================================================================= */

CREATE UNIQUE INDEX UX_Users_Email              ON dbo.Users (Email)                 WHERE Email IS NOT NULL;
CREATE UNIQUE INDEX UX_Customers_UserId         ON dbo.Customers (UserId)            WHERE UserId IS NOT NULL;
CREATE UNIQUE INDEX UX_CustomerGroups_Default   ON dbo.CustomerGroups (IsDefault)    WHERE IsDefault = 1;
CREATE UNIQUE INDEX UX_CustomerAddresses_Default ON dbo.CustomerAddresses (CustomerId) WHERE IsDefault = 1;
CREATE UNIQUE INDEX UX_Carts_CustomerId         ON dbo.Carts (CustomerId)            WHERE CustomerId IS NOT NULL;
CREATE UNIQUE INDEX UX_Carts_SessionToken       ON dbo.Carts (SessionToken)          WHERE SessionToken IS NOT NULL;
CREATE UNIQUE INDEX UX_Payments_Provider_Txn    ON dbo.Payments (Provider, ProviderTransactionId) WHERE ProviderTransactionId IS NOT NULL;
CREATE UNIQUE INDEX UX_ReturnItems_Serial       ON dbo.ReturnItems (ReturnRequestId, SerialItemId) WHERE SerialItemId IS NOT NULL;
CREATE UNIQUE INDEX UX_Attendances_Schedule     ON dbo.Attendances (EmployeeId, ScheduleId) WHERE ScheduleId IS NOT NULL;
CREATE UNIQUE INDEX UX_CommissionEntries_Earn   ON dbo.CommissionEntries (EmployeeId, OrderId) WHERE EntryType = 0;

/* =============================================================================
   12. INDEX HỖ TRỢ TRUY VẤN
   ============================================================================= */

-- Danh mục, lọc theo thông số
CREATE INDEX IX_Products_Category_Status ON dbo.Products (CategoryId, Status) INCLUDE (BrandId, ListPrice, Name);
CREATE INDEX IX_Products_Brand           ON dbo.Products (BrandId);
CREATE INDEX IX_Products_ListPrice       ON dbo.Products (ListPrice);
CREATE INDEX IX_Specs_CPU_Socket         ON dbo.Specs_CPU (Socket);
CREATE INDEX IX_Specs_CPU_Cores          ON dbo.Specs_CPU (Cores);
CREATE INDEX IX_Specs_RAM_Filter         ON dbo.Specs_RAM (MemoryType, CapacityGbPerKit, BusMhz);
CREATE INDEX IX_Specs_Storage_Filter     ON dbo.Specs_Storage (StorageType, CapacityGb);
CREATE INDEX IX_Specs_Monitor_Filter     ON dbo.Specs_Monitor (SizeInch, Resolution, RefreshRateHz);
CREATE INDEX IX_Specs_Mainboard_Filter   ON dbo.Specs_Mainboard (Socket, MemoryType);

-- Giá, khuyến mãi, giỏ hàng
CREATE INDEX IX_Promotions_Scope         ON dbo.Promotions (ScopeType, ScopeId, StartAt, EndAt) WHERE IsActive = 1;
CREATE INDEX IX_CartItems_Product        ON dbo.CartItems (ProductId);

-- Đơn hàng, thanh toán
CREATE INDEX IX_Orders_Customer          ON dbo.Orders (CustomerId, CreatedAt DESC);
CREATE INDEX IX_Orders_Branch_Status     ON dbo.Orders (BranchId, Status, CreatedAt DESC);
CREATE INDEX IX_Orders_Expires           ON dbo.Orders (Status, ExpiresAt) WHERE ExpiresAt IS NOT NULL AND Status IN (0, 4);
CREATE INDEX IX_Orders_Completed         ON dbo.Orders (CompletedAt) INCLUDE (BranchId, GrandTotal) WHERE Status = 7;
CREATE INDEX IX_OrderItems_Product       ON dbo.OrderItems (ProductId);
CREATE INDEX IX_OrderStatusHistory_Order ON dbo.OrderStatusHistory (OrderId, ChangedAt);
CREATE INDEX IX_OrderSalesStaff_Employee ON dbo.OrderSalesStaff (EmployeeId);
CREATE INDEX IX_Payments_Order           ON dbo.Payments (OrderId);
CREATE INDEX IX_CustomerLedger_Customer  ON dbo.CustomerLedger (CustomerId, CreatedAt);

-- Kho, serial
CREATE INDEX IX_StockMovements_Stock     ON dbo.StockMovements (BranchId, ProductId, CreatedAt DESC);
CREATE INDEX IX_StockMovements_Ref       ON dbo.StockMovements (RefType, RefId);
CREATE INDEX IX_SerialItems_Stock        ON dbo.SerialItems (ProductId, BranchId, Status);
CREATE INDEX IX_SerialItems_OrderItem    ON dbo.SerialItems (OrderItemId) WHERE OrderItemId IS NOT NULL;
CREATE INDEX IX_SerialItems_WarrantyEnd  ON dbo.SerialItems (WarrantyEndDate) WHERE Status = 3;
CREATE INDEX IX_SerialStatusHistory      ON dbo.SerialStatusHistory (SerialItemId, ChangedAt);
CREATE INDEX IX_PurchaseReceipts_Supplier ON dbo.PurchaseReceipts (SupplierId, ReceivedAt);
CREATE INDEX IX_SupplierLedger_Supplier  ON dbo.SupplierLedger (SupplierId, DueDate);

-- Bảo hành, đổi trả
CREATE INDEX IX_WarrantyTickets_Serial   ON dbo.WarrantyTickets (SerialItemId);
CREATE INDEX IX_WarrantyTickets_Branch   ON dbo.WarrantyTickets (BranchId, Status, ReceivedAt DESC);
CREATE INDEX IX_WarrantyTickets_Customer ON dbo.WarrantyTickets (CustomerId);
CREATE INDEX IX_ReturnRequests_Order     ON dbo.ReturnRequests (OrderId);
CREATE INDEX IX_ReturnRequests_Branch    ON dbo.ReturnRequests (BranchId, Status);

-- Nhân sự, hoa hồng, nhật ký
CREATE INDEX IX_Employees_Branch         ON dbo.Employees (BranchId, Status);
CREATE INDEX IX_EmployeeSchedules_Branch ON dbo.EmployeeSchedules (BranchId, WorkDate);
CREATE INDEX IX_Attendances_Employee     ON dbo.Attendances (EmployeeId, WorkDate);
CREATE INDEX IX_CommissionEntries_Period ON dbo.CommissionEntries (PeriodId, EmployeeId);
CREATE INDEX IX_AuditLogs_Entity         ON dbo.AuditLogs (EntityType, EntityId, CreatedAt DESC);
CREATE INDEX IX_AuditLogs_User           ON dbo.AuditLogs (UserId, CreatedAt DESC);
CREATE INDEX IX_AuditLogs_Branch         ON dbo.AuditLogs (BranchId, CreatedAt DESC);

/* =============================================================================
   13. DỮ LIỆU KHỞI TẠO (seed)
   ============================================================================= */

INSERT INTO dbo.Roles (Code, Name) VALUES
    ('Customer',    N'Khách hàng'),
    ('Cashier',     N'Thu ngân'),
    ('TechAdvisor', N'Tư vấn kỹ thuật'),
    ('Storekeeper', N'Thủ kho'),
    ('Manager',     N'Quản lý chi nhánh'),
    ('Admin',       N'Quản trị viên');

INSERT INTO dbo.CustomerGroups (Code, Name, IsDefault) VALUES
    ('RETAIL',    N'Khách lẻ', 1),
    ('WHOLESALE', N'Khách sỉ (đại lý)', 0);

INSERT INTO dbo.Shifts (Code, Name, StartTime, EndTime) VALUES
    ('MORNING',   N'Ca sáng',   '08:00', '12:00'),
    ('AFTERNOON', N'Ca chiều',  '13:00', '18:00'),
    ('FULL',      N'Full-time', '08:00', '18:00');

INSERT INTO dbo.Categories (Code, Name, SpecType) VALUES
    ('CPU',        N'CPU',                1),
    ('RAM',        N'RAM',                2),
    ('STORAGE',    N'Ổ cứng (SSD/HDD)',   3),
    ('MONITOR',    N'Màn hình',           4),
    ('MAINBOARD',  N'Bo mạch chủ',        5),
    ('PSU',        N'Nguồn máy tính',     6);

INSERT INTO dbo.SystemSettings ([Key], [Value], Description) VALUES
    ('hold.gateway.minutes',  '30', N'Thời hạn giữ hàng với đơn thanh toán qua cổng (phút)'),
    ('hold.transfer.hours',   '24', N'Thời hạn giữ hàng với đơn chuyển khoản (giờ)'),
    ('pickup.hold.days',      '3',  N'Số ngày giữ hàng chờ khách nhận tại cửa hàng'),
    ('pending.cod.alert.hours','24', N'Nhắc xử lý đơn COD chưa xác nhận sau (giờ)'),
    ('return.window.days',    '7',  N'Hạn đổi trả kể từ ngày giao (ngày)'),
    ('compat.psu.tdp.ratio',  '0.8', N'Ngưỡng cảnh báo tổng TDP so với công suất nguồn');

COMMIT TRANSACTION;
PRINT N'Tạo bảng, chỉ mục và dữ liệu khởi tạo thành công.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'Có lỗi, đã rollback toàn bộ. Chi tiết:';
    THROW;
END CATCH;

/* =============================================================================
   14. (TÙY CHỌN) CHẶN SỬA/XÓA NHẬT KÝ - chạy riêng sau khi tạo user ứng dụng
   -----------------------------------------------------------------------------
   Thay [app_user] bằng tên user/role mà ứng dụng .NET dùng để kết nối.

   DENY UPDATE, DELETE ON dbo.AuditLogs      TO [app_user];
   DENY UPDATE, DELETE ON dbo.StockMovements TO [app_user];
   ============================================================================= */