using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case gán vai trò cho người dùng (thao tác trên bảng nối <c>dbo.UserRoles</c>).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Khóa chính tổ hợp</b>: mọi thao tác đều làm việc theo cặp <c>(UserId, RoleId)</c>; dòng thừa
/// được <c>Remove</c>, dòng thiếu được <c>Add</c> — không sinh SQL <c>MERGE</c> hay cập nhật khóa chính.</item>
/// <item><b>Kiểm tra tồn tại trước khi ghi</b>: người dùng và vai trò phải tồn tại (khóa ngoại
/// <c>FK_UserRoles_Users</c>/<c>FK_UserRoles_Roles</c>) → trả 404/400 thay vì lỗi SQL thô.</item>
/// <item><b>Nhật ký</b>: thay đổi quyền của tài khoản là thao tác nhạy cảm nên luôn gọi <c>usp_Audit_Write</c>.</item>
/// </list>
/// </remarks>
public sealed class UserRoleService : IUserRoleService
{
    private readonly IReadRepository<User> _users;
    private readonly IReadRepository<Role> _roles;
    private readonly IReadRepository<UserRole> _userRoles;
    private readonly IWriteRepository<UserRole> _userRolesWriteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogWriter _auditLogWriter;

    public UserRoleService(
        IReadRepository<User> users,
        IReadRepository<Role> roles,
        IReadRepository<UserRole> userRoles,
        IWriteRepository<UserRole> userRolesWriteRepository,
        IUnitOfWork unitOfWork,
        IAuditLogWriter auditLogWriter)
    {
        _users = users;
        _roles = roles;
        _userRoles = userRoles;
        _userRolesWriteRepository = userRolesWriteRepository;
        _unitOfWork = unitOfWork;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserRoleDto>> GetRolesAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        return await BuildResultAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserRoleDto>> SetRolesAsync(
        long userId,
        SetUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        var roles = await _roles.ListAsync(cancellationToken);
        var roleLookup = roles.ToDictionary(role => role.Id);

        var targetRoleIds = request.RoleIds.Distinct().ToList();

        var unknownRoleIds = targetRoleIds.Where(roleId => !roleLookup.ContainsKey(roleId)).ToList();

        if (unknownRoleIds.Count > 0)
        {
            throw new BadRequestException($"Vai trò không tồn tại: {string.Join(", ", unknownRoleIds)}.");
        }

        var currentRows = (await _userRoles.ListAsync(cancellationToken))
            .Where(userRole => userRole.UserId == userId)
            .ToList();

        var currentRoleIds = currentRows.Select(userRole => userRole.RoleId).ToHashSet();

        var oldCodes = currentRoleIds
            .Where(roleLookup.ContainsKey)
            .Select(roleId => roleLookup[roleId].Code)
            .OrderBy(code => code)
            .ToList();

        foreach (var row in currentRows.Where(row => !targetRoleIds.Contains(row.RoleId)))
        {
            _userRolesWriteRepository.Remove(row);
        }

        foreach (var roleId in targetRoleIds.Where(roleId => !currentRoleIds.Contains(roleId)))
        {
            await _userRolesWriteRepository.AddAsync(
                new UserRole { UserId = userId, RoleId = roleId },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var newCodes = targetRoleIds
            .Select(roleId => roleLookup[roleId].Code)
            .OrderBy(code => code)
            .ToList();

        await _auditLogWriter.WriteAsync(
            "UserRolesChanged",
            "User",
            userId,
            oldValues: new { user.Username, Roles = oldCodes },
            newValues: new { user.Username, Roles = newCodes },
            reason: $"Gán {newCodes.Count} vai trò cho tài khoản '{user.Username}'.",
            cancellationToken: cancellationToken);

        return await BuildResultAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserRoleDto>> AssignRoleAsync(
        long userId,
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var role = await GetRoleOrThrowAsync(roleId, cancellationToken);

        var currentRows = (await _userRoles.ListAsync(cancellationToken))
            .Where(userRole => userRole.UserId == userId)
            .ToList();

        if (currentRows.Any(userRole => userRole.RoleId == roleId))
        {
            // Đã có vai trò này → thao tác idempotent, không ghi gì thêm.
            return await BuildResultAsync(userId, cancellationToken);
        }

        var oldCodes = currentRows.Select(row => row.RoleId).ToList();

        await _userRolesWriteRepository.AddAsync(
            new UserRole { UserId = userId, RoleId = roleId },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var allRoles = await _roles.ListAsync(cancellationToken);
        var roleLookup = allRoles.ToDictionary(item => item.Id);

        await _auditLogWriter.WriteAsync(
            "UserRoleAssigned",
            "User",
            userId,
            oldValues: new
            {
                user.Username,
                Roles = oldCodes.Where(roleLookup.ContainsKey).Select(id => roleLookup[id].Code).OrderBy(code => code).ToList()
            },
            newValues: new { user.Username, RoleCode = role.Code },
            cancellationToken: cancellationToken);

        return await BuildResultAsync(userId, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Đọc lại danh sách vai trò của người dùng sau khi ghi, kèm mã/tên vai trò.</summary>
    private async Task<IReadOnlyList<UserRoleDto>> BuildResultAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var roles = await _roles.ListAsync(cancellationToken);
        var roleLookup = roles.ToDictionary(role => role.Id);

        var rows = (await _userRoles.ListAsync(cancellationToken))
            .Where(userRole => userRole.UserId == userId)
            .ToList();

        return rows
            .Where(userRole => roleLookup.ContainsKey(userRole.RoleId))
            .Select(userRole => UserRoleDto.FromEntities(userRole, roleLookup[userRole.RoleId]))
            .OrderBy(dto => dto.RoleCode)
            .ToList();
    }

    private async Task<User> GetUserOrThrowAsync(long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            throw new BadRequestException("userId phải là số nguyên dương.");
        }

        return await _users.GetByIdAsync(userId, cancellationToken)
               ?? throw new NotFoundException($"Không tìm thấy tài khoản Id = {userId}.");
    }

    private async Task<Role> GetRoleOrThrowAsync(int roleId, CancellationToken cancellationToken)
    {
        if (roleId <= 0)
        {
            throw new BadRequestException("roleId phải là số nguyên dương.");
        }

        return await _roles.GetByIdAsync(roleId, cancellationToken)
               ?? throw new NotFoundException($"Không tìm thấy vai trò Id = {roleId}.");
    }

    private async Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken)
        => await GetUserOrThrowAsync(userId, cancellationToken);
}
