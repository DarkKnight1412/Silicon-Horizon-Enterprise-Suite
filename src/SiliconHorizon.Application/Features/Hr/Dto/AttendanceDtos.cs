namespace SiliconHorizon.Application.Features.Hr.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO chấm công — khớp CHÍNH XÁC result set của usp_Attendance_CheckIn,
// usp_Attendance_CheckOut, usp_Attendance_Adjust, usp_Attendance_GetMonthly
// (Database/06_sp_hr_audit_reports.sql; Database/README.md mục 8 — HR-05, HR-06).
//
// Quy ước phạm vi (BRD 2.2): chấm công vào/ra LUÔN dùng EmployeeId trong token
// (không nhận từ client); điều chỉnh công chỉ Quản lý/Admin và nhân viên phải thuộc
// chi nhánh trong token.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Result set của <c>dbo.usp_Attendance_CheckIn</c> — AttendanceId, ScheduleId, CheckInAt.</summary>
public sealed class AttendanceCheckInRow
{
    public long AttendanceId { get; set; }

    /// <summary>Ca hôm nay được chấm (ca chưa có chấm công, giờ bắt đầu sớm nhất).</summary>
    public long ScheduleId { get; set; }

    /// <summary>Thời điểm chấm công vào — UTC (cột <c>datetime2(0)</c>).</summary>
    public DateTime CheckInAt { get; set; }
}

/// <summary>Kết quả POST /api/v1/attendance/check-in.</summary>
public class AttendanceCheckInDto
{
    public long AttendanceId { get; init; }

    public long ScheduleId { get; init; }

    /// <summary>UTC (ISO 8601).</summary>
    public DateTime CheckInAt { get; init; }

    public static AttendanceCheckInDto FromRow(AttendanceCheckInRow row) => new()
    {
        AttendanceId = row.AttendanceId,
        ScheduleId = row.ScheduleId,
        CheckInAt = row.CheckInAt
    };
}

/// <summary>Result set của <c>dbo.usp_Attendance_CheckOut</c> — AttendanceId, CheckOutAt.</summary>
public sealed class AttendanceCheckOutRow
{
    public long AttendanceId { get; set; }

    /// <summary>Thời điểm chấm công ra — UTC.</summary>
    public DateTime CheckOutAt { get; set; }
}

/// <summary>Kết quả POST /api/v1/attendance/check-out.</summary>
public class AttendanceCheckOutDto
{
    public long AttendanceId { get; init; }

    /// <summary>UTC (ISO 8601).</summary>
    public DateTime CheckOutAt { get; init; }

    public static AttendanceCheckOutDto FromRow(AttendanceCheckOutRow row) => new()
    {
        AttendanceId = row.AttendanceId,
        CheckOutAt = row.CheckOutAt
    };
}

/// <summary>Result set của <c>dbo.usp_Attendance_Adjust</c> — chỉ trả <c>AttendanceId</c>.</summary>
public sealed class AttendanceAdjustRow
{
    public long AttendanceId { get; set; }
}

/// <summary>Kết quả POST /api/v1/attendance/adjust (bút toán đã ghi nhật ký AUD-01).</summary>
public class AttendanceAdjustResultDto
{
    /// <summary>Id bản ghi công bị tạo mới hoặc cập nhật.</summary>
    public long AttendanceId { get; init; }

    public static AttendanceAdjustResultDto FromRow(AttendanceAdjustRow row) => new()
    {
        AttendanceId = row.AttendanceId
    };
}

/// <summary>
/// Một ngày công trong bảng công tháng — result set 1 của <c>dbo.usp_Attendance_GetMonthly</c>
/// (EmployeeId, FullName, WorkDate, ShiftCode, StartTime, CheckInAt, CheckOutAt, Source, AdjustReason,
/// WorkedMinutes, IsLate).
/// </summary>
public sealed class AttendanceDayRow
{
    public long EmployeeId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Cột <c>date</c> (giờ Việt Nam).</summary>
    public DateTime WorkDate { get; set; }

    /// <summary>Mã ca — NULL khi bản ghi công không gắn ca nào (điều chỉnh tay).</summary>
    public string? ShiftCode { get; set; }

