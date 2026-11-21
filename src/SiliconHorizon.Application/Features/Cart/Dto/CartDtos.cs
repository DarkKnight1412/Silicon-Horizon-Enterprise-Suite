namespace SiliconHorizon.Application.Features.Cart.Dto;

/// <summary>
/// Toàn bộ giỏ hàng — gom 3 result set của <c>usp_Cart_Get</c> thành một DTO.
/// </summary>
/// <remarks>
/// <c>usp_Cart_Get</c> tự kiểm tra tồn tức thời trước khi trả dữ liệu: món hết hàng bị bỏ tick,
/// món có tồn ít hơn số lượng trong giỏ bị giảm số lượng; hai trường hợp đó nằm ở <see cref="Warnings"/>.
/// Xem giỏ không tạo giỏ hàng mới (proc gọi với <c>@Create = 0</c>).
/// </remarks>
public class CartDto
{
    /// <summary>Các dòng giỏ hàng, sắp theo thời điểm thêm vào giỏ.</summary>
    public IReadOnlyList<CartItemDto> Items { get; init; } = Array.Empty<CartItemDto>();

    /// <summary>Tổng tiền các dòng đang được tick (dùng để hiển thị trước khi đặt hàng).</summary>
    public CartTotalsDto Totals { get; init; } = new();

    /// <summary>Cảnh báo tồn kho — rỗng nghĩa là giỏ vẫn khớp tồn thực tế.</summary>
    public IReadOnlyList<CartWarningDto> Warnings { get; init; } = Array.Empty<CartWarningDto>();
}

/// <summary>Một dòng giỏ hàng (result set 1 của <c>usp_Cart_Get</c>).</summary>
public class CartItemDto
{
    public long CartItemId { get; init; }

    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? ImageUrl { get; init; }

    public int Quantity { get; init; }

    /// <summary>Dòng có được tick để đặt hàng hay không.</summary>
    public bool IsSelected { get; init; }

    /// <summary>Giá niêm yết.</summary>
    public decimal BasePrice { get; init; }

    /// <summary>Đơn giá sau khuyến mãi và bảng giá theo nhóm khách.</summary>
    public decimal UnitPrice { get; init; }

    public long? PromotionId { get; init; }

    /// <summary>Phần giảm trên mỗi đơn vị.</summary>
    public decimal DiscountPerUnit { get; init; }

    /// <summary>Thành tiền dòng.</summary>
    public decimal LineTotal { get; init; }

    /// <summary>Tồn khả dụng tức thời — client nên chặn tăng số lượng vượt giá trị này.</summary>
    public int AvailableQty { get; init; }
}

/// <summary>Tổng tiền các dòng được tick (result set 2 của <c>usp_Cart_Get</c>).</summary>
public class CartTotalsDto
{
    /// <summary>Số dòng đang được tick.</summary>
    public int SelectedCount { get; init; }

    /// <summary>Tổng giá niêm yết của các dòng được tick.</summary>
    public decimal SubTotal { get; init; }

    /// <summary>Tổng tiền khuyến mãi đã trừ.</summary>
    public decimal DiscountTotal { get; init; }

    /// <summary>Thành tiền phải trả (<c>SubTotal - DiscountTotal</c>).</summary>
    public decimal Total { get; init; }
}

/// <summary>Cảnh báo tồn kho (result set 3 của <c>usp_Cart_Get</c>).</summary>
public class CartWarningDto
{
    public long ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary><c>OUT_OF_STOCK</c> = hết hàng và đã bị bỏ tick; <c>QUANTITY_REDUCED</c> = đã giảm số lượng theo tồn.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Số lượng trong giỏ trước khi proc điều chỉnh.</summary>
    public int OldQuantity { get; init; }

    /// <summary>Số lượng sau khi proc điều chỉnh (với <c>OUT_OF_STOCK</c> thì bằng <see cref="OldQuantity"/>, chỉ bỏ tick).</summary>
    public int NewQuantity { get; init; }
}

/// <summary>POST /api/v1/cart/items — khớp <c>usp_Cart_AddItem</c>.</summary>
/// <remarks>
/// Không có <c>customerId</c> trong body: chủ giỏ luôn lấy từ token hoặc header <c>X-Cart-Session</c>.
/// </remarks>
public class AddCartItemRequest
{
    /// <summary>Sản phẩm cần thêm; phải đang ở trạng thái kinh doanh, nếu không proc báo 50001 (404).</summary>
    public long ProductId { get; set; }

    /// <summary>Số lượng; phải lớn hơn 0 (proc báo 50004 nếu không).</summary>
    public int Quantity { get; set; } = 1;

    /// <summary><c>true</c> = đặt đúng số lượng này, <c>false</c> = cộng dồn vào số lượng đang có trong giỏ.</summary>
    public bool Replace { get; set; }
}

/// <summary>Sản phẩm sau khi thêm vào giỏ — result set của <c>usp_Cart_AddItem</c>.</summary>
public class AddedCartItemDto
{
    public long CartId { get; init; }

    public long ProductId { get; init; }

    /// <summary>Số lượng cuối cùng trong giỏ (đã cộng dồn hoặc thay thế).</summary>
    public int Quantity { get; init; }
}

/// <summary>PATCH /api/v1/cart/items/{productId} — khớp <c>usp_Cart_UpdateItem</c>.</summary>
/// <remarks>Cả hai trường đều bỏ trống thì API trả 400 (không có gì để cập nhật).</remarks>
public class UpdateCartItemRequest
{
    /// <summary>Số lượng mới; bỏ trống = giữ nguyên. Giá trị <c>&lt;= 0</c> làm proc xóa dòng khỏi giỏ.</summary>
    public int? Quantity { get; set; }

    /// <summary>Tick/bỏ tick dòng; bỏ trống = giữ nguyên. Không tick được món đã hết hàng (proc báo 50003).</summary>
    public bool? IsSelected { get; set; }
}

/// <summary>POST /api/v1/cart/select-all — khớp <c>usp_Cart_SelectAll</c>.</summary>
public class SelectAllCartItemsRequest
{
    /// <summary><c>true</c> = chọn tất cả, <c>false</c> = bỏ chọn tất cả. Món hết hàng không bao giờ được tick.</summary>
    public bool IsSelected { get; set; } = true;
}
