namespace SiliconHorizon.Application.Features.Warranty.Dto;

/// <summary>
/// Trạng thái phiếu bảo hành (<c>WarrantyTickets.Status</c>) — khớp đúng giá trị và CHECK constraint
/// <c>CK_WarrantyTickets_Status</c> trong Database/table_and_data.sql mục 9.
/// </summary>
/// <remarks>
/// Bảng <c>WarrantyTickets</c> chưa có enum ở tầng <c>SiliconHorizon.Domain.Enums</c> (khác với
/// <c>ReturnRequestStatus</c> của đổi trả) nên hằng số được khai báo tại đây, cùng quy ước với
/// <c>InventoryStatusText</c> của module Kho.
/// </remarks>
public static class WarrantyStatusText
{
    /// <summary>Mới tiếp nhận tại quầy/chi nhánh (WAR-04).</summary>
    public const byte Received = 0;

    /// <summary>Kỹ thuật viên đang chẩn đoán.</summary>
    public const byte Diagnosing = 1;

    /// <summary>Đã gửi hãng/nhà cung cấp để xử lý (WAR-07).</summary>
    public const byte SentToVendor = 2;

    /// <summary>Đã sửa xong, chờ trả khách.</summary>
    public const byte Repaired = 3;

    /// <summary>Đổi mới (bắt buộc có serial thay thế).</summary>
    public const byte Replaced = 4;

    /// <summary>Từ chối bảo hành (bắt buộc có lý do) — cũng là trạng thái khi phiếu hết hạn bảo hành.</summary>
    public const byte Rejected = 5;

    /// <summary>Đã trả khách (kết thúc phiếu).</summary>
    public const byte ReturnedToCustomer = 6;

    /// <summary>Tên trạng thái hiển thị tiếng Việt.</summary>
    public static string StatusText(byte status) => status switch
    {
        Received => "Đã tiếp nhận",
        Diagnosing => "Đang chẩn đoán",
        SentToVendor => "Đã gửi hãng",
        Repaired => "Đã sửa xong",
        Replaced => "Đã đổi mới",
        Rejected => "Từ chối bảo hành",
        ReturnedToCustomer => "Đã trả khách",
        _ => "Không xác định"
    };

    /// <summary>
    /// Trạng thái đích hợp lệ của <c>POST /api/warranty-tickets/{id}/status</c> — theo sơ đồ chuyển
    /// trạng thái trong Database/05_sp_warranty_returns_commission.sql (0→1,5 | 1→2,3,4,5 | 2→3,4,5 | 3,4,5→6).
    /// API chặn sớm giá trị ngoài danh sách này; cặp chuyển đổi thực tế vẫn do proc quyết định (50002 → 409).
    /// </summary>
    public static bool IsAllowedTarget(byte toStatus)
        => toStatus is Diagnosing or SentToVendor or Repaired or Replaced or Rejected or ReturnedToCustomer;
}