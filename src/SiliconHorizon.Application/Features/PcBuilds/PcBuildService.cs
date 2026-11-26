using System.Text.Json;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Cart;
using SiliconHorizon.Application.Features.PcBuilds.Dto;

namespace SiliconHorizon.Application.Features.PcBuilds;

/// <summary>
/// Hiện thực use case "PC build sẵn" qua stored procedure (Database/10_pc_builds.sql).
/// <para>
/// Giá bộ do proc tính bằng <c>fn_ProductPrice</c> (bảng giá theo nhóm khách + khuyến mãi đang chạy)
/// nên giá hiển thị của bộ LUÔN bằng tổng giá linh kiện khi thêm vào giỏ — không có chênh lệch.
/// </para>
/// </summary>
public sealed class PcBuildService : IPcBuildService
{
    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;

    public PcBuildService(ISqlExecutor sqlExecutor, ICurrentUser currentUser)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<PcBuildListItemDto>> SearchAsync(
        PcBuildSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var rows = await _sqlExecutor.QueryAsync<PcBuildSearchRow>(
            StoredProcedureNames.PcBuildSearch,
            new
            {
                request.Keyword,
                request.Tier,
                request.CustomerGroupId,
                request.MinPrice,
                request.MaxPrice,
                request.InStockOnly,
                request.FeaturedOnly,
                request.SortBy,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            },
            cancellationToken);

        return PagedResult<PcBuildListItemDto>.Create(
            rows.Select(ToListItem).ToList(),
            request.PageNumber,
            request.PageSize,
            rows.Count > 0 ? rows[0].TotalCount : 0);
    }

    public async Task<PcBuildDetailDto> GetByIdAsync(
        long id,
        int? customerGroupId = null,
        CancellationToken cancellationToken = default)
    {
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.PcBuildGetById,
            new { PcBuildId = id, CustomerGroupId = customerGroupId },
            cancellationToken);

        // Thứ tự result set của usp_PcBuild_GetById: (1) header bộ, (2) linh kiện trong bộ.
        var header = await results.ReadFirstOrDefaultAsync<PcBuildDetailRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bộ PC build sẵn Id = {id}.");

        var components = await results.ReadListAsync<PcBuildComponentRow>(cancellationToken);