    /// <summary>Giờ bắt đầu ca — NULL khi không gắn ca.</summary>
    public TimeSpan? StartTime { get; set; }

    public DateTime? CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    /// <summary>0 Device, 1 ManualAdjust — xem <c>AttendanceSource</c>.</summary>
    public byte Source { get; set; }

    public string? AdjustReason { get; set; }

    /// <summary>Số phút làm việc — NULL khi thiếu giờ vào hoặc giờ ra.</summary>
    public int? WorkedMinutes { get; set; }

    /// <summary>Vào muộn hơn giờ bắt đầu ca trên 5 phút.</summary>
    public bool IsLate { get; set; }
}

/// <summary>Một ngày công trả ra API — GET /api/v1/attendance?yearMonth=.</summary>
public class AttendanceDayDto
{
    public long EmployeeId { get; init; }

    public string EmployeeName { get; init; } = string.Empty;

    public DateOnly WorkDate { get; init; }

    /// <summary>Mã ca (NULL nếu bản ghi công không gắn ca — ví dụ do điều chỉnh tay).</summary>
    public string? ShiftCode { get; init; }

    public TimeOnly? StartTime { get; init; }

    /// <summary>UTC.</summary>
    public DateTime? CheckInAt { get; init; }

    /// <summary>UTC.</summary>
    public DateTime? CheckOutAt { get; init; }

    public byte Source { get; init; }

    public string SourceName { get; init; } = string.Empty;

    /// <summary>Lý do điều chỉnh (chỉ có khi <c>Source = 1</c>).</summary>
    public string? AdjustReason { get; init; }

    /// <summary>Số phút làm việc (NULL khi thiếu giờ vào hoặc giờ ra).</summary>
    public int? WorkedMinutes { get; init; }

    /// <summary>1 khi vào muộn hơn giờ bắt đầu ca trên 5 phút.</summary>
    public bool IsLate { get; init; }

    public static AttendanceDayDto FromRow(AttendanceDayRow row) => new()
    {
        EmployeeId = row.EmployeeId,
        EmployeeName = row.FullName,
        WorkDate = DateOnly.FromDateTime(row.WorkDate),
        ShiftCode = row.ShiftCode,
        StartTime = row.StartTime is { } start ? TimeOnly.FromTimeSpan(start) : null,
        CheckInAt = row.CheckInAt,
        CheckOutAt = row.CheckOutAt,
        Source = row.Source,
        SourceName = AttendanceStatusText.SourceText(row.Source),
        AdjustReason = row.AdjustReason,
        WorkedMinutes = row.WorkedMinutes,
        IsLate = row.IsLate
    };
}

/// <summary>
/// Tổng hợp công của một nhân viên trong tháng — result set 2 của <c>dbo.usp_Attendance_GetMonthly</c>
/// (EmployeeId, FullName, DaysWorked, TotalHours, ScheduledShifts, MissingCheckOut).
/// </summary>
public sealed class AttendanceSummaryRow
{
    public long EmployeeId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Số ngày có chấm công trong tháng.</summary>
    public int DaysWorked { get; set; }

    /// <summary>Tổng giờ làm (tổng số phút / 60, 2 chữ số thập phân như proc).</summary>
    public decimal TotalHours { get; set; }

    /// <summary>Số ca đã xếp trong tháng (EmployeeSchedules).</summary>
    public int ScheduledShifts { get; set; }

    /// <summary>Số bản ghi công thiếu giờ ra (quên chấm công ra).</summary>
    public int MissingCheckOut { get; set; }
}

/// <summary>Tổng hợp công tháng trả ra API.</summary>
public class AttendanceSummaryDto
{
    public long EmployeeId { get; init; }

    public string EmployeeName { get; init; } = string.Empty;

    public int DaysWorked { get; init; }

    public decimal TotalHours { get; init; }

    public int ScheduledShifts { get; init; }

    public int MissingCheckOut { get; init; }

