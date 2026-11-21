using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Entities.Commission;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case quy tắc hoa hồng (CRUD bằng EF Core).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Ràng buộc phần trăm</b>: <c>ratePercent</c> và <c>bonusRatePercent</c> nằm trong 0..100
/// (khớp <c>CK_CommissionRules_Rate</c> và <c>DECIMAL(5,2)</c>).</item>
/// <item><b>Phạm vi</b>: <c>scopeId</c> bắt buộc khi phạm vi là Category/Product và được đối chiếu tồn tại
/// (bảng gốc không có khóa ngoại cho cột này); phạm vi <c>All</c> thì <c>scopeId</c> để <c>null</c>.</item>
/// <item><b>Thời hạn</b>: <c>effectiveTo</c> phải sau <c>effectiveFrom</c>.</item>
/// <item><b>Không có cột trạng thái</b>: quy tắc "hết hiệu lực" bằng cách đặt <c>effectiveTo</c> trong quá khứ,
/// nên API không cung cấp endpoint DELETE (bút toán <c>CommissionEntries</c> đang trỏ tới <c>RuleId</c>).</item>
/// </list>
/// </remarks>
public sealed class CommissionRuleService : ICommissionRuleService
{
    /// <summary>CommissionRules.Name nvarchar(150).</summary>
    private const int NameMaxLength = 150;

    private readonly IReadRepository<CommissionRule> _readRepository;
    private readonly IWriteRepository<CommissionRule> _writeRepository;
    private readonly IReadRepository<Category> _categories;
    private readonly IReadRepository<Product> _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogWriter _auditLogWriter;

