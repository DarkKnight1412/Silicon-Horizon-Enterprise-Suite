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
    /// <summary><c>@SessionToken</c> của proc là <c>varchar(64)</c> — chặn sớm để trả 400 thay vì lỗi hệ thống khi tràn cột.</summary>
    private const int SessionTokenMaxLength = 64;

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

    /// <summary>
    /// Xác định chủ giỏ hàng. Ưu tiên khách đã đăng nhập (<c>CustomerId</c> từ token — API ép, không tin client),
    /// ngược lại dùng sessionToken của khách vãng lai; thiếu cả hai → 400.
    /// </summary>
    private CartOwner ResolveOwner(string? sessionToken)
    {
        if (_currentUser.CustomerId is { } customerId)
        {
            // Giỏ theo tài khoản: không truyền sessionToken để proc không tra nhầm giỏ vãng lai.
            return new CartOwner(customerId, null);
        }

        var token = NormalizeSessionToken(sessionToken)
            ?? throw new BadRequestException(
                "Thiếu thông tin chủ giỏ hàng: cần đăng nhập hoặc gửi mã phiên khách vãng lai qua header X-Cart-Session.");

        return new CartOwner(null, token);
    }

    /// <summary>
    /// Chuẩn hóa sessionToken khách vãng lai: rỗng → <c>null</c>; dài quá <c>varchar(64)</c> của proc → 400.
    /// </summary>
    private static string? NormalizeSessionToken(string? sessionToken)
    {
        var token = sessionToken?.Trim();

        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        if (token.Length > SessionTokenMaxLength)
        {
            throw new BadRequestException($"Mã phiên khách vãng lai tối đa {SessionTokenMaxLength} ký tự.");
        }

        return token;
    }

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

    /// <summary>Bộ đôi nhận dạng chủ giỏ — đúng một trong hai có giá trị, giá trị còn lại là <c>NULL</c>.</summary>
    private sealed record CartOwner(long? CustomerId, string? SessionToken);
}
