using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Pricing;
using SiliconHorizon.Application.Features.Pricing.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD giá bậc thang theo nhóm khách — <c>dbo.PriceTiers</c>
/// (Database/README.md mục 10: <c>/api/admin/products/{id}/price-tiers</c>; nghiệp vụ PRC-02).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. Ràng buộc: <c>minQuantity &gt; 1</c> (<c>CK_PriceTiers_MinQty</c>) và bộ ba
/// (productId, customerGroupId, minQuantity) không trùng (<c>UQ_PriceTiers</c>) → trùng trả 409.
/// Mỗi lần đổi giá đều ghi nhật ký <c>usp_Audit_Write</c>.
/// </remarks>
[ApiController]
[Route("api/v1/admin/products/{productId:long}/price-tiers")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class PriceTiersController : ControllerBase
{
    private readonly IPriceTierService _priceTierService;

    public PriceTiersController(IPriceTierService priceTierService) => _priceTierService = priceTierService;

    /// <summary>GET .../products/{productId}/price-tiers?customerGroupId=&amp;pageNumber=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PriceTierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<PriceTierDto>>> GetAll(
        long productId,
        [FromQuery] PriceTierListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _priceTierService.GetPagedAsync(productId, query, cancellationToken));

    /// <summary>GET .../products/{productId}/price-tiers/{tierId}</summary>
    [HttpGet("{tierId:long}")]
    [ProducesResponseType(typeof(PriceTierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceTierDto>> GetById(
        long productId,
        long tierId,
        CancellationToken cancellationToken)
        => Ok(await _priceTierService.GetByIdAsync(productId, tierId, cancellationToken));

    /// <summary>POST .../products/{productId}/price-tiers — trùng bậc giá (nhóm khách + số lượng) trả 409.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PriceTierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PriceTierDto>> Create(
        long productId,
        [FromBody] CreatePriceTierRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _priceTierService.CreateAsync(productId, request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { productId, tierId = created.Id }, created);
    }

    /// <summary>PUT .../products/{productId}/price-tiers/{tierId} — sửa bậc giá (ghi nhật ký đổi giá).</summary>
    [HttpPut("{tierId:long}")]
    [ProducesResponseType(typeof(PriceTierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PriceTierDto>> Update(
        long productId,
        long tierId,
        [FromBody] UpdatePriceTierRequest request,
        CancellationToken cancellationToken)
        => Ok(await _priceTierService.UpdateAsync(productId, tierId, request, cancellationToken));
}
