using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD quy tắc hoa hồng — <c>dbo.CommissionRules</c>
/// (Database/README.md mục 10: <c>/api/admin/commission-rules</c>; nghiệp vụ KPI-01..03).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. <c>usp_Commission_Summary</c>/<c>usp_Commission_ClosePeriod</c> (module Commission)
/// đọc quy tắc này khi tính hoa hồng. Không có endpoint DELETE vì <c>CommissionEntries</c> đang trỏ tới
/// <c>RuleId</c>; muốn ngừng áp dụng thì đặt <c>effectiveTo</c> trong quá khứ.
/// </remarks>
[ApiController]
[Route("api/v1/admin/commission-rules")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class CommissionRulesController : ControllerBase
{
    private readonly ICommissionRuleService _commissionRuleService;

    public CommissionRulesController(ICommissionRuleService commissionRuleService)
        => _commissionRuleService = commissionRuleService;

    /// <summary>GET /api/v1/admin/commission-rules?keyword=&amp;scopeType=&amp;basisType=&amp;effectiveOn=...</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CommissionRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CommissionRuleDto>>> GetAll(
        [FromQuery] CommissionRuleListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _commissionRuleService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/commission-rules/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CommissionRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommissionRuleDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _commissionRuleService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/commission-rules — tỷ lệ 0..100, phạm vi phải tồn tại nếu theo danh mục/sản phẩm.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CommissionRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommissionRuleDto>> Create(
        [FromBody] CreateCommissionRuleRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _commissionRuleService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/commission-rules/{id}</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CommissionRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommissionRuleDto>> Update(
        int id,
        [FromBody] UpdateCommissionRuleRequest request,
        CancellationToken cancellationToken)
        => Ok(await _commissionRuleService.UpdateAsync(id, request, cancellationToken));
}
