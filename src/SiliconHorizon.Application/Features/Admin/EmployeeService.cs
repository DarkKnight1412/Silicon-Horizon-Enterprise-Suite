using System.Text.RegularExpressions;
using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;
using SiliconHorizon.Domain.Entities.Common;
using SiliconHorizon.Domain.Entities.Hr;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case nhân viên — đồng bộ ba bảng <c>Users</c>, <c>UserRoles</c>, <c>Employees</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Một giao dịch cho ba bảng</b>: nếu bước sau lỗi thì bước trước được rollback
/// (nhờ <c>ITransactionScope</c> của <c>IUnitOfWork</c>).</item>
/// <item><b>Mật khẩu</b>: băm bằng <c>IPasswordHasher</c> (PBKDF2), tối thiểu 8 ký tự; không log, không trả ra API.</item>
/// <item><b>Vai trò theo chức vụ</b>: Cashier/TechAdvisor/Storekeeper/Manager ↔ mã vai trò cùng tên trong
/// <c>dbo.Roles</c>. Khi sửa, chỉ nhóm vai trò chức vụ được thay; vai trò ngoài nhóm (ví dụ <c>Admin</c>) giữ nguyên
/// để không vô tình thu hồi quyền quản trị.</item>
/// <item><b>Nghỉ việc</b>: <c>Status = Terminated</c> + <c>TerminatedDate</c> + khóa <c>Users.IsActive</c>;
/// không xóa cứng vì nhân viên đã tham chiếu ở đơn hàng/ca làm/chấm công/hoa hồng.</item>
/// <item><b>Nhật ký</b>: tạo/sửa/đổi trạng thái đều gọi <c>usp_Audit_Write</c> (thao tác nhạy cảm).</item>
/// </list>
/// </remarks>
public sealed class EmployeeService : IEmployeeService
{
    /// <summary>Employees.EmployeeCode varchar(20).</summary>
    private const int EmployeeCodeMaxLength = 20;

    /// <summary>Users.Username varchar(50).</summary>
    private const int UsernameMaxLength = 50;

    /// <summary>Users.PasswordHash varchar(255) — giới hạn đầu vào để hash luôn vừa cột.</summary>
    private const int PasswordMaxLength = 128;

    /// <summary>Độ dài mật khẩu tối thiểu khi tạo tài khoản.</summary>
    private const int PasswordMinLength = 8;

    /// <summary>Users.Email varchar(150).</summary>
    private const int EmailMaxLength = 150;

    /// <summary>Users.Phone / Employees.Phone varchar(20).</summary>
    private const int PhoneMaxLength = 20;

    /// <summary>Employees.FullName nvarchar(150).</summary>
    private const int FullNameMaxLength = 150;

    /// <summary>Tiền tố mã nhân viên tự sinh.</summary>
    private const string EmployeeCodePrefix = "NV";

