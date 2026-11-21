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
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Hiện thực use case khuyến mãi (PRC-03..06) — CRUD bằng EF Core trên <c>dbo.Promotions</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Ràng buộc của bảng</b> <c>CK_Promotions_Scope/Type/Value/Dates</c> được kiểm tra trước khi ghi:
/// <c>scopeType</c> thuộc 0..2, <c>discountType</c> thuộc 0..1, <c>discountValue &gt; 0</c>, <c>endAt &gt; startAt</c>.</item>
/// <item><b>Phạm vi phải tồn tại</b>: <c>scopeId</c> được đối chiếu đúng bảng theo <c>scopeType</c>
/// (Product → Products, Brand → Brands, Category → Categories) vì bảng gốc không có khóa ngoại cho cột này.</item>
/// <item><b>Tắt thay vì xóa</b>: khuyến mãi đã chạy chỉ nên đặt <c>isActive = false</c> để giữ lịch sử tính giá.</item>
/// <item><b>Nhật ký</b>: mọi thay đổi gọi <c>usp_Audit_Write</c> (mục 10 của Database/README.md).</item>
/// </list>
/// </remarks>
public sealed class PromotionService : IPromotionService
{
    /// <summary>Promotions.Name nvarchar(200).</summary>
    private const int NameMaxLength = 200;

    private readonly IReadRepository<Promotion> _readRepository;
    private readonly IWriteRepository<Promotion> _writeRepository;
    private readonly IReadRepository<Product> _products;
    private readonly IReadRepository<Brand> _brands;
    private readonly IReadRepository<Category> _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogWriter _auditLogWriter;

