using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.PcBuilds;
using SiliconHorizon.Application.Features.PcBuilds.Dto;

namespace SiliconHorizon.Api.Controllers.PcBuilds;

/// <summary>
/// PC BUILD SẴN (combo linh kiện) — API công khai.
/// <para>
/// Dành cho khách KHÔNG có chuyên môn: xem các bộ máy cửa hàng đã dựng sẵn, rồi thêm MỘT LẦN toàn bộ
/// linh kiện của bộ vào giỏ hàng. Giá bộ = tổng giá linh kiện theo giá hiện hành nên khớp tuyệt đối
/// với số tiền trong giỏ/đơn hàng.
/// </para>
/// <para>
/// Chủ giỏ do backend quyết định: khách đã đăng nhập (customerId ÉP từ token) hoặc khách vãng lai
/// (header <c>X-Cart-Session</c>) — giống hệt module giỏ hàng nên không cần đăng nhập vẫn mua được.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/pc-builds")]
[Produces("application/json")]
public class PcBuildsController : ControllerBase
{
    /// <summary>Header mang mã phiên giỏ tạm của khách vãng lai — trùng tên với module giỏ hàng.</summary>
    private const string CartSessionHeader = "X-Cart-Session";

    private readonly IPcBuildService _pcBuildService;

    public PcBuildsController(IPcBuildService pcBuildService) => _pcBuildService = pcBuildService;

    /// <summary>
    /// GET /api/v1/pc-builds — danh sách bộ PC build sẵn (lọc theo hạng/khoảng giá/còn hàng, sắp xếp, phân trang).
    /// Ví dụ: <c>?tier=1&amp;inStockOnly=true&amp;sortBy=price_asc</c>.
    /// <c>partsPrice</c> = tổng giá linh kiện (giá bộ) · <c>availableBuilds</c> = số bộ còn lắp được.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PcBuildListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PcBuildListItemDto>>> Search(
        [FromQuery] PcBuildSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _pcBuildService.SearchAsync(request, cancellationToken));

    /// <summary>
    /// GET /api/v1/pc-builds/{id} — chi tiết bộ: mô tả + danh sách linh kiện kèm giá và tồn kho hiện tại
    /// (để màn hình hiển thị cảnh báo linh kiện sắp hết).
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(PcBuildDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PcBuildDetailDto>> GetById(
        long id,
        [FromQuery] int? customerGroupId,
        CancellationToken cancellationToken)
        => Ok(await _pcBuildService.GetByIdAsync(id, customerGroupId, cancellationToken));

    /// <summary>
    /// GET /api/v1/pc-builds/builder/catalog — danh mục linh kiện cho tab "TỰ BUILD PC":
    /// 6 nhóm theo ĐÚNG THỨ TỰ CHỌN của khách (bo mạch chủ → CPU → RAM → ổ cứng → nguồn → màn hình),
    /// kèm giá, tồn kho, ảnh và thông số kỹ thuật (<c>specs</c>) để giao diện chỉ gợi ý những linh kiện
    /// PHÙ HỢP với các lựa chọn trước đó (socket · chuẩn RAM · số khe RAM · công suất nguồn).
    /// </summary>
    [HttpGet("builder/catalog")]
    [ProducesResponseType(typeof(PcBuilderCatalogDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PcBuilderCatalogDto>> GetBuilderCatalog(
        [FromQuery] int? customerGroupId,
        CancellationToken cancellationToken)
        => Ok(await _pcBuildService.GetBuilderCatalogAsync(customerGroupId, cancellationToken));

    /// <summary>
    /// POST /api/v1/pc-builds/{id}/add-to-cart — thêm TẤT CẢ linh kiện của bộ vào giỏ trong một giao dịch.
    /// Body: <c>{ "quantity": 1 }</c> (số bộ). Thiếu tồn ở BẤT KỲ linh kiện nào ⇒ 409 kèm tên linh kiện
    /// thiếu và không thêm gì cả. Sau khi thêm, FE nạp lại GET /api/v1/cart để badge/tổng tiền khớp.
    /// </summary>
    [HttpPost("{id:long}/add-to-cart")]
    [ProducesResponseType(typeof(AddPcBuildToCartResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddPcBuildToCartResultDto>> AddToCart(
        long id,
        [FromBody] AddPcBuildToCartRequest request,
        [FromHeader(Name = CartSessionHeader)] string? sessionToken,
        CancellationToken cancellationToken)
        => Ok(await _pcBuildService.AddToCartAsync(id, request, sessionToken, cancellationToken));
}