    /// <summary>Nhận dạng mã nhân viên dạng NV0001 để tự sinh số kế tiếp.</summary>
    private static readonly Regex EmployeeCodePattern = new(
        $"^{EmployeeCodePrefix}(\\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IReadRepository<Employee> _readRepository;
    private readonly IWriteRepository<Employee> _writeRepository;
    private readonly IReadRepository<User> _users;
    private readonly IWriteRepository<User> _usersWriteRepository;
    private readonly IReadRepository<Role> _roles;
    private readonly IReadRepository<UserRole> _userRoles;
    private readonly IWriteRepository<UserRole> _userRolesWriteRepository;
    private readonly IReadRepository<Branch> _branches;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogWriter _auditLogWriter;

    public EmployeeService(
        IReadRepository<Employee> readRepository,
        IWriteRepository<Employee> writeRepository,
        IReadRepository<User> users,
        IWriteRepository<User> usersWriteRepository,
        IReadRepository<Role> roles,
        IReadRepository<UserRole> userRoles,
        IWriteRepository<UserRole> userRolesWriteRepository,
        IReadRepository<Branch> branches,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _users = users;
        _usersWriteRepository = usersWriteRepository;
        _roles = roles;
        _userRoles = userRoles;
        _userRolesWriteRepository = userRolesWriteRepository;
        _branches = branches;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public async Task<PagedResult<EmployeeDto>> GetPagedAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        // Đọc kèm tài khoản/chi nhánh/vai trò: bảng cấu hình nhỏ nên ghép trong bộ nhớ
        // (cùng cách làm của SupplierService/CatalogReferenceService).
        var users = await _users.ListAsync(cancellationToken);
        var branches = await _branches.ListAsync(cancellationToken);
        var roles = await _roles.ListAsync(cancellationToken);
        var userRoles = await _userRoles.ListAsync(cancellationToken);

        var userLookup = users.ToDictionary(user => user.Id);
        var branchLookup = branches.ToDictionary(branch => branch.Id);
        var roleLookup = roles.ToDictionary(role => role.Id);
        var roleCodesByUser = userRoles
            .Where(userRole => roleLookup.ContainsKey(userRole.RoleId))
            .GroupBy(userRole => userRole.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(userRole => roleLookup[userRole.RoleId].Code).ToList());

        var hasFilter = !string.IsNullOrEmpty(keyword)
                        || query.BranchId.HasValue
                        || query.Position.HasValue
                        || query.Status.HasValue;

        Func<Employee, bool>? predicate = hasFilter
            ? employee => MatchesKeyword(employee, userLookup, keyword)
                          && (query.BranchId is null || employee.BranchId == query.BranchId)
                          && (query.Position is null || employee.Position == query.Position)
                          && (query.Status is null || employee.Status == query.Status)
            : null;

        return await PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<Employee> employees) => employees.OrderBy(employee => employee.EmployeeCode),
            query.PageNumber,
            query.PageSize,
            predicate,
            employee => ToDto(employee, userLookup, branchLookup, roleCodesByUser),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ToDtoAsync(await GetOrThrowAsync(id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<EmployeeDto> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = InputGuard.RequireText(request.Username, UsernameMaxLength, "Tên đăng nhập");
        var password = InputGuard.RequireText(request.Password, PasswordMaxLength, "Mật khẩu");

        if (password.Length < PasswordMinLength)
        {
            throw new BadRequestException($"Mật khẩu phải có ít nhất {PasswordMinLength} ký tự.");
        }

        var fullName = InputGuard.RequireText(request.FullName, FullNameMaxLength, "Họ tên nhân viên");
        var email = InputGuard.OptionalText(request.Email, EmailMaxLength, "Email");
        var phone = InputGuard.OptionalText(request.Phone, PhoneMaxLength, "Điện thoại");

        InputGuard.EnsureDefined(request.Position, "Chức vụ (0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager)");

        if (request.HireDate == default)
        {
            throw new BadRequestException("Ngày vào làm (hireDate) là bắt buộc.");
        }

        await EnsureBranchExistsAsync(request.BranchId, cancellationToken);
        var role = await GetRoleForPositionAsync(request.Position, cancellationToken);

        var employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode)
            ? await GenerateEmployeeCodeAsync(cancellationToken)
            : InputGuard.RequireCode(request.EmployeeCode, EmployeeCodeMaxLength, "Mã nhân viên");

        await EnsureAccountIsUniqueAsync(username, employeeCode, null, cancellationToken);

        var now = _clock.UtcNow;

        // Ba bảng phải cùng thành công: Users → UserRoles → Employees.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var user = new User
        {
            Username = username,
            Email = email,
            Phone = phone,
            PasswordHash = _passwordHasher.Hash(password),
            UserType = 1,                       // 1 = Employee
            IsActive = true,
            CreatedAt = now,
            CreatedBy = _currentUser.UserId
        };

        await _usersWriteRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _userRolesWriteRepository.AddAsync(
            new UserRole { UserId = user.Id, RoleId = role.Id },
            cancellationToken);

        var employee = new Employee
        {
            EmployeeCode = employeeCode,
            UserId = user.Id,
            FullName = fullName,
            Phone = phone,
            Position = request.Position,
            BranchId = request.BranchId,
            Status = EmployeeStatus.Active,
            HireDate = request.HireDate,
            CreatedAt = now,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "EmployeeCreated",
            "Employee",
            employee.Id,
            newValues: new
            {
                employee.EmployeeCode,
                employee.UserId,
                employee.FullName,
                employee.Position,
                employee.BranchId,
                employee.HireDate,
                RoleCode = role.Code
            },
            reason: $"Tạo nhân viên kèm tài khoản '{username}' và vai trò '{role.Code}'.",
            cancellationToken: cancellationToken);

        return await ToDtoAsync(employee, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> UpdateAsync(
        long id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetOrThrowAsync(id, cancellationToken);
        var user = await GetUserOrThrowAsync(employee, cancellationToken);

        var fullName = InputGuard.RequireText(request.FullName, FullNameMaxLength, "Họ tên nhân viên");
        var email = InputGuard.OptionalText(request.Email, EmailMaxLength, "Email");
        var phone = InputGuard.OptionalText(request.Phone, PhoneMaxLength, "Điện thoại");

        InputGuard.EnsureDefined(request.Position, "Chức vụ (0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager)");

        if (request.HireDate == default)
        {
            throw new BadRequestException("Ngày vào làm (hireDate) là bắt buộc.");
        }

        if (request.TerminatedDate is { } terminatedDate && terminatedDate < request.HireDate)
        {
            throw new BadRequestException("Ngày nghỉ việc (terminatedDate) không thể trước ngày vào làm (hireDate).");
        }

        await EnsureBranchExistsAsync(request.BranchId, cancellationToken);
        var role = await GetRoleForPositionAsync(request.Position, cancellationToken);

        var oldValues = new
        {
            employee.FullName,
            employee.Phone,
            employee.Position,
            employee.BranchId,
            employee.HireDate,
            employee.TerminatedDate,
            employee.Status,
            user.Email,
            user.IsActive
        };

        var now = _clock.UtcNow;

        // Đồng bộ Employees ↔ Users ↔ UserRoles trong cùng một giao dịch.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        employee.FullName = fullName;
        employee.Phone = phone;
        employee.Position = request.Position;
        employee.BranchId = request.BranchId;
        employee.HireDate = request.HireDate;
        employee.TerminatedDate = request.TerminatedDate;
        employee.UpdatedAt = now;
        employee.UpdatedBy = _currentUser.UserId;

        user.Email = email;
        user.Phone = phone;
        user.IsActive = request.IsUserActive;
        user.UpdatedAt = now;
        user.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(employee);
        _usersWriteRepository.Update(user);

        await SyncPositionRoleAsync(user.Id, role, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "EmployeeUpdated",
            "Employee",
            employee.Id,
            oldValues,
            new
            {
                employee.FullName,
                employee.Phone,
                employee.Position,
                employee.BranchId,
                employee.HireDate,
                employee.TerminatedDate,
                employee.Status,
                user.Email,
                user.IsActive,
                RoleCode = role.Code
            },
            cancellationToken: cancellationToken);

        return await ToDtoAsync(employee, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> SetStatusAsync(
        long id,
        SetEmployeeStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetOrThrowAsync(id, cancellationToken);
        var user = await GetUserOrThrowAsync(employee, cancellationToken);

        InputGuard.EnsureDefined(request.Status, "Trạng thái nhân viên (0 Đang làm việc, 1 Đã nghỉ việc)");

        var shouldBeActive = request.Status == EmployeeStatus.Active;

        if (request.Status == EmployeeStatus.Terminated && request.TerminatedDate is null)
        {
            throw new BadRequestException("Cần ngày nghỉ việc (terminatedDate) khi chuyển trạng thái sang đã nghỉ việc.");
        }

        if (request.TerminatedDate is { } terminatedDate && terminatedDate < employee.HireDate)
        {
            throw new BadRequestException("Ngày nghỉ việc (terminatedDate) không thể trước ngày vào làm (hireDate).");
        }

        if (employee.Status == request.Status && user.IsActive == shouldBeActive)
        {
            return await ToDtoAsync(employee, cancellationToken);
        }

        var oldValues = new
        {
            employee.Status,
            employee.TerminatedDate,
            IsUserActive = user.IsActive
        };

        var now = _clock.UtcNow;

        employee.Status = request.Status;
        employee.TerminatedDate = shouldBeActive ? null : request.TerminatedDate;
        employee.UpdatedAt = now;
        employee.UpdatedBy = _currentUser.UserId;

        // Nghỉ việc thì khóa luôn tài khoản đăng nhập (Users.IsActive) — không xóa cứng dữ liệu.
        user.IsActive = shouldBeActive;
        user.UpdatedAt = now;
        user.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(employee);
        _usersWriteRepository.Update(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            shouldBeActive ? "EmployeeReinstated" : "EmployeeTerminated",
            "Employee",
            employee.Id,
            oldValues,
            new { employee.Status, employee.TerminatedDate, IsUserActive = user.IsActive },
            reason: shouldBeActive ? "Cho nhân viên làm việc lại." : "Cho nhân viên nghỉ việc và khóa tài khoản.",
            cancellationToken: cancellationToken);

        return await ToDtoAsync(employee, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Mã vai trò tương ứng chức vụ — khớp seed <c>dbo.Roles</c> ở mục 13 của table_and_data.sql.</summary>
    private static readonly string[] PositionRoleCodes =
    [
        RoleCodes.Cashier,
        RoleCodes.TechAdvisor,
        RoleCodes.Storekeeper,
        RoleCodes.Manager
    ];

    private async Task<Employee> GetOrThrowAsync(long id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy nhân viên Id = {id}.");

    /// <summary>
    /// Nhân viên luôn phải có tài khoản (<c>Employees.UserId</c> NOT NULL và duy nhất) — thiếu tài khoản
    /// nghĩa là dữ liệu đã lệch giữa <c>Users</c> và <c>Employees</c>, báo 404 để quản trị biết mà xử lý.
    /// </summary>
    private async Task<User> GetUserOrThrowAsync(Employee employee, CancellationToken cancellationToken)
        => await _users.GetByIdAsync(employee.UserId, cancellationToken)
           ?? throw new NotFoundException(
               $"Nhân viên Id = {employee.Id} đang trỏ tới tài khoản không tồn tại (UserId = {employee.UserId}).");

    private async Task<EmployeeDto> ToDtoAsync(Employee employee, CancellationToken cancellationToken)
    {
        var user = await GetUserOrThrowAsync(employee, cancellationToken);
        var branch = await _branches.GetByIdAsync(employee.BranchId, cancellationToken);
        var roles = await _roles.ListAsync(cancellationToken);
        var userRoles = await _userRoles.ListAsync(cancellationToken);

        var roleLookup = roles.ToDictionary(role => role.Id);
        var codes = userRoles
            .Where(userRole => userRole.UserId == employee.UserId && roleLookup.ContainsKey(userRole.RoleId))
            .Select(userRole => roleLookup[userRole.RoleId].Code)
            .ToList();

        return EmployeeDto.FromEntities(employee, user, ResolveRoleCode(codes), branch?.Name);
    }

    private static EmployeeDto ToDto(
        Employee employee,
        IReadOnlyDictionary<long, User> userLookup,
        IReadOnlyDictionary<int, Branch> branchLookup,
        IReadOnlyDictionary<long, List<string>> roleCodesByUser)
    {
        userLookup.TryGetValue(employee.UserId, out var user);
        branchLookup.TryGetValue(employee.BranchId, out var branch);
        roleCodesByUser.TryGetValue(employee.UserId, out var codes);

        // Nếu dữ liệu lệch (thiếu tài khoản), DTO vẫn trả được hồ sơ nhân viên với tên đăng nhập rỗng.
        var safeUser = user ?? new User
        {
            Id = employee.UserId,
            Username = string.Empty,
            PasswordHash = string.Empty,
            UserType = 1,
            IsActive = false
        };

        return EmployeeDto.FromEntities(employee, safeUser, ResolveRoleCode(codes), branch?.Name);
    }

    /// <summary>
    /// Chọn vai trò đại diện: ưu tiên vai trò thuộc nhóm chức vụ (Cashier/TechAdvisor/Storekeeper/Manager),
    /// nếu không có thì lấy vai trò đầu tiên (ví dụ nhân viên đồng thời là <c>Admin</c>).
    /// </summary>
    private static string ResolveRoleCode(IReadOnlyCollection<string>? codes)
    {
        if (codes is null || codes.Count == 0)
        {
            return string.Empty;
        }

        return codes.FirstOrDefault(code => PositionRoleCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
               ?? codes.First();
    }

    private static bool MatchesKeyword(
        Employee employee,
        IReadOnlyDictionary<long, User> userLookup,
        string? keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return true;
        }

        if (employee.EmployeeCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || employee.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || (employee.Phone?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }

        return userLookup.TryGetValue(employee.UserId, out var user)
               && user.Username.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Chi nhánh phải tồn tại (<c>Employees.BranchId</c> là khóa ngoại bắt buộc).</summary>
    private async Task EnsureBranchExistsAsync(int branchId, CancellationToken cancellationToken)
    {
        InputGuard.EnsurePositive(branchId, "branchId");

        var branches = await _branches.ListAsync(cancellationToken);

        if (branches.All(branch => branch.Id != branchId))
        {
            throw new BadRequestException($"Chi nhánh Id = {branchId} không tồn tại.");
        }
    }

    /// <summary>
    /// Tìm vai trò trong <c>dbo.Roles</c> theo chức vụ. Nếu database chưa seed vai trò tương ứng
    /// (xem mục 13 của Database/table_and_data.sql) thì trả 409 để quản trị seed lại trước.
    /// </summary>
    private async Task<Role> GetRoleForPositionAsync(
        EmployeePosition position,
        CancellationToken cancellationToken)
    {
        var roleCode = RoleCodeForPosition(position);
        var roles = await _roles.ListAsync(cancellationToken);

        return roles.FirstOrDefault(role => string.Equals(role.Code, roleCode, StringComparison.OrdinalIgnoreCase))
               ?? throw new ConflictException(
                   $"Vai trò '{roleCode}' chưa tồn tại trong dbo.Roles. Hãy tạo vai trò này trước khi thêm nhân viên.",
                   "SQL-50005");
    }

    /// <summary>Ánh xạ chức vụ → mã vai trò (<c>dbo.Roles.Code</c>).</summary>
    private static string RoleCodeForPosition(EmployeePosition position) => position switch
    {
        EmployeePosition.Cashier => RoleCodes.Cashier,
        EmployeePosition.TechAdvisor => RoleCodes.TechAdvisor,
        EmployeePosition.Storekeeper => RoleCodes.Storekeeper,
        EmployeePosition.Manager => RoleCodes.Manager,
        _ => throw new BadRequestException("Chức vụ không hợp lệ.")
    };

    /// <summary>Sinh mã nhân viên kế tiếp dạng <c>NV0001</c> dựa trên mã lớn nhất đang có.</summary>
    private async Task<string> GenerateEmployeeCodeAsync(CancellationToken cancellationToken)
    {
        var employees = await _readRepository.ListAsync(cancellationToken);

        var maxNumber = 0;

        foreach (var employee in employees)
        {
            var match = EmployeeCodePattern.Match(employee.EmployeeCode);

            if (match.Success
                && int.TryParse(match.Groups[1].Value, out var number)
                && number > maxNumber)
            {
                maxNumber = number;
            }
        }

        var next = maxNumber + 1;
        var code = $"{EmployeeCodePrefix}{next:D4}";

        if (code.Length > EmployeeCodeMaxLength)
        {
            throw new ConflictException(
                $"Không sinh được mã nhân viên mới (vượt {EmployeeCodeMaxLength} ký tự). Hãy truyền employeeCode thủ công.",
                "SQL-50005");
        }

        return code;
    }

    /// <summary>
    /// Kiểm tra duy nhất của <c>UQ_Users_Username</c> và <c>UQ_Employees_Code</c>
    /// (bỏ qua chính bản ghi đang sửa) để trả 409 thay vì lỗi SQL thô.
    /// </summary>
    private async Task EnsureAccountIsUniqueAsync(
        string username,
        string employeeCode,
        long? currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var users = await _users.ListAsync(cancellationToken);

        if (users.Any(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"Tên đăng nhập {username} đã tồn tại.", "SQL-50005");
        }

        var employees = await _readRepository.ListAsync(cancellationToken);

        if (employees.Any(employee =>
                employee.Id != currentEmployeeId
                && string.Equals(employee.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"Mã nhân viên {employeeCode} đã tồn tại.", "SQL-50005");
        }
    }

    /// <summary>
    /// Đồng bộ dòng <c>UserRoles</c> theo chức vụ: chỉ xử lý nhóm vai trò chức vụ
    /// (Cashier/TechAdvisor/Storekeeper/Manager); vai trò ngoài nhóm như <c>Admin</c> được giữ nguyên.
    /// Thao tác xóa/thêm chỉ được ghi nhận khi <c>SaveChangesAsync</c> của caller chạy.
    /// </summary>
    private async Task SyncPositionRoleAsync(long userId, Role targetRole, CancellationToken cancellationToken)
    {
        var roles = await _roles.ListAsync(cancellationToken);
        var roleLookup = roles.ToDictionary(role => role.Id);

        var currentRows = (await _userRoles.ListAsync(cancellationToken))
            .Where(userRole => userRole.UserId == userId)
            .ToList();

        var positionRows = currentRows
            .Where(userRole => roleLookup.TryGetValue(userRole.RoleId, out var role)
                               && PositionRoleCodes.Contains(role.Code, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (positionRows.Any(row => row.RoleId == targetRole.Id))
        {
            // Đã có vai trò đích → chỉ gỡ các vai trò chức vụ khác.
            foreach (var row in positionRows.Where(row => row.RoleId != targetRole.Id))
            {
                _userRolesWriteRepository.Remove(row);
            }

            return;
        }

        // Chưa có vai trò đích → gỡ hết vai trò chức vụ cũ rồi gán vai trò mới (tránh trùng khóa chính).
        foreach (var row in positionRows)
        {
            _userRolesWriteRepository.Remove(row);
        }

        await _userRolesWriteRepository.AddAsync(
            new UserRole { UserId = userId, RoleId = targetRole.Id },
            cancellationToken);
    }
}

