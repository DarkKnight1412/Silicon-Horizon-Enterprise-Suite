using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Customers.Dto;
using SiliconHorizon.Domain.Entities.Customers;

namespace SiliconHorizon.Application.Features.Customers;

/// <summary>
/// Hiện thực use case sổ địa chỉ của khách (EF Core trên <c>dbo.CustomerAddresses</c>).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Phạm vi dữ liệu</b>: <c>customerId</c> luôn lấy từ token; địa chỉ không thuộc khách đang gọi
/// bị coi như không tồn tại (404) — cùng cách làm với <c>ReturnAccessGuard</c>/<c>OrderAccessGuard</c>.</item>
/// <item><b>Một địa chỉ mặc định</b>: đặt <c>isDefault = true</c> sẽ tự bỏ cờ mặc định của các địa chỉ khác.</item>
/// <item><b>Xóa cứng được phép</b>: bảng không có cột trạng thái và <c>Orders</c> lưu snapshot địa chỉ giao,
/// nên xóa địa chỉ không phá lịch sử đơn hàng.</item>
/// </list>
/// </remarks>
public sealed class CustomerAddressService : ICustomerAddressService
{
    /// <summary>CustomerAddresses.RecipientName nvarchar(150).</summary>
    private const int RecipientNameMaxLength = 150;

    /// <summary>CustomerAddresses.Phone varchar(20).</summary>
    private const int PhoneMaxLength = 20;

    /// <summary>CustomerAddresses.Province/District/Ward nvarchar(100).</summary>
    private const int AreaNameMaxLength = 100;

    /// <summary>CustomerAddresses.Street nvarchar(250).</summary>
    private const int StreetMaxLength = 250;

    private readonly IReadRepository<CustomerAddress> _readRepository;
    private readonly IWriteRepository<CustomerAddress> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public CustomerAddressService(
        IReadRepository<CustomerAddress> readRepository,
        IWriteRepository<CustomerAddress> writeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerAddressDto>> GetMyAddressesAsync(
        CancellationToken cancellationToken = default)
    {
        var customerId = RequireCustomerId();

        var addresses = await _readRepository.ListAsync(cancellationToken);

        return addresses
            .Where(address => address.CustomerId == customerId)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.Id)
            .Select(CustomerAddressDto.FromEntity)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CustomerAddressDto> GetMyAddressAsync(long id, CancellationToken cancellationToken = default)
        => CustomerAddressDto.FromEntity(await GetOwnedOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<CustomerAddressDto> CreateAsync(
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = RequireCustomerId();
        var validated = Validate(request);

        if (request.IsDefault)
        {
            await ClearDefaultAsync(customerId, null, cancellationToken);
        }

        var address = new CustomerAddress
        {
            CustomerId = customerId,
            RecipientName = validated.RecipientName,
            Phone = validated.Phone,
            Province = validated.Province,
            District = validated.District,
            Ward = validated.Ward,
            Street = validated.Street,
            IsDefault = request.IsDefault
        };

        await _writeRepository.AddAsync(address, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CustomerAddressDto.FromEntity(address);
    }

    /// <inheritdoc />
    public async Task<CustomerAddressDto> UpdateAsync(
        long id,
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var address = await GetOwnedOrThrowAsync(id, cancellationToken);
        var validated = Validate(request);

        if (request.IsDefault)
        {
            await ClearDefaultAsync(address.CustomerId, address.Id, cancellationToken);
        }
        else if (address.IsDefault)
        {
            throw new BadRequestException(
                "Không thể bỏ cờ mặc định của địa chỉ duy nhất đang là mặc định. Hãy đặt địa chỉ khác làm mặc định trước.");
        }

        address.RecipientName = validated.RecipientName;
        address.Phone = validated.Phone;
        address.Province = validated.Province;
        address.District = validated.District;
        address.Ward = validated.Ward;
        address.Street = validated.Street;
        address.IsDefault = request.IsDefault;

        _writeRepository.Update(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CustomerAddressDto.FromEntity(address);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var address = await GetOwnedOrThrowAsync(id, cancellationToken);

        _writeRepository.Remove(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ép <c>customerId</c> từ token. Tài khoản không gắn hồ sơ khách (ví dụ nhân viên) không dùng được
    /// sổ địa chỉ → 403.
    /// </summary>
    private long RequireCustomerId()
        => _currentUser.CustomerId
           ?? throw new ForbiddenException("Tài khoản hiện tại không gắn hồ sơ khách hàng nên không có sổ địa chỉ.");

    /// <summary>Địa chỉ phải thuộc khách đang đăng nhập; nếu không, coi như không tồn tại (404).</summary>
    private async Task<CustomerAddress> GetOwnedOrThrowAsync(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new BadRequestException("Id địa chỉ phải là số nguyên dương.");
        }

        var customerId = RequireCustomerId();
        var address = await _readRepository.GetByIdAsync(id, cancellationToken);

        if (address is null || address.CustomerId != customerId)
        {
            throw new NotFoundException($"Không tìm thấy địa chỉ Id = {id}.");
        }

        return address;
    }

    /// <summary>Bỏ cờ mặc định của các địa chỉ khác cùng khách (mỗi khách chỉ một địa chỉ mặc định).</summary>
    private async Task ClearDefaultAsync(long customerId, long? keepId, CancellationToken cancellationToken)
    {
        var addresses = await _readRepository.ListAsync(cancellationToken);

        foreach (var other in addresses.Where(address =>
                     address.CustomerId == customerId && address.IsDefault && address.Id != keepId))
        {
            other.IsDefault = false;
            _writeRepository.Update(other);
        }
    }

    /// <summary>Kiểm tra dữ liệu địa chỉ theo ràng buộc bảng (các trường địa chỉ đều NOT NULL).</summary>
    private static ValidatedAddress Validate(UpsertCustomerAddressRequest request)
        => new(
            InputGuard.RequireText(request.RecipientName, RecipientNameMaxLength, "Tên người nhận"),
            InputGuard.RequireText(request.Phone, PhoneMaxLength, "Số điện thoại người nhận"),
            InputGuard.RequireText(request.Province, AreaNameMaxLength, "Tỉnh/Thành phố"),
            InputGuard.RequireText(request.District, AreaNameMaxLength, "Quận/Huyện"),
            InputGuard.RequireText(request.Ward, AreaNameMaxLength, "Phường/Xã"),
            InputGuard.RequireText(request.Street, StreetMaxLength, "Địa chỉ đường"));

    /// <summary>Dữ liệu địa chỉ đã kiểm tra và chuẩn hóa.</summary>
    private sealed record ValidatedAddress(
        string RecipientName,
        string Phone,
        string Province,
        string District,
        string Ward,
        string Street);
}
