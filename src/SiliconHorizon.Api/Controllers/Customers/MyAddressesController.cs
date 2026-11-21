using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Customers;
using SiliconHorizon.Application.Features.Customers.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Customers;

/// <summary>
/// Sổ địa chỉ giao hàng của khách — <c>dbo.CustomerAddresses</c>, khách tự quản lý
/// (Database/README.md mục 10: <c>/api/me/addresses</c>, quyền <b>KH</b>).
/// </summary>
/// <remarks>
/// <c>customerId</c> luôn ép từ token (<c>ICurrentUser.CustomerId</c>): khách chỉ thấy/sửa địa chỉ của mình,
/// địa chỉ của khách khác trả 404 để không lộ sự tồn tại của dữ liệu (BRD 2.2).
/// </remarks>
[ApiController]
[Route("api/v1/me/addresses")]
[Authorize(Roles = RoleCodes.Customer)]
[Produces("application/json")]
public class MyAddressesController : ControllerBase
{
    private readonly ICustomerAddressService _customerAddressService;

    public MyAddressesController(ICustomerAddressService customerAddressService)
        => _customerAddressService = customerAddressService;

    /// <summary>GET /api/v1/me/addresses — địa chỉ mặc định lên đầu.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressDto>>> GetMine(CancellationToken cancellationToken)
        => Ok(await _customerAddressService.GetMyAddressesAsync(cancellationToken));

    /// <summary>GET /api/v1/me/addresses/{id}</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerAddressDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await _customerAddressService.GetMyAddressAsync(id, cancellationToken));

    /// <summary>POST /api/v1/me/addresses — đặt <c>isDefault = true</c> sẽ bỏ cờ mặc định của địa chỉ khác.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerAddressDto>> Create(
        [FromBody] UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _customerAddressService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/me/addresses/{id} — sửa địa chỉ của chính khách.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerAddressDto>> Update(
        long id,
        [FromBody] UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken)
        => Ok(await _customerAddressService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// DELETE /api/v1/me/addresses/{id} — xóa địa chỉ của chính khách.
    /// An toàn với lịch sử đơn hàng vì <c>Orders</c> lưu snapshot người nhận/địa chỉ giao.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _customerAddressService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
