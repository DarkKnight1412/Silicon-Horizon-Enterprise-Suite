using System.Text.Json;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Catalog.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Catalog;

/// <summary>
/// Hiện thực use case danh mục hàng hoá qua stored procedure.
/// Thông số kỹ thuật và ảnh được serialize camelCase đúng như proc đọc bằng <c>OPENJSON</c>.
/// </summary>
public sealed class ProductService : IProductService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;

    public ProductService(ISqlExecutor sqlExecutor, ICurrentUser currentUser)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ProductListItemDto>> SearchAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var rows = await _sqlExecutor.QueryAsync<ProductSearchRow>(
            StoredProcedureNames.ProductSearch,
            new
            {
                CustomerGroupId = request.CustomerGroupId,
                CategoryCode = request.CategoryCode,
                BrandId = request.BrandId,
                Keyword = request.Keyword,
                MinPrice = request.MinPrice,
                MaxPrice = request.MaxPrice,
                InStockOnly = request.InStockOnly,
                OnPromotion = request.OnPromotion,
                SpecFiltersJson = BuildSpecFiltersJson(request.ParseSpecFilters()),
                SortBy = request.SortBy,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            },
            cancellationToken);

        return PagedResult<ProductListItemDto>.Create(
            rows.Select(ToListItem).ToList(),
            request.PageNumber,
            request.PageSize,
            rows.Count > 0 ? rows[0].TotalCount : 0);
    }

    public async Task<ProductDetailDto> GetByIdAsync(
        long id,
        int? customerGroupId = null,
        CancellationToken cancellationToken = default)
    {
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.ProductGetById,
            new { ProductId = id, CustomerGroupId = customerGroupId },
            cancellationToken);

        var header = await results.ReadFirstOrDefaultAsync<ProductDetailRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sản phẩm Id = {id}.");

        var images = await results.ReadListAsync<ProductImageRow>(cancellationToken);

        // Số result set thông số phụ thuộc SpecType: 0 (Generic) = không có; 4 (Monitor) = 2 set
        // (Specs_Monitor rồi Specs_Monitor_Ports); các loại khác = 1 set.
        IReadOnlyDictionary<string, object?>? specifications = null;
        var monitorPorts = new List<IReadOnlyDictionary<string, object?>>();

        if (header.SpecType != 0)
        {
            specifications = (await results.ReadDictionaryListAsync(cancellationToken)).FirstOrDefault();

            if (header.SpecType == 4)
            {
                monitorPorts = (await results.ReadDictionaryListAsync(cancellationToken)).ToList();
            }
        }

        var stock = await results.ReadListAsync<ProductStockByBranchRow>(cancellationToken);

        return new ProductDetailDto
        {
            Id = header.Id,
            Sku = header.Sku,
            Name = header.Name,
            Slug = header.Slug,
            BrandId = header.BrandId,
            BrandName = header.BrandName,
            CategoryId = header.CategoryId,
            CategoryCode = header.CategoryCode,
            CategoryName = header.CategoryName,
            SpecType = header.SpecType,
            ShortDescription = header.ShortDescription,
            Description = header.Description,
            RequiresSerial = header.RequiresSerial,
            WarrantyMonths = header.WarrantyMonths,
            WarrantyTerms = header.WarrantyTerms,
            Status = header.Status,
            ListPrice = header.ListPrice,
            BasePrice = header.BasePrice,
            Price = header.Price,
            PromotionId = header.PromotionId,
            AvailableQty = header.AvailableQty,
            Images = images.Select(image => new ProductImageDto
            {
                Id = image.Id,
                Url = image.Url,
                SortOrder = image.SortOrder,
                IsPrimary = image.IsPrimary
            }).ToList(),
            Specifications = specifications,
            MonitorPorts = monitorPorts,
            StockByBranch = stock.Select(item => new ProductStockByBranchDto
            {
                BranchId = item.BranchId,
                BranchName = item.BranchName,
                AvailableQty = item.AvailableQty
            }).ToList()
        };
    }

    public async Task<long> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<ProductIdRow>(
            StoredProcedureNames.ProductCreate,
            new
            {
                request.Sku,
                request.Name,
                request.Slug,
                request.BrandId,
                request.CategoryId,
                request.ShortDescription,
                request.Description,
                request.ListPrice,
                request.LastImportPrice,
                request.RequiresSerial,
                request.MinStockDefault,
                request.WarrantyMonths,
                request.WarrantyTerms,
                request.Status,
                SpecsJson = ToRawJson(request.Specs),
                ImagesJson = ToImagesJson(request.Images),
                UserId = _currentUser.UserId
            },
            cancellationToken);

        return row?.ProductId
            ?? throw new ConflictException("Không tạo được sản phẩm.", "CATALOG-CREATE-FAILED");
    }

    public Task UpdateAsync(
        long id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
        => _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.ProductUpdate,
            new
            {
                ProductId = id,
                request.Name,
                request.Slug,
                request.BrandId,
                request.ShortDescription,
                request.Description,
                request.ListPrice,
                request.RequiresSerial,
                request.MinStockDefault,
                request.WarrantyMonths,
                request.WarrantyTerms,
                SpecsJson = ToRawJson(request.Specs),
                ImagesJson = ToImagesJson(request.Images),
                UserId = _currentUser.UserId
            },
            cancellationToken);

    public Task SetStatusAsync(
        long id,
        SetProductStatusRequest request,
        CancellationToken cancellationToken = default)
        => _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.ProductSetStatus,
            new { ProductId = id, request.Status, UserId = _currentUser.UserId },
            cancellationToken);

    public Task UpsertPriceAsync(
        long id,
        int customerGroupId,
        UpsertProductPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Giá thấp hơn giá vốn chỉ lưu được khi có người duyệt: API tự xác định theo vai trò của người gọi,
        // KHÔNG dùng ApprovedBy do client gửi lên (BRD 2.2 — Quản lý chi nhánh/Admin duyệt).
        var approvedBy = IsApprover() ? _currentUser.UserId : null;

        return _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.ProductPriceUpsert,
            new
            {
                ProductId = id,
                CustomerGroupId = customerGroupId,
                request.Price,
                UserId = _currentUser.UserId,
                ApprovedBy = approvedBy
            },
            cancellationToken);
    }

    public async Task<CompatibilityCheckResultDto> CheckCompatibilityAsync(
        CompatibilityCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new BadRequestException("Cần ít nhất một sản phẩm để kiểm tra tương thích.");
        }

        var itemsJson = JsonSerializer.Serialize(
            request.Items.Select(item => new { productId = item.ProductId, quantity = item.Quantity }),
            JsonOptions);

        var warnings = await _sqlExecutor.QueryAsync<CompatibilityWarningRow>(
            StoredProcedureNames.CompatCheck,
            new { ItemsJson = itemsJson },
            cancellationToken);

        return new CompatibilityCheckResultDto
        {
            Warnings = warnings.Select(warning => new CompatibilityWarningDto
            {
                Severity = warning.Severity,
                Code = warning.Code,
                Message = warning.Message
            }).ToList()
        };
    }

    private bool IsApprover()
        => _currentUser.IsInRole(RoleCodes.Manager) || _currentUser.IsInRole(RoleCodes.Admin);

    /// <summary>Thông số gửi nguyên văn (giữ nguyên camelCase client gửi) — proc đọc bằng OPENJSON.</summary>
    private static string? ToRawJson(JsonElement? element) => element?.GetRawText();

    private static string? ToImagesJson(IReadOnlyCollection<ProductImageInput>? images)
        => images is null || images.Count == 0
            ? null
            : JsonSerializer.Serialize(images, JsonOptions);

    private static ProductListItemDto ToListItem(ProductListItemDtoShape row) => new()
    {
        Id = row.Id,
        Sku = row.Sku,
        Name = row.Name,
        Slug = row.Slug,
        BrandId = row.BrandId,
        BrandName = row.BrandName,
        CategoryId = row.CategoryId,
        RequiresSerial = row.RequiresSerial,
        WarrantyMonths = row.WarrantyMonths,
        BasePrice = row.BasePrice,
        Price = row.Price,
        PromotionId = row.PromotionId,
        AvailableQty = row.AvailableQty,
        ImageUrl = row.ImageUrl,
        CreatedAt = row.CreatedAt
    };

    private static string? BuildSpecFiltersJson(IReadOnlyCollection<ProductSpecFilter> filters)
        => filters.Count == 0
            ? null
            : JsonSerializer.Serialize(
                filters.Select(filter => new { field = filter.Field, value = filter.Value }),
                JsonOptions);
}
