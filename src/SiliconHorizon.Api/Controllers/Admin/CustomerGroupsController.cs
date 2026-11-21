using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Pricing;
using SiliconHorizon.Application.Features.Pricing.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD nhóm khách hàng — <c>dbo.CustomerGroups</c> (Database/README.md mục 10: <c>/api/admin/customer-groups</c>).
/// Nhóm khách quyết định bảng giá (<c>ProductPrices</c>/<c>PriceTiers</c>) và voucher được phép dùng.
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. Bảng không có cột trạng thái nên không có endpoint DELETE
/// (nhóm khách đang được khách hàng/bảng giá/voucher tham chiếu).
/// </remarks>
[ApiController]
[Route("api/v1/admin/customer-groups")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class CustomerGroupsController : ControllerBase
{
    private readonly ICustomerGroupService _customerGroupService;

    public CustomerGroupsController(ICustomerGroupService customerGroupService)
        => _customerGroupService = customerGroupService;

    /// <summary>GET /api/v1/admin/customer-groups?keyword=&amp;isDefault=&amp;pageNumber=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerGroupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerGroupDto>>> GetAll(
        [FromQuery] CustomerGroupListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _customerGroupService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/customer-groups/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerGroupDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _customerGroupService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/customer-groups — mã nhóm duy nhất (409 nếu trùng).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerGroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerGroupDto>> Create(
        [FromBody] CreateCustomerGroupRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _customerGroupService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/customer-groups/{id} — đổi tên và cờ mặc định (không đổi mã).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CustomerGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerGroupDto>> Update(
        int id,
        [FromBody] UpdateCustomerGroupRequest request,
        CancellationToken cancellationToken)
        => Ok(await _customerGroupService.UpdateAsync(id, request, cancellationToken));
}
