namespace SiliconHorizon.Domain.Enums;

/// <summary>CommissionRules.ScopeType — phạm vi áp dụng quy tắc hoa hồng (KPI-02).</summary>
public enum CommissionScopeType : byte
{
    All = 0,
    Category = 1,
    Product = 2
}

/// <summary>CommissionRules.BasisType — cơ sở tính hoa hồng.</summary>
public enum CommissionBasisType : byte
{
    Revenue = 0,
    GrossProfit = 1
}

/// <summary>CommissionPeriods.Status — kỳ đang mở hay đã chốt (KPI-05).</summary>
public enum CommissionPeriodStatus : byte
{
    Open = 0,
    Closed = 1
}

/// <summary>CommissionEntries.EntryType — Amount âm với Clawback.</summary>
public enum CommissionEntryType : byte
{
    Earn = 0,
    Clawback = 1,
    Adjustment = 2
}
