using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Pricing;
using SiliconHorizon.Application.Features.Pricing.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD mã giảm giá — <c>dbo.Vouchers</c> (Database/README.md mục 10: <c>/api/admin/vouchers</c>).
/// Khách kiểm tra mã ở <c>POST /api/v1/vouchers/validate</c> (xem <c>Pricing/VouchersController</c>).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. "Xóa" = PATCH trạng thái (khóa mã) vì mã đã có thể phát sinh lượt dùng
/// (<c>VoucherUsages</c>, <c>Orders.VoucherId</c>). <c>usedCount</c> chỉ do proc đặt hàng tăng.
/// </remarks>
[ApiController]
[Route("api/v1/admin/vouchers")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class VouchersAdminController : ControllerBase
{
    private readonly IVoucherService _voucherService;

    public VouchersAdminController(IVoucherService voucherService) => _voucherService = voucherService;

    /// <summary>GET /api/v1/admin/vouchers?keyword=&amp;isActive=&amp;customerGroupId=&amp;pageNumber=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VoucherDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VoucherDto>>> GetAll(
        [FromQuery] VoucherListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _voucherService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/vouchers/{id}</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(VoucherDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VoucherDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await _voucherService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/vouchers — mã duy nhất (409 nếu trùng); ràng buộc giá/ngày sai trả 400.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VoucherDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VoucherDto>> Create(
        [FromBody] CreateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _voucherService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/vouchers/{id} — cập nhật voucher (không đổi mã, không sửa lượt dùng).</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(VoucherDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VoucherDto>> Update(
        long id,
        [FromBody] UpdateVoucherRequest request,
        CancellationToken cancellationToken)
        => Ok(await _voucherService.UpdateAsync(id, request, cancellationToken));

    /// <summary>PATCH /api/v1/admin/vouchers/{id}/status — khóa/mở mã giảm giá.</summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        long id,
        [FromBody] SetVoucherStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _voucherService.SetStatusAsync(id, request, cancellationToken);

        return NoContent();
    }
}
