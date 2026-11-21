using SiliconHorizon.Application.Features.Customers.Dto;

namespace SiliconHorizon.Application.Features.Customers;

/// <summary>
/// Use case sổ địa chỉ của khách — <c>/api/v1/me/addresses</c>
/// (Database/README.md mục 10: khách tự quản lý địa chỉ; bảng <c>dbo.CustomerAddresses</c>).
/// </summary>
/// <remarks>
/// Mọi phương thức đều ép <c>customerId</c> từ token (BRD 2.2, mục 1.1 của Database/README.md):
/// khách chỉ thấy/sửa được địa chỉ của chính mình; địa chỉ của khách khác trả 404 để không lộ sự tồn tại.
/// </remarks>
public interface ICustomerAddressService
{
    /// <summary>GET /api/v1/me/addresses — danh sách địa chỉ của khách đang đăng nhập (mặc định lên đầu).</summary>
    Task<IReadOnlyList<CustomerAddressDto>> GetMyAddressesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/me/addresses/{id} — chi tiết một địa chỉ của chính khách; không thuộc mình → 404.</summary>
    Task<CustomerAddressDto> GetMyAddressAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/me/addresses — thêm địa chỉ giao hàng.
    /// <c>isDefault = true</c> sẽ bỏ cờ mặc định của các địa chỉ khác (mỗi khách chỉ một địa chỉ mặc định).
    /// </summary>
    Task<CustomerAddressDto> CreateAsync(
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/me/addresses/{id} — sửa địa chỉ của chính khách.</summary>
    Task<CustomerAddressDto> UpdateAsync(
        long id,
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE /api/v1/me/addresses/{id} — xóa địa chỉ của chính khách.
    /// Xóa cứng an toàn vì <c>Orders</c> lưu <b>snapshot</b> người nhận/địa chỉ giao
    /// (<c>RecipientName</c>, <c>RecipientPhone</c>, <c>ShippingAddress</c>) chứ không tham chiếu
    /// <c>CustomerAddresses.Id</c>, nên lịch sử đơn hàng không bị ảnh hưởng.
    /// </summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
