using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Pricing;
using SiliconHorizon.Application.Features.Pricing.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD khuyến mãi — <c>dbo.Promotions</c> (Database/README.md mục 10: <c>/api/admin/promotions</c>;
/// nghiệp vụ PRC-03..06). <c>fn_ProductPrice</c> áp giá tốt nhất theo phạm vi và thời hạn đang hiệu lực.
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. "Xóa" = PATCH trạng thái (<c>isActive = false</c>), không xóa cứng
/// để giữ lịch sử tính giá.
/// </remarks>
[ApiController]
[Route("api/v1/admin/promotions")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService) => _promotionService = promotionService;

    /// <summary>GET /api/v1/admin/promotions?keyword=&amp;scopeType=&amp;scopeId=&amp;isActive=...</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PromotionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PromotionDto>>> GetAll(
        [FromQuery] PromotionListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _promotionService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/promotions/{id}</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(PromotionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await _promotionService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/admin/promotions — sai <c>discountType</c>/<c>scopeType</c>, <c>discountValue &lt;= 0</c>
    /// hoặc <c>endAt &lt;= startAt</c> trả 400; <c>scopeId</c> không tồn tại trả 400.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PromotionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromotionDto>> Create(
        [FromBody] CreatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _promotionService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/promotions/{id}</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(PromotionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionDto>> Update(
        long id,
        [FromBody] UpdatePromotionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _promotionService.UpdateAsync(id, request, cancellationToken));

    /// <summary>PATCH /api/v1/admin/promotions/{id}/status — bật/tắt khuyến mãi.</summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        long id,
        [FromBody] SetPromotionStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _promotionService.SetStatusAsync(id, request, cancellationToken);

        return NoContent();
    }
}
