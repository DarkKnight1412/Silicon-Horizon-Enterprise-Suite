using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD danh mục ca làm việc — <c>dbo.Shifts</c> (Database/README.md mục 10: <c>/api/admin/shifts</c>).
/// Dữ liệu chủ cho <c>usp_Schedule_Assign</c> (xếp ca, HR-04): proc từ chối <c>@ShiftId</c> không tồn tại.
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. Khung giờ dùng <c>time(0)</c> (serialized <c>HH:mm:ss</c>) và phải có
/// <c>endTime &gt; startTime</c>. Bảng không có cột trạng thái nên không có endpoint DELETE.
/// </remarks>
[ApiController]
[Route("api/v1/admin/shifts")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService) => _shiftService = shiftService;

    /// <summary>GET /api/v1/admin/shifts?keyword=&amp;pageNumber=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ShiftDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ShiftDto>>> GetAll(
        [FromQuery] ShiftListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _shiftService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/shifts/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _shiftService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/shifts — mã ca duy nhất (409 nếu trùng); khung giờ sai trả 400.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftDto>> Create(
        [FromBody] CreateShiftRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _shiftService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/shifts/{id} — đổi tên/khung giờ (không đổi mã ca).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftDto>> Update(
        int id,
        [FromBody] UpdateShiftRequest request,
        CancellationToken cancellationToken)
        => Ok(await _shiftService.UpdateAsync(id, request, cancellationToken));
}
