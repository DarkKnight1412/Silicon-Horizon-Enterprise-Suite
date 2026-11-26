using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;

namespace SiliconHorizon.Application.Features.Cart;

/// <summary>
/// Bộ đôi nhận dạng chủ giỏ hàng — đúng MỘT trong hai có giá trị, giá trị còn lại là <c>NULL</c>
/// (khớp tham số <c>@CustomerId</c> / <c>@SessionToken</c> của các proc giỏ hàng).
/// </summary>
public sealed record CartOwner(long? CustomerId, string? SessionToken);

/// <summary>
/// Xác định chủ giỏ hàng cho MỌI use case động vào giỏ — dùng chung cho <c>CartService</c> và
/// <c>PcBuildService</c> (thêm cả bộ PC build sẵn vào giỏ) để quy tắc bảo mật chỉ có một nơi.
/// <para>
/// Khách đã đăng nhập ⇒ dùng <c>CustomerId</c> lấy từ token (API ÉP, không tin giá trị client gửi);
/// khách vãng lai ⇒ dùng <c>sessionToken</c> mà tầng API đọc từ header <c>X-Cart-Session</c>;
/// thiếu cả hai ⇒ 400 (proc cũng báo 50004 trong trường hợp này).
/// </para>
/// </summary>
public static class CartOwnerResolver
{
    /// <summary><c>@SessionToken</c> của proc là <c>varchar(64)</c> — chặn sớm để trả 400 thay vì lỗi hệ thống khi tràn cột.</summary>
    public const int SessionTokenMaxLength = 64;

    /// <summary>Xác định chủ giỏ; ném <see cref="BadRequestException"/> khi không xác định được.</summary>
    public static CartOwner Resolve(ICurrentUser currentUser, string? sessionToken)
    {
        if (currentUser.CustomerId is { } customerId)
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
    public static string? NormalizeSessionToken(string? sessionToken)
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
}
