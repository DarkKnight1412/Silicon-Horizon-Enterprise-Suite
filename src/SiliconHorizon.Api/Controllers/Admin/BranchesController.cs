using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD chi nhánh — <c>dbo.Branches</c> (Database/README.md mục 10: <c>/api/admin/branches</c>).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. "Xóa" = PATCH trạng thái vì chi nhánh đã gắn với nhân viên, tồn kho, đơn hàng;
/// các proc bán hàng chỉ xét chi nhánh đang hoạt động (<c>br.IsActive = 1</c>).
/// </remarks>
[ApiController]
[Route("api/v1/admin/branches")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService) => _branchService = branchService;

    /// <summary>GET /api/v1/admin/branches?keyword=&amp;isActive=&amp;pageNumber=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BranchDto>>> GetAll(
        [FromQuery] BranchListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _branchService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/branches/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _branchService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/branches — mã chi nhánh duy nhất (409 nếu trùng).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BranchDto>> Create(
        [FromBody] CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _branchService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/branches/{id} — không đổi mã chi nhánh.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchDto>> Update(
        int id,
        [FromBody] UpdateBranchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _branchService.UpdateAsync(id, request, cancellationToken));

    /// <summary>PATCH /api/v1/admin/branches/{id}/status — ngừng/cho hoạt động lại chi nhánh.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        int id,
        [FromBody] SetBranchStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _branchService.SetStatusAsync(id, request, cancellationToken);

        return NoContent();
    }
}
