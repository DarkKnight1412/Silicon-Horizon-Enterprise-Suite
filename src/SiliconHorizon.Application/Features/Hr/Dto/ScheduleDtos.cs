namespace SiliconHorizon.Application.Features.Hr.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO xếp ca — khớp CHÍNH XÁC cột của usp_Schedule_List / usp_Schedule_Assign
// (Database/06_sp_hr_audit_reports.sql) và Database/README.md mục 8 (HR-04, UC-22).
//
// Quy ước thời gian: proc trả cột kiểu `date` và `time(0)`. Lớp *Row đọc bằng
// DateTime/TimeSpan (ánh xạ an toàn với mọi phiên bản Dapper) rồi chuyển sang
// DateOnly/TimeOnly ở lớp *Dto để JSON gọn ("2026-11-05", "08:00:00").
// Tham số truyền vào proc cũng dùng DateTime (nửa đêm UTC) vì cùng lý do.
//
// Quy ước phạm vi (BRD 2.2): ca làm LUÔN thuộc chi nhánh trong token
// (ICurrentUser.BranchId) — vì vậy request KHÔNG có `branchId`.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Một dòng bảng xếp ca — result set của <c>dbo.usp_Schedule_List</c>
/// (ScheduleId, WorkDate, BranchId, EmployeeId, FullName, ShiftCode, ShiftName, StartTime, EndTime).
/// </summary>
public sealed class ScheduleRow
{
    public long ScheduleId { get; set; }

    /// <summary>Cột <c>EmployeeSchedules.WorkDate</c> kiểu <c>date</c> (giờ Việt Nam).</summary>
    public DateTime WorkDate { get; set; }

    public int BranchId { get; set; }

    public long EmployeeId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Mã ca: MORNING, AFTERNOON, FULL.</summary>
    public string ShiftCode { get; set; } = string.Empty;

    public string ShiftName { get; set; } = string.Empty;

    /// <summary>Cột <c>Shifts.StartTime</c> kiểu <c>time(0)</c>.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Cột <c>Shifts.EndTime</c> kiểu <c>time(0)</c>.</summary>
    public TimeSpan EndTime { get; set; }
}

/// <summary>Một ca đã xếp trả ra API — GET /api/v1/schedules.</summary>
public class ScheduleItemDto
{
    public long ScheduleId { get; init; }

    /// <summary>Ngày làm việc theo giờ Việt Nam (UTC+7) — <c>fn_VnDate</c>.</summary>
    public DateOnly WorkDate { get; init; }

    /// <summary>Chi nhánh của ca (ca làm theo chi nhánh).</summary>
    public int BranchId { get; init; }

    public long EmployeeId { get; init; }

    public string EmployeeName { get; init; } = string.Empty;

    public string ShiftCode { get; init; } = string.Empty;

    public string ShiftName { get; init; } = string.Empty;

    public TimeOnly StartTime { get; init; }

    public TimeOnly EndTime { get; init; }

    public static ScheduleItemDto FromRow(ScheduleRow row) => new()
    {
        ScheduleId = row.ScheduleId,
        WorkDate = DateOnly.FromDateTime(row.WorkDate),
        BranchId = row.BranchId,
        EmployeeId = row.EmployeeId,
        EmployeeName = row.FullName,
        ShiftCode = row.ShiftCode,
        ShiftName = row.ShiftName,
        StartTime = TimeOnly.FromTimeSpan(row.StartTime),
        EndTime = TimeOnly.FromTimeSpan(row.EndTime)
    };
}

/// <summary>Result set của <c>dbo.usp_Schedule_Assign</c> — chỉ trả <c>ScheduleId</c> vừa tạo.</summary>
public sealed class CreatedScheduleRow
{
    public long ScheduleId { get; set; }
}

/// <summary>Kết quả POST /api/v1/schedules.</summary>
public class CreatedScheduleDto
{
    /// <summary>Id ca vừa xếp (EmployeeSchedules.Id).</summary>
    public long ScheduleId { get; init; }
}

/// <summary>
/// Tham số GET /api/v1/schedules — bảng xếp ca của nhân viên / chi nhánh.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="FromDate"/>/<see cref="ToDate"/> là ngày theo giờ Việt Nam. Bỏ trống thì mặc định
/// lấy trọn tháng hiện tại (giờ Việt Nam) — đúng khoảng thời gian quản lý chi nhánh thường xem.</item>
/// <item><see cref="EmployeeId"/>/<see cref="BranchId"/> <b>chỉ có hiệu lực với Admin</b>: nhân viên bị ép
/// <c>EmployeeId</c> từ token (chỉ xem ca của mình), Quản lý bị ép <c>BranchId</c> từ token (BRD 2.2).</item>
/// </list>
/// </remarks>
public class ScheduleListQuery
{
    /// <summary>Ngày bắt đầu (bao gồm). Bỏ trống = ngày 1 của tháng hiện tại.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Ngày kết thúc (bao gồm). Bỏ trống = ngày cuối của tháng hiện tại.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>Chỉ Admin: lọc theo một nhân viên cụ thể.</summary>
    public long? EmployeeId { get; set; }

    /// <summary>Chỉ Admin: lọc theo chi nhánh (bỏ trống = toàn hệ thống).</summary>
    public int? BranchId { get; set; }
}

/// <summary>
/// POST /api/v1/schedules — xếp ca cho nhân viên trong chi nhánh của người gọi
/// (usp_Schedule_Assign, HR-04).
/// </summary>
/// <remarks>
/// Chi nhánh của ca ép từ token (<c>@BranchId</c>), KHÔNG nhận từ client. Nhân viên được xếp ca phải
/// thuộc chi nhánh đó, nếu không → 403. Trùng (nhân viên, ngày, ca) → 409 (SQL 50005 từ proc).
/// </remarks>
public class AssignScheduleRequest
{
    /// <summary>Nhân viên được xếp ca (bắt buộc; proc báo 50001 nếu không tồn tại hoặc đã nghỉ việc).</summary>
    public long EmployeeId { get; set; }

    /// <summary>Ca làm việc (<c>Shifts.Id</c>: MORNING, AFTERNOON, FULL) — bắt buộc.</summary>
    public int ShiftId { get; set; }

    /// <summary>Ngày làm việc theo giờ Việt Nam — bắt buộc.</summary>
    public DateOnly? WorkDate { get; set; }
}
