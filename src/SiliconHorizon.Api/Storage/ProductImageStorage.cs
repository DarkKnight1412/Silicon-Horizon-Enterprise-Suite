using SiliconHorizon.Application.Features.Admin;

namespace SiliconHorizon.Api.Storage;

/// <summary>
/// Ghi file ảnh sản phẩm vào <c>wwwroot/&lt;thư mục&gt;</c> của web API và trả đường dẫn TƯƠNG ĐỐI
/// (controller sẽ ghép base URL để client lưu vào <c>ProductImages.Url</c>).
/// <para>
/// Đây là hiện thực của abstraction <see cref="IProductImageStorage"/> ở tầng Application — giống cách
/// <c>ICurrentUser</c> được hiện thực trong tầng Api (xem ARCHITECTURE.md).
/// </para>
/// </summary>
public sealed class ProductImageStorage : IProductImageStorage
{
    /// <summary>Định dạng ảnh cho phép upload.</summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly string _folder;

    public ProductImageStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _folder = configuration.GetValue("Storage:ProductImagesFolder", "images/products") ?? "images/products";
    }

    public async Task<StoredProductImage> SaveAsync(
        string sku,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"Định dạng ảnh không hỗ trợ (\"{extension}\"). Chỉ nhận: {string.Join(", ", AllowedExtensions)}.");
        }

        // Tên file = <sku an toàn>-<8 ký tự ngẫu nhiên>.<ext> ⇒ không trùng, không path traversal.
        var fileName = $"{Slug(sku)}-{Guid.NewGuid().ToString("N")[..8]}{extension.ToLowerInvariant()}";

        var root = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        var directory = Path.Combine(root, _folder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        await using (var target = File.Create(Path.Combine(directory, fileName)))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        return new StoredProductImage($"/{_folder.Trim('/')}/{fileName}");
    }

    /// <summary>Chuẩn hoá SKU thành phần tên file an toàn (chỉ a-z, 0-9 và dấu gạch).</summary>
    private static string Slug(string value)
    {
        var slug = new string(value.Trim().ToLowerInvariant()
            .Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');

        return slug.Length == 0 ? "san-pham" : slug;
    }
}
