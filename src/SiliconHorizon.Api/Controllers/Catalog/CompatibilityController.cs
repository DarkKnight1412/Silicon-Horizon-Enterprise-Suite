using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Catalog;
using SiliconHorizon.Application.Features.Catalog.Dto;

namespace SiliconHorizon.Api.Controllers.Catalog;

/// <summary>
/// Kiểm tra tương thích linh kiện (PRD-10/11) — Public, chỉ CẢNH BÁO, không chặn đặt hàng.
/// </summary>
[ApiController]
[Route("api/v1/compatibility")]
[Produces("application/json")]
public class CompatibilityController : ControllerBase
{
    private readonly IProductService _productService;

    public CompatibilityController(IProductService productService) => _productService = productService;

    /// <summary>
    /// POST /api/v1/compatibility/check — kiểm tra CPU/Mainboard (socket), RAM/Mainboard (chuẩn + số khe),
    /// công suất nguồn so với tổng TDP CPU.
    /// </summary>
    [HttpPost("check")]
    [ProducesResponseType(typeof(CompatibilityCheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompatibilityCheckResultDto>> Check(
        [FromBody] CompatibilityCheckRequest request,
        CancellationToken cancellationToken)
        => Ok(await _productService.CheckCompatibilityAsync(request, cancellationToken));
}
