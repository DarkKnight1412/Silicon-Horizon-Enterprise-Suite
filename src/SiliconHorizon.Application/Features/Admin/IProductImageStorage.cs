namespace SiliconHorizon.Application.Features.Admin;

/// <summary>Kết quả lưu một file ảnh sản phẩm.</summary>
/// <param name="RelativePath">
/// Đường dẫn tương đối trên web API, ví dụ <c>/images/products/cpu-intel-i9-14900k-1a2b3c4d.jpg</c>.
/// </param>
public record StoredProductImage(string RelativePath);

/// <summary>
/// Lưu file ảnh sản phẩm do NHÂN VIÊN upload từ trang quản trị, để lấy đường dẫn ghi vào
/// <c>dbo.ProductImages.Url</c> (Database/README.md mục 3.3).
/// <para>
/// Hiện thực nằm ở tầng Api (ghi ra <c>wwwroot</c> của web API) vì cần biết thư mục web tĩnh —
/// cùng cách với <c>ICurrentUser</c> (abstraction ở Application, hiện thực ở Api).
/// </para>
/// </summary>
public interface IProductImageStorage
{
    /// <summary>
    /// Ghi file ảnh vào thư mục ảnh sản phẩm và trả đường dẫn tương đối để client ghép base URL.
    /// Ném <see cref="InvalidOperationException"/> khi định dạng file không được hỗ trợ.
    /// </summary>
    /// <param name="sku">Mã SKU dùng làm tiền tố tên file (được chuẩn hoá an toàn trước khi ghi).</param>
    Task<StoredProductImage> SaveAsync(
        string sku,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default);
}
