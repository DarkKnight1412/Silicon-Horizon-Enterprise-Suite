using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case quy tắc tương thích linh kiện — CRUD bằng EF Core trên <c>dbo.CompatibilityRules</c>
/// (Database/README.md mục 10: <c>/api/admin/compatibility-rules</c>; nghiệp vụ PRD-11).
/// </summary>
public interface ICompatibilityRuleService
{
    /// <summary>GET /api/v1/admin/compatibility-rules — danh sách quy tắc, có phân trang và lọc.</summary>
    Task<PagedResult<CompatibilityRuleDto>> GetPagedAsync(
        CompatibilityRuleListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/compatibility-rules/{id} — chi tiết quy tắc; không tồn tại → 404.</summary>
    Task<CompatibilityRuleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/compatibility-rules — tạo quy tắc.
    /// <c>operator</c> chỉ nhận EQ/LTE/GTE (<c>CK_CompatibilityRules_Operator</c>), <c>severity</c> chỉ nhận 0/1;
    /// hai danh mục phải tồn tại → nếu không trả 400.
    /// </summary>
    Task<CompatibilityRuleDto> CreateAsync(
        CreateCompatibilityRuleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/compatibility-rules/{id} — cập nhật quy tắc (cùng ràng buộc như khi tạo).</summary>
    Task<CompatibilityRuleDto> UpdateAsync(
        int id,
        UpdateCompatibilityRuleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCH /api/v1/admin/compatibility-rules/{id}/status — tắt/bật quy tắc.
    /// Không xóa cứng để giữ lịch sử cấu hình đã dùng khi kiểm tra tương thích.
    /// </summary>
    Task SetStatusAsync(
        int id,
        SetCompatibilityRuleStatusRequest request,
        CancellationToken cancellationToken = default);
}
