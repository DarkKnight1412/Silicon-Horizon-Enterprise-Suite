using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case danh mục ca làm việc — CRUD bằng EF Core trên <c>dbo.Shifts</c>
/// (Database/README.md mục 10: <c>/api/admin/shifts</c>).
/// </summary>
/// <remarks>
/// Danh mục ca là dữ liệu chủ cho <c>usp_Schedule_Assign</c> (xếp ca nhân viên, HR-04):
/// proc từ chối <c>@ShiftId</c> không tồn tại, nên ca phải được tạo trước khi xếp ca.
/// Vì bảng <c>Shifts</c> không có cột trạng thái, API không cung cấp endpoint DELETE
/// (xóa ca sẽ phá khóa ngoại từ <c>EmployeeSchedules</c>).
/// </remarks>
public interface IShiftService
{
    /// <summary>GET /api/v1/admin/shifts — danh sách ca làm, có phân trang và lọc theo mã/tên.</summary>
    Task<PagedResult<ShiftDto>> GetPagedAsync(ShiftListQuery query, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/shifts/{id} — chi tiết ca; không tồn tại → 404.</summary>
    Task<ShiftDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/shifts — tạo ca. Mã duy nhất (<c>UQ_Shifts_Code</c>) → trùng trả 409;
    /// <c>endTime</c> phải sau <c>startTime</c> → sai trả 400.
    /// </summary>
    Task<ShiftDto> CreateAsync(CreateShiftRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/shifts/{id} — cập nhật tên và khung giờ (không đổi mã).</summary>
    Task<ShiftDto> UpdateAsync(int id, UpdateShiftRequest request, CancellationToken cancellationToken = default);
}
