using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case nhân viên — CRUD bằng EF Core trên <c>dbo.Employees</c>
/// (Database/README.md mục 10: <c>/api/admin/employees</c>).
/// </summary>
/// <remarks>
/// Đúng ghi chú cuối mục 10 của Database/README.md: <i>"khi tạo/sửa nhân viên cần đồng bộ
/// <c>Users</c>, <c>UserRoles</c>, <c>Employees</c>"</i>. Mọi phương thức ghi đều chạy trong một giao dịch
/// của <c>IUnitOfWork</c> để ba bảng không lệch trạng thái; mật khẩu được băm bằng
/// <c>IPasswordHasher</c> (không bao giờ lưu/trả bản rõ — NFR-03).
/// </remarks>
public interface IEmployeeService
{
    /// <summary>GET /api/v1/admin/employees — danh sách nhân viên (kèm tài khoản, vai trò, chi nhánh).</summary>
    Task<PagedResult<EmployeeDto>> GetPagedAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/employees/{id} — chi tiết nhân viên; không tồn tại → 404.</summary>
    Task<EmployeeDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/employees — tạo nhân viên: thêm <c>Users</c> (UserType = Employee) →
    /// gán <c>UserRoles</c> theo chức vụ → thêm <c>Employees</c>, tất cả trong một giao dịch.
    /// <c>username</c> (<c>UQ_Users_Username</c>) và <c>employeeCode</c> (<c>UQ_Employees_Code</c>) phải duy nhất → trùng 409.
    /// </summary>
    Task<EmployeeDto> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PUT /api/v1/admin/employees/{id} — cập nhật hồ sơ và đồng bộ tài khoản/vai trò.
    /// Khi chức vụ đổi, vai trò thuộc nhóm chức vụ được thay tương ứng; các vai trò khác (ví dụ Admin) giữ nguyên.
    /// </summary>
    Task<EmployeeDto> UpdateAsync(
        long id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCH /api/v1/admin/employees/{id}/status — cho nghỉ việc / cho làm lại.
    /// Nghỉ việc sẽ ghi <c>terminatedDate</c> và khóa tài khoản đăng nhập; <b>không xóa cứng</b>
    /// vì nhân viên đã gắn với đơn hàng, ca làm, chấm công và hoa hồng.
    /// </summary>
    Task<EmployeeDto> SetStatusAsync(
        long id,
        SetEmployeeStatusRequest request,
        CancellationToken cancellationToken = default);
}
