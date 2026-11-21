using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Customers.Dto;
using SiliconHorizon.Domain.Entities.Common;
using SiliconHorizon.Domain.Entities.Customers;
using SiliconHorizon.Domain.Entities.Pricing;

namespace SiliconHorizon.Application.Features.Customers;

/// <summary>
/// Hiện thực use case khách hàng (CRUD bằng EF Core).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Không xóa cứng</b>: khách đã có đơn hàng/công nợ chỉ được đặt <c>isActive = false</c>
/// (đúng cảnh báo ở cuối mục 10 của Database/README.md).</item>
/// <item><b>Tài khoản gắn kèm</b>: <c>Customers.UserId</c> có thể <c>null</c> (khách vãng lai mua tại quầy);
/// nếu Admin gắn tài khoản thì phải tồn tại và chưa thuộc hồ sơ khách khác.</item>
/// <item><b>Nhóm khách</b> là dữ liệu chủ cho bảng giá/voucher nên <c>customerGroupId</c> phải tồn tại.</item>
/// <item><b>Nhật ký</b>: tạo/sửa/đổi trạng thái (kèm <c>creditLimit</c>) đều gọi <c>usp_Audit_Write</c>.</item>
/// </list>
/// </remarks>
public sealed class CustomerService : ICustomerService
{
    /// <summary>Customers.FullName nvarchar(150).</summary>
    private const int FullNameMaxLength = 150;

    /// <summary>Customers.Phone varchar(20).</summary>
    private const int PhoneMaxLength = 20;

    /// <summary>Customers.Email varchar(150).</summary>
    private const int EmailMaxLength = 150;

    /// <summary>Customers.TaxCode varchar(20).</summary>
    private const int TaxCodeMaxLength = 20;

    private readonly IReadRepository<Customer> _readRepository;
    private readonly IWriteRepository<Customer> _writeRepository;
    private readonly IReadRepository<CustomerGroup> _customerGroups;
    private readonly IReadRepository<User> _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogWriter _auditLogWriter;

