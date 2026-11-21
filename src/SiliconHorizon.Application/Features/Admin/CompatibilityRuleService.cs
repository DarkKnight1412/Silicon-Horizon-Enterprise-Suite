using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case quy tắc tương thích (CRUD bằng EF Core).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Operator giới hạn EQ/LTE/GTE</b> theo <c>CK_CompatibilityRules_Operator</c> — giá trị khác trả 400
/// (chuẩn hóa về chữ HOA trước khi lưu).</item>
/// <item><b>Severity 0/1</b>: 0 Info (chỉ thông tin), 1 Warning (cảnh báo) — khớp <c>CompatibilitySeverity</c>.</item>
/// <item><b>Trường thông số</b>: <c>leftField</c>/<c>rightField</c> là tên cột trong các bảng <c>Specs_*</c>
/// (ví dụ <c>socket</c>, <c>memoryType</c>, <c>tdpWatt</c>), tối đa 50 ký tự (varchar(50)).</item>
/// <item><b>Nhật ký</b>: mọi thay đổi gọi <c>usp_Audit_Write</c>.</item>
/// </list>
/// </remarks>
public sealed class CompatibilityRuleService : ICompatibilityRuleService
{
    /// <summary>CompatibilityRules.Name nvarchar(150).</summary>
    private const int NameMaxLength = 150;

    /// <summary>CompatibilityRules.LeftField/RightField varchar(50) — tên cột trong Specs_*.</summary>
    private const int FieldMaxLength = 50;

    /// <summary>CompatibilityRules.Operator varchar(10).</summary>
    private const int OperatorMaxLength = 10;

    /// <summary>Tập toán tử hợp lệ theo <c>CK_CompatibilityRules_Operator</c>.</summary>
    private static readonly string[] AllowedOperators = ["EQ", "LTE", "GTE"];

    private readonly IReadRepository<CompatibilityRule> _readRepository;
    private readonly IWriteRepository<CompatibilityRule> _writeRepository;
    private readonly IReadRepository<Category> _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogWriter _auditLogWriter;

    public CompatibilityRuleService(
        IReadRepository<CompatibilityRule> readRepository,
        IWriteRepository<CompatibilityRule> writeRepository,
        IReadRepository<Category> categories,
        IUnitOfWork unitOfWork,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public Task<PagedResult<CompatibilityRuleDto>> GetPagedAsync(
        CompatibilityRuleListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        var hasFilter = !string.IsNullOrEmpty(keyword)
                        || query.CategoryId.HasValue
                        || query.IsActive.HasValue;

        Func<CompatibilityRule, bool>? predicate = hasFilter
            ? rule => (string.IsNullOrEmpty(keyword)
                       || rule.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                      && (query.CategoryId is null
                          || rule.LeftCategoryId == query.CategoryId
                          || rule.RightCategoryId == query.CategoryId)
                      && (query.IsActive is null || rule.IsActive == query.IsActive)
            : null;

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<CompatibilityRule> rules) => rules.OrderBy(rule => rule.Name),
            query.PageNumber,
            query.PageSize,
            predicate,
            CompatibilityRuleDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CompatibilityRuleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => CompatibilityRuleDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<CompatibilityRuleDto> CreateAsync(
        CreateCompatibilityRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = await ValidateAsync(
            request.Name,
            request.LeftCategoryId,
            request.RightCategoryId,
            request.LeftField,
            request.RightField,
            request.Operator,
            request.Severity,
            cancellationToken);

        var rule = new CompatibilityRule
        {
            Name = validated.Name,
            LeftCategoryId = request.LeftCategoryId,
            RightCategoryId = request.RightCategoryId,
            LeftField = validated.LeftField,
            RightField = validated.RightField,
            Operator = validated.Operator,
            Severity = validated.Severity,
            IsActive = true
        };

        await _writeRepository.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "CompatibilityRuleCreated",
            "CompatibilityRule",
            rule.Id,
            newValues: new
            {
                rule.Name,
                rule.LeftCategoryId,
                rule.RightCategoryId,
                rule.LeftField,
                rule.RightField,
                rule.Operator,
                rule.Severity
            },
            cancellationToken: cancellationToken);

        return CompatibilityRuleDto.FromEntity(rule);
    }

    /// <inheritdoc />
    public async Task<CompatibilityRuleDto> UpdateAsync(
        int id,
        UpdateCompatibilityRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = await GetOrThrowAsync(id, cancellationToken);

        var validated = await ValidateAsync(
            request.Name,
            request.LeftCategoryId,
            request.RightCategoryId,
            request.LeftField,
            request.RightField,
            request.Operator,
            request.Severity,
            cancellationToken);

        var oldValues = new
        {
            rule.Name,
            rule.LeftCategoryId,
            rule.RightCategoryId,
            rule.LeftField,
            rule.RightField,
            rule.Operator,
            rule.Severity
        };

        rule.Name = validated.Name;
        rule.LeftCategoryId = request.LeftCategoryId;
        rule.RightCategoryId = request.RightCategoryId;
        rule.LeftField = validated.LeftField;
        rule.RightField = validated.RightField;
        rule.Operator = validated.Operator;
        rule.Severity = validated.Severity;

        _writeRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "CompatibilityRuleUpdated",
            "CompatibilityRule",
            rule.Id,
            oldValues,
            new
            {
                rule.Name,
                rule.LeftCategoryId,
                rule.RightCategoryId,
                rule.LeftField,
                rule.RightField,
                rule.Operator,
                rule.Severity
            },
            cancellationToken: cancellationToken);

        return CompatibilityRuleDto.FromEntity(rule);
    }

