namespace SiliconHorizon.Domain.Enums;

/// <summary>ReturnRequests.Status — vòng đời yêu cầu đổi trả (RET-01..04).</summary>
public enum ReturnRequestStatus : byte
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    GoodsReceived = 3,
    Refunded = 4,
    Closed = 5
}
