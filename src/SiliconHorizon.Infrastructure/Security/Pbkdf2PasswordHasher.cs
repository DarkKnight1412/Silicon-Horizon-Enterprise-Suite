using System.Globalization;
using System.Security.Cryptography;
using SiliconHorizon.Application.Abstractions.Security;

namespace SiliconHorizon.Infrastructure.Security;

/// <summary>
/// Băm mật khẩu bằng PBKDF2-HMACSHA256 — thuật toán có sẵn trong .NET nên không cần package ngoài.
/// <para>
/// Định dạng lưu: <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;base64Salt&gt;$&lt;base64Hash&gt;</c>
/// (khoảng 90 ký tự, vừa với <c>Users.PasswordHash varchar(255)</c>).
/// Tham số thuật toán nằm trong chuỗi băm nên có thể nâng số vòng lặp sau này mà vẫn kiểm tra được mật khẩu cũ.
/// </para>
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string AlgorithmName = "pbkdf2-sha256";
    private const int SaltSize = 16;          // 128 bit
    private const int KeySize = 32;           // 256 bit
    private const int Iterations = 210_000;   // khuyến nghị OWASP cho PBKDF2-HMACSHA256

    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithm, KeySize);

        return string.Join(
            '$',
            AlgorithmName,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$');

        if (parts.Length != 4 || !string.Equals(parts[0], AlgorithmName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations) || iterations < 1)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedKey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expectedKey.Length == 0)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithm, expectedKey.Length);

        // So sánh theo thời gian hằng định để không lộ thông tin qua thời gian phản hồi.
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
