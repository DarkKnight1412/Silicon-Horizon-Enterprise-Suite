namespace SiliconHorizon.Application.Features.Commission.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO hoa hồng — khớp KPI-01..06 và Database/README.md mục 8.
//
// Quy ước phạm vi dữ liệu (BRD mục 2.2): nhân viên chỉ xem hoa hồng của mình (ép EmployeeId từ token),
// Quản lý chi nhánh chỉ xem trong chi nhánh mình (ép BranchId từ token); Admin xem toàn hệ thống.
// Vì vậy DTO request KHÔNG có branchId/employeeId bắt buộc từ client (chỉ Admin được lọc tùy chọn).
//
// Lớp *Row khớp CHÍNH XÁC tên cột của từng result set trong
// Database/05_sp_warranty_returns_commission.sql (usp_Commission_Summary trả 3 result set);
// lớp *Dto là dữ liệu trả ra API.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Kỳ hoa hồng — result set 1 của <c>dbo.usp_Commission_Summary</c>.</summary>
/// <remarks>Rỗng (null) khi tháng được hỏi chưa có kỳ hoa hồng nào (chưa phát sinh bút toán).</remarks>
public sealed class CommissionPeriodRow
{
    public int PeriodId { get; set; }

    /// <summary>Định dạng <c>yyyy-MM</c> (char(7)).</summary>
    public string YearMonth { get; set; } = string.Empty;

    /// <summary>0 Open, 1 Closed — xem <c>CommissionPeriodStatus</c>.</summary>
    public byte Status { get; set; }
}

/// <summary>Tổng hợp theo nhân viên — result set 2 của <c>dbo.usp_Commission_Summary</c>.</summary>
public sealed class CommissionSummaryRow
{
    public long EmployeeId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public int BranchId { get; set; }

    /// <summary>Tổng hoa hồng phát sinh (EntryType = 0).</summary>
    public decimal Earned { get; set; }

    /// <summary>Tổng thu hồi do đổi trả (EntryType = 1, giá trị âm).</summary>
    public decimal Clawback { get; set; }

    /// <summary>Tổng điều chỉnh thủ công (EntryType = 2).</summary>
    public decimal Adjustment { get; set; }

    /// <summary>Thực nhận của kỳ = Earned + Clawback + Adjustment.</summary>
    public decimal Total { get; set; }
}

/// <summary>Chi tiết một bút toán hoa hồng — result set 3 của <c>dbo.usp_Commission_Summary</c>.</summary>
public sealed class CommissionEntryRow
{
    public long Id { get; set; }

    public long EmployeeId { get; set; }