        return new PcBuildDetailDto
        {
            Id = header.Id,
            Code = header.Code,
            Name = header.Name,
            Slug = header.Slug,
            Tier = header.Tier,
            Summary = header.Summary,
            Description = header.Description,
            ImageUrl = header.ImageUrl,
            IsFeatured = header.IsFeatured,
            ComponentCount = header.ComponentCount,
            PartsPrice = header.PartsPrice,
            AvailableBuilds = header.AvailableBuilds,
            Components = components.Select(ToComponent).ToList()
        };
    }

    public async Task<AddPcBuildToCartResultDto> AddToCartAsync(
        long id,
        AddPcBuildToCartRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default)
    {
        // Số bộ phải > 0: proc cũng kiểm tra (50004) nhưng chặn sớm để không mở giao dịch vô ích.
        if (request.Quantity <= 0)
        {
            throw new BadRequestException("Số bộ cần thêm phải lớn hơn 0.");
        }

        // Chủ giỏ giống mọi thao tác giỏ khác: khách đăng nhập (customerId ÉP từ token) hoặc mã phiên vãng lai.
        var owner = CartOwnerResolver.Resolve(_currentUser, sessionToken);

        // Proc tự kiểm tra: bộ đang bán (50001), bộ có linh kiện, tồn kho từng linh kiện (50003),
        // rồi thêm/cập nhật TẤT CẢ linh kiện trong một giao dịch.
        var rows = await _sqlExecutor.QueryAsync<PcBuildCartItemRow>(
            StoredProcedureNames.PcBuildAddToCart,
            new { owner.CustomerId, owner.SessionToken, PcBuildId = id, request.Quantity },
            cancellationToken);

        if (rows.Count == 0)
        {
            // Lưới an toàn: các trường hợp lỗi đã được proc báo từ trước nên nhánh này gần như không xảy ra.
            throw new ConflictException(
                "Không thêm được bộ PC build sẵn vào giỏ hàng.",
                "PCBUILD-ADD-FAILED");
        }

        return new AddPcBuildToCartResultDto
        {
            PcBuildId = id,
            Quantity = request.Quantity,
            ItemCount = rows.Count,
            Items = rows.Select(row => new AddedPcBuildComponentDto
            {
                ProductId = row.ProductId,
                Name = row.Name,
                AddedQuantity = row.AddedQuantity,
                CartQuantity = row.CartQuantity
            }).ToList()
        };
    }

    public async Task<PcBuilderCatalogDto> GetBuilderCatalogAsync(
        int? customerGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _sqlExecutor.QueryAsync<PcBuilderCatalogRow>(
            StoredProcedureNames.PcBuildBuilderCatalog,
            new { CustomerGroupId = customerGroupId },
            cancellationToken);

        // Proc trả MỘT result set phẳng cho cả 6 nhóm ⇒ nhóm lại theo nhóm linh kiện (giữ thứ tự chọn).
        var categories = rows
            .GroupBy(row => new { row.StepOrder, row.CategoryId, row.CategoryCode, row.CategoryName, row.SpecType })
            .OrderBy(group => group.Key.StepOrder)
            .Select(group => new PcBuilderCategoryDto
            {
                StepOrder = group.Key.StepOrder,
                CategoryId = group.Key.CategoryId,
                CategoryCode = group.Key.CategoryCode,
                CategoryName = group.Key.CategoryName,
                SpecType = group.Key.SpecType,
                Products = group.Select(ToBuilderProduct).ToList()
            })
            .ToList();

        return new PcBuilderCatalogDto { Categories = categories };
    }

    /// <summary>
    /// Chuỗi JSON thông số (khoá khác nhau theo nhóm linh kiện) → <see cref="JsonElement"/> để API trả
    /// nguyên cấu trúc cho giao diện đọc trực tiếp. Chuỗi rỗng ⇒ <c>null</c>.
    /// </summary>
    private static JsonElement? ToSpecsElement(string? specsJson)
    {
        if (string.IsNullOrWhiteSpace(specsJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(specsJson);
        return document.RootElement.Clone();
    }

    private static PcBuilderProductDto ToBuilderProduct(PcBuilderCatalogRow row) => new()
    {
        ProductId = row.ProductId,
        Sku = row.Sku,
        Name = row.Name,
        Slug = row.Slug,
        BrandName = row.BrandName,
        BasePrice = row.BasePrice,
        Price = row.Price,
        AvailableQty = row.AvailableQty,
        ImageUrl = row.ImageUrl,
        WarrantyMonths = row.WarrantyMonths,
        RequiresSerial = row.RequiresSerial,
        Specs = ToSpecsElement(row.SpecsJson)
    };

    private static PcBuildListItemDto ToListItem(PcBuildSearchRow row) => new()
    {
        Id = row.Id,
        Code = row.Code,
        Name = row.Name,
        Slug = row.Slug,
        Tier = row.Tier,
        Summary = row.Summary,
        ImageUrl = row.ImageUrl,
        IsFeatured = row.IsFeatured,
        ComponentCount = row.ComponentCount,
        PartsPrice = row.PartsPrice,
        AvailableBuilds = row.AvailableBuilds,
        ComponentPreview = row.ComponentPreview
    };

    private static PcBuildComponentDto ToComponent(PcBuildComponentRow row) => new()
    {
        ProductId = row.ProductId,
        Sku = row.Sku,
        Name = row.Name,
        Slug = row.Slug,
        CategoryCode = row.CategoryCode,
        CategoryName = row.CategoryName,
        SpecType = row.SpecType,
        BrandName = row.BrandName,
        Quantity = row.Quantity,
        UnitPrice = row.UnitPrice,
        LineTotal = row.LineTotal,
        AvailableQty = row.AvailableQty,
        ImageUrl = row.ImageUrl,
        WarrantyMonths = row.WarrantyMonths,
        RequiresSerial = row.RequiresSerial,
        Status = row.Status,
        Note = row.Note,
        SortOrder = row.SortOrder
    };
}
