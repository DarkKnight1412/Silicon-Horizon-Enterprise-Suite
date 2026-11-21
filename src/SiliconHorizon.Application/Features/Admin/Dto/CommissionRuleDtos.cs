using SiliconHorizon.Domain.Entities.Commission;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Quy tắc hoa hồng trả ra API — CRUD bằng EF Core trên <c>dbo.CommissionRules</c>
/// (Database/README.md mục 10: <c>/api/admin/commission-rules</c>; nghiệp vụ KPI-01..03).
/// </summary>
public class CommissionRuleDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>0 All (toàn bộ), 1 Category, 2 Product.</summary>
    public CommissionScopeType ScopeType { get; init; }

    /// <summary>CategoryId/ProductId khi <see cref="ScopeType"/> khác <c>All</c>.</summary>
    public long? ScopeId { get; init; }

    /// <summary>0 Revenue (doanh thu), 1 GrossProfit (lợi nhuận gộp).</summary>
    public CommissionBasisType BasisType { get; init; }

    /// <summary>Tỷ lệ hoa hồng (%) — <c>CK_CommissionRules_Rate</c> yêu cầu 0..100.</summary>
    public decimal RatePercent { get; init; }

    /// <summary>Chỉ tiêu để tính thưởng vượt chỉ tiêu (chưa áp dụng trong proc — xem mục 12 của README).</summary>
    public decimal? TargetAmount { get; init; }

    public decimal? BonusRatePercent { get; init; }

    public DateOnly EffectiveFrom { get; init; }

    public DateOnly? EffectiveTo { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static CommissionRuleDto FromEntity(CommissionRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        ScopeType = rule.ScopeType,
        ScopeId = rule.ScopeId,
        BasisType = rule.BasisType,
        RatePercent = rule.RatePercent,
        TargetAmount = rule.TargetAmount,
        BonusRatePercent = rule.BonusRatePercent,
        EffectiveFrom = rule.EffectiveFrom,
        EffectiveTo = rule.EffectiveTo,
        CreatedAt = rule.CreatedAt,
        UpdatedAt = rule.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách quy tắc hoa hồng — GET /api/v1/admin/commission-rules.</summary>
public class CommissionRuleListQuery
{
    public string? Keyword { get; set; }

    public CommissionScopeType? ScopeType { get; set; }

    public CommissionBasisType? BasisType { get; set; }

    /// <summary>Chỉ lấy quy tắc còn hiệu lực tại ngày này (theo <c>EffectiveFrom/EffectiveTo</c>).</summary>
    public DateOnly? EffectiveOn { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Yêu cầu tạo quy tắc hoa hồng — POST /api/v1/admin/commission-rules.
/// <c>ratePercent</c> trong 0..100; <c>scopeId</c> bắt buộc khi phạm vi là danh mục/sản phẩm.
/// </summary>
public class CreateCommissionRuleRequest
{
    public string? Name { get; set; }

    public CommissionScopeType ScopeType { get; set; }

    public long? ScopeId { get; set; }

    public CommissionBasisType BasisType { get; set; }

    public decimal RatePercent { get; set; }

    public decimal? TargetAmount { get; set; }

    public decimal? BonusRatePercent { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>Yêu cầu cập nhật quy tắc hoa hồng — PUT /api/v1/admin/commission-rules/{id}.</summary>
public class UpdateCommissionRuleRequest
{
    public string? Name { get; set; }

    public CommissionScopeType ScopeType { get; set; }

    public long? ScopeId { get; set; }

    public CommissionBasisType BasisType { get; set; }

    public decimal RatePercent { get; set; }

    public decimal? TargetAmount { get; set; }

    public decimal? BonusRatePercent { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}
