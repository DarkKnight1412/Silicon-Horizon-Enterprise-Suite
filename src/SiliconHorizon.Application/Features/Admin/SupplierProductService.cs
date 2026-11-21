using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Entities.Inventory;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case liên kết nhà cung cấp ↔ sản phẩm (EF Core trên <c>dbo.SupplierProducts</c>).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Khóa chính tổ hợp</b> <c>(SupplierId, ProductId)</c> nên không dùng <c>GetByIdAsync</c> (vốn nhận
/// một khóa); service đọc bảng nối rồi tìm theo cặp khóa — bảng nhỏ, ít thay đổi.</item>
/// <item><b>Kiểm tra tồn tại</b>: nhà cung cấp và sản phẩm phải tồn tại (khóa ngoại của bảng nối) → 404/400.</item>
/// <item><b>Nhật ký</b>: thêm/sửa/gỡ liên kết đều gọi <c>usp_Audit_Write</c> vì ảnh hưởng tới lập phiếu nhập.</item>
/// </list>
/// </remarks>
public sealed class SupplierProductService : ISupplierProductService
{
    /// <summary>SupplierProducts.SupplierSku varchar(50).</summary>
    private const int SupplierSkuMaxLength = 50;

    private readonly IReadRepository<SupplierProduct> _readRepository;
    private readonly IWriteRepository<SupplierProduct> _writeRepository;
    private readonly IReadRepository<Supplier> _suppliers;
    private readonly IReadRepository<Product> _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogWriter _auditLogWriter;

    public SupplierProductService(
        IReadRepository<SupplierProduct> readRepository,
        IWriteRepository<SupplierProduct> writeRepository,
        IReadRepository<Supplier> suppliers,
        IReadRepository<Product> products,
        IUnitOfWork unitOfWork,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _suppliers = suppliers;
        _products = products;
        _unitOfWork = unitOfWork;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierProductDto>> GetBySupplierAsync(
        long supplierId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSupplierExistsAsync(supplierId, cancellationToken);

        var productLookup = (await _products.ListAsync(cancellationToken)).ToDictionary(product => product.Id);

        var links = await _readRepository.ListAsync(cancellationToken);

        return links
            .Where(link => link.SupplierId == supplierId)
            .OrderBy(link => link.ProductId)
            .Select(link => SupplierProductDto.FromEntity(
                link,
                productLookup.TryGetValue(link.ProductId, out var product) ? product : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SupplierProductDto> GetByIdAsync(
        long supplierId,
        long productId,
        CancellationToken cancellationToken = default)
        => await ToDtoAsync(await GetLinkOrThrowAsync(supplierId, productId, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<SupplierProductDto> CreateAsync(
        long supplierId,
        UpsertSupplierProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSupplierExistsAsync(supplierId, cancellationToken);
        InputGuard.EnsurePositive(request.ProductId, "productId");
        await EnsureProductExistsAsync(request.ProductId, cancellationToken);

        var supplierSku = InputGuard.OptionalText(request.SupplierSku, SupplierSkuMaxLength, "Mã sản phẩm nhà cung cấp");

        var links = await _readRepository.ListAsync(cancellationToken);

        if (links.Any(link => link.SupplierId == supplierId && link.ProductId == request.ProductId))
        {
            throw new ConflictException(
                $"Sản phẩm Id = {request.ProductId} đã được gắn với nhà cung cấp Id = {supplierId}.",
                "SQL-50005");
        }

        var link = new SupplierProduct
        {
            SupplierId = supplierId,
            ProductId = request.ProductId,
            SupplierSku = supplierSku
        };

        await _writeRepository.AddAsync(link, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "SupplierProductLinked",
            "SupplierProduct",
            supplierId,
            newValues: new { link.SupplierId, link.ProductId, link.SupplierSku },
            reason: $"Gắn sản phẩm {link.ProductId} vào nhà cung cấp {supplierId}.",
            cancellationToken: cancellationToken);

        return await ToDtoAsync(link, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierProductDto> UpdateAsync(
        long supplierId,
        long productId,
        UpsertSupplierProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var link = await GetLinkOrThrowAsync(supplierId, productId, cancellationToken);
        var supplierSku = InputGuard.OptionalText(request.SupplierSku, SupplierSkuMaxLength, "Mã sản phẩm nhà cung cấp");

        var oldSku = link.SupplierSku;
        link.SupplierSku = supplierSku;

        _writeRepository.Update(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "SupplierProductUpdated",
            "SupplierProduct",
            supplierId,
            oldValues: new { SupplierSku = oldSku },
            newValues: new { link.SupplierSku },
            reason: $"Sửa liên kết nhà cung cấp {supplierId} — sản phẩm {productId}.",
            cancellationToken: cancellationToken);

        return await ToDtoAsync(link, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long supplierId, long productId, CancellationToken cancellationToken = default)
    {
        var link = await GetLinkOrThrowAsync(supplierId, productId, cancellationToken);

        _writeRepository.Remove(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "SupplierProductUnlinked",
            "SupplierProduct",
            supplierId,
            oldValues: new { link.SupplierId, link.ProductId, link.SupplierSku },
            reason: $"Gỡ sản phẩm {productId} khỏi nhà cung cấp {supplierId}.",
            cancellationToken: cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Đọc dòng liên kết theo cặp khóa; không có → 404.</summary>
    private async Task<SupplierProduct> GetLinkOrThrowAsync(
        long supplierId,
        long productId,
        CancellationToken cancellationToken)
    {
        InputGuard.EnsurePositive(supplierId, "supplierId");
        InputGuard.EnsurePositive(productId, "productId");

        var links = await _readRepository.ListAsync(cancellationToken);

        return links.FirstOrDefault(link => link.SupplierId == supplierId && link.ProductId == productId)
               ?? throw new NotFoundException(
                   $"Nhà cung cấp Id = {supplierId} chưa gắn sản phẩm Id = {productId}.");
    }

    private async Task<SupplierProductDto> ToDtoAsync(
        SupplierProduct link,
        CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(link.ProductId, cancellationToken);

        return SupplierProductDto.FromEntity(link, product);
    }

    private async Task EnsureSupplierExistsAsync(long supplierId, CancellationToken cancellationToken)
    {
        InputGuard.EnsurePositive(supplierId, "supplierId");

        var suppliers = await _suppliers.ListAsync(cancellationToken);

        if (suppliers.All(supplier => supplier.Id != supplierId))
        {
            throw new NotFoundException($"Không tìm thấy nhà cung cấp Id = {supplierId}.");
        }
    }

    private async Task EnsureProductExistsAsync(long productId, CancellationToken cancellationToken)
    {
        var products = await _products.ListAsync(cancellationToken);

        if (products.All(product => product.Id != productId))
        {
            throw new BadRequestException($"Sản phẩm Id = {productId} không tồn tại.");
        }
    }
}
