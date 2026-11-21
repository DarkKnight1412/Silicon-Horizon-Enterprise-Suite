namespace SiliconHorizon.Domain.Common;

/// <summary>
/// Cột audit dùng chung cho hầu hết bảng nghiệp vụ
/// (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy — xem Database/table_and_data.sql).
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }

    long? CreatedBy { get; set; }

    DateTime? UpdatedAt { get; set; }

    long? UpdatedBy { get; set; }
}

/// <summary>
/// Bảng có cột <c>rowversion</c> để kiểm soát đồng thời khi UPDATE (ví dụ dbo.Orders, dbo.StockLevels).
/// </summary>
public interface IHasRowVersion
{
    byte[] RowVersion { get; set; }
}
