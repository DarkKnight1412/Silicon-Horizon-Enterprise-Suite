using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Pricing.Dto;
using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Entities.Pricing;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Hiện thực use case giá bậc thang (PRC-02) — CRUD bằng EF Core trên bảng cấu hình <c>dbo.PriceTiers</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Sản phẩm là bắt buộc</b>: bậc giá luôn thuộc một sản phẩm; endpoint lồng dưới
/// <c>/admin/products/{id}/price-tiers</c> nên mọi thao tác đều kiểm tra sản phẩm tồn tại (404 nếu không).</item>
/// <item><b>Ràng buộc duy nhất</b> <c>UQ_PriceTiers (ProductId, CustomerGroupId, MinQuantity)</c> —
/// kiểm tra trước khi ghi để trả 409.</item>
/// <item><b>Đổi giá ghi nhật ký</b>: mọi lần tạo/sửa bậc giá đều gọi <c>usp_Audit_Write</c>
/// (AUD-01..04, yêu cầu ở cuối mục 10 của Database/README.md).</item>
/// <item>Không xóa cứng: bậc giá đã dùng để bán thì chỉ sửa lại giá trị (không có endpoint DELETE).</item>
/// </list>
/// </remarks>
public sealed class PriceTierService : IPriceTierService
{
    /// <summary>Ràng buộc <c>CK_PriceTiers_MinQty</c>: MinQuantity &gt; 1.</summary>
    private const int MinimumQuantityExclusive = 1;

    private readonly IReadRepository<PriceTier> _readRepository;
    private readonly IWriteRepository<PriceTier> _writeRepository;
    private readonly IReadRepository<Product> _products;
    private readonly IReadRepository<CustomerGroup> _customerGroups;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogWriter _auditLogWriter;

    public PriceTierService(
        IReadRepository<PriceTier> readRepository,
        IWriteRepository<PriceTier> writeRepository,
        IReadRepository<Product> products,
        IReadRepository<CustomerGroup> customerGroups,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _products = products;
        _customerGroups = customerGroups;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public async Task<PagedResult<PriceTierDto>> GetPagedAsync(
        long productId,
        PriceTierListQuery query,
        CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);

        Func<PriceTier, bool>? predicate = query.CustomerGroupId is null
            ? tier => tier.ProductId == productId
            : tier => tier.ProductId == productId && tier.CustomerGroupId == query.CustomerGroupId;

        return await PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<PriceTier> tiers) => tiers
                .OrderBy(tier => tier.CustomerGroupId)
                .ThenBy(tier => tier.MinQuantity),
            query.PageNumber,
            query.PageSize,
            predicate,
            PriceTierDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PriceTierDto> GetByIdAsync(
        long productId,
        long tierId,
        CancellationToken cancellationToken = default)
        => PriceTierDto.FromEntity(await GetOrThrowAsync(productId, tierId, cancellationToken));

    /// <inheritdoc />
    public async Task<PriceTierDto> CreateAsync(
        long productId,
        CreatePriceTierRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);
        await ValidateAsync(productId, request.CustomerGroupId, request.MinQuantity, request.Price, null, cancellationToken);

        var tier = new PriceTier
        {
            ProductId = productId,
            CustomerGroupId = request.CustomerGroupId,
            MinQuantity = request.MinQuantity,
            Price = request.Price,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(tier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "PriceTierCreated",
            "PriceTier",
            tier.Id,
            newValues: new { tier.ProductId, tier.CustomerGroupId, tier.MinQuantity, tier.Price },
            cancellationToken: cancellationToken);

        return PriceTierDto.FromEntity(tier);
    }

    /// <inheritdoc />
    public async Task<PriceTierDto> UpdateAsync(
        long productId,
        long tierId,
        UpdatePriceTierRequest request,
        CancellationToken cancellationToken = default)
    {
        var tier = await GetOrThrowAsync(productId, tierId, cancellationToken);

        await ValidateAsync(productId, request.CustomerGroupId, request.MinQuantity, request.Price, tierId, cancellationToken);

        var oldValues = new { tier.CustomerGroupId, tier.MinQuantity, tier.Price };

        tier.CustomerGroupId = request.CustomerGroupId;
        tier.MinQuantity = request.MinQuantity;
        tier.Price = request.Price;
        tier.UpdatedAt = _clock.UtcNow;
        tier.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(tier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Đổi giá là thao tác nhạy cảm → bắt buộc có vết trong AuditLogs (AUD-02).
        await _auditLogWriter.WriteAsync(
            "PriceTierChanged",
            "PriceTier",
            tier.Id,
            oldValues,
            new { tier.CustomerGroupId, tier.MinQuantity, tier.Price },
            cancellationToken: cancellationToken);

        return PriceTierDto.FromEntity(tier);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Bậc giá phải thuộc đúng sản phẩm trên route, nếu không coi như không tồn tại (404).</summary>
    private async Task<PriceTier> GetOrThrowAsync(long productId, long tierId, CancellationToken cancellationToken)
    {
        var tier = await _readRepository.GetByIdAsync(tierId, cancellationToken);

        if (tier is null || tier.ProductId != productId)
        {
            throw new NotFoundException($"Không tìm thấy bậc giá Id = {tierId} của sản phẩm Id = {productId}.");
        }

        return tier;
    }

    private async Task EnsureProductExistsAsync(long productId, CancellationToken cancellationToken)
    {
        InputGuard.EnsurePositive(productId, "productId");

        var products = await _products.ListAsync(cancellationToken);

        if (products.All(product => product.Id != productId))
        {
            throw new NotFoundException($"Không tìm thấy sản phẩm Id = {productId}.");
        }
    }

    /// <summary>
    /// Kiểm tra nhóm khách tồn tại, <c>minQuantity &gt; 1</c>, <c>price &gt;= 0</c> và không trùng bộ ba
    /// (productId, customerGroupId, minQuantity).
    /// </summary>
    private async Task ValidateAsync(
        long productId,
        int customerGroupId,
        int minQuantity,
        decimal price,
        long? currentTierId,
        CancellationToken cancellationToken)
    {
        InputGuard.EnsurePositive(customerGroupId, "customerGroupId");

        if (minQuantity <= MinimumQuantityExclusive)
        {
            throw new BadRequestException(
                $"Số lượng tối thiểu của bậc giá phải lớn hơn {MinimumQuantityExclusive}.");
        }

        InputGuard.EnsureNotNegative(price, "Giá bán");

        var groups = await _customerGroups.ListAsync(cancellationToken);

        if (groups.All(group => group.Id != customerGroupId))
        {
            throw new BadRequestException($"Nhóm khách Id = {customerGroupId} không tồn tại.");
        }

        var tiers = await _readRepository.ListAsync(cancellationToken);

        var duplicated = tiers.Any(tier =>
            tier.Id != currentTierId
            && tier.ProductId == productId
            && tier.CustomerGroupId == customerGroupId
            && tier.MinQuantity == minQuantity);

        if (duplicated)
        {
            throw new ConflictException(
                $"Bậc giá cho sản phẩm {productId}, nhóm khách {customerGroupId}, số lượng từ {minQuantity} đã tồn tại.",
                "SQL-50005");
        }
    }
}
