using SiliconHorizon.Domain.Entities.Customers;

namespace SiliconHorizon.Application.Features.Customers.Dto;

/// <summary>
/// Khách hàng trả ra API — CRUD bằng EF Core trên <c>dbo.Customers</c>
/// (Database/README.md mục 10: <c>/api/customers</c>; khách tự quản lý địa chỉ ở <c>/api/me/addresses</c>).
/// </summary>
public class CustomerDto
{
    public long Id { get; init; }

    /// <summary><c>null</c> = khách vãng lai (chưa có tài khoản đăng nhập).</summary>
    public long? UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string? Email { get; init; }

    /// <summary>Nhóm khách quyết định bảng giá và voucher áp dụng được.</summary>
    public int CustomerGroupId { get; init; }

    public string? CustomerGroupCode { get; init; }

    public string? TaxCode { get; init; }

    /// <summary>Hạn mức công nợ cho khách sỉ (PAY-04).</summary>
    public decimal CreditLimit { get; init; }

    /// <summary>Số ngày được nợ — proc đặt hàng dùng để tính hạn thanh toán.</summary>
    public int PaymentTermDays { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static CustomerDto FromEntity(Customer customer, string? customerGroupCode) => new()
    {
        Id = customer.Id,
        UserId = customer.UserId,
        FullName = customer.FullName,
        Phone = customer.Phone,
        Email = customer.Email,
        CustomerGroupId = customer.CustomerGroupId,
        CustomerGroupCode = customerGroupCode,
        TaxCode = customer.TaxCode,
        CreditLimit = customer.CreditLimit,
        PaymentTermDays = customer.PaymentTermDays,
        IsActive = customer.IsActive,
        CreatedAt = customer.CreatedAt,
        UpdatedAt = customer.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách khách hàng — GET /api/v1/customers.</summary>
public class CustomerListQuery
{
    /// <summary>Tìm theo tên, số điện thoại hoặc email.</summary>
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public int? CustomerGroupId { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Yêu cầu tạo khách hàng — POST /api/v1/customers.</summary>
public class CreateCustomerRequest
{
    /// <summary>Gắn với tài khoản đăng nhập có sẵn (tùy chọn). Mỗi tài khoản chỉ gắn một hồ sơ khách.</summary>
    public long? UserId { get; set; }

    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Phải tồn tại trong <c>dbo.CustomerGroups</c>.</summary>
    public int CustomerGroupId { get; set; }

    public string? TaxCode { get; set; }

    /// <summary>Hạn mức công nợ, không âm.</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>Số ngày được nợ, không âm.</summary>
    public int PaymentTermDays { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật khách hàng — PUT /api/v1/customers/{id}.
/// Không đổi <c>userId</c> để tránh tách hồ sơ khách khỏi tài khoản đã đăng nhập.
/// </summary>
public class UpdateCustomerRequest
{
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public int CustomerGroupId { get; set; }

    public string? TaxCode { get; set; }

    public decimal CreditLimit { get; set; }

    public int PaymentTermDays { get; set; }
}

/// <summary>
/// Đổi trạng thái khách hàng — PATCH /api/v1/customers/{id}/status.
/// Ngừng giao dịch = <c>isActive = false</c>; <b>không xóa cứng</b> vì khách đã có đơn hàng/công nợ.
/// </summary>
public record SetCustomerStatusRequest(bool IsActive);

/// <summary>Địa chỉ giao hàng trả ra API — <c>dbo.CustomerAddresses</c> (<c>/api/me/addresses</c>).</summary>
public class CustomerAddressDto
{
    public long Id { get; init; }

    public long CustomerId { get; init; }

    public string RecipientName { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string District { get; init; } = string.Empty;

    public string Ward { get; init; } = string.Empty;

    public string Street { get; init; } = string.Empty;

    /// <summary>Địa chỉ mặc định — mỗi khách chỉ có một địa chỉ mặc định.</summary>
    public bool IsDefault { get; init; }

    public static CustomerAddressDto FromEntity(CustomerAddress address) => new()
    {
        Id = address.Id,
        CustomerId = address.CustomerId,
        RecipientName = address.RecipientName,
        Phone = address.Phone,
        Province = address.Province,
        District = address.District,
        Ward = address.Ward,
        Street = address.Street,
        IsDefault = address.IsDefault
    };
}

/// <summary>Yêu cầu thêm/sửa địa chỉ — POST/PUT <c>/api/v1/me/addresses</c>.</summary>
public class UpsertCustomerAddressRequest
{
    public string? RecipientName { get; set; }

    public string? Phone { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? Ward { get; set; }

    public string? Street { get; set; }

    /// <summary>Đặt <c>true</c> sẽ bỏ cờ mặc định của các địa chỉ khác cùng khách.</summary>
    public bool IsDefault { get; set; }
}
