using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Cart;
using SiliconHorizon.Application.Features.Cart.Dto;

namespace SiliconHorizon.Api.Controllers.Cart;

/// <summary>
/// Giỏ hàng (CART-01..11) — Database/README.md mục 4.
/// <para>
/// Chủ giỏ: khách đã đăng nhập (customerId lấy từ token) hoặc khách vãng lai (mã phiên ở header
/// <c>X-Cart-Session</c>). Endpoint KHÔNG nhận customerId từ client — API luôn ép giá trị này theo token.
/// </para>
/// <para>
/// Giỏ hàng là Public nên không gắn <c>[Authorize]</c> ở cấp class; riêng <c>merge</c> yêu cầu đăng nhập
/// vì phải gộp vào giỏ của chính tài khoản (service kiểm tra tiếp hồ sơ khách hàng trong token).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/cart")]
[Produces("application/json")]
public class CartController : ControllerBase
{
    /// <summary>
    /// Header mang mã phiên giỏ tạm của khách vãng lai — khai báo một chỗ cho cả module.
    /// Client (web khách hàng) tự sinh mã khi gọi lần đầu, gửi lại ở mọi request giỏ hàng (có thể lưu vào cookie).
    /// </summary>
    private const string CartSessionHeader = "X-Cart-Session";

    private readonly ICartService _cartService;

    public CartController(ICartService cartService) => _cartService = cartService;

    /// <summary>
    /// GET /api/v1/cart — xem giỏ hàng (usp_Cart_Get). Proc tự kiểm tra tồn tức thời: bỏ tick món hết hàng,
    /// giảm số lượng nếu tồn ít hơn số lượng trong giỏ; chi tiết trả ở <c>warnings</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartDto>> Get(
        [FromHeader(Name = CartSessionHeader)] string? sessionToken,
        CancellationToken cancellationToken)
        => Ok(await _cartService.GetAsync(sessionToken, cancellationToken));

    /// <summary>
    /// POST /api/v1/cart/items — thêm sản phẩm vào giỏ (usp_Cart_AddItem). <c>replace = true</c> để đặt đúng
    /// số lượng thay vì cộng dồn. Số lượng sau khi thêm vượt tồn khả dụng → 409.
    /// </summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(AddedCartItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddedCartItemDto>> AddItem(
        [FromBody] AddCartItemRequest request,
        [FromHeader(Name = CartSessionHeader)] string? sessionToken,
        CancellationToken cancellationToken)
        => Ok(await _cartService.AddItemAsync(request, sessionToken, cancellationToken));

    /// <summary>
    /// PATCH /api/v1/cart/items/{productId} — đổi số lượng và/hoặc tick chọn (usp_Cart_UpdateItem).
    /// <c>quantity &lt;= 0</c> thì dòng bị xóa khỏi giỏ; sản phẩm không có trong giỏ → 404;
    /// vượt tồn khả dụng hoặc tick món đã hết hàng → 409.
    /// </summary>
    [HttpPatch("items/{productId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateItem(
        long productId,
        [FromBody] UpdateCartItemRequest request,
        [FromHeader(Name = CartSessionHeader)] string? sessionToken,
        CancellationToken cancellationToken)
    {
        await _cartService.UpdateItemAsync(productId, request, sessionToken, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// DELETE /api/v1/cart/items/{productId} — xóa dòng khỏi giỏ (usp_Cart_RemoveItem).
    /// Idempotent: dòng không còn trong giỏ vẫn trả 204.
    /// </summary>
    [HttpDelete("items/{productId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveItem(
        long productId,
        [FromHeader(Name = CartSessionHeader)] string? sessionToken,
        CancellationToken cancellationToken)
    {
        await _cartService.RemoveItemAsync(productId, sessionToken, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/cart/select-all — chọn hoặc bỏ chọn tất cả (usp_Cart_SelectAll).
    /// Body: <c>{ "isSelected": true }</c>; món đã hết hàng không bao giờ được tick.
    /// </summary>
    [HttpPost("select-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SelectAll(
        [FromBody] SelectAllCartItemsRequest request,
        [FromHeader(Name = CartSessionHeader)] string? sessionToken,
        CancellationToken cancellationToken)
    {
        await _cartService.SelectAllAsync(request, sessionToken, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/cart/merge — gộp giỏ tạm vào giỏ tài khoản sau khi đăng nhập (CART-02, usp_Cart_Merge).
    /// Mã phiên giỏ tạm gửi ở header <c>X-Cart-Session</c>; customerId lấy từ token nên bắt buộc đăng nhập.
    /// Proc cộng dồn nhưng không vượt tồn khả dụng, bỏ món đã hết hàng rồi xóa giỏ tạm.
    /// </summary>
    [HttpPost("merge")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Merge(
        [FromHeader(Name = CartSessionHeader)] string? sessionToken,
        CancellationToken cancellationToken)
    {
        await _cartService.MergeAsync(sessionToken, cancellationToken);

        return NoContent();
    }
}
