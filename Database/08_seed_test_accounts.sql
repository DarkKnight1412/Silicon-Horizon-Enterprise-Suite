/* =============================================================================
   08 — BỘ TÀI KHOẢN KIỂM THỬ (mỗi vai trò 1 tài khoản)
   -----------------------------------------------------------------------------
   Chạy SAU `table_and_data.sql` … `07_seed_admin_user_and_permissions.sql`
   (và KHÔNG tạo database mới — dùng đúng DB hiện có, ví dụ BRD_DEV).

   Danh sách tài khoản: xem file TAI_KHOAN_KIEM_THU.md ở thư mục gốc repo.
   Mật khẩu lưu dạng PBKDF2-HMACSHA256 (210.000 vòng, salt 16 byte) — khớp
   chính xác Pbkdf2PasswordHasher của API.

   Script chạy lại nhiều lần đều an toàn (IF NOT EXISTS).
   ============================================================================= */

SET NOCOUNT ON;
GO

-- Chi nhánh cho nhân viên (nếu chưa có)
IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = 'CN01')
BEGIN
    INSERT INTO dbo.Branches (Code, Name, Address, Phone)
    VALUES ('CN01', N'Chi nhánh trung tâm', N'(cập nhật địa chỉ thật)', '0900000000');
    PRINT N'Đã tạo chi nhánh CN01.';
END
GO

DECLARE @Seed TABLE
(
    Username     varchar(50)   NOT NULL,
    PasswordHash varchar(255)  NOT NULL,
    UserType     tinyint       NOT NULL,   -- 0 Khách hàng, 1 Nhân viên
    RoleCode     varchar(50)   NOT NULL,
    FullName     nvarchar(150) NOT NULL,
    Phone        varchar(20)   NOT NULL,
    Position     tinyint       NULL,       -- 0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager
    EmployeeCode varchar(20)   NULL
);

INSERT INTO @Seed (Username, PasswordHash, UserType, RoleCode, FullName, Phone, Position, EmployeeCode) VALUES
('admin',    'pbkdf2-sha256$210000$8eDpEXmo41C9bP7bwIZG1g==$XAPGyz9yblwS/2U91KJNp83j4+RnbsBSvAOccXeWTT0=', 1, 'Admin',        N'Quản trị viên hệ thống', '0900000001', 3, 'NV0001'),
('manager',  'pbkdf2-sha256$210000$Ach5aPQ+4HHDOwuVgzjdiA==$lb1ClH3bNnSKPcGoBH3f8apemWhx6BXihtgJZtviKlk=', 1, 'Manager',      N'Quản lý chi nhánh',      '0900000002', 3, 'NV0002'),
('cashier',  'pbkdf2-sha256$210000$raGqWNHoqt33H9fTbvltJA==$W55YKwvcjgmyeY6sZLqzU8HZCcx/9SAnB8ICkPLDfis=', 1, 'Cashier',      N'Thu ngân',               '0900000003', 0, 'NV0003'),
('tech',     'pbkdf2-sha256$210000$Zf/8IqoMjPeHFFHcIc7AEA==$bs31YG5/3IEfkhw5rPHOqcs3rJasUfeSb9Ag500CY7g=', 1, 'TechAdvisor',  N'Tư vấn kỹ thuật',        '0900000004', 1, 'NV0004'),
('store',    'pbkdf2-sha256$210000$NbErOhxRHoi+aM2N/uTewQ==$Ygjq7csS9rK94SsjRRQmRONPbBYUifxJIDdlF9mrBuY=', 1, 'Storekeeper',  N'Thủ kho',                '0900000005', 2, 'NV0005'),
('customer', 'pbkdf2-sha256$210000$bUQcy0LR8Hhz0fLIm1dhoA==$zQFFnI+ISLmivWpFwegX212wBe7YPlO5YSMtCk5U8Dw=', 0, 'Customer',     N'Khách hàng kiểm thử',   '0900000006', NULL, NULL);

DECLARE @Username varchar(50), @PasswordHash varchar(255), @UserType tinyint, @RoleCode varchar(50),
        @FullName nvarchar(150), @Phone varchar(20), @Position tinyint, @EmployeeCode varchar(20);

DECLARE seed_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Username, PasswordHash, UserType, RoleCode, FullName, Phone, Position, EmployeeCode FROM @Seed;

OPEN seed_cursor;
FETCH NEXT FROM seed_cursor INTO @Username, @PasswordHash, @UserType, @RoleCode, @FullName, @Phone, @Position, @EmployeeCode;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username)
    BEGIN
        DECLARE @UserId bigint;
        DECLARE @Email varchar(150) = @Username + '@siliconhorizon.local';

        INSERT INTO dbo.Users (Username, Email, Phone, PasswordHash, UserType, IsActive)
        VALUES (@Username, @Email, @Phone, @PasswordHash, @UserType, 1);

        SET @UserId = CAST(SCOPE_IDENTITY() AS bigint);

        INSERT INTO dbo.UserRoles (UserId, RoleId)
        SELECT @UserId, Id FROM dbo.Roles WHERE Code = @RoleCode;

        IF @UserType = 1
        BEGIN
            INSERT INTO dbo.Employees (EmployeeCode, UserId, FullName, Phone, Position, BranchId, Status, HireDate)
            SELECT @EmployeeCode, @UserId, @FullName, @Phone, @Position, Id, 0, CAST(GETDATE() AS date)
            FROM dbo.Branches WHERE Code = 'CN01';
        END
        ELSE
        BEGIN
            INSERT INTO dbo.Customers (UserId, FullName, Phone, Email, CustomerGroupId)
            SELECT @UserId, @FullName, @Phone, @Email, Id
            FROM dbo.CustomerGroups WHERE IsDefault = 1;
        END

        PRINT N'Đã tạo tài khoản: ' + @Username + N' (vai trò ' + @RoleCode + N')';
    END
    ELSE
    BEGIN
        PRINT N'Bỏ qua (đã tồn tại): ' + @Username;
    END

    FETCH NEXT FROM seed_cursor INTO @Username, @PasswordHash, @UserType, @RoleCode, @FullName, @Phone, @Position, @EmployeeCode;
END

CLOSE seed_cursor;
DEALLOCATE seed_cursor;
GO

/* Kiểm tra kết quả */
SELECT u.Id AS UserId, u.Username, u.UserType, u.IsActive,
       STUFF((SELECT ', ' + r.Code FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id = ur.RoleId WHERE ur.UserId = u.Id FOR XML PATH('')), 1, 2, '') AS VaiTro
FROM dbo.Users u
ORDER BY u.Id;
GO
