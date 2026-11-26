using SiliconHorizon.Application.Features.Cart.Dto;

namespace SiliconHorizon.Application.Features.Cart;

/// <summary>
/// Use case giỏ hàng (CART-01..11). Tất cả đều đi qua stored procedure trong
/// Database/02_sp_foundation_catalog_cart.sql — không truy vấn bảng trực tiếp.
/// </summary>
/// <remarks>
/// Mọi thao tác đều nhận <c>sessionToken</c> của khách vãng lai (tầng API đọc từ header <c>X-Cart-Session</c>).
/// Khách đã đăng nhập thì service tự dùng <c>ICurrentUser.CustomerId</c> từ token và bỏ qua sessionToken —
/// customerId do client gửi lên (nếu có) không bao giờ được dùng.
/// </remarks>
public interface ICartService
{
    /// <summary>GET /api/v1/cart — dòng giỏ, tổng tiền dòng được tick, cảnh báo tồn (<c>usp_Cart_Get</c>).</summary>
    Task<CartDto> GetAsync(string? sessionToken, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v1/cart/items — thêm sản phẩm, cộng dồn hoặc thay thế số lượng (<c>usp_Cart_AddItem</c>).</summary>
    Task<AddedCartItemDto> AddItemAsync(
        AddCartItemRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCH /api/v1/cart/items/{productId} — đổi số lượng và/hoặc tick chọn (<c>usp_Cart_UpdateItem</c>).
    /// Số lượng <c>&lt;= 0</c> thì xóa dòng; dòng không có trong giỏ → 404.
    /// </summary>
    Task UpdateItemAsync(
        long productId,
        UpdateCartItemRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default);

    /// <summary>DELETE /api/v1/cart/items/{productId} — xóa dòng khỏi giỏ, idempotent (<c>usp_Cart_RemoveItem</c>).</summary>
    Task RemoveItemAsync(long productId, string? sessionToken, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v1/cart/select-all — chọn/bỏ chọn tất cả, món hết hàng không được tick (<c>usp_Cart_SelectAll</c>).</summary>
    Task SelectAllAsync(
        SelectAllCartItemsRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/cart/items/bulk — thêm NHIỀU sản phẩm trong MỘT giao dịch (<c>usp_Cart_AddItems</c>).
    /// Dùng cho tab "Tự build PC": khách chốt cấu hình ⇒ thêm mọi linh kiện một lần, không sợ thêm nửa chừng.
    /// Bất kỳ dòng nào ngừng bán hoặc thiếu tồn ⇒ không thêm gì và báo rõ sản phẩm lỗi (404/409).
    /// </summary>
    Task<AddedCartItemsResultDto> AddItemsAsync(
        AddCartItemsRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/cart/merge — gộp giỏ tạm vào giỏ tài khoản sau khi đăng nhập (CART-02, <c>usp_Cart_Merge</c>).
    /// Cần đăng nhập và tài khoản phải gắn hồ sơ khách hàng; proc tự cắt số lượng theo tồn khả dụng.
    /// </summary>
    Task MergeAsync(string? sessionToken, CancellationToken cancellationToken = default);
}
