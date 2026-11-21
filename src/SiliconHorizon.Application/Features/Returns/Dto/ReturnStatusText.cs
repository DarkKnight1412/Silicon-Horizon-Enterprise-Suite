using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Returns.Dto;

/// <summary>
/// Hằng số trạng thái + tên hiển thị tiếng Việt dùng chung cho module Đổi trả
/// (Database/README.md mục 7.2, RET-01..05).
/// </summary>
/// <remarks>
/// <c>ReturnRequests.Status</c> đã có enum ở tầng Domain (<see cref="ReturnRequestStatus"/>);
/// <c>ReturnItems.InspectionResult</c> chưa có enum nên hằng số khai báo tại đây, khớp CHECK constraint
/// <c>CK_ReturnItems_Inspection</c> trong Database/table_and_data.sql mục 9.
/// </remarks>
public static class ReturnStatusText
{
    // ── ReturnItems.InspectionResult ─────────────────────────────────────────
    /// <summary>Chưa kiểm tra (mặc định khi khách gửi yêu cầu).</summary>
    public const byte InspectionPending = 0;

    /// <summary>Đạt — serial về InStock và được cộng lại tồn bán được.</summary>
    public const byte InspectionPass = 1;

    /// <summary>Không đạt — serial về Defective, KHÔNG vào tồn bán được.</summary>
    public const byte InspectionFail = 2;

    /// <summary>Tên trạng thái yêu cầu đổi trả (ReturnRequests.Status).</summary>
    public static string StatusText(byte status) => (ReturnRequestStatus)status switch
    {
        ReturnRequestStatus.Requested => "Chờ duyệt",
        ReturnRequestStatus.Approved => "Đã duyệt",
        ReturnRequestStatus.Rejected => "Đã từ chối",
        ReturnRequestStatus.GoodsReceived => "Đã nhận hàng & kiểm tra",
        ReturnRequestStatus.Refunded => "Đã hoàn tiền",
        ReturnRequestStatus.Closed => "Đã đóng",
        _ => "Không xác định"
    };

    /// <summary>Tên kết quả kiểm tra hàng trả (ReturnItems.InspectionResult).</summary>
    public static string InspectionResultText(byte result) => result switch
    {
        InspectionPending => "Chưa kiểm tra",
        InspectionPass => "Đạt",
        InspectionFail => "Không đạt",
        _ => "Không xác định"
    };

    /// <summary>
    /// Trạng thái đơn sau khi hoàn tiền đổi trả — <c>usp_Return_Refund</c> luôn trả 9 hoặc 10.
    /// </summary>
    public static string OrderStatusText(byte status) => (OrderStatus)status switch
    {
        OrderStatus.PartiallyReturned => "Trả một phần",
        OrderStatus.Returned => "Đã trả hết",
        _ => "Không xác định"
    };
}