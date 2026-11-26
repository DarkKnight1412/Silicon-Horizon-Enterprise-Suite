using System.Text.Json;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Cart.Dto;

namespace SiliconHorizon.Application.Features.Cart;

/// <summary>
/// Use case giỏ hàng (CART-01..11) — chỉ gọi stored procedure trong Database/02_sp_foundation_catalog_cart.sql.
/// </summary>
/// <remarks>
/// Chủ giỏ: khách đã đăng nhập dùng <c>CustomerId</c> lấy từ token (API ép, không tin giá trị client gửi);
/// khách vãng lai dùng <c>sessionToken</c> mà tầng API đọc từ header <c>X-Cart-Session</c>. Thiếu cả hai
/// thì ném <see cref="BadRequestException"/> (proc cũng báo 50004 trong trường hợp này).
/// </remarks>
public sealed class CartService : ICartService
{
    /// <summary>JSON camelCase đúng như proc đọc bằng <c>OPENJSON</c> (productId · quantity).</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;

    public CartService(ISqlExecutor sqlExecutor, ICurrentUser currentUser)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
    }

    public async Task<CartDto> GetAsync(string? sessionToken, CancellationToken cancellationToken = default)
    {
        var owner = ResolveOwner(sessionToken);

        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.CartGet,
            new { owner.CustomerId, owner.SessionToken },
            cancellationToken);

        // Thứ tự result set của usp_Cart_Get: (1) dòng giỏ hàng, (2) tổng tiền dòng được tick, (3) cảnh báo tồn.
        var items = await results.ReadListAsync<CartItemRow>(cancellationToken);
        var totals = await results.ReadFirstOrDefaultAsync<CartTotalsRow>(cancellationToken);
        var warnings = await results.ReadListAsync<CartWarningRow>(cancellationToken);

        return new CartDto
        {
            Items = items.Select(ToItemDto).ToList(),
            Totals = ToTotalsDto(totals),
            Warnings = warnings.Select(ToWarningDto).ToList()
        };
    }

    public async Task<AddedCartItemDto> AddItemAsync(
        AddCartItemRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default)
    {
        var owner = ResolveOwner(sessionToken);

        // Proc tự kiểm tra: số lượng > 0 (50004), sản phẩm đang kinh doanh (50001),
        // và số lượng sau khi cộng dồn không vượt tồn khả dụng (50003).
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<AddedCartItemRow>(
            StoredProcedureNames.CartAddItem,
            new { owner.CustomerId, owner.SessionToken, request.ProductId, request.Quantity, request.Replace },
            cancellationToken)
            ?? throw new ConflictException("Không thêm được sản phẩm vào giỏ hàng.", "CART-ADD-FAILED");

        return new AddedCartItemDto
        {
            CartId = row.CartId,
            ProductId = row.ProductId,
            Quantity = row.Quantity
        };
    }

    public async Task UpdateItemAsync(
        long productId,
        UpdateCartItemRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity is null && request.IsSelected is null)
        {
            throw new BadRequestException("Cần cung cấp ít nhất một trong hai: quantity hoặc isSelected.");
        }

        var owner = ResolveOwner(sessionToken);

        // Proc: dòng không có trong giỏ → 404 (50001); số lượng <= 0 → xóa dòng;
        // vượt tồn khả dụng hoặc tick món đã hết hàng → 409 (50003).
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.CartUpdateItem,
            new
            {
                owner.CustomerId,
                owner.SessionToken,
                ProductId = productId,
                request.Quantity,
                request.IsSelected
            },
            cancellationToken);
    }

    public async Task RemoveItemAsync(
        long productId,
        string? sessionToken,
        CancellationToken cancellationToken = default)
    {
        var owner = ResolveOwner(sessionToken);

        // Idempotent: xóa dòng không còn trong giỏ vẫn thành công vì proc không báo lỗi.
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.CartRemoveItem,
            new { owner.CustomerId, owner.SessionToken, ProductId = productId },
            cancellationToken);
    }

    public async Task SelectAllAsync(
        SelectAllCartItemsRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default)
    {
        var owner = ResolveOwner(sessionToken);

        // Chọn tất cả chỉ áp cho món còn tồn khả dụng; bỏ chọn tất cả áp cho mọi dòng.
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.CartSelectAll,
            new { owner.CustomerId, owner.SessionToken, request.IsSelected },
            cancellationToken);
    }

    public async Task MergeAsync(string? sessionToken, CancellationToken cancellationToken = default)
    {
        var customerId = _currentUser.CustomerId
            ?? throw new ForbiddenException("Chỉ tài khoản khách hàng mới gộp được giỏ tạm vào giỏ tài khoản.");

        var token = NormalizeSessionToken(sessionToken)
            ?? throw new BadRequestException("Thiếu mã phiên giỏ tạm (header X-Cart-Session).");

        // Proc: cộng dồn nhưng không vượt tồn khả dụng, bỏ món đã hết hàng, rồi xóa giỏ tạm (CART-02).
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.CartMerge,
            new { SessionToken = token, CustomerId = customerId },
            cancellationToken);
    }

    public async Task<AddedCartItemsResultDto> AddItemsAsync(
        AddCartItemsRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new BadRequestException("Cần ít nhất một sản phẩm để thêm vào giỏ hàng.");
        }

        var owner = ResolveOwner(sessionToken);
        var itemsJson = JsonSerializer.Serialize(
            request.Items.Select(item => new { productId = item.ProductId, quantity = item.Quantity }),
            JsonOptions);

        // Proc thêm TẤT CẢ trong một giao dịch: dòng nào ngừng bán (50001) hoặc vượt tồn (50003)
        // thì KHÔNG thêm gì cả ⇒ giỏ hàng không bao giờ ở trạng thái nửa vời.
        var rows = await _sqlExecutor.QueryAsync<AddedCartItemsRow>(
            StoredProcedureNames.CartAddItems,
            new { owner.CustomerId, owner.SessionToken, ItemsJson = itemsJson },
            cancellationToken);

        if (rows.Count == 0)
        {
            throw new ConflictException("Không thêm được sản phẩm vào giỏ hàng.", "CART-ADD-ITEMS-FAILED");
        }

        return new AddedCartItemsResultDto
        {
            ItemCount = rows.Count,
            Items = rows.Select(row => new AddedCartItemDetailDto
            {
                ProductId = row.ProductId,
                Name = row.Name,
                AddedQuantity = row.AddedQuantity,
                CartQuantity = row.CartQuantity
            }).ToList()
        };
    }

    /// <summary>
    /// Xác định chủ giỏ hàng — uỷ quyền cho <see cref="CartOwnerResolver"/> để quy tắc bảo mật
    /// (customerId ÉP từ token · mã phiên vãng lai ở header X-Cart-Session) chỉ tồn tại ở MỘT nơi,
    /// dùng chung với các use case khác cũng ghi vào giỏ (ví dụ thêm cả bộ PC build sẵn).
    /// </summary>
    private CartOwner ResolveOwner(string? sessionToken)
        => CartOwnerResolver.Resolve(_currentUser, sessionToken);

    /// <summary>Chuẩn hóa mã phiên khách vãng lai (uỷ quyền cho <see cref="CartOwnerResolver"/>).</summary>
    private static string? NormalizeSessionToken(string? sessionToken)
        => CartOwnerResolver.NormalizeSessionToken(sessionToken);

    private static CartItemDto ToItemDto(CartItemRow row) => new()
    {
        CartItemId = row.CartItemId,
        ProductId = row.ProductId,
        Sku = row.Sku,
        Name = row.Name,
        ImageUrl = row.ImageUrl,
        Quantity = row.Quantity,
        IsSelected = row.IsSelected,
        BasePrice = row.BasePrice,
        UnitPrice = row.UnitPrice,
        PromotionId = row.PromotionId,
        DiscountPerUnit = row.DiscountPerUnit,
        LineTotal = row.LineTotal,
        AvailableQty = row.AvailableQty
    };

    /// <summary>Giỏ rỗng thì proc trả tổng tiền bằng 0, chỉ phòng trường hợp không có dòng nào.</summary>
    private static CartTotalsDto ToTotalsDto(CartTotalsRow? row) => row is null
        ? new CartTotalsDto()
        : new CartTotalsDto
        {
            SelectedCount = row.SelectedCount,
            SubTotal = row.SubTotal,
            DiscountTotal = row.DiscountTotal,
            Total = row.Total
        };

    private static CartWarningDto ToWarningDto(CartWarningRow row) => new()
    {
        ProductId = row.ProductId,
        Name = row.Name,
        Code = row.Code,
        OldQuantity = row.OldQuantity,
        NewQuantity = row.NewQuantity
    };
}
