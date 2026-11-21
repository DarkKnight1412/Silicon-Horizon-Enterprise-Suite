using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Catalog;
using SiliconHorizon.Application.Features.Catalog.Dto;

namespace SiliconHorizon.Api.Controllers.Catalog;

/// <summary>
/// Danh mục hàng hoá — API công khai (Public), khớp Database/README.md mục 3.1.
/// Giá trả về đã được proc áp bảng giá theo nhóm khách và khuyến mãi đang hiệu lực.
/// </summary>
[ApiController]
[Route("api/v1/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService) => _productService = productService;

    /// <summary>
    /// GET /api/v1/products — tìm kiếm, lọc chung + theo thông số, sắp xếp, phân trang.
    /// Ví dụ: <c>?categoryCode=RAM&amp;specFilters=memoryType:DDR5,capacityGbPerKit:32&amp;sortBy=price_asc</c>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> Search(
        [FromQuery] ProductSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _productService.SearchAsync(request, cancellationToken));

    /// <summary>GET /api/v1/products/{id} — chi tiết: header, ảnh, thông số, tồn từng chi nhánh.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> GetById(
        long id,
        [FromQuery] int? customerGroupId,
        CancellationToken cancellationToken)
        => Ok(await _productService.GetByIdAsync(id, customerGroupId, cancellationToken));
}
