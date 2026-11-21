using SiliconHorizon.Domain.Common;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Domain.Entities.Commission;

/// <summary>dbo.CommissionRules — quy tắc hoa hồng (KPI-01..03). CRUD bằng EF Core.</summary>
public class CommissionRule : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public CommissionScopeType ScopeType { get; set; }

    public long? ScopeId { get; set; }

    public CommissionBasisType BasisType { get; set; }

    public decimal RatePercent { get; set; }

    public decimal? TargetAmount { get; set; }

    public decimal? BonusRatePercent { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}

/// <summary>dbo.CommissionPeriods — kỳ hoa hồng theo tháng (YearMonth dạng 2026-11).</summary>
public class CommissionPeriod
{
    public int Id { get; set; }

    public string YearMonth { get; set; } = null!;

    public CommissionPeriodStatus Status { get; set; } = CommissionPeriodStatus.Open;

    public long? ClosedBy { get; set; }

    public DateTime? ClosedAt { get; set; }
}

/// <summary>dbo.CommissionEntries — bút toán hoa hồng của từng nhân viên trong kỳ.</summary>
public class CommissionEntry
{
    public long Id { get; set; }

    public int PeriodId { get; set; }

    public long EmployeeId { get; set; }

    public long OrderId { get; set; }

    public long? ReturnRequestId { get; set; }

    public int RuleId { get; set; }

    public CommissionEntryType EntryType { get; set; }

    public decimal BaseAmount { get; set; }

    public decimal RatePercent { get; set; }

    /// <summary>Số tiền hoa hồng; âm khi thu hồi (Clawback).</summary>
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }
}