    public PromotionService(
        IReadRepository<Promotion> readRepository,
        IWriteRepository<Promotion> writeRepository,
        IReadRepository<Product> products,
        IReadRepository<Brand> brands,
        IReadRepository<Category> categories,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _products = products;
        _brands = brands;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public Task<PagedResult<PromotionDto>> GetPagedAsync(
        PromotionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        var hasFilter = !string.IsNullOrEmpty(keyword)
                        || query.ScopeType.HasValue
                        || query.ScopeId.HasValue
                        || query.IsActive.HasValue;

        Func<Promotion, bool>? predicate = hasFilter
            ? promotion => (string.IsNullOrEmpty(keyword)
                            || promotion.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                           && (query.ScopeType is null || promotion.ScopeType == query.ScopeType)
                           && (query.ScopeId is null || promotion.ScopeId == query.ScopeId)
                           && (query.IsActive is null || promotion.IsActive == query.IsActive)
            : null;

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<Promotion> promotions) => promotions.OrderByDescending(promotion => promotion.StartAt),
            query.PageNumber,
            query.PageSize,
            predicate,
            PromotionDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PromotionDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => PromotionDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<PromotionDto> CreateAsync(
        CreatePromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên khuyến mãi");

        ValidateBusinessRules(
            request.ScopeType,
            request.DiscountType,
            request.DiscountValue,
            request.StartAt,
            request.EndAt,
            request.MaxQuantity,
            request.MaxPerCustomer);

        await EnsureScopeExistsAsync(request.ScopeType, request.ScopeId, cancellationToken);

        var promotion = new Promotion
        {
            Name = name,
            ScopeType = request.ScopeType,
            ScopeId = request.ScopeId,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            MaxQuantity = request.MaxQuantity,
            MaxPerCustomer = request.MaxPerCustomer,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(promotion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "PromotionCreated",
            "Promotion",
            promotion.Id,
            newValues: new
            {
                promotion.Name,
                promotion.ScopeType,
                promotion.ScopeId,
                promotion.DiscountType,
                promotion.DiscountValue,
                promotion.StartAt,
                promotion.EndAt
            },
            cancellationToken: cancellationToken);

        return PromotionDto.FromEntity(promotion);
    }

    /// <inheritdoc />
    public async Task<PromotionDto> UpdateAsync(
        long id,
        UpdatePromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        var promotion = await GetOrThrowAsync(id, cancellationToken);
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên khuyến mãi");

        ValidateBusinessRules(
            request.ScopeType,
            request.DiscountType,
            request.DiscountValue,
            request.StartAt,
            request.EndAt,
            request.MaxQuantity,
            request.MaxPerCustomer);

        await EnsureScopeExistsAsync(request.ScopeType, request.ScopeId, cancellationToken);

        var oldValues = new
        {
            promotion.Name,
            promotion.ScopeType,
            promotion.ScopeId,
            promotion.DiscountType,
            promotion.DiscountValue,
            promotion.StartAt,
            promotion.EndAt,
            promotion.MaxQuantity,
            promotion.MaxPerCustomer
        };

        promotion.Name = name;
        promotion.ScopeType = request.ScopeType;
        promotion.ScopeId = request.ScopeId;
        promotion.DiscountType = request.DiscountType;
        promotion.DiscountValue = request.DiscountValue;
        promotion.StartAt = request.StartAt;
        promotion.EndAt = request.EndAt;
        promotion.MaxQuantity = request.MaxQuantity;
        promotion.MaxPerCustomer = request.MaxPerCustomer;
        promotion.UpdatedAt = _clock.UtcNow;
        promotion.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(promotion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "PromotionUpdated",
            "Promotion",
            promotion.Id,
            oldValues,
            new
            {
                promotion.Name,
                promotion.ScopeType,
                promotion.ScopeId,
                promotion.DiscountType,
                promotion.DiscountValue,
                promotion.StartAt,
                promotion.EndAt,
                promotion.MaxQuantity,
                promotion.MaxPerCustomer
            },
            cancellationToken: cancellationToken);

        return PromotionDto.FromEntity(promotion);
    }

    /// <inheritdoc />
    public async Task SetStatusAsync(
        long id,
        SetPromotionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var promotion = await GetOrThrowAsync(id, cancellationToken);

        if (promotion.IsActive == request.IsActive)
        {
            return;
        }

        promotion.IsActive = request.IsActive;
        promotion.UpdatedAt = _clock.UtcNow;
        promotion.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(promotion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            request.IsActive ? "PromotionActivated" : "PromotionDeactivated",
            "Promotion",
            promotion.Id,
            oldValues: new { IsActive = !request.IsActive },
            newValues: new { promotion.IsActive },
            cancellationToken: cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Promotion> GetOrThrowAsync(long id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy khuyến mãi Id = {id}.");

    /// <summary>Kiểm tra ràng buộc <c>CK_Promotions_Scope/Type/Value/Dates</c> trước khi ghi.</summary>
    private static void ValidateBusinessRules(
        PromotionScopeType scopeType,
        DiscountType discountType,
        decimal discountValue,
        DateTime startAt,
        DateTime endAt,
        int? maxQuantity,
        int? maxPerCustomer)
    {
        InputGuard.EnsureDefined(scopeType, "Phạm vi khuyến mãi (0 Product, 1 Brand, 2 Category)");
        InputGuard.EnsureDefined(discountType, "Loại giảm giá (0 Phần trăm, 1 Số tiền)");
        InputGuard.EnsurePositiveAmount(discountValue, "Giá trị giảm");
        InputGuard.EnsureDateRange(startAt, endAt, "startAt", "endAt");

        if (maxQuantity.HasValue)
        {
            InputGuard.EnsurePositive(maxQuantity.Value, "Số lượng áp dụng tối đa");
        }

        if (maxPerCustomer.HasValue)
        {
            InputGuard.EnsurePositive(maxPerCustomer.Value, "Số lượng tối đa mỗi khách");
        }
    }

    /// <summary>
    /// <c>Promotions.ScopeId</c> không có khóa ngoại (đa hình theo <c>ScopeType</c>) nên phải kiểm tra
    /// thủ công để tránh khuyến mãi trỏ vào sản phẩm/thương hiệu/danh mục không tồn tại.
    /// </summary>
    private async Task EnsureScopeExistsAsync(
        PromotionScopeType scopeType,
        long scopeId,
        CancellationToken cancellationToken)
    {
        InputGuard.EnsurePositive(scopeId, "scopeId");

        var exists = scopeType switch
        {
            PromotionScopeType.Product => (await _products.ListAsync(cancellationToken))
                .Any(product => product.Id == scopeId),
            PromotionScopeType.Brand => (await _brands.ListAsync(cancellationToken))
                .Any(brand => brand.Id == scopeId),
            PromotionScopeType.Category => (await _categories.ListAsync(cancellationToken))
                .Any(category => category.Id == scopeId),
            _ => throw new BadRequestException("Phạm vi khuyến mãi không hợp lệ.")
        };

        if (!exists)
        {
            throw new BadRequestException($"Không tìm thấy {ScopeTypeName(scopeType)} Id = {scopeId} cho phạm vi khuyến mãi.");
        }
    }

    private static string ScopeTypeName(PromotionScopeType scopeType) => scopeType switch
    {
        PromotionScopeType.Product => "sản phẩm",
        PromotionScopeType.Brand => "thương hiệu",
        PromotionScopeType.Category => "danh mục",
        _ => "đối tượng"
    };
}
