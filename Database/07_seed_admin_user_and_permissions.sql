/* =============================================================================
   SEED BỔ SUNG — CHẠY SAU 6 FILE SQL GỐC (table_and_data.sql → 06_...)
   -----------------------------------------------------------------------------
   VÌ SAO CẦN FILE NÀY:
   1. table_and_data.sql CHƯA seed bảng Permissions / RolePermissions
      → mọi endpoint dùng [RequirePermission] sẽ trả 403 cho tất cả người dùng.
   2. table_and_data.sql CHƯA seed bảng Branches và Users
      → không có tài khoản nào để đăng nhập/kiểm thử.

   TÀI KHOẢN ĐƯỢC TẠO (chỉ để kiểm thử — ĐỔI MẬT KHẨU trước khi dùng thật):
      Username : admin
      Password : Admin@123456
      Vai trò  : Admin (toàn quyền) + nhân viên chi nhánh CN01

   Mật khẩu lưu dạng PBKDF2-HMACSHA256 (210.000 vòng, salt 16 byte) — khớp
   chính xác Pbkdf2PasswordHasher của API (Infrastructure/Security/Pbkdf2PasswordHasher.cs).

   Script chạy lại nhiều lần đều an toàn (đã chống trùng bằng IF NOT EXISTS).
   ============================================================================= */

SET NOCOUNT ON;
GO

/* =============================================================================
   1. QUYỀN (dbo.Permissions) — mã theo ma trận BRD 2.2
   ============================================================================= */
INSERT INTO dbo.Permissions (Code)
SELECT v.Code
FROM (VALUES
    ('catalog.manage'),        ('price.manage'),          ('promotion.manage'),
    ('order.create'),          ('order.confirm'),         ('order.pack'),
    ('order.ship'),            ('order.cancel.request'),  ('order.cancel.approve'),
    ('stock.receipt'),         ('stock.transfer'),        ('stock.take'),
    ('stock.adjust.approve'),  ('serial.manage'),
    ('warranty.receive'),      ('warranty.process'),
    ('return.request'),        ('return.review'),         ('return.inspect'),
    ('return.refund'),
    ('hr.schedule.manage'),    ('hr.attendance.adjust'),
    ('commission.manage'),     ('audit.view'),            ('settings.manage')
) AS v(Code)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.Code = v.Code);
GO

/* =============================================================================
   2. GÁN QUYỀN CHO VAI TRÒ (dbo.RolePermissions)
   ============================================================================= */

-- Admin: toàn quyền
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM dbo.Roles r
CROSS JOIN dbo.Permissions p
WHERE r.Code = 'Admin'
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO

-- Manager: nhóm duyệt trong phạm vi chi nhánh
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM dbo.Roles r
JOIN dbo.Permissions p ON p.Code IN
    ('order.cancel.approve', 'stock.adjust.approve', 'return.review', 'return.refund',
     'warranty.process', 'hr.schedule.manage', 'hr.attendance.adjust', 'audit.view')
WHERE r.Code = 'Manager'
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO

-- Cashier (thu ngân): bán tại quầy, xác nhận/giao hàng, đề xuất hủy đơn
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM dbo.Roles r
JOIN dbo.Permissions p ON p.Code IN
    ('order.create', 'order.confirm', 'order.ship', 'order.cancel.request',
     'return.request', 'warranty.receive')
WHERE r.Code = 'Cashier'
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO

-- TechAdvisor (tư vấn kỹ thuật): tạo đơn, tiếp nhận bảo hành, đề xuất hủy đơn
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM dbo.Roles r
JOIN dbo.Permissions p ON p.Code IN
    ('order.create', 'order.confirm', 'order.cancel.request', 'warranty.receive', 'return.request')
WHERE r.Code = 'TechAdvisor'
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO

