namespace SiliconHorizon.Domain.Enums;

/// <summary>Orders.Channel — kênh bán (ORD-03).</summary>
public enum OrderChannel : byte
{
    Online = 0,
    Pos = 1
}

/// <summary>Orders.FulfillmentType — giao hàng hay nhận tại cửa hàng.</summary>
public enum FulfillmentType : byte
{
    Delivery = 0,
    Pickup = 1
}

/// <summary>
/// Orders.Status — vòng đời đơn hàng (ORD-07, sơ đồ trạng thái trong ERD).
/// Chỉ chuyển theo đúng luật: <c>usp_Internal_SetOrderStatus</c>.
/// </summary>
public enum OrderStatus : byte
{
    PendingPayment = 0,
    Pending = 1,
    Confirmed = 2,
    Packing = 3,
    ReadyForPickup = 4,
    Shipping = 5,
    DeliveryFailed = 6,
    Completed = 7,
    Cancelled = 8,
    PartiallyReturned = 9,
    Returned = 10
}

/// <summary>Orders.PaymentMethod — phương thức thanh toán (PAY-01).</summary>
public enum PaymentMethod : byte
{
    Cod = 0,
    BankTransfer = 1,
    Gateway = 2,
    Cash = 3,
    Credit = 4
}

/// <summary>Orders.PaymentStatus.</summary>
public enum PaymentStatus : byte
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Refunded = 3
}

/// <summary>Payments.PaymentType — giao dịch thu hay hoàn tiền.</summary>
public enum PaymentType : byte
{
    Payment = 0,
    Refund = 1
}

/// <summary>CustomerLedger.EntryType — bút toán công nợ khách (số dương: tăng nợ).</summary>
public enum LedgerEntryType : byte
{
    Charge = 0,
    Payment = 1,
    Adjustment = 2
}
