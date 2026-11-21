using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Commission.Dto;

/// <summary>
/// Hằng số trạng thái + tên hiển thị tiếng Việt dùng chung cho module Hoa hồng
/// (KPI-01..06, Database/README.md mục 8).
/// </summary>
/// <remarks>
/// Hai enum đã có sẵn ở tầng Domain: <see cref="CommissionPeriodStatus"/> (CommissionPeriods.Status)
/// và <see cref="CommissionEntryType"/> (CommissionEntries.EntryType); lớp này chỉ chuyển sang nhãn tiếng Việt.
/// </remarks>
public static class CommissionStatusText
{
    /// <summary>Tên trạng thái kỳ hoa hồng (CommissionPeriods.Status).</summary>
    public static string PeriodStatusText(byte status) => (CommissionPeriodStatus)status switch
    {
        CommissionPeriodStatus.Open => "Đang mở",
        CommissionPeriodStatus.Closed => "Đã chốt",
        _ => "Không xác định"
    };

    /// <summary>
    /// Tên loại bút toán hoa hồng (CommissionEntries.EntryType).
    /// <c>Clawback</c> là bút toán ÂM sinh ra khi khách đổi trả (KPI-04).
    /// </summary>
    public static string EntryTypeText(byte entryType) => (CommissionEntryType)entryType switch
    {
        CommissionEntryType.Earn => "Hoa hồng phát sinh",
        CommissionEntryType.Clawback => "Thu hồi hoa hồng (Clawback)",
        CommissionEntryType.Adjustment => "Điều chỉnh thủ công",
        _ => "Không xác định"
    };
}