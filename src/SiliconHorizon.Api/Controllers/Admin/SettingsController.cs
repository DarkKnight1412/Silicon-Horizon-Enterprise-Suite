using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// Cấu hình hệ thống — <c>dbo.SystemSettings</c> (khóa chính là cột <c>[Key]</c>)
/// theo Database/README.md mục 10: <c>/api/admin/settings</c> (thời hạn giữ hàng, hạn đổi trả...).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. Vì khóa là chính, <c>PUT /{key}</c> là <b>upsert</b> (khóa chưa có thì tạo mới).
/// Không có endpoint DELETE để tránh làm proc đọc cấu hình thiếu giá trị.
/// </remarks>
[ApiController]
[Route("api/v1/admin/settings")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
    private readonly ISystemSettingService _systemSettingService;

    public SettingsController(ISystemSettingService systemSettingService)
        => _systemSettingService = systemSettingService;

    /// <summary>GET /api/v1/admin/settings?keyword=&amp;pageNumber=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SystemSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SystemSettingDto>>> GetAll(
        [FromQuery] SystemSettingListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _systemSettingService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/settings/{key} — khóa dạng chuỗi, ví dụ <c>hold.gateway.minutes</c>.</summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(SystemSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemSettingDto>> GetByKey(
        string key,
        CancellationToken cancellationToken)
        => Ok(await _systemSettingService.GetByKeyAsync(key, cancellationToken));

    /// <summary>PUT /api/v1/admin/settings/{key} — đặt giá trị (tạo mới nếu khóa chưa tồn tại) và ghi nhật ký.</summary>
    [HttpPut("{key}")]
    [ProducesResponseType(typeof(SystemSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SystemSettingDto>> Upsert(
        string key,
        [FromBody] UpsertSystemSettingRequest request,
        CancellationToken cancellationToken)
        => Ok(await _systemSettingService.UpsertAsync(key, request, cancellationToken));
}