    public static AttendanceSummaryDto FromRow(AttendanceSummaryRow row) => new()
    {
        EmployeeId = row.EmployeeId,
        EmployeeName = row.FullName,
        DaysWorked = row.DaysWorked,
        TotalHours = row.TotalHours,
        ScheduledShifts = row.ScheduledShifts,
        MissingCheckOut = row.MissingCheckOut
    };
}

/// <summary>
/// Bảng công tháng — GET /api/v1/attendance?yearMonth= (2 result set của <c>usp_Attendance_GetMonthly</c>).
/// </summary>
public class AttendanceMonthDto
{
    /// <summary>Tháng được hỏi, định dạng <c>yyyy-MM</c>.</summary>
    public string YearMonth { get; init; } = string.Empty;

    /// <summary>Chi tiết từng ngày công.</summary>
    public IReadOnlyList<AttendanceDayDto> Days { get; init; } = Array.Empty<AttendanceDayDto>();

    /// <summary>Tổng hợp theo nhân viên (kể cả nhân viên chưa chấm công ngày nào).</summary>
    public IReadOnlyList<AttendanceSummaryDto> Employees { get; init; } = Array.Empty<AttendanceSummaryDto>();
}

/// <summary>
/// Tham số GET /api/v1/attendance — bảng công tháng (HR-05).
/// </summary>
/// <remarks>
/// <c>yearMonth</c> bắt buộc theo định dạng <c>yyyy-MM</c> (khớp <c>@YearMonth char(7)</c>).
/// <see cref="EmployeeId"/>/<see cref="BranchId"/> chỉ có hiệu lực với Admin: nhân viên bị ép
/// <c>EmployeeId</c> từ token, Quản lý bị ép <c>BranchId</c> từ token.
/// </remarks>
public class AttendanceMonthlyQuery
{
    /// <summary>Tháng cần xem, định dạng <c>yyyy-MM</c> (ví dụ 2026-11) — bắt buộc.</summary>
    public string? YearMonth { get; set; }

    /// <summary>Chỉ Admin: lọc theo một nhân viên cụ thể.</summary>
    public long? EmployeeId { get; set; }

    /// <summary>Chỉ Admin: lọc theo chi nhánh.</summary>
    public int? BranchId { get; set; }
}

/// <summary>
/// POST /api/v1/attendance/adjust — điều chỉnh công (HR-06), chỉ Quản lý chi nhánh / Admin.
/// </summary>
/// <remarks>
/// Quy tắc nghiệp vụ (proc cũng kiểm tra lại, lỗi 50004 → 400):
/// <list type="bullet">
/// <item><b>Bắt buộc có lý do</b> — không thể điều chỉnh công "âm thầm".</item>
/// <item>Phải có ít nhất một trong hai mốc <see cref="CheckInAt"/> / <see cref="CheckOutAt"/>.</item>
/// <item>Giờ ra không được trước giờ vào.</item>
/// <item>Nếu có <see cref="ScheduleId"/> thì ca phải thuộc đúng nhân viên + ngày đã chọn.</item>
/// <item>Mọi lần điều chỉnh đều ghi nhật ký (<c>AttendanceAdjusted</c>) kèm giá trị cũ/mới.</item>
/// </list>
/// </remarks>
public class AdjustAttendanceRequest
{
    /// <summary>Nhân viên được điều chỉnh công — phải thuộc chi nhánh trong token.</summary>
    public long EmployeeId { get; set; }

    /// <summary>Ngày công cần điều chỉnh (giờ Việt Nam) — bắt buộc.</summary>
    public DateOnly? WorkDate { get; set; }

    /// <summary>Ca liên quan (tùy chọn) — nếu truyền thì proc kiểm tra ca thuộc đúng nhân viên và ngày.</summary>
    public long? ScheduleId { get; set; }

    /// <summary>Giờ vào sau khi điều chỉnh — UTC (datetime2(0)).</summary>
    public DateTime? CheckInAt { get; set; }

    /// <summary>Giờ ra sau khi điều chỉnh — UTC (datetime2(0)).</summary>
    public DateTime? CheckOutAt { get; set; }

    /// <summary>Lý do điều chỉnh — bắt buộc, tối đa 300 ký tự (Attendances.AdjustReason).</summary>
    public string? Reason { get; set; }
}
