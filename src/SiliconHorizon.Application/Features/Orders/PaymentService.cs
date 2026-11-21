using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Orders.Dto;
using SiliconHorizon.Application.Security;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Orders;

/// <summary>
/// Hiện thực use case thanh toán/công nợ qua stored procedure (Database/03_sp_orders_payments.sql và
/// <c>usp_Customer_GetLedger</c> ở Database/06_sp_hr_audit_reports.sql).
/// </summary>
/// <remarks>
/// Không cập nhật bảng nào trực tiếp: mọi bút toán (Payments, CustomerLedger) do proc ghi trong giao dịch của nó.
/// Ghi nhận thanh toán và hoàn tiền phải qua kiểm tra phạm vi chi nhánh — trừ callback của cổng thanh toán,
/// vì đó là luồng hệ thống chứ không phải thao tác của nhân viên.
/// </remarks>
public sealed class PaymentService : IPaymentService
{
    /// <summary>Payments.Provider varchar(30).</summary>
    private const int ProviderMaxLength = 30;

    /// <summary>Payments.ProviderTransactionId varchar(100).</summary>
    private const int ProviderTransactionIdMaxLength = 100;

    /// <summary>Tham số @Reason của hoàn tiền nvarchar(300).</summary>
    private const int ReasonMaxLength = 300;

    /// <summary>usp_Customer_GetLedger tự cắt @PageSize về tối đa 200.</summary>
    private const int LedgerPageSizeMax = 200;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;
    private readonly OrderAccessGuard _guard;

    public PaymentService(ISqlExecutor sqlExecutor, ICurrentUser currentUser, OrderAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
        _guard = guard;
    }

    public async Task<RecordedPaymentDto> RecordAsync(
        long orderId,
        RecordPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Thu ngân chỉ thu tiền cho đơn thuộc chi nhánh mình (Admin bỏ qua).
        await _guard.ForStaffAsync(orderId, cancellationToken);

        ValidateMethod(request.Method);
        ValidateAmount(request.Amount);

        // Thu ngân xác nhận: KHÔNG truyền Provider/ProviderTransactionId (đó là luồng callback của cổng — PAY-02).
        // Proc tự cộng PaidAmount, cập nhật PaymentStatus và chuyển đơn PendingPayment → Pending khi đã thu đủ.
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<PaymentRecordRow>(
            StoredProcedureNames.PaymentRecord,
            new
            {
                OrderId = orderId,
                request.Method,
                request.Amount,
                Provider = (string?)null,
                ProviderTransactionId = (string?)null,
                request.PaidAt,
                UserId = _currentUser.UserId
            },
            cancellationToken)
            ?? throw new ConflictException("Không ghi nhận được thanh toán cho đơn hàng.");

        return ToRecordedPayment(row);
    }

    public async Task<RecordedPaymentDto> HandleCallbackAsync(
        string provider,
        PaymentCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerName = Normalize(provider)
            ?? throw new BadRequestException("Thiếu mã cổng thanh toán trên đường dẫn.");

        if (providerName.Length > ProviderMaxLength)
        {
            throw new BadRequestException($"Mã cổng thanh toán tối đa {ProviderMaxLength} ký tự.");
        }

        ValidateAmount(request.Amount);

        // Chỉ chuyển khoản (1) hoặc cổng thanh toán (2) mới có callback; COD/tiền mặt do thu ngân xác nhận.
        var method = request.Method ?? (byte)PaymentMethod.Gateway;
        if (method is not ((byte)PaymentMethod.BankTransfer) and not ((byte)PaymentMethod.Gateway))
        {
            throw new BadRequestException("Callback thanh toán chỉ áp dụng cho chuyển khoản hoặc cổng thanh toán.");
        }

        // Cổng có thể trả về id nội bộ hoặc mã đơn nằm trong nội dung giao dịch.
        var orderId = await ResolveCallbackOrderIdAsync(request, cancellationToken);

        var transactionId = Normalize(request.ProviderTransactionId);
        if (transactionId is not null && transactionId.Length > ProviderTransactionIdMaxLength)
        {
            throw new BadRequestException($"Mã giao dịch của cổng tối đa {ProviderTransactionIdMaxLength} ký tự.");
        }

        // Idempotent theo (Provider, ProviderTransactionId) — PAY-02: gửi lại cùng giao dịch không cộng tiền lần hai.
        // NeedsReview = 1 khi tiền về cho đơn đã hủy hoặc thanh toán thừa → cần đối soát/hoàn tiền thủ công.
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<PaymentRecordRow>(
            StoredProcedureNames.PaymentRecord,
            new
            {
                OrderId = orderId,
                Method = method,
                request.Amount,
                Provider = providerName,
                ProviderTransactionId = transactionId,
                request.PaidAt,
                UserId = _currentUser.UserId
            },
            cancellationToken)
            ?? throw new ConflictException("Không ghi nhận được giao dịch từ cổng thanh toán.");

        return ToRecordedPayment(row);
    }