    public CustomerService(
        IReadRepository<Customer> readRepository,
        IWriteRepository<Customer> writeRepository,
        IReadRepository<CustomerGroup> customerGroups,
        IReadRepository<User> users,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _customerGroups = customerGroups;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CustomerDto>> GetPagedAsync(
        CustomerListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();
        var groupCodes = (await _customerGroups.ListAsync(cancellationToken))
            .ToDictionary(group => group.Id, group => group.Code);

        var hasFilter = !string.IsNullOrEmpty(keyword)
                        || query.IsActive.HasValue
                        || query.CustomerGroupId.HasValue;

        Func<Customer, bool>? predicate = hasFilter
            ? customer => (string.IsNullOrEmpty(keyword)
                           || customer.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                           || customer.Phone.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                           || (customer.Email?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                          && (query.IsActive is null || customer.IsActive == query.IsActive)
                          && (query.CustomerGroupId is null || customer.CustomerGroupId == query.CustomerGroupId)
            : null;

        return await PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<Customer> customers) => customers.OrderBy(customer => customer.FullName),
            query.PageNumber,
            query.PageSize,
            predicate,
            customer => ToDto(customer, groupCodes),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var customer = await GetOrThrowAsync(id, cancellationToken);
        var groupCodes = (await _customerGroups.ListAsync(cancellationToken))
            .ToDictionary(group => group.Id, group => group.Code);

        return ToDto(customer, groupCodes);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = await ValidateAsync(
            request.FullName,
            request.Phone,
            request.Email,
            request.CustomerGroupId,
            request.TaxCode,
            request.CreditLimit,
            request.PaymentTermDays,
            cancellationToken);

        if (request.UserId is { } userId)
        {
            InputGuard.EnsurePositive(userId, "userId");
            await EnsureUserIsAvailableAsync(userId, null, cancellationToken);
        }

        var customer = new Customer
        {
            UserId = request.UserId,
            FullName = validated.FullName,
            Phone = validated.Phone,
            Email = validated.Email,
            CustomerGroupId = request.CustomerGroupId,
            TaxCode = validated.TaxCode,
            CreditLimit = request.CreditLimit,
            PaymentTermDays = request.PaymentTermDays,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "CustomerCreated",
            "Customer",
            customer.Id,
            newValues: new
            {
                customer.UserId,
                customer.FullName,
                customer.Phone,
                customer.CustomerGroupId,
                customer.CreditLimit,
                customer.PaymentTermDays
            },
            cancellationToken: cancellationToken);

        var groupCodes = (await _customerGroups.ListAsync(cancellationToken))
            .ToDictionary(group => group.Id, group => group.Code);

        return ToDto(customer, groupCodes);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> UpdateAsync(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetOrThrowAsync(id, cancellationToken);

        var validated = await ValidateAsync(
            request.FullName,
            request.Phone,
            request.Email,
            request.CustomerGroupId,
            request.TaxCode,
            request.CreditLimit,
            request.PaymentTermDays,
            cancellationToken);

        var oldValues = new
        {
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.CustomerGroupId,
            customer.TaxCode,
            customer.CreditLimit,
            customer.PaymentTermDays
        };

        customer.FullName = validated.FullName;
        customer.Phone = validated.Phone;
        customer.Email = validated.Email;
        customer.CustomerGroupId = request.CustomerGroupId;
        customer.TaxCode = validated.TaxCode;
        customer.CreditLimit = request.CreditLimit;
        customer.PaymentTermDays = request.PaymentTermDays;
        customer.UpdatedAt = _clock.UtcNow;
        customer.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "CustomerUpdated",
            "Customer",
            customer.Id,
            oldValues,
            new
            {
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.CustomerGroupId,
                customer.TaxCode,
                customer.CreditLimit,
                customer.PaymentTermDays
            },
            cancellationToken: cancellationToken);

        var groupCodes = (await _customerGroups.ListAsync(cancellationToken))
            .ToDictionary(group => group.Id, group => group.Code);

        return ToDto(customer, groupCodes);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> SetStatusAsync(
        long id,
        SetCustomerStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetOrThrowAsync(id, cancellationToken);

        if (customer.IsActive == request.IsActive)
        {
            var unchangedCodes = (await _customerGroups.ListAsync(cancellationToken))
                .ToDictionary(group => group.Id, group => group.Code);

            return ToDto(customer, unchangedCodes);
        }

        customer.IsActive = request.IsActive;
        customer.UpdatedAt = _clock.UtcNow;
        customer.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            request.IsActive ? "CustomerActivated" : "CustomerDeactivated",
            "Customer",
            customer.Id,
            oldValues: new { IsActive = !request.IsActive },
            newValues: new { customer.IsActive },
            cancellationToken: cancellationToken);

        var groupCodes = (await _customerGroups.ListAsync(cancellationToken))
            .ToDictionary(group => group.Id, group => group.Code);

        return ToDto(customer, groupCodes);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private static CustomerDto ToDto(Customer customer, IReadOnlyDictionary<int, string> groupCodes)
        => CustomerDto.FromEntity(
            customer,
            groupCodes.TryGetValue(customer.CustomerGroupId, out var code) ? code : null);

    private async Task<Customer> GetOrThrowAsync(long id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy khách hàng Id = {id}.");

    /// <summary>
    /// Kiểm tra dữ liệu khách hàng theo ràng buộc bảng: <c>FullName</c>/<c>Phone</c> bắt buộc,
    /// <c>CustomerGroupId</c> là khóa ngoại, <c>CreditLimit &gt;= 0</c> (<c>CK_Customers_Credit</c>),
    /// <c>PaymentTermDays &gt;= 0</c>.
    /// </summary>
    private async Task<ValidatedCustomer> ValidateAsync(
        string? fullName,
        string? phone,
        string? email,
        int customerGroupId,
        string? taxCode,
        decimal creditLimit,
        int paymentTermDays,
        CancellationToken cancellationToken)
    {
        var normalizedFullName = InputGuard.RequireText(fullName, FullNameMaxLength, "Tên khách hàng");
        var normalizedPhone = InputGuard.RequireText(phone, PhoneMaxLength, "Số điện thoại");
        var normalizedEmail = InputGuard.OptionalText(email, EmailMaxLength, "Email");
        var normalizedTaxCode = InputGuard.OptionalText(taxCode, TaxCodeMaxLength, "Mã số thuế");

        InputGuard.EnsureNotNegative(creditLimit, "Hạn mức công nợ");
        InputGuard.EnsureNotNegative(paymentTermDays, "Số ngày được nợ");
        InputGuard.EnsurePositive(customerGroupId, "customerGroupId");

        var groups = await _customerGroups.ListAsync(cancellationToken);

        if (groups.All(group => group.Id != customerGroupId))
        {
            throw new BadRequestException($"Nhóm khách Id = {customerGroupId} không tồn tại.");
        }

        return new ValidatedCustomer(normalizedFullName, normalizedPhone, normalizedEmail, normalizedTaxCode);
    }

    /// <summary>
    /// Tài khoản gắn với hồ sơ khách phải tồn tại và chưa thuộc hồ sơ khác
    /// (quan hệ 1-1 theo nghiệp vụ, dù SQL Server chỉ ràng buộc khóa ngoại).
    /// </summary>
    private async Task EnsureUserIsAvailableAsync(
        long userId,
        long? currentCustomerId,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
                   ?? throw new BadRequestException($"Tài khoản Id = {userId} không tồn tại.");

        if (user.UserType != 0)
        {
            throw new BadRequestException(
                $"Tài khoản Id = {userId} không phải tài khoản khách hàng (Users.UserType = Customer).");
        }

        var customers = await _readRepository.ListAsync(cancellationToken);

        if (customers.Any(customer => customer.Id != currentCustomerId && customer.UserId == userId))
        {
            throw new ConflictException($"Tài khoản Id = {userId} đã gắn với hồ sơ khách hàng khác.", "SQL-50005");
        }
    }

    /// <summary>Dữ liệu khách hàng đã kiểm tra và chuẩn hóa.</summary>
    private sealed record ValidatedCustomer(
        string FullName,
        string Phone,
        string? Email,
        string? TaxCode);
}
