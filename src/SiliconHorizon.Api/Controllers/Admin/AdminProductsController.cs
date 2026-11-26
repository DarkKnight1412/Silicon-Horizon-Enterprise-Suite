using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Catalog;
using SiliconHorizon.Application.Features.Catalog.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// Quản trị sản phẩm (PRD-01..09, PRC-01/06/07) — Database/README.md mục 3.2.
/// <para>
/// Quyền theo vai trò tại từng action (không đặt ở cấp class, vì endpoint giá cho phép cả Quản lý chi nhánh).
/// Khi đã seed bảng Permissions, siết thêm <c>[RequirePermission(PermissionCodes.CatalogManage)]</c>.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/products")]
[Authorize]
[Produces("application/json")]
public class AdminProductsController : ControllerBase
{
    /// <summary>Giới hạn dung lượng ảnh upload (5 MB).</summary>
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    private readonly IProductService _productService;
    private readonly IProductImageStorage _imageStorage;

    public AdminProductsController(IProductService productService, IProductImageStorage imageStorage)
    {
        _productService = productService;
        _imageStorage = imageStorage;
    }

    /// <summary>POST /api/v1/admin/products — tạo sản phẩm kèm thông số theo loại và ảnh (UC-25).</summary>
    [HttpPost]
    [Authorize(Roles = RoleCodes.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var productId = await _productService.CreateAsync(request, cancellationToken);

        // Trả về 201 kèm đường dẫn chi tiết sản phẩm vừa tạo.
        return Created($"/api/v1/products/{productId}", new { productId });
    }

    /// <summary>
    /// POST /api/v1/admin/products/images — nhân viên upload MỘT ảnh sản phẩm (multipart/form-data).
    /// <para>
    /// Field: <c>file</c> (bắt buộc, ≤ 5 MB, jpg/jpeg/png/webp) và <c>sku</c> (dùng làm tiền tố tên file).
    /// File lưu vào <c>wwwroot/images/products/</c> của API (được phục vụ qua <c>UseStaticFiles()</c>),
    /// nên KHÔNG cần build lại web khách hàng khi thêm ảnh.
    /// </para>
    /// <para>
    /// Trả <c>{ "url": "..." }</c> là URL **đầy đủ** để client ghi vào <c>ProductImages.Url</c> khi lưu
    /// sản phẩm (Database/README.md mục 3.3). Nếu triển khai sau reverse proxy, cấu hình
    /// <c>Storage:PublicBaseUrl</c> để URL trả về luôn đúng domain công khai.
    /// </para>
    /// </summary>
    [HttpPost("images")]
    [Authorize(Roles = RoleCodes.Admin)]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(
        [FromForm] string? sku,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return InvalidImage("Vui lòng chọn một file ảnh.");
        }

        if (file.Length > MaxUploadBytes)
        {
            return InvalidImage("Ảnh tối đa 5 MB.");
        }

        StoredProductImage stored;

        try
        {
            await using var stream = file.OpenReadStream();
            stored = await _imageStorage.SaveAsync(sku ?? string.Empty, stream, file.FileName, cancellationToken);
        }
        catch (InvalidOperationException error)
        {
            return InvalidImage(error.Message);
        }

        var baseUrl = Request.Scheme + "://" + Request.Host;

        return Ok(new { url = baseUrl.TrimEnd('/') + stored.RelativePath });
    }

    /// <summary>Trả 400 kèm lỗi ở field <c>file</c> để client hiển thị đúng trường bị sai.</summary>
    private IActionResult InvalidImage(string message)
        => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["file"] = new[] { message }
        }));

    /// <summary>PUT /api/v1/admin/products/{id} — sửa sản phẩm (không đổi danh mục), thay toàn bộ thông số/ảnh.</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleCodes.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        await _productService.UpdateAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>PATCH /api/v1/admin/products/{id}/status — 0 Draft, 1 Active, 2 Discontinued.</summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = RoleCodes.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetStatus(
        long id,
        [FromBody] SetProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _productService.SetStatusAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// PUT /api/v1/admin/products/{id}/prices/{groupId} — đặt giá theo nhóm khách.
    /// Giá thấp hơn giá vốn chỉ được lưu khi người gọi là Quản lý chi nhánh/Admin (proc kiểm tra).
    /// </summary>
    [HttpPut("{id:long}/prices/{groupId:int}")]
    [Authorize(Roles = RoleCodes.Approvers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertPrice(
        long id,
        int groupId,
        [FromBody] UpsertProductPriceRequest request,
        CancellationToken cancellationToken)
    {
        await _productService.UpsertPriceAsync(id, groupId, request, cancellationToken);

        return NoContent();
    }
}