    public long OrderId { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    /// <summary>Yêu cầu đổi trả làm phát sinh bút toán thu hồi (chỉ có với EntryType = 1).</summary>
    public long? ReturnRequestId { get; set; }

    /// <summary>0 Earn, 1 Clawback, 2 Adjustment — xem <c>CommissionEntryType</c>.</summary>
    public byte EntryType { get; set; }

    /// <summary>Doanh thu hoặc lợi nhuận gộp dùng làm cơ sở tính (0 với bút toán điều chỉnh).</summary>
    public decimal BaseAmount { get; set; }

    /// <summary>Tỷ lệ hoa hồng áp dụng (0 với bút toán điều chỉnh).</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Số tiền của bút toán — ÂM với Clawback.</summary>
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Kết quả tạo bút toán điều chỉnh — result set của <c>dbo.usp_Commission_Adjust</c>.</summary>
public sealed class CommissionAdjustmentRow
{
    public long EntryId { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Dữ liệu trả ra API
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Kỳ hoa hồng trả ra API — GET /api/v1/commissions?yearMonth=.</summary>
public class CommissionPeriodDto
{
    public int PeriodId { get; init; }

    public string YearMonth { get; init; } = string.Empty;

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    /// <summary><c>true</c> = kỳ đã chốt, không điều chỉnh được nữa (bút toán mới sang kỳ đang mở kế tiếp).</summary>
    public bool IsClosed => Status == (byte)Domain.Enums.CommissionPeriodStatus.Closed;

    public static CommissionPeriodDto FromRow(CommissionPeriodRow row) => new()
    {
        PeriodId = row.PeriodId,
        YearMonth = row.YearMonth,
        Status = row.Status,
        StatusName = CommissionStatusText.PeriodStatusText(row.Status)
    };
}

/// <summary>Tổng hợp hoa hồng của một nhân viên trong kỳ.</summary>
public class CommissionEmployeeSummaryDto
{
    public long EmployeeId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public int BranchId { get; init; }

    public decimal Earned { get; init; }

    public decimal Clawback { get; init; }

    public decimal Adjustment { get; init; }

    public decimal Total { get; init; }

    public static CommissionEmployeeSummaryDto FromRow(CommissionSummaryRow row) => new()
    {
        EmployeeId = row.EmployeeId,
        FullName = row.FullName,
        BranchId = row.BranchId,
        Earned = row.Earned,
        Clawback = row.Clawback,
        Adjustment = row.Adjustment,
        Total = row.Total
    };
}

/// <summary>Một bút toán hoa hồng trả ra API.</summary>
public class CommissionEntryDto
{
    public long Id { get; init; }

    public long EmployeeId { get; init; }

    public long OrderId { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public long? ReturnRequestId { get; init; }

    public byte EntryType { get; init; }

    public string EntryTypeName { get; init; } = string.Empty;

    public decimal BaseAmount { get; init; }

    public decimal RatePercent { get; init; }

    public decimal Amount { get; init; }

    public DateTime CreatedAt { get; init; }

    public static CommissionEntryDto FromRow(CommissionEntryRow row) => new()
    {
        Id = row.Id,
        EmployeeId = row.EmployeeId,
        OrderId = row.OrderId,
        OrderCode = row.OrderCode,
        ReturnRequestId = row.ReturnRequestId,
        EntryType = row.EntryType,
        EntryTypeName = CommissionStatusText.EntryTypeText(row.EntryType),
        BaseAmount = row.BaseAmount,
        RatePercent = row.RatePercent,
        Amount = row.Amount,
        CreatedAt = row.CreatedAt
    };
}

/// <summary>
/// Kết quả GET /api/v1/commissions — gồm kỳ hoa hồng, tổng hợp theo nhân viên và chi tiết từng bút toán.
/// </summary>
public class CommissionSummaryDto
{
    /// <summary><c>null</c> khi tháng chưa có kỳ hoa hồng (đơn chưa hoàn tất nên chưa phát sinh hoa hồng).</summary>
    public CommissionPeriodDto? Period { get; init; }

    public IReadOnlyList<CommissionEmployeeSummaryDto> Employees { get; init; } = Array.Empty<CommissionEmployeeSummaryDto>();

    public IReadOnlyList<CommissionEntryDto> Entries { get; init; } = Array.Empty<CommissionEntryDto>();
}

/// <summary>Kết quả điều chỉnh hoa hồng — POST /api/v1/admin/commissions/adjustments.</summary>
public class CommissionAdjustmentResultDto
{
    /// <summary>Id bút toán điều chỉnh vừa tạo (EntryType = 2).</summary>
    public long EntryId { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Tham số request
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tham số của GET /api/v1/commissions. <c>yearMonth</c> là bắt buộc (<c>yyyy-MM</c>, ví dụ <c>2026-11</c>).
/// </summary>
/// <remarks>
/// <see cref="EmployeeId"/> và <see cref="BranchId"/> chỉ có hiệu lực với <b>Admin</b>: nhân viên bị ép
/// <c>EmployeeId</c> từ token, Quản lý chi nhánh bị ép <c>BranchId</c> từ token (giá trị client gửi bị bỏ qua).
/// </remarks>
public class CommissionSummaryQuery
{
    /// <summary>Tháng cần xem, định dạng <c>yyyy-MM</c> (khớp <c>@YearMonth char(7)</c>).</summary>
    public string? YearMonth { get; set; }

    /// <summary>Chỉ Admin: lọc theo một nhân viên cụ thể.</summary>
    public long? EmployeeId { get; set; }

    /// <summary>Chỉ Admin: lọc theo chi nhánh.</summary>
    public int? BranchId { get; set; }
}

/// <summary>
/// POST /api/v1/admin/commissions/adjustments — điều chỉnh hoa hồng thủ công (usp_Commission_Adjust, chỉ Admin).
/// </summary>
/// <remarks>
/// Bút toán điều chỉnh được ghi vào kỳ hoa hồng <b>đang mở</b> của ngày hiện tại
/// (<c>usp_Internal_GetOpenPeriod</c>): nếu kỳ của tháng hiện tại đã chốt, bút toán tự rơi vào kỳ đang mở
/// kế tiếp nên dữ liệu đã chốt không bị sửa (KPI-06).
/// </remarks>
public class AdjustCommissionRequest
{
    /// <summary>Nhân viên được điều chỉnh hoa hồng (bắt buộc; proc báo 50001 nếu không tồn tại).</summary>
    public long EmployeeId { get; set; }

    /// <summary>Đơn hàng gắn với bút toán (bắt buộc — CommissionEntries.OrderId NOT NULL).</summary>
    public long OrderId { get; set; }

    /// <summary>Số tiền điều chỉnh: dương = cộng thêm, âm = trừ bớt; phải khác 0 (proc báo 50004 nếu bằng 0).</summary>
    public decimal Amount { get; set; }

    /// <summary>Lý do điều chỉnh — bắt buộc, tối đa 300 ký tự; được ghi vào nhật ký AUD-01.</summary>
    public string? Reason { get; set; }
}