namespace SiliconHorizon.Application.Features.Cart.Dto;

// Lớp ánh xạ cột result set của proc giỏ hàng — tên property PHẢI khớp đúng tên cột.
// Dùng property get; set; thường (không phải init) để Dapper gán được.

/// <summary>Result set 1 của <c>usp_Cart_Get</c> — một dòng giỏ hàng; giá đã được proc áp bảng giá nhóm khách + khuyến mãi.</summary>
public class CartItemRow
{
    public long CartItemId { get; set; }

    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public int Quantity { get; set; }

    public bool IsSelected { get; set; }

    /// <summary>Giá niêm yết.</summary>
    public decimal BasePrice { get; set; }

    /// <summary>Đơn giá sau bảng giá nhóm khách và khuyến mãi đang hiệu lực.</summary>
    public decimal UnitPrice { get; set; }

    public long? PromotionId { get; set; }

    /// <summary>Phần khuyến mãi trên mỗi đơn vị.</summary>
    public decimal DiscountPerUnit { get; set; }

    /// <summary>Thành tiền dòng = Quantity * UnitPrice.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Tồn khả dụng tức thời của sản phẩm (tổng các chi nhánh đang hoạt động).</summary>
    public int AvailableQty { get; set; }
}

/// <summary>Result set 2 của <c>usp_Cart_Get</c> — luôn có đúng một dòng, chỉ tính các dòng đang được tick.</summary>
public class CartTotalsRow
{
    public int SelectedCount { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal Total { get; set; }
}

/// <summary>Result set 3 của <c>usp_Cart_Get</c> — cảnh báo tồn kho tại thời điểm xem giỏ (CART-07).</summary>
public class CartWarningRow
{
    public long ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>OUT_OF_STOCK hoặc QUANTITY_REDUCED.</summary>
    public string Code { get; set; } = string.Empty;

    public int OldQuantity { get; set; }

    public int NewQuantity { get; set; }
}

/// <summary>Result set của <c>usp_Cart_AddItem</c> — trạng thái dòng giỏ sau khi thêm.</summary>
public class AddedCartItemRow
{
    public long CartId { get; set; }

    public long ProductId { get; set; }

    /// <summary>Số lượng sau khi cộng dồn hoặc thay thế.</summary>
    public int Quantity { get; set; }
}
