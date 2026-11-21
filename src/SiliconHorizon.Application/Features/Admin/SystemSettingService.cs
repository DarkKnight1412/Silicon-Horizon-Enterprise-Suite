using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case cấu hình hệ thống (CRUD bằng EF Core trên bảng có khóa chính kiểu chuỗi).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Khóa là chính</b>: <c>SystemSettings.[Key]</c> là khóa chính nên <c>PUT /{key}</c> là upsert —
/// khóa mới sẽ được tạo, khóa cũ chỉ đổi <c>Value</c> (và <c>Description</c> nếu client truyền).</item>
/// <item><b>Nhật ký</b>: bảng <c>AuditLogs.EntityId</c> là <c>bigint</c> nên không chứa được khóa dạng chuỗi;
/// dòng nhật ký vì thế truyền <c>@EntityId = 0</c> và đặt khóa cấu hình vào
/// <c>NewValues</c>/<c>Reason</c> để vẫn tra cứu được (xem giải thích trong <c>usp_Audit_Write</c>).</item>
/// <item><b>Không xóa</b>: không có endpoint DELETE — xóa khóa cấu hình sẽ làm proc đọc cấu hình thiếu giá trị.</item>
/// </list>
/// </remarks>
public sealed class SystemSettingService : ISystemSettingService
{
    /// <summary>SystemSettings.[Key] varchar(100).</summary>
    private const int KeyMaxLength = 100;

    /// <summary>SystemSettings.[Value] nvarchar(500).</summary>
    private const int ValueMaxLength = 500;

    /// <summary>SystemSettings.Description nvarchar(300).</summary>
    private const int DescriptionMaxLength = 300;

    /// <summary><c>@EntityId</c> của <c>usp_Audit_Write</c> là bigint → dùng 0 cho khóa chuỗi.</summary>
    private const long NonNumericEntityId = 0;

    private readonly IReadRepository<SystemSetting> _readRepository;
    private readonly IWriteRepository<SystemSetting> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogWriter _auditLogWriter;

    public SystemSettingService(
        IReadRepository<SystemSetting> readRepository,
        IWriteRepository<SystemSetting> writeRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public Task<PagedResult<SystemSettingDto>> GetPagedAsync(
        SystemSettingListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        Func<SystemSetting, bool>? predicate = string.IsNullOrEmpty(keyword)
            ? null
            : setting => setting.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                         || (setting.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<SystemSetting> settings) => settings.OrderBy(setting => setting.Key),
            query.PageNumber,
            query.PageSize,
            predicate,
            SystemSettingDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SystemSettingDto> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => SystemSettingDto.FromEntity(await GetOrThrowAsync(key, cancellationToken));

    /// <inheritdoc />
    public async Task<SystemSettingDto> UpsertAsync(
        string key,
        UpsertSystemSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var value = InputGuard.RequireText(request.Value, ValueMaxLength, "Giá trị cấu hình");
        var description = InputGuard.OptionalText(request.Description, DescriptionMaxLength, "Mô tả cấu hình");

        var existing = await _readRepository.GetByIdAsync(normalizedKey, cancellationToken);

        var isNew = existing is null;
        var setting = existing ?? new SystemSetting { Key = normalizedKey, Value = value, Description = description };

        if (existing is null)
        {
            await _writeRepository.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = value;

            // Chỉ cập nhật mô tả khi client thực sự truyền để tránh xóa mất mô tả sẵn có.
            if (description is not null)
            {
                setting.Description = description;
            }

            _writeRepository.Update(setting);
        }

        setting.UpdatedAt = _clock.UtcNow;
        setting.UpdatedBy = _currentUser.UserId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Đổi cấu hình là thao tác nhạy cảm (AUD-02). EntityId bigint không chứa được khóa chuỗi
        // nên khóa được ghi vào NewValues/Reason để vẫn tra cứu được trong /api/v1/audit-logs.
        await _auditLogWriter.WriteAsync(
            isNew ? "SettingCreated" : "SettingChanged",
            "SystemSetting",
            NonNumericEntityId,
            oldValues: isNew ? null : new { Key = normalizedKey },
            newValues: new { Key = normalizedKey, setting.Value, setting.Description },
            reason: $"Cấu hình '{normalizedKey}' được {(isNew ? "tạo" : "cập nhật")} qua /api/v1/admin/settings.",
            cancellationToken: cancellationToken);

        return SystemSettingDto.FromEntity(setting);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<SystemSetting> GetOrThrowAsync(string key, CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(key);

        return await _readRepository.GetByIdAsync(normalizedKey, cancellationToken)
               ?? throw new NotFoundException($"Không tìm thấy cấu hình có khóa '{normalizedKey}'.");
    }

    /// <summary>
    /// Khóa cấu hình: bắt buộc, tối đa 100 ký tự, không khoảng trắng (theo dạng seed
    /// <c>hold.gateway.minutes</c>, <c>hold.transfer.hours</c>).
    /// </summary>
    private static string NormalizeKey(string? key)
    {
        var normalized = InputGuard.RequireText(key, KeyMaxLength, "Khóa cấu hình");

        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new BadRequestException("Khóa cấu hình không được chứa khoảng trắng.");
        }

        return normalized;
    }
}
