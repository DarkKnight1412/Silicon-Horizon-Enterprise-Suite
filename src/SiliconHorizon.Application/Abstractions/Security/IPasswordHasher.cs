namespace SiliconHorizon.Application.Abstractions.Security;

/// <summary>
/// Băm và kiểm tra mật khẩu. Chuỗi băm lưu ở <c>Users.PasswordHash</c> (varchar(255)) và
/// <b>không bao giờ</b> được trả ra API (NFR-03).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Tạo chuỗi băm mới (kèm salt ngẫu nhiên và tham số thuật toán).</summary>
    string Hash(string password);

    /// <summary>Kiểm tra mật khẩu với chuỗi băm đã lưu (so sánh an toàn theo thời gian).</summary>
    bool Verify(string password, string passwordHash);
}