    public async Task<RefundResultDto> RefundAsync(
        long orderId,
        RefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = RequireReason(request.Reason);

        ValidateMethod(request.Method);
        ValidateAmount(request.Amount);

        // Quản lý chi nhánh/Admin duyệt hoàn tiền, chỉ với đơn thuộc chi nhánh mình.
        await _guard.ForStaffAsync(orderId, cancellationToken);

        // Proc chỉ cho hoàn tiền thủ công với đơn đã hủy (8) hoặc giao thất bại (6); số tiền không vượt PaidAmount.
        // Bản ghi hoàn là dòng mới trong Payments (PaymentType = 1) trỏ về giao dịch gốc — không sửa giao dịch gốc (PAY-04).
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<RefundRow>(
            StoredProcedureNames.PaymentRefund,
            new
            {
                OrderId = orderId,
                request.Amount,
                request.Method,
                Reason = reason,
                UserId = _currentUser.UserId
            },
            cancellationToken)
            ?? throw new ConflictException("Không ghi nhận được giao dịch hoàn tiền.");

        return new RefundResultDto { PaymentId = row.PaymentId };
    }

    public async Task<CustomerLedgerDto> GetCustomerLedgerAsync(
        long customerId,
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureLedgerScope(customerId);

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = Math.Clamp(request.PageSize, 1, LedgerPageSizeMax);

        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.CustomerGetLedger,
            new { CustomerId = customerId, PageNumber = pageNumber, PageSize = pageSize },
            cancellationToken);

        // Thứ tự result set: (1) số dư + hạn mức công nợ, (2) các bút toán kèm cột TotalCount.
        var summary = await results.ReadFirstOrDefaultAsync<LedgerSummaryRow>(cancellationToken);
        var entries = await results.ReadListAsync<LedgerEntryRow>(cancellationToken);

        return new CustomerLedgerDto
        {
            CustomerId = customerId,
            Balance = summary?.Balance ?? 0m,
            CreditLimit = summary?.CreditLimit ?? 0m,
            Entries = entries.Select(ToLedgerEntry).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = entries.Count > 0 ? entries[0].TotalCount : 0
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Xác định đơn hàng từ dữ liệu callback: ưu tiên <c>orderId</c>; nếu cổng chỉ trả mã đơn thì tra
    /// <c>usp_Order_Get @OrderCode</c> (không tìm thấy → 404).
    /// </summary>
    private async Task<long> ResolveCallbackOrderIdAsync(PaymentCallbackRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderId is { } orderId and > 0)
        {
            return orderId;
        }

        var orderCode = Normalize(request.OrderCode)
            ?? throw new BadRequestException("Callback thanh toán phải kèm orderId hoặc orderCode.");

        var header = await _guard.LoadByCodeAsync(orderCode, cancellationToken);

        return header.Id;
    }

    /// <summary>
    /// Phạm vi sao kê công nợ: khách chỉ xem của chính mình; Quản lý chi nhánh và Admin xem được khách hàng
    /// bất kỳ (proc không có tham số lọc theo chi nhánh — xem BRD 2.2 và ghi chú trong báo cáo module).
    /// </summary>
    private void EnsureLedgerScope(long customerId)
    {
        if (_currentUser.IsInRole(RoleCodes.Customer))
        {
            if (_currentUser.CustomerId != customerId)
            {
                throw new ForbiddenException("Bạn chỉ xem được sao kê công nợ của chính mình.");
            }

            return;
        }

        if (!_currentUser.IsInRole(RoleCodes.Manager) && !_currentUser.IsInRole(RoleCodes.Admin))
        {
            throw new ForbiddenException("Chỉ khách hàng, Quản lý chi nhánh và Admin xem được sao kê công nợ.");
        }
    }

    private static RecordedPaymentDto ToRecordedPayment(PaymentRecordRow row) => new()
    {
        PaymentId = row.PaymentId,
        IsDuplicate = row.IsDuplicate,
        PaidAmount = row.PaidAmount,
        OrderStatus = row.OrderStatus,
        NeedsReview = row.NeedsReview ?? false
    };

    private static CustomerLedgerEntryDto ToLedgerEntry(LedgerEntryRow row) => new()
    {
        Id = row.Id,
        EntryType = row.EntryType,
        Amount = row.Amount,
        DueDate = row.DueDate,
        OrderId = row.OrderId,
        PaymentId = row.PaymentId,
        CreatedAt = row.CreatedAt
    };

    /// <summary>Số tiền phải lớn hơn 0 (proc cũng kiểm tra và trả 50004).</summary>
    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new BadRequestException("Số tiền phải lớn hơn 0.");
        }
    }

    /// <summary>Phương thức thanh toán hợp lệ: 0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ (PAY-01).</summary>
    private static void ValidateMethod(byte method)
    {
        if (method > (byte)PaymentMethod.Credit)
        {
            throw new BadRequestException("Phương thức thanh toán không hợp lệ (0..4).");
        }
    }

    /// <summary>Hoàn tiền bắt buộc có lý do và không vượt 300 ký tự (PAY-04).</summary>
    private static string RequireReason(string? reason)
    {
        var text = Normalize(reason)
            ?? throw new BadRequestException("Phải nhập lý do hoàn tiền.");

        if (text.Length > ReasonMaxLength)
        {
            throw new BadRequestException($"Lý do hoàn tiền tối đa {ReasonMaxLength} ký tự.");
        }

        return text;
    }

    /// <summary>Chuỗi rỗng/khoảng trắng → <c>null</c> để proc dùng giá trị mặc định của tham số.</summary>
    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
