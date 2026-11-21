using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Quy tắc tương thích trả ra API — CRUD bằng EF Core trên <c>dbo.CompatibilityRules</c>
/// (Database/README.md mục 10: <c>/api/admin/compatibility-rules</c>; nghiệp vụ PRD-11).
/// </summary>
/// <remarks>
/// ⚠️ Lưu ý hiện trạng đã ghi ở mục 12 của Database/README.md: <c>usp_Compat_Check</c> hiện <b>viết cứng</b>
/// 4 quy tắc (socket, chuẩn RAM, số khe RAM, công suất nguồn) và <b>chưa đọc</b> bảng này. Vì vậy dữ liệu
/// quản trị ở đây là "nguồn sự thật" để proc chuyển sang dùng sau, chưa ảnh hưởng kết quả
/// <c>POST /api/v1/compatibility/check</c>.
/// </remarks>
public class CompatibilityRuleDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Danh mục chứa thông số bên trái (thường là CPU/RAM).</summary>
    public int LeftCategoryId { get; init; }

    /// <summary>Danh mục chứa thông số bên phải (thường là Mainboard).</summary>
    public int RightCategoryId { get; init; }

    /// <summary>Tên trường thông số bên trái, ví dụ <c>socket</c>, <c>memoryType</c>, <c>tdpWatt</c>.</summary>
    public string LeftField { get; init; } = string.Empty;

    public string RightField { get; init; } = string.Empty;

    /// <summary>EQ, LTE, GTE (<c>CK_CompatibilityRules_Operator</c>).</summary>
    public string Operator { get; init; } = string.Empty;

    /// <summary>0 Info, 1 Warning.</summary>
    public CompatibilitySeverity Severity { get; init; }

    public bool IsActive { get; init; }

    public static CompatibilityRuleDto FromEntity(CompatibilityRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        LeftCategoryId = rule.LeftCategoryId,
        RightCategoryId = rule.RightCategoryId,
        LeftField = rule.LeftField,
        RightField = rule.RightField,
        Operator = rule.Operator,
        Severity = rule.Severity,
        IsActive = rule.IsActive
    };
}

/// <summary>Tham số lọc danh sách quy tắc tương thích — GET /api/v1/admin/compatibility-rules.</summary>
public class CompatibilityRuleListQuery
{
    public string? Keyword { get; set; }

    /// <summary>Lọc quy tắc có liên quan tới danh mục này (bên trái hoặc bên phải).</summary>
    public int? CategoryId { get; set; }

    public bool? IsActive { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Yêu cầu tạo quy tắc tương thích — POST /api/v1/admin/compatibility-rules.
/// <c>operator</c> chỉ nhận EQ/LTE/GTE, <c>severity</c> chỉ nhận 0 (Info) hoặc 1 (Warning).
/// </summary>
public class CreateCompatibilityRuleRequest
{
    public string? Name { get; set; }

    public int LeftCategoryId { get; set; }

    public int RightCategoryId { get; set; }

    public string? LeftField { get; set; }

    public string? RightField { get; set; }

    public string? Operator { get; set; }

    /// <summary>0 Info (chỉ thông tin), 1 Warning (cảnh báo).</summary>
    public byte Severity { get; set; } = 1;
}

/// <summary>Yêu cầu cập nhật quy tắc tương thích — PUT /api/v1/admin/compatibility-rules/{id}.</summary>
public class UpdateCompatibilityRuleRequest
{
    public string? Name { get; set; }

    public int LeftCategoryId { get; set; }

    public int RightCategoryId { get; set; }

    public string? LeftField { get; set; }

    public string? RightField { get; set; }

    public string? Operator { get; set; }

    public byte Severity { get; set; } = 1;
}

/// <summary>
/// Đổi trạng thái quy tắc — PATCH /api/v1/admin/compatibility-rules/{id}/status.
/// Tắt quy tắc (<c>isActive = false</c>) thay vì xóa cứng để giữ lịch sử cấu hình đã dùng để kiểm tra.
/// </summary>
public record SetCompatibilityRuleStatusRequest(bool IsActive);
