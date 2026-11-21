using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case chi nhánh — CRUD bằng EF Core trên bảng cấu hình <c>dbo.Branches</c>
/// (Database/README.md mục 10: <c>/api/admin/branches</c>).
/// </summary>
public interface IBranchService
{
    /// <summary>GET /api/v1/admin/branches — danh sách chi nhánh, có phân trang và lọc.</summary>
    Task<PagedResult<BranchDto>> GetPagedAsync(BranchListQuery query, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/branches/{id} — chi tiết chi nhánh; không tồn tại → 404.</summary>
    Task<BranchDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/branches — tạo chi nhánh.
    /// Mã duy nhất (<c>UQ_Branches_Code</c>) → trùng trả 409.
    /// </summary>
    Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/branches/{id} — cập nhật tên/địa chỉ/điện thoại (không đổi mã).</summary>
    Task<BranchDto> UpdateAsync(int id, UpdateBranchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCH /api/v1/admin/branches/{id}/status — ngừng/cho hoạt động lại chi nhánh.
    /// Không xóa cứng vì chi nhánh đã gắn với nhân viên, tồn kho, đơn hàng, ca làm.
    /// </summary>
    Task SetStatusAsync(int id, SetBranchStatusRequest request, CancellationToken cancellationToken = default);
}
