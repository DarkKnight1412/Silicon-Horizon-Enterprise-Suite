using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case quy tắc hoa hồng — CRUD bằng EF Core trên <c>dbo.CommissionRules</c>
/// (Database/README.md mục 10: <c>/api/admin/commission-rules</c>; nghiệp vụ KPI-01..03).
/// </summary>
/// <remarks>
/// Module Commission hiện có chỉ xử lý kỳ/summary/điều chỉnh/chốt kỳ (<c>usp_Commission_*</c>);
/// danh mục quy tắc là bảng cấu hình nên nằm trong phạm vi CRUD quản trị này.
/// </remarks>
public interface ICommissionRuleService
{
    /// <summary>GET /api/v1/admin/commission-rules — danh sách quy tắc, có phân trang và lọc.</summary>
    Task<PagedResult<CommissionRuleDto>> GetPagedAsync(
        CommissionRuleListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/commission-rules/{id} — chi tiết quy tắc; không tồn tại → 404.</summary>
    Task<CommissionRuleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/commission-rules — tạo quy tắc.
    /// <c>ratePercent</c> trong 0..100 (<c>CK_CommissionRules_Rate</c>), <c>scopeType</c> 0..2,
    /// <c>basisType</c> 0..1; phạm vi danh mục/sản phẩm phải tồn tại → nếu không trả 400.
    /// </summary>
    Task<CommissionRuleDto> CreateAsync(
        CreateCommissionRuleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/commission-rules/{id} — cập nhật quy tắc (cùng ràng buộc như khi tạo).</summary>
    Task<CommissionRuleDto> UpdateAsync(
        int id,
        UpdateCommissionRuleRequest request,
        CancellationToken cancellationToken = default);
}
