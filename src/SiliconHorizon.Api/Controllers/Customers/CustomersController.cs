using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Customers;
using SiliconHorizon.Application.Features.Customers.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Customers;

/// <summary>
/// CRUD khách hàng dành cho quản trị — <c>dbo.Customers</c>
/// (Database/README.md mục 10: <c>/api/customers</c>, quyền <b>AD</b>).
/// Khách tự quản lý địa chỉ của mình ở <c>/api/v1/me/addresses</c>.
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. "Xóa" = PATCH trạng thái (<c>isActive = false</c>) — không xóa cứng vì khách
/// đã gắn với đơn hàng, công nợ và địa chỉ.
/// </remarks>
[ApiController]
[Route("api/v1/customers")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService) => _customerService = customerService;

    /// <summary>GET /api/v1/customers?keyword=&amp;isActive=&amp;customerGroupId=...</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> GetAll(
        [FromQuery] CustomerListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _customerService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/customers/{id}</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await _customerService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/customers — tạo hồ sơ khách (có thể gắn tài khoản đăng nhập đã có).
    /// Nhóm khách/tài khoản không tồn tại trả 400; tài khoản đã gắn hồ sơ khác trả 409.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _customerService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/customers/{id} — cập nhật hồ sơ (nhóm khách, hạn mức nợ, số ngày được nợ...).</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> Update(
        long id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
        => Ok(await _customerService.UpdateAsync(id, request, cancellationToken));

    /// <summary>PATCH /api/v1/customers/{id}/status — ngừng/cho giao dịch lại với khách.</summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> SetStatus(
        long id,
        [FromBody] SetCustomerStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await _customerService.SetStatusAsync(id, request, cancellationToken));
}