    /// <inheritdoc />
    public async Task SetStatusAsync(
        int id,
        SetCompatibilityRuleStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = await GetOrThrowAsync(id, cancellationToken);

        if (rule.IsActive == request.IsActive)
        {
            return;
        }

        rule.IsActive = request.IsActive;

        _writeRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            request.IsActive ? "CompatibilityRuleActivated" : "CompatibilityRuleDeactivated",
            "CompatibilityRule",
            rule.Id,
            oldValues: new { IsActive = !request.IsActive },
            newValues: new { rule.IsActive },
            cancellationToken: cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<CompatibilityRule> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy quy tắc tương thích Id = {id}.");

    /// <summary>
    /// Kiểm tra và chuẩn hóa dữ liệu quy tắc: toán tử thuộc EQ/LTE/GTE, mức cảnh báo 0/1,
    /// hai danh mục tồn tại (bảng gốc có khóa ngoại nên phải chặn sớm để trả 400 thay vì lỗi SQL thô).
    /// </summary>
    private async Task<ValidatedRule> ValidateAsync(
        string? name,
        int leftCategoryId,
        int rightCategoryId,
        string? leftField,
        string? rightField,
        string? operatorValue,
        byte severity,
        CancellationToken cancellationToken)
    {
        var normalizedName = InputGuard.RequireText(name, NameMaxLength, "Tên quy tắc");
        var normalizedLeftField = InputGuard.RequireText(leftField, FieldMaxLength, "Trường thông số bên trái");
        var normalizedRightField = InputGuard.RequireText(rightField, FieldMaxLength, "Trường thông số bên phải");
        var normalizedOperator = InputGuard
            .RequireText(operatorValue, OperatorMaxLength, "Toán tử so sánh")
            .ToUpperInvariant();

        if (!AllowedOperators.Contains(normalizedOperator))
        {
            throw new BadRequestException(
                $"Toán tử so sánh chỉ nhận {string.Join("/", AllowedOperators)}.");
        }

        if (!Enum.IsDefined((CompatibilitySeverity)severity))
        {
            throw new BadRequestException("Mức cảnh báo (severity) chỉ nhận 0 (Info) hoặc 1 (Warning).");
        }

        await EnsureCategoryExistsAsync(leftCategoryId, "trái", cancellationToken);
        await EnsureCategoryExistsAsync(rightCategoryId, "phải", cancellationToken);

        return new ValidatedRule(
            normalizedName,
            normalizedLeftField,
            normalizedRightField,
            normalizedOperator,
            (CompatibilitySeverity)severity);
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, string side, CancellationToken cancellationToken)
    {
        InputGuard.EnsurePositive(categoryId, $"Danh mục bên {side}");

        var categories = await _categories.ListAsync(cancellationToken);

        if (categories.All(category => category.Id != categoryId))
        {
            throw new BadRequestException($"Danh mục bên {side} (Id = {categoryId}) không tồn tại.");
        }
    }

    /// <summary>Giá trị quy tắc đã kiểm tra và chuẩn hóa, sẵn sàng gán vào entity.</summary>
    private sealed record ValidatedRule(
        string Name,
        string LeftField,
        string RightField,
        string Operator,
        CompatibilitySeverity Severity);
}