    public CommissionRuleService(
        IReadRepository<CommissionRule> readRepository,
        IWriteRepository<CommissionRule> writeRepository,
        IReadRepository<Category> categories,
        IReadRepository<Product> products,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _categories = categories;
        _products = products;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public Task<PagedResult<CommissionRuleDto>> GetPagedAsync(
        CommissionRuleListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        var hasFilter = !string.IsNullOrEmpty(keyword)
                        || query.ScopeType.HasValue
                        || query.BasisType.HasValue
                        || query.EffectiveOn.HasValue;

        Func<CommissionRule, bool>? predicate = hasFilter
            ? rule => (string.IsNullOrEmpty(keyword)
                       || rule.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                      && (query.ScopeType is null || rule.ScopeType == query.ScopeType)
                      && (query.BasisType is null || rule.BasisType == query.BasisType)
                      && (query.EffectiveOn is null
                          || (rule.EffectiveFrom <= query.EffectiveOn
                              && (rule.EffectiveTo == null || rule.EffectiveTo >= query.EffectiveOn)))
            : null;

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<CommissionRule> rules) => rules
                .OrderByDescending(rule => rule.EffectiveFrom)
                .ThenBy(rule => rule.Name),
            query.PageNumber,
            query.PageSize,
            predicate,
            CommissionRuleDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CommissionRuleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => CommissionRuleDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<CommissionRuleDto> CreateAsync(
        CreateCommissionRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = await ValidateAsync(
            request.Name,
            request.ScopeType,
            request.ScopeId,
            request.BasisType,
            request.RatePercent,
            request.TargetAmount,
            request.BonusRatePercent,
            request.EffectiveFrom,
            request.EffectiveTo,
            cancellationToken);

        var rule = new CommissionRule
        {
            Name = validated.Name,
            ScopeType = request.ScopeType,
            ScopeId = validated.ScopeId,
            BasisType = request.BasisType,
            RatePercent = request.RatePercent,
            TargetAmount = request.TargetAmount,
            BonusRatePercent = request.BonusRatePercent,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "CommissionRuleCreated",
            "CommissionRule",
            rule.Id,
            newValues: new
            {
                rule.Name,
                rule.ScopeType,
                rule.ScopeId,
                rule.BasisType,
                rule.RatePercent,
                rule.TargetAmount,
                rule.BonusRatePercent,
                rule.EffectiveFrom,
                rule.EffectiveTo
            },
            cancellationToken: cancellationToken);

        return CommissionRuleDto.FromEntity(rule);
    }

    /// <inheritdoc />
    public async Task<CommissionRuleDto> UpdateAsync(
        int id,
        UpdateCommissionRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = await GetOrThrowAsync(id, cancellationToken);

        var validated = await ValidateAsync(
            request.Name,
            request.ScopeType,
            request.ScopeId,
            request.BasisType,
            request.RatePercent,
            request.TargetAmount,
            request.BonusRatePercent,
            request.EffectiveFrom,
            request.EffectiveTo,
            cancellationToken);

        var oldValues = new
        {
            rule.Name,
            rule.ScopeType,
            rule.ScopeId,
            rule.BasisType,
            rule.RatePercent,
            rule.TargetAmount,
            rule.BonusRatePercent,
            rule.EffectiveFrom,
            rule.EffectiveTo
        };

        rule.Name = validated.Name;
        rule.ScopeType = request.ScopeType;
        rule.ScopeId = validated.ScopeId;
        rule.BasisType = request.BasisType;
        rule.RatePercent = request.RatePercent;
        rule.TargetAmount = request.TargetAmount;
        rule.BonusRatePercent = request.BonusRatePercent;
        rule.EffectiveFrom = request.EffectiveFrom;
        rule.EffectiveTo = request.EffectiveTo;
        rule.UpdatedAt = _clock.UtcNow;
        rule.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "CommissionRuleUpdated",
            "CommissionRule",
            rule.Id,
            oldValues,
            new
            {
                rule.Name,
                rule.ScopeType,
                rule.ScopeId,
                rule.BasisType,
                rule.RatePercent,
                rule.TargetAmount,
                rule.BonusRatePercent,
                rule.EffectiveFrom,
                rule.EffectiveTo
            },
            cancellationToken: cancellationToken);

        return CommissionRuleDto.FromEntity(rule);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<CommissionRule> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy quy tắc hoa hồng Id = {id}.");

    /// <summary>
    /// Kiểm tra ràng buộc <c>CK_CommissionRules_Scope/Basis/Rate</c>, thời hạn hiệu lực và tính tồn tại
    /// của phạm vi khi quy tắc áp dụng theo danh mục/sản phẩm.
    /// </summary>
    private async Task<ValidatedRule> ValidateAsync(
        string? name,
        CommissionScopeType scopeType,
        long? scopeId,
        CommissionBasisType basisType,
        decimal ratePercent,
        decimal? targetAmount,
        decimal? bonusRatePercent,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        CancellationToken cancellationToken)
    {
        var normalizedName = InputGuard.RequireText(name, NameMaxLength, "Tên quy tắc hoa hồng");

        InputGuard.EnsureDefined(scopeType, "Phạm vi hoa hồng (0 All, 1 Category, 2 Product)");
        InputGuard.EnsureDefined(basisType, "Cơ sở tính hoa hồng (0 Revenue, 1 GrossProfit)");
        InputGuard.EnsureWithin(ratePercent, 0m, 100m, "Tỷ lệ hoa hồng (%)");

        if (targetAmount.HasValue)
        {
            InputGuard.EnsureNotNegative(targetAmount.Value, "Chỉ tiêu thưởng");
        }

        if (bonusRatePercent.HasValue)
        {
            InputGuard.EnsureWithin(bonusRatePercent.Value, 0m, 100m, "Tỷ lệ thưởng vượt chỉ tiêu (%)");
        }

        if (effectiveFrom == default)
        {
            throw new BadRequestException("Ngày bắt đầu hiệu lực (effectiveFrom) là bắt buộc.");
        }

        InputGuard.EnsureOptionalDateRange(effectiveFrom, effectiveTo, "effectiveFrom", "effectiveTo");

        var resolvedScopeId = await ResolveScopeIdAsync(scopeType, scopeId, cancellationToken);

        return new ValidatedRule(normalizedName, resolvedScopeId);
    }

    /// <summary>
    /// Phạm vi toàn bộ (<c>All</c>) không dùng <c>scopeId</c>; các phạm vi khác phải trỏ tới
    /// danh mục/sản phẩm tồn tại.
    /// </summary>
    private async Task<long?> ResolveScopeIdAsync(
        CommissionScopeType scopeType,
        long? scopeId,
        CancellationToken cancellationToken)
    {
        if (scopeType == CommissionScopeType.All)
        {
            return null;
        }

        if (scopeId is null or <= 0)
        {
            throw new BadRequestException(
                "Phạm vi theo danh mục/sản phẩm bắt buộc phải có scopeId là số dương.");
        }

        var exists = scopeType switch
        {
            CommissionScopeType.Category => (await _categories.ListAsync(cancellationToken))
                .Any(category => category.Id == scopeId.Value),
            CommissionScopeType.Product => (await _products.ListAsync(cancellationToken))
                .Any(product => product.Id == scopeId.Value),
            _ => false
        };

        if (!exists)
        {
            throw new BadRequestException($"Không tìm thấy phạm vi (scopeId = {scopeId}) cho quy tắc hoa hồng.");
        }

        return scopeId;
    }

    /// <summary>Giá trị quy tắc đã kiểm tra/chuẩn hóa, sẵn sàng gán vào entity.</summary>
    private sealed record ValidatedRule(string Name, long? ScopeId);
}
