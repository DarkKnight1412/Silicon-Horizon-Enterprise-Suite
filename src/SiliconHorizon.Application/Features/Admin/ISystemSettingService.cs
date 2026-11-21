using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case cấu hình hệ thống — <c>dbo.SystemSettings</c> (khóa chính là cột <c>[Key]</c>, kiểu chuỗi).
/// Database/README.md mục 10: <c>/api/admin/settings</c> (thời hạn giữ hàng, hạn đổi trả...).
/// </summary>
public interface ISystemSettingService
{
    /// <summary>GET /api/v1/admin/settings — danh sách cấu hình, có phân trang và lọc theo khóa/mô tả.</summary>
    Task<PagedResult<SystemSettingDto>> GetPagedAsync(
        SystemSettingListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/settings/{key} — chi tiết một khóa cấu hình; không tồn tại → 404.</summary>
    Task<SystemSettingDto> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// PUT /api/v1/admin/settings/{key} — đặt giá trị (upsert: khóa chưa có thì tạo mới).
    /// Mọi thay đổi đều ghi nhật ký vì đây là thao tác "đổi cấu hình".
    /// </summary>
    Task<SystemSettingDto> UpsertAsync(
        string key,
        UpsertSystemSettingRequest request,
        CancellationToken cancellationToken = default);
}
