using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD quy tắc tương thích linh kiện — <c>dbo.CompatibilityRules</c>
/// (Database/README.md mục 10: <c>/api/admin/compatibility-rules</c>; nghiệp vụ PRD-11).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. <c>operator</c> chỉ nhận EQ/LTE/GTE (<c>CK_CompatibilityRules_Operator</c>),
/// <c>severity</c> chỉ nhận 0 (Info) / 1 (Warning).
/// <para>
/// Hiện trạng đã ghi ở mục 12 của Database/README.md: <c>usp_Compat_Check</c> còn viết cứng 4 quy tắc và
/// chưa đọc bảng này — API ở đây chuẩn bị dữ liệu chủ để chuyển proc sang đọc bảng.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/admin/compatibility-rules")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class CompatibilityRulesController : ControllerBase
{
    private readonly ICompatibilityRuleService _compatibilityRuleService;

    public CompatibilityRulesController(ICompatibilityRuleService compatibilityRuleService)
        => _compatibilityRuleService = compatibilityRuleService;

    /// <summary>GET /api/v1/admin/compatibility-rules?keyword=&amp;categoryId=&amp;isActive=...</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CompatibilityRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CompatibilityRuleDto>>> GetAll(
        [FromQuery] CompatibilityRuleListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _compatibilityRuleService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/compatibility-rules/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CompatibilityRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompatibilityRuleDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _compatibilityRuleService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/compatibility-rules — sai toán tử/mức cảnh báo hoặc danh mục không tồn tại → 400.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CompatibilityRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompatibilityRuleDto>> Create(
        [FromBody] CreateCompatibilityRuleRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _compatibilityRuleService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/compatibility-rules/{id}</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CompatibilityRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompatibilityRuleDto>> Update(
        int id,
        [FromBody] UpdateCompatibilityRuleRequest request,
        CancellationToken cancellationToken)
        => Ok(await _compatibilityRuleService.UpdateAsync(id, request, cancellationToken));

    /// <summary>PATCH /api/v1/admin/compatibility-rules/{id}/status — tắt/bật quy tắc (không xóa cứng).</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        int id,
        [FromBody] SetCompatibilityRuleStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _compatibilityRuleService.SetStatusAsync(id, request, cancellationToken);

        return NoContent();
    }
}
