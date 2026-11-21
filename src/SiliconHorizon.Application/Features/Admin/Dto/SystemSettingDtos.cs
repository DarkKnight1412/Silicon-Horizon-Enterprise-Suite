using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Cấu hình hệ thống trả ra API — <c>dbo.SystemSettings</c> (khóa chính là cột <c>[Key]</c>).
/// Database/README.md mục 10: <c>/api/admin/settings</c> (thời hạn giữ hàng, hạn đổi trả...).
/// </summary>
public class SystemSettingDto
{
    /// <summary>Khóa cấu hình, ví dụ <c>hold.gateway.minutes</c>, <c>hold.transfer.hours</c>.</summary>
    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    /// <summary>Giải thích cho người quản trị.</summary>
    public string? Description { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static SystemSettingDto FromEntity(SystemSetting setting) => new()
    {
        Key = setting.Key,
        Value = setting.Value,
        Description = setting.Description,
        UpdatedAt = setting.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách cấu hình — GET /api/v1/admin/settings.</summary>
public class SystemSettingListQuery
{
    /// <summary>Tìm theo khóa hoặc mô tả.</summary>
    public string? Keyword { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Yêu cầu đặt giá trị cấu hình — PUT /api/v1/admin/settings/{key}.
/// Là <b>upsert</b>: khóa chưa có sẽ được tạo mới (tầng Application tự sinh <c>updatedAt</c>/<c>updatedBy</c>).
/// </summary>
public class UpsertSystemSettingRequest
{
    /// <summary>Giá trị (nvarchar(500)); <c>Key</c> lấy từ route nên không truyền trong body.</summary>
    public string? Value { get; set; }

    /// <summary>Mô tả tùy chọn; bỏ trống khi sửa sẽ giữ nguyên mô tả cũ.</summary>
    public string? Description { get; set; }
}
