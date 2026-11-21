using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Hr;
using SiliconHorizon.Application.Features.Hr.Dto;

namespace SiliconHorizon.Api.Controllers.Hr;

/// <summary>
/// Chấm công — POST /api/v1/attendance/check-in | check-out | adjust, GET /api/v1/attendance?yearMonth=
/// (Database/README.md mục 8, HR-05 / HR-06 / UC-19, UC-23).
/// </summary>
/// <remarks>
/// Chấm công vào/ra LUÔN dùng <c>employeeId</c> trong token (không nhận từ client); điều chỉnh công chỉ
/// Quản lý chi nhánh/Admin và nhân viên phải thuộc chi nhánh trong token (BRD 2.2).
/// </remarks>
[ApiController]
[Route("api/v1/attendance")]
[Authorize]
[Produces("application/json")]
public class AttendanceController : ControllerBase
{
    private readonly IHrService _hrService;

    public AttendanceController(IHrService hrService) => _hrService = hrService;

    /// <summary>
    /// POST /api/v1/attendance/check-in — chấm công vào ca hôm nay chưa chấm, ca bắt đầu sớm nhất
    /// (usp_Attendance_CheckIn).
    /// </summary>
    /// <remarks>
    /// Chưa được xếp ca hôm nay hoặc đã chấm công vào đủ các ca → 400 (SQL 50004). Quyền: NV (mọi nhân viên).
    /// </remarks>
    [HttpPost("check-in")]
    [Authorize(Roles = HrRoles.AttendanceStaff)]
    [ProducesResponseType(typeof(AttendanceCheckInDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AttendanceCheckInDto>> CheckIn(CancellationToken cancellationToken)
        => Ok(await _hrService.CheckInAsync(cancellationToken));

    /// <summary>
    /// POST /api/v1/attendance/check-out — chấm công ra ca đang mở của hôm nay (usp_Attendance_CheckOut).
    /// </summary>
    /// <remarks>
    /// Không có ca nào đang chờ chấm công ra → 409 (SQL 50002). Quyền: NV (mọi nhân viên).
    /// </remarks>
    [HttpPost("check-out")]
    [Authorize(Roles = HrRoles.AttendanceStaff)]
    [ProducesResponseType(typeof(AttendanceCheckOutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceCheckOutDto>> CheckOut(CancellationToken cancellationToken)
        => Ok(await _hrService.CheckOutAsync(cancellationToken));

    /// <summary>
    /// POST /api/v1/attendance/adjust — điều chỉnh công (quên chấm, sai giờ) (usp_Attendance_Adjust, HR-06).
    /// </summary>
    /// <remarks>
    /// Bắt buộc có <c>reason</c> và ít nhất một trong <c>checkInAt</c>/<c>checkOutAt</c> (UTC), giờ ra phải sau
    /// giờ vào → sai thì 400 (SQL 50004). Bản ghi chuyển sang <c>Source = 1</c> (quản lý điều chỉnh) và ghi
    /// nhật ký <c>AttendanceAdjusted</c> kèm giá trị cũ/mới. Quyền: QL, AD.
    /// </remarks>
    [HttpPost("adjust")]
    [Authorize(Roles = HrRoles.AttendanceAdjusters)]
    [ProducesResponseType(typeof(AttendanceAdjustResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceAdjustResultDto>> Adjust(
        [FromBody] AdjustAttendanceRequest request,
        CancellationToken cancellationToken)
        => Ok(await _hrService.AdjustAttendanceAsync(request, cancellationToken));

    /// <summary>
    /// GET /api/v1/attendance?yearMonth=2026-11 — bảng công tháng: chi tiết từng ngày + tổng hợp theo
    /// nhân viên (usp_Attendance_GetMonthly, 2 result set).
    /// </summary>
    /// <remarks>
    /// <c>yearMonth</c> bắt buộc theo định dạng <c>yyyy-MM</c>. <c>employeeId</c>/<c>branchId</c> chỉ có hiệu
    /// lực với Admin — nhân viên bị ép <c>employeeId</c> từ token, Quản lý chi nhánh bị ép <c>branchId</c>.
    /// Quyền: NV (của mình), QL, AD.
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = HrRoles.AttendanceViewers)]
    [ProducesResponseType(typeof(AttendanceMonthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AttendanceMonthDto>> GetMonthly(
        [FromQuery] AttendanceMonthlyQuery query,
        CancellationToken cancellationToken)
        => Ok(await _hrService.GetMonthlyAttendanceAsync(query, cancellationToken));
}
