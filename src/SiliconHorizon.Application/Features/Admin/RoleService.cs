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
/// Hiện thực use case vai trò (CRUD + gán quyền).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Bảng nối khóa chính tổ hợp</b>: <c>RolePermissions(RoleId, PermissionId)</c> — API xử lý theo
/// kiểu "thay thế toàn bộ" nên dòng cần thêm thì <c>Add</c>, dòng không còn thì <c>Remove</c>.</item>
/// <item><b>Mã vai trò bất biến khi sửa</b>: <c>UserRoles.RoleId</c> và <c>RolePermissions.RoleId</c> đang trỏ tới
/// vai trò; đổi mã sẽ làm sai lệch <c>RoleCodes</c> dùng trong <c>[Authorize(Roles = ...)]</c>.</item>
/// <item><b>Không có cột trạng thái</b> nên không có endpoint DELETE — xóa vai trò sẽ vi phạm khóa ngoại.</item>
/// <item><b>Nhật ký</b>: mọi thay đổi gọi <c>usp_Audit_Write</c> (thao tác nhạy cảm về phân quyền).</item>
/// </list>
/// </remarks>
public sealed class RoleService : IRoleService
{
    /// <summary>Roles.Code varchar(50).</summary>
    private const int CodeMaxLength = 50;

    /// <summary>Roles.Name nvarchar(100).</summary>
    private const int NameMaxLength = 100;

    private readonly IReadRepository<Role> _readRepository;
    private readonly IWriteRepository<Role> _writeRepository;
    private readonly IReadRepository<Permission> _permissions;
    private readonly IReadRepository<RolePermission> _rolePermissions;
    private readonly IWriteRepository<RolePermission> _rolePermissionsWriteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogWriter _auditLogWriter;

    public RoleService(
        IReadRepository<Role> readRepository,
        IWriteRepository<Role> writeRepository,
        IReadRepository<Permission> permissions,
        IReadRepository<RolePermission> rolePermissions,
        IWriteRepository<RolePermission> rolePermissionsWriteRepository,
        IUnitOfWork unitOfWork,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _permissions = permissions;
        _rolePermissions = rolePermissions;
        _rolePermissionsWriteRepository = rolePermissionsWriteRepository;
        _unitOfWork = unitOfWork;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public async Task<PagedResult<RoleDto>> GetPagedAsync(
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var rolePermissions = await _rolePermissions.ListAsync(cancellationToken);
        var permissionCountByRole = rolePermissions
            .GroupBy(rolePermission => rolePermission.RoleId)
            .ToDictionary(group => group.Key, group => group.Count());

        var paged = await _readRepository.ListPagedAsync(
            (IQueryable<Role> roles) => roles.OrderBy(role => role.Code),
            page.PageNumber,
            page.PageSize,
            cancellationToken);

        return PagedResult<RoleDto>.Create(
            paged.Items
                .Select(role => RoleDto.FromEntity(
                    role,
                    permissionCountByRole.TryGetValue(role.Id, out var count) ? count : 0))
                .ToList(),
            paged.PageNumber,
            paged.PageSize,
            paged.TotalCount);
    }

    /// <inheritdoc />
    public async Task<RoleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await ToDtoAsync(await GetOrThrowAsync(id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<RoleDto> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = InputGuard.RequireText(request.Code, CodeMaxLength, "Mã vai trò");
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên vai trò");

        await EnsureCodeIsUniqueAsync(code, null, cancellationToken);

        var role = new Role { Code = code, Name = name };

        await _writeRepository.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "RoleCreated",
            "Role",
            role.Id,
            newValues: new { role.Code, role.Name },
            cancellationToken: cancellationToken);

        return RoleDto.FromEntity(role, 0);
    }

    /// <inheritdoc />
    public async Task<RoleDto> UpdateAsync(
        int id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await GetOrThrowAsync(id, cancellationToken);
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên vai trò");

        var oldName = role.Name;
        role.Name = name;

        _writeRepository.Update(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "RoleUpdated",
            "Role",
            role.Id,
            oldValues: new { Name = oldName },
            newValues: new { role.Name },
            cancellationToken: cancellationToken);

        return await ToDtoAsync(role, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RoleDto> SetPermissionsAsync(
        int id,
        SetRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await GetOrThrowAsync(id, cancellationToken);

        var permissions = await _permissions.ListAsync(cancellationToken);
        var permissionLookup = permissions.ToDictionary(permission => permission.Id);

        // Bỏ id trùng và kiểm tra mọi PermissionId đều tồn tại.
        var targetIds = request.PermissionIds.Distinct().ToList();

        var unknownIds = targetIds.Where(permissionId => !permissionLookup.ContainsKey(permissionId)).ToList();

        if (unknownIds.Count > 0)
        {
            throw new BadRequestException(
                $"Quyền không tồn tại: {string.Join(", ", unknownIds)}.");
        }

        var currentRows = (await _rolePermissions.ListAsync(cancellationToken))
            .Where(rolePermission => rolePermission.RoleId == id)
            .ToList();

        var currentIds = currentRows.Select(rolePermission => rolePermission.PermissionId).ToHashSet();

        var oldCodes = currentIds
            .Where(permissionLookup.ContainsKey)
            .Select(permissionId => permissionLookup[permissionId].Code)
            .OrderBy(code => code)
            .ToList();

        foreach (var row in currentRows.Where(row => !targetIds.Contains(row.PermissionId)))
        {
            _rolePermissionsWriteRepository.Remove(row);
        }

        foreach (var permissionId in targetIds.Where(permissionId => !currentIds.Contains(permissionId)))
        {
            await _rolePermissionsWriteRepository.AddAsync(
                new RolePermission { RoleId = id, PermissionId = permissionId },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var newCodes = targetIds
            .Select(permissionId => permissionLookup[permissionId].Code)
            .OrderBy(code => code)
            .ToList();

        await _auditLogWriter.WriteAsync(
            "RolePermissionsChanged",
            "Role",
            role.Id,
            oldValues: new { role.Code, Permissions = oldCodes },
            newValues: new { role.Code, Permissions = newCodes },
            reason: $"Gán {newCodes.Count} quyền cho vai trò '{role.Code}'.",
            cancellationToken: cancellationToken);

        return await ToDtoAsync(role, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Role> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy vai trò Id = {id}.");

    private async Task<RoleDto> ToDtoAsync(Role role, CancellationToken cancellationToken)
    {
        var rolePermissions = await _rolePermissions.ListAsync(cancellationToken);
        var count = rolePermissions.Count(rolePermission => rolePermission.RoleId == role.Id);

        return RoleDto.FromEntity(role, count);
    }

    /// <summary>UQ_Roles_Code — so khớp không phân biệt hoa thường, bỏ qua bản ghi đang sửa.</summary>
    private async Task EnsureCodeIsUniqueAsync(string code, int? currentId, CancellationToken cancellationToken)
    {
        var roles = await _readRepository.ListAsync(cancellationToken);

        var duplicated = roles.Any(role =>
            role.Id != currentId
            && string.Equals(role.Code, code, StringComparison.OrdinalIgnoreCase));

        if (duplicated)
        {
            throw new ConflictException($"Mã vai trò {code} đã tồn tại.", "SQL-50005");
        }
    }
}
