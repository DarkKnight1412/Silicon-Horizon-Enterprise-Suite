namespace SiliconHorizon.Application.Common.Audit;

/// <summary>
/// Ghi nhật ký hệ thống cho các thao tác CRUD nhạy cảm (AUD-01..04) bằng cách gọi trực tiếp
/// <c>dbo.usp_Audit_Write</c> (Database/02_sp_foundation_catalog_cart.sql) — đúng ghi chú cuối mục 10
/// của Database/README.md: <i>"Vẫn ghi Audit khi sửa dữ liệu nhạy cảm (gọi <c>usp_Audit_Write</c>)"</i>.
/// <para>
/// Dùng cho các bảng cấu hình đi qua EF Core (không có stored procedure riêng để ghi nhật ký):
/// đổi giá (PriceTiers), đổi cấu hình (SystemSettings), khuyến mãi/voucher, quy tắc hoa hồng/tương thích,
/// chi nhánh/ca làm, nhân viên và phân quyền.
/// </para>
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// Ghi một dòng nhật ký. <paramref name="userId"/>/<paramref name="ipAddress"/>/<paramref name="userAgent"/>
    /// lấy từ token (không nhận từ client); <c>OldValues</c>/<c>NewValues</c> được serialize JSON camelCase.
    /// </summary>
    /// <param name="action">Hành động, ví dụ <c>PriceChanged</c>, <c>SettingChanged</c>, <c>EmployeeCreated</c> (tối đa 60 ký tự).</param>
    /// <param name="entityType">Loại đối tượng, ví dụ <c>PriceTier</c>, <c>SystemSetting</c>, <c>Employee</c> (tối đa 50 ký tự).</param>
    /// <param name="entityId">Khóa của đối tượng bị tác động (bigint).</param>
    /// <param name="oldValues">Giá trị trước khi sửa (null khi tạo mới).</param>
    /// <param name="newValues">Giá trị sau khi sửa.</param>
    /// <param name="reason">Lý do (tùy chọn, tối đa 300 ký tự).</param>
    Task WriteAsync(
        string action,
        string entityType,
        long entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
