using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case quyền — CRUD bằng EF Core trên <c>dbo.Permissions</c>
/// (Database/README.md mục 10: <c>/api/admin/permissions</c>). Bảng chỉ có <c>Id</c> và <c>Code</c>.
/// </summary>
public interface IPermissionService
{
    /// <summary>GET /api/v1/admin/permissions — danh sách quyền, có phân trang và lọc theo mã.</summary>
    Task<PagedResult<PermissionDto>> GetPagedAsync(
        PermissionListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/permissions/{id} — chi tiết quyền; không tồn tại → 404.</summary>
    Task<PermissionDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v1/admin/permissions — tạo quyền; <c>code</c> duy nhất (<c>UQ_Permissions_Code</c>) → trùng 409.</summary>
    Task<PermissionDto> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/permissions/{id} — sửa mã quyền (mã phải duy nhất).</summary>
    Task<PermissionDto> UpdateAsync(int id, UpdatePermissionRequest request, CancellationToken cancellationToken = default);
}
