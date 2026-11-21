using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case quyền (CRUD bằng EF Core).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Mã quyền duy nhất</b> <c>UQ_Permissions_Code</c> — kiểm tra trước khi ghi để trả 409.</item>
/// <item>Mã quyền khớp <c>PermissionCodes</c> và giá trị dùng cho <c>[RequirePermission("...")]</c>,
/// nên đổi mã cần rà lại các endpoint đang dùng (gắn policy sớm trước khi seed sẽ trả 403 cho mọi người).</item>
/// <item><b>Không có cột trạng thái</b> nên không có endpoint DELETE — xóa quyền sẽ vi phạm khóa ngoại
/// từ <c>RolePermissions</c>.</item>
/// </list>
/// </remarks>
public sealed class PermissionService : IPermissionService
{
    /// <summary>Permissions.Code varchar(100).</summary>
    private const int CodeMaxLength = 100;

    private readonly IReadRepository<Permission> _readRepository;
    private readonly IWriteRepository<Permission> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogWriter _auditLogWriter;

    public PermissionService(
        IReadRepository<Permission> readRepository,
        IWriteRepository<Permission> writeRepository,
        IUnitOfWork unitOfWork,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public Task<PagedResult<PermissionDto>> GetPagedAsync(
        PermissionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        Func<Permission, bool>? predicate = string.IsNullOrEmpty(keyword)
            ? null
            : permission => permission.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<Permission> permissions) => permissions.OrderBy(permission => permission.Code),
            query.PageNumber,
            query.PageSize,
            predicate,
            PermissionDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PermissionDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => PermissionDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<PermissionDto> CreateAsync(
        CreatePermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = InputGuard.RequireText(request.Code, CodeMaxLength, "Mã quyền");

        await EnsureCodeIsUniqueAsync(code, null, cancellationToken);

        var permission = new Permission { Code = code };

        await _writeRepository.AddAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "PermissionCreated",
            "Permission",
            permission.Id,
            newValues: new { permission.Code },
            cancellationToken: cancellationToken);

        return PermissionDto.FromEntity(permission);
    }

    /// <inheritdoc />
    public async Task<PermissionDto> UpdateAsync(
        int id,
        UpdatePermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var permission = await GetOrThrowAsync(id, cancellationToken);
        var code = InputGuard.RequireText(request.Code, CodeMaxLength, "Mã quyền");

        await EnsureCodeIsUniqueAsync(code, id, cancellationToken);

        var oldCode = permission.Code;
        permission.Code = code;

        _writeRepository.Update(permission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "PermissionUpdated",
            "Permission",
            permission.Id,
            oldValues: new { Code = oldCode },
            newValues: new { permission.Code },
            cancellationToken: cancellationToken);

        return PermissionDto.FromEntity(permission);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Permission> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy quyền Id = {id}.");

    /// <summary>UQ_Permissions_Code — so khớp không phân biệt hoa thường, bỏ qua bản ghi đang sửa.</summary>
    private async Task EnsureCodeIsUniqueAsync(string code, int? currentId, CancellationToken cancellationToken)
    {
        var permissions = await _readRepository.ListAsync(cancellationToken);

        var duplicated = permissions.Any(permission =>
            permission.Id != currentId
            && string.Equals(permission.Code, code, StringComparison.OrdinalIgnoreCase));

        if (duplicated)
        {
            throw new ConflictException($"Mã quyền {code} đã tồn tại.", "SQL-50005");
        }
    }
}