-- Storekeeper (thủ kho): nhập/xuất/chuyển/kiểm kê kho, serial, đóng gói, kiểm tra hàng trả
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM dbo.Roles r
JOIN dbo.Permissions p ON p.Code IN
    ('stock.receipt', 'stock.transfer', 'stock.take', 'serial.manage', 'order.pack',
     'warranty.process', 'return.inspect')
WHERE r.Code = 'Storekeeper'
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO

-- Customer (khách hàng): chỉ được tạo yêu cầu đổi trả
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM dbo.Roles r
JOIN dbo.Permissions p ON p.Code IN ('return.request')
WHERE r.Code = 'Customer'
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO

/* =============================================================================
   3. CHI NHÁNH (dbo.Branches) — bắt buộc phải có trước khi tạo nhân viên
   ============================================================================= */
IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = 'CN01')
BEGIN
    INSERT INTO dbo.Branches (Code, Name, Address, Phone)
    VALUES ('CN01', N'Chi nhánh trung tâm', N'(cập nhật địa chỉ thật)', '0900000000');

    PRINT N'Đã tạo chi nhánh CN01.';
END
ELSE
    PRINT N'Chi nhánh CN01 đã tồn tại, bỏ qua.';
GO

/* =============================================================================
   4. TÀI KHOẢN ADMIN ĐỂ KIỂM THỬ (dbo.Users + UserRoles + Employees)
   ============================================================================= */
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
BEGIN
    DECLARE @PasswordHash varchar(255) =
        'pbkdf2-sha256$210000$hlnfU2qCPwhN68cUa+lGuQ==$eSfE5SD9qI3i1hIcK88lT0kJzRO+rRJ9vexkWquClOk=';

    DECLARE @BranchId int = (SELECT TOP 1 Id FROM dbo.Branches ORDER BY Id);
    DECLARE @AdminRoleId int = (SELECT Id FROM dbo.Roles WHERE Code = 'Admin');
    DECLARE @UserId bigint;

    IF @BranchId IS NULL OR @AdminRoleId IS NULL
    BEGIN
        RAISERROR (N'Thiếu Branches hoặc Roles — hãy chạy Database/table_and_data.sql trước.', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.Users (Username, Email, Phone, PasswordHash, UserType, IsActive)
    VALUES ('admin', 'admin@siliconhorizon.local', '0900000001', @PasswordHash, 1, 1);

    SET @UserId = CAST(SCOPE_IDENTITY() AS bigint);

    INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @AdminRoleId);

    -- Position = 3 (Manager) là chức vụ gần nhất trong bảng Employees; quyền Admin nằm ở vai trò Admin.
    INSERT INTO dbo.Employees (EmployeeCode, UserId, FullName, Phone, Position, BranchId, Status, HireDate)
    VALUES ('NV0001', @UserId, N'Quản trị viên hệ thống', '0900000001', 3, @BranchId, 0, CAST(GETDATE() AS date));

    PRINT N'Đã tạo tài khoản admin. Mật khẩu tạm: Admin@123456 — ĐỔI trước khi dùng thật.';
END
ELSE
    PRINT N'Tài khoản admin đã tồn tại, bỏ qua.';
GO

/* =============================================================================
   5. KIỂM TRA KẾT QUẢ
   ============================================================================= */
PRINT N'--- Kiểm tra ---';

SELECT  u.Id AS UserId, u.Username, u.UserType, u.IsActive,
        COUNT(ur.RoleId) AS SoVaiTro, COUNT(rp.PermissionId) AS SoQuyen
FROM dbo.Users u
LEFT JOIN dbo.UserRoles ur       ON ur.UserId = u.Id
LEFT JOIN dbo.RolePermissions rp ON rp.RoleId = ur.RoleId
WHERE u.Username = 'admin'
GROUP BY u.Id, u.Username, u.UserType, u.IsActive;

SELECT COUNT(*) AS TongSoQuyen FROM dbo.Permissions;
SELECT COUNT(*) AS TongSoGanQuyen FROM dbo.RolePermissions;
GO
