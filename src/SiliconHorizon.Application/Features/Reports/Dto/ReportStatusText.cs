namespace SiliconHorizon.Application.Features.Reports.Dto;

/// <summary>
/// Nhãn tiếng Việt cho các con số trong báo cáo (Database/README.md mục 9, BRD mục 11).
/// </summary>
/// <remarks>
/// Module Báo cáo chỉ ĐỌC dữ liệu của các bảng khác nên giữ bản nhãn riêng thay vì phụ thuộc
/// <c>InventoryStatusText</c> (module Kho) hay <c>WarrantyStatusText</c> (module Bảo hành):
/// giá trị số vẫn khớp đúng CHECK constraint trong Database/table_and_data.sql (mục 8 và 9).
/// </remarks>
public static class ReportStatusText
{
    /// <summary>Tên trạng thái serial (SerialItems.Status: 0 InStock … 9 Scrapped).</summary>
    public static string SerialStatusText(byte status) => status switch
    {
        0 => "Trong kho",
        1 => "Đã giữ cho đơn",
        2 => "Đang chuyển kho",
        3 => "Đã bán",
        4 => "Đang bảo hành",
        5 => "Khách đã trả",
        6 => "Đã đổi mới",
        7 => "Hàng lỗi",
        8 => "Đã trả nhà cung cấp",
        9 => "Đã hủy",
        _ => "Không xác định"
    };

    /// <summary>Tên trạng thái phiếu bảo hành (WarrantyTickets.Status: 0 Received … 6 ReturnedToCustomer).</summary>
    public static string WarrantyTicketStatusText(byte status) => status switch
    {
        0 => "Đã tiếp nhận",
        1 => "Đang chẩn đoán",
        2 => "Đã gửi hãng",
        3 => "Đã sửa xong",
        4 => "Đã đổi mới",
        5 => "Từ chối bảo hành",
        6 => "Đã trả khách",
        _ => "Không xác định"
    };
}
