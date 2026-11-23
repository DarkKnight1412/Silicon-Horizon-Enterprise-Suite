using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Catalog;
using SiliconHorizon.Application.Features.Catalog.Dto;

namespace SiliconHorizon.Api.Controllers.Catalog;

/// <summary>
/// Dữ liệu tra cứu CÔNG KHAI — KHÔNG cần đăng nhập: danh mục, thương hiệu và chi nhánh
/// (Database/README.md mục 3.1 — đọc bằng EF Core).
/// Dùng route tuyệt đối trên từng action vì tài liệu đặt các endpoint ở các đường dẫn khác nhau.
/// </summary>
[ApiController]
[Produces("application/json")]
public class CatalogReferenceController : ControllerBase
{
    private readonly ICatalogReferenceService _referenceService;

    public CatalogReferenceController(ICatalogReferenceService referenceService)
        => _referenceService = referenceService;

    /// <summary>GET /api/v1/categories — danh mục đang hoạt động (kèm SpecType để client biết bộ thông số).</summary>
    [HttpGet("/api/v1/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<ReferenceItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> GetCategories(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetCategoriesAsync(cancellationToken));

    /// <summary>GET /api/v1/brands — thương hiệu đang hoạt động.</summary>
    [HttpGet("/api/v1/brands")]
    [ProducesResponseType(typeof(IReadOnlyList<ReferenceItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> GetBrands(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetBrandsAsync(cancellationToken));

    /// <summary>
    /// GET /api/v1/branches — chi nhánh ĐANG HOẠT ĐỘNG (id, mã, tên, địa chỉ, điện thoại).
    /// Khách hàng dùng để chọn nơi nhận hàng khi đặt đơn; đây là dữ liệu công khai nên DTO không có
    /// trường quản trị (xem <see cref="BranchReferenceDto"/>).
    /// </summary>
    [HttpGet("/api/v1/branches")]
    [ProducesResponseType(typeof(IReadOnlyList<BranchReferenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchReferenceDto>>> GetBranches(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetBranchesAsync(cancellationToken));
}

