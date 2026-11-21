using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Hr;
using SiliconHorizon.Application.Features.Hr.Dto;

namespace SiliconHorizon.Api.Controllers.Hr;

/// <summary>
/// Xếp ca làm việc — GET/POST /api/v1/schedules, DELETE /api/v1/schedules/{id}
/// (Database/README.md mục 8, HR-04 / UC-22).
/// </summary>
/// <remarks>
/// API ép <c>@BranchId</c> từ token (BRD 2.2) và KHÔNG nhận <c>branchId</c> từ client:
/// ca luôn được tạo trong chi nhánh của người gọi và nhân viên được xếp ca phải thuộc chi nhánh đó,
/// nếu không → 403. Nhân viên chỉ xem được ca của mình, Quản lý chi nhánh chỉ xem ca chi nhánh mình.
/// </remarks>
[ApiController]
[Route("api/v1/schedules")]
[Authorize]
[Produces("application/json")]
public class SchedulesController : ControllerBase
{
    private readonly IHrService _hrService;

    public SchedulesController(IHrService hrService) => _hrService = hrService;

    /// <summary>
    /// GET /api/v1/schedules — bảng xếp ca theo khoảng ngày (usp_Schedule_List).
    /// </summary>
    /// <remarks>
    /// <c>fromDate</c>/<c>toDate</c> là ngày theo giờ Việt Nam và <b>bao gồm</b> cả hai đầu; bỏ trống thì
    /// lấy trọn tháng hiện tại. <c>employeeId</c>/<c>branchId</c> chỉ có hiệu lực với Admin — nhân viên chỉ
    /// thấy ca của mình, Quản lý chi nhánh bị ép về chi nhánh trong token. Quyền: NV (của mình), QL, AD.
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = HrRoles.ScheduleViewers)]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduleItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ScheduleItemDto>>> GetSchedules(
        [FromQuery] ScheduleListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _hrService.GetSchedulesAsync(query, cancellationToken));

    /// <summary>
    /// POST /api/v1/schedules — xếp ca cho một nhân viên (usp_Schedule_Assign).
    /// </summary>
    /// <remarks>
    /// <c>workDate</c> theo giờ Việt Nam. Trùng (nhân viên, ngày, ca) → 409; nhân viên không tồn tại/đã nghỉ
    /// việc hoặc ca làm việc không tồn tại → 404. Quyền: QL, AD (nháp <c>hr.schedule.manage</c>).
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = HrRoles.ScheduleApprovers)]
    [ProducesResponseType(typeof(CreatedScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedScheduleDto>> AssignSchedule(
        [FromBody] AssignScheduleRequest request,
        CancellationToken cancellationToken)
        => Ok(await _hrService.AssignScheduleAsync(request, cancellationToken));

    /// <summary>
    /// DELETE /api/v1/schedules/{id} — xóa ca chưa có chấm công (usp_Schedule_Remove).
    /// </summary>
    /// <remarks>
    /// Ca đã có dữ liệu chấm công → 409 (SQL 50002); ca không tồn tại hoặc thuộc chi nhánh khác → 404/403.
    /// Quyền: QL, AD.
    /// </remarks>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = HrRoles.ScheduleApprovers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveSchedule(long id, CancellationToken cancellationToken)
    {
        await _hrService.RemoveScheduleAsync(id, cancellationToken);

        return NoContent();
    }
}
