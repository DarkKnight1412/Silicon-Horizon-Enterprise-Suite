using SiliconHorizon.Application.Features.Hr.Dto;

namespace SiliconHorizon.Application.Features.Hr;

/// <summary>
/// Use case Nhân sự — xếp ca và chấm công (HR-04, HR-05, HR-06 / UC-19, UC-22, UC-23).
/// Mọi phương thức gọi stored procedure trong <c>Database/06_sp_hr_audit_reports.sql</c>
/// (nhóm <c>usp_Schedule_*</c> và <c>usp_Attendance_*</c>).
/// </summary>
public interface IHrService
{
    /// <summary>
    /// Bảng xếp ca theo khoảng ngày — GET /api/v1/schedules (usp_Schedule_List).
    /// Nhân viên chỉ thấy ca của mình, Quản lý chỉ thấy ca chi nhánh mình (BRD 2.2).
    /// </summary>
    Task<IReadOnlyList<ScheduleItemDto>> GetSchedulesAsync(
        ScheduleListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xếp ca cho nhân viên — POST /api/v1/schedules (usp_Schedule_Assign). Chi nhánh ép từ token,
    /// nhân viên phải thuộc chi nhánh đó; trùng ca của cùng nhân viên trong ngày → 409.
    /// </summary>
    Task<CreatedScheduleDto> AssignScheduleAsync(
        AssignScheduleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa ca chưa có chấm công — DELETE /api/v1/schedules/{id} (usp_Schedule_Remove).
    /// Ca đã có dữ liệu chấm công → 409 (SQL 50002 từ proc).
    /// </summary>
    Task RemoveScheduleAsync(long scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chấm công vào ca gần nhất chưa chấm của hôm nay — POST /api/v1/attendance/check-in
    /// (usp_Attendance_CheckIn). Nhân viên ép từ token; chưa được xếp ca → 400 (SQL 50004).
    /// </summary>
    Task<AttendanceCheckInDto> CheckInAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Chấm công ra ca đang mở — POST /api/v1/attendance/check-out (usp_Attendance_CheckOut).
    /// Không có ca nào chờ chấm công ra → 409 (SQL 50002).
    /// </summary>
    Task<AttendanceCheckOutDto> CheckOutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Điều chỉnh công (quên chấm, sai giờ) — POST /api/v1/attendance/adjust (usp_Attendance_Adjust).
    /// Bắt buộc có lý do, có ít nhất một mốc giờ vào/ra và mọi thay đổi đều ghi nhật ký (HR-06).
    /// </summary>
    Task<AttendanceAdjustResultDto> AdjustAttendanceAsync(
        AdjustAttendanceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bảng công tháng (chi tiết từng ngày + tổng hợp theo nhân viên) — GET /api/v1/attendance?yearMonth=
    /// (usp_Attendance_GetMonthly, 2 result set).
    /// </summary>
    Task<AttendanceMonthDto> GetMonthlyAttendanceAsync(
        AttendanceMonthlyQuery query,
        CancellationToken cancellationToken = default);
}
