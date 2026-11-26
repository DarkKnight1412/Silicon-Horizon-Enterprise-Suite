using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Auth.Dto;

namespace SiliconHorizon.Application.Features.Auth;

/// <summary>
/// Use case tài khoản. Chỉ gọi stored procedure (không truy vấn bảng trực tiếp) — đúng thiết kế trong
/// Database/README.md mục 2: proc trả dữ liệu, API so khớp hash mật khẩu và phát JWT.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const int MinimumPasswordLength = 8;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public AuthService(
        ISqlExecutor sqlExecutor,
        IJwtTokenService jwtTokenService,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IGoogleTokenValidator googleTokenValidator)
    {
        _sqlExecutor = sqlExecutor;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _googleTokenValidator = googleTokenValidator;
    }

    public async Task<AuthTokensDto> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BadRequestException("Vui lòng nhập tên đăng nhập và mật khẩu.");
        }

        var account = await LoadAccountAsync(request.Username, cancellationToken);

        // Thông điệp giống nhau cho mọi trường hợp sai để không lộ tài khoản có tồn tại hay không.
        if (account is null || !account.User.IsActive || !_passwordHasher.Verify(request.Password, account.User.PasswordHash))
        {
            throw new UnauthorizedException();
        }

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.AuthUpdateLastLogin,
            new { UserId = account.User.Id },
            cancellationToken);

        return IssueTokens(account);
    }

    /// <summary>
    /// Đăng nhập bằng Google (ID token của Google Identity Services).
    /// <para>
    /// Luồng: xác minh ID token → tìm tài khoản theo EMAIL (đã được Google xác minh) → chưa có thì tạo
    /// tài khoản KHÁCH HÀNG mới (mật khẩu ngẫu nhiên, không dùng để đăng nhập bằng mật khẩu) → phát JWT.
    /// </para>
    /// <para>
    /// An toàn: KHÔNG bao giờ tự cấp vai trò nhân viên/Admin qua đường này; tài khoản đã tồn tại chỉ được
    /// đăng nhập và giữ NGUYÊN vai trò hiện có.
    /// </para>
    /// </summary>
    public async Task<AuthTokensDto> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            throw new BadRequestException("Thiếu ID token của Google.");
        }

        var googleUser = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken)
            ?? throw new UnauthorizedException(
                "Đăng nhập Google không hợp lệ hoặc đã hết hạn. Vui lòng thử lại.",
                "AUTH-GOOGLE-INVALID");

        // (1) Email đã có tài khoản (khách hoặc nhân viên) ⇒ đăng nhập luôn, giữ nguyên vai trò.
        var account = await LoadAccountAsync(googleUser.Email, cancellationToken);

        // (2) Chưa có ⇒ tạo tài khoản KHÁCH HÀNG mới.
        account ??= await RegisterGoogleCustomerAsync(googleUser, cancellationToken);

        if (!account.User.IsActive)
        {
            throw new UnauthorizedException("Tài khoản đã bị khoá.", "AUTH-ACCOUNT-INACTIVE");
        }

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.AuthUpdateLastLogin,
            new { UserId = account.User.Id },
            cancellationToken);

        return IssueTokens(account);
    }

    /// <summary>
    /// Tạo tài khoản khách hàng cho email Google chưa tồn tại (dùng lại <c>usp_Auth_RegisterCustomer</c>).
    /// Tên đăng nhập lấy từ phần trước '@' của email; nếu đã bị dùng thì thêm hậu tố ngẫu nhiên ngắn
    /// (khách vẫn đăng nhập bằng Google hoặc bằng email, không cần nhớ tên đăng nhập này).
    /// </summary>
    private async Task<AuthAccount> RegisterGoogleCustomerAsync(
        GoogleUserInfo googleUser,
        CancellationToken cancellationToken)
    {
        var username = BuildUsername(googleUser.Email);

        if (await LoadAccountAsync(username, cancellationToken) is not null)
        {
            username = $"{username}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<RegisteredCustomerRow>(
            StoredProcedureNames.AuthRegisterCustomer,
            new
            {
                Username = username,

                // Mật khẩu ngẫu nhiên: KHÔNG ai biết (không đăng nhập bằng mật khẩu được với tài khoản này).
                PasswordHash = _passwordHasher.Hash(Guid.NewGuid().ToString("N")),
                Email = googleUser.Email,
                Phone = (string?)null,
                FullName = string.IsNullOrWhiteSpace(googleUser.Name) ? username : googleUser.Name
            },
            cancellationToken);

        if (row is null)
        {
            throw new ConflictException(
                "Không tạo được tài khoản khách hàng từ Google.",
                "AUTH-GOOGLE-PROVISION-FAILED");
        }

        return await LoadAccountAsync(username, cancellationToken)
            ?? throw new ConflictException(
                "Đã tạo tài khoản nhưng không tải lại được hồ sơ.",
                "AUTH-GOOGLE-PROVISION-FAILED");
    }

    /// <summary>Lấy phần trước '@' của email và chuẩn hoá thành tên đăng nhập an toàn (≤ 50 ký tự).</summary>
    private static string BuildUsername(string email)
    {
        var atIndex = email.IndexOf('@');
        var local = atIndex > 0 ? email[..atIndex] : email;

        var sanitized = new string(local
            .Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-')
            .ToArray())
            .Trim('.', '_', '-');

        return sanitized.Length == 0 ? "khach-google" : sanitized[..Math.Min(sanitized.Length, 50)];
    }

    public async Task<RegisteredCustomerDto> RegisterCustomerAsync(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRegistration(request);

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<RegisteredCustomerRow>(
            StoredProcedureNames.AuthRegisterCustomer,
            new
            {
                Username = request.Username,
                PasswordHash = _passwordHasher.Hash(request.Password),
                Email = request.Email,
                Phone = request.Phone,
                FullName = request.FullName
            },
            cancellationToken);

        if (row is null)
        {
            throw new ConflictException("Không tạo được tài khoản khách hàng.", "AUTH-REGISTER-FAILED");
        }

        return new RegisteredCustomerDto(row.UserId, row.CustomerId);
    }

    public async Task<AuthTokensDto> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = await _jwtTokenService.ReadRefreshTokenAsync(request.RefreshToken, cancellationToken)
            ?? throw new UnauthorizedException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", "AUTH-REFRESH-INVALID");

        // Đọc lại từ database để quyền/vai trò luôn là bản mới nhất (có thể đã thay đổi sau khi phát token).
        var account = await LoadAccountAsync(payload.Username, cancellationToken);

        if (account is null || !account.User.IsActive)
        {
            throw new UnauthorizedException("Tài khoản không còn hiệu lực.", "AUTH-ACCOUNT-INACTIVE");
        }

        return IssueTokens(account);
    }

    public async Task<CurrentAccountDto> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Username))
        {
            throw new UnauthorizedException("Vui lòng đăng nhập.", "AUTH-REQUIRED");
        }

        var account = await LoadAccountAsync(_currentUser.Username, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");

        return new CurrentAccountDto
        {
            UserId = account.User.Id,
            Username = account.User.Username,
            Email = account.User.Email,
            Phone = account.User.Phone,
            UserType = account.User.UserType,
            Roles = account.Roles,
            Permissions = account.Permissions,
            CustomerId = account.Customer?.CustomerId,
            EmployeeId = account.Employee?.EmployeeId,
            BranchId = account.Employee?.BranchId,
            EmployeePosition = account.Employee?.Position
        };
    }

    /// <summary>
    /// Gọi <c>usp_Auth_GetUserByUsername</c> và đọc đủ 5 result set theo đúng thứ tự:
    /// user, vai trò, quyền, nhân viên, khách hàng.
    /// </summary>
    private async Task<AuthAccount?> LoadAccountAsync(string username, CancellationToken cancellationToken)
    {
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.AuthGetUserByUsername,
            new { Username = username },
            cancellationToken);

        var user = await results.ReadFirstOrDefaultAsync<AuthUserRow>(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = (await results.ReadListAsync<AuthRoleRow>(cancellationToken))
            .Select(row => row.RoleCode)
            .ToList();

        var permissions = (await results.ReadListAsync<AuthPermissionRow>(cancellationToken))
            .Select(row => row.PermissionCode)
            .ToList();

        var employee = await results.ReadFirstOrDefaultAsync<AuthEmployeeRow>(cancellationToken);
        var customer = await results.ReadFirstOrDefaultAsync<AuthCustomerRow>(cancellationToken);

        return new AuthAccount(user, roles, permissions, employee, customer);
    }

    private AuthTokensDto IssueTokens(AuthAccount account)
    {
        var subject = CreateSubject(account);
        var accessToken = _jwtTokenService.CreateAccessToken(subject);
        var refreshToken = _jwtTokenService.CreateRefreshToken(subject);

        return new AuthTokensDto
        {
            AccessToken = accessToken.Value,
            RefreshToken = refreshToken.Value,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc
        };
    }

    /// <summary>
    /// Chỉ nhân viên đang làm việc (Status = 0) mới được cấp phạm vi chi nhánh — nguyên tắc quyền tối thiểu
    /// (nhân viên đã nghỉ vẫn đăng nhập được để xem dữ liệu của mình nếu nghiệp vụ cho phép, nhưng không có quyền chi nhánh).
    /// </summary>
    private static TokenSubject CreateSubject(AuthAccount account)
    {
        var activeEmployee = account.Employee is { Status: 0 } ? account.Employee : null;

        return new TokenSubject
        {
            UserId = account.User.Id,
            Username = account.User.Username,
            UserType = account.User.UserType,
            Roles = account.Roles,
            Permissions = account.Permissions,
            CustomerId = account.Customer?.CustomerId,
            EmployeeId = activeEmployee?.EmployeeId,
            BranchId = activeEmployee?.BranchId,
            EmployeePosition = activeEmployee?.Position
        };
    }

    private static void ValidateRegistration(RegisterCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 50)
        {
            throw new BadRequestException("Tên đăng nhập bắt buộc và tối đa 50 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinimumPasswordLength)
        {
            throw new BadRequestException($"Mật khẩu tối thiểu {MinimumPasswordLength} ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Length > 20)
        {
            throw new BadRequestException("Số điện thoại bắt buộc và tối đa 20 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 150)
        {
            throw new BadRequestException("Họ tên bắt buộc và tối đa 150 ký tự.");
        }

        if (request.Email is { Length: > 150 })
        {
            throw new BadRequestException("Email tối đa 150 ký tự.");
        }
    }

    /// <summary>Dữ liệu tài khoản đã gộp từ 5 result set.</summary>
    private sealed record AuthAccount(
        AuthUserRow User,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions,
        AuthEmployeeRow? Employee,
        AuthCustomerRow? Customer);
}
