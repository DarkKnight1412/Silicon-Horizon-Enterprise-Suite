using System.Text.Json;
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
/// Hiện thực use case đơn hàng qua stored procedure (Database/03_sp_orders_payments.sql).
/// </summary>
/// <remarks>
/// Nguyên tắc quan trọng:
/// <list type="bullet">
/// <item>Mọi thao tác chuyển trạng thái/thanh toán đều kiểm tra phạm vi qua <see cref="OrderAccessGuard"/> trước khi gọi proc.</item>
/// <item>Trạng thái đích do API cố định theo từng endpoint (xác nhận = 2, đóng gói = 3, hoàn tất = 7...);
/// proc <c>usp_Internal_SetOrderStatus</c> kiểm tra chuyển trạng thái hợp lệ (ORD-07) và ghi lịch sử (ORD-08),
/// chuyển sai → SQL 50002 → 409.</item>
/// <item>Không mở transaction ở tầng API: mỗi proc đã tự quản lý giao dịch và khóa dòng tồn kho (NFR-01).</item>
/// <item>Proc giữ/trừ nhiều dòng tồn kho có thể gặp deadlock; <c>SqlErrorTranslator</c>
/// đang dịch mã 1205 thành 503 (Database/README.md mục 1.2 khuyến nghị thử lại 2–3 lần). Việc thử lại nên làm bằng
/// một decorator cho <c>ISqlExecutor</c> ở tầng Infrastructure để áp dụng cho mọi module, không xử lý riêng ở đây.</item>
/// </list>
/// </remarks>
public sealed class OrderService : IOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ── Giới hạn độ dài cột (chặn sớm để trả 400 thay vì lỗi hệ thống khi tràn cột) ──
    /// <summary>Orders.RecipientName nvarchar(150).</summary>
    private const int RecipientNameMaxLength = 150;

    /// <summary>Orders.RecipientPhone varchar(20).</summary>
    private const int RecipientPhoneMaxLength = 20;

    /// <summary>Orders.ShippingAddress nvarchar(500).</summary>
    private const int ShippingAddressMaxLength = 500;

    /// <summary>Orders.Note nvarchar(500).</summary>
    private const int NoteMaxLength = 500;

    /// <summary>Vouchers.Code varchar(40).</summary>
    private const int VoucherCodeMaxLength = 40;

    /// <summary>Tham số @Reason (nvarchar(300)) của hủy đơn / giao thất bại / gỡ serial.</summary>
    private const int ReasonMaxLength = 300;

    /// <summary>SerialItems.SerialNumber varchar(100).</summary>
    private const int SerialNumberMaxLength = 100;

    /// <summary>@Keyword của usp_Order_List là nvarchar(50).</summary>
    private const int KeywordMaxLength = 50;

    /// <summary>usp_Order_List tự cắt @PageSize về tối đa 100 — API dùng cùng mức để TotalPages khớp dữ liệu trả về.</summary>
    private const int ListPageSizeMax = 100;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;
    private readonly OrderAccessGuard _guard;

    public OrderService(ISqlExecutor sqlExecutor, ICurrentUser currentUser, OrderAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
        _guard = guard;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tạo đơn (UC-04 khách online, UC-09 đơn tại quầy)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<CreatedOrderDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        // Phạm vi dữ liệu: khách chỉ đặt được đơn cho chính mình (ép CustomerId từ token).
        var customerId = _currentUser.CustomerId
            ?? throw new ForbiddenException("Chỉ tài khoản khách hàng mới đặt được đơn hàng.");

        ValidateContactInfo(request.RecipientName, request.RecipientPhone, request.ShippingAddress, request.Note);
        ValidateLength(request.VoucherCode, VoucherCodeMaxLength, "Mã giảm giá");

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CreatedOrderRow>(
            StoredProcedureNames.OrderCreate,
            new
            {
                CustomerId = customerId,
                request.BranchId,
                Channel = (byte)OrderChannel.Online,
                request.FulfillmentType,
                request.RecipientName,
                request.RecipientPhone,
                request.ShippingAddress,
                request.ShippingFee,
                request.PaymentMethod,
                request.VoucherCode,
                // @ItemsJson = NULL: proc lấy các dòng IsSelected = 1 trong giỏ rồi xóa chúng khỏi giỏ (CART-05).
                ItemsJson = (string?)null,
                SalesStaffJson = (string?)null,
                request.Note,
                UserId = _currentUser.UserId
            },
            cancellationToken)
            ?? throw new ConflictException("Không tạo được đơn hàng từ giỏ hiện tại.");

        return ToCreatedOrder(row);
    }

    public async Task<CreatedOrderDto> CreatePosAsync(CreatePosOrderRequest request, CancellationToken cancellationToken = default)
    {
        // ORD-03: đơn tại quầy dùng chi nhánh của nhân viên — ép từ token, KHÔNG nhận từ client.
        var branchId = _currentUser.BranchId
            ?? throw new ForbiddenException("Tài khoản nhân viên chưa được gắn chi nhánh nên không tạo được đơn tại quầy.");

        if (request.CustomerId <= 0)
        {
            throw new BadRequestException("Thiếu khách hàng của đơn tại quầy.");
        }

        if (request.Items.Count == 0)
        {
            throw new BadRequestException("Đơn tại quầy phải có ít nhất một sản phẩm.");
        }

        ValidateContactInfo(request.RecipientName, request.RecipientPhone, request.ShippingAddress, request.Note);
        ValidateLength(request.VoucherCode, VoucherCodeMaxLength, "Mã giảm giá");

        var itemsJson = JsonSerializer.Serialize(
            request.Items.Select(item => new { productId = item.ProductId, quantity = item.Quantity }),
            JsonOptions);

        // Tổng ShareRate phải bằng 100 và nhân viên phải đang làm việc — proc kiểm tra (50004 → 400).
        // Không truyền thì proc tự gán nhân viên tạo đơn 100% (KPI-02).
        var salesStaffJson = request.SalesStaff is { Count: > 0 }
            ? JsonSerializer.Serialize(
                request.SalesStaff.Select(staff => new { employeeId = staff.EmployeeId, shareRate = staff.ShareRate }),
                JsonOptions)
            : null;

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CreatedOrderRow>(
            StoredProcedureNames.OrderCreate,
            new
            {
                CustomerId = request.CustomerId,
                BranchId = branchId,
                Channel = (byte)OrderChannel.Pos,
                request.FulfillmentType,
                request.RecipientName,
                request.RecipientPhone,
                request.ShippingAddress,
                request.ShippingFee,
                request.PaymentMethod,
                request.VoucherCode,
                ItemsJson = itemsJson,
                SalesStaffJson = salesStaffJson,
                request.Note,
                UserId = _currentUser.UserId
            },
            cancellationToken)
            ?? throw new ConflictException("Không tạo được đơn tại quầy.");

        return ToCreatedOrder(row);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Danh sách đơn (usp_Order_List)
    // ─────────────────────────────────────────────────────────────────────────

    public Task<PagedResult<OrderListItemDto>> ListMineAsync(OrderListRequest request, CancellationToken cancellationToken = default)
    {
        // Khách chỉ xem đơn của mình: ép @CustomerId từ token, không nhận từ query string.
        var customerId = _currentUser.CustomerId
            ?? throw new ForbiddenException("Chỉ tài khoản khách hàng mới xem được danh sách đơn của mình.");

        return ListAsync(request, customerId, branchId: null, cancellationToken);
    }

    public Task<PagedResult<OrderListItemDto>> ListBranchAsync(OrderListRequest request, CancellationToken cancellationToken = default)
    {
        // Nhân viên chỉ xem đơn của chi nhánh mình: ép @BranchId từ token (BRD 2.2).
        var branchId = _currentUser.BranchId
            ?? throw new ForbiddenException("Tài khoản nhân viên chưa được gắn chi nhánh nên không xem được danh sách đơn.");

        return ListAsync(request, customerId: null, branchId, cancellationToken);
    }

    public Task<PagedResult<OrderListItemDto>> ListAllAsync(OrderListRequest request, CancellationToken cancellationToken = default)
        => ListAsync(request, customerId: null, branchId: null, cancellationToken);

    private async Task<PagedResult<OrderListItemDto>> ListAsync(
        OrderListRequest request,
        long? customerId,
        int? branchId,
        CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = Math.Clamp(request.PageSize, 1, ListPageSizeMax);
        var keyword = Normalize(request.Keyword);
        ValidateLength(keyword, KeywordMaxLength, "Từ khóa");

        var rows = await _sqlExecutor.QueryAsync<OrderListRow>(
            StoredProcedureNames.OrderList,
            new
            {
                CustomerId = customerId,
                BranchId = branchId,
                request.Status,
                request.FromDate,
                request.ToDate,
                Keyword = keyword,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            cancellationToken);

        return PagedResult<OrderListItemDto>.Create(
            rows.Select(ToListItem).ToList(),
            pageNumber,
            pageSize,
            rows.Count > 0 ? rows[0].TotalCount : 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chi tiết đơn (usp_Order_Get — 6 result set)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<OrderDetailDto> GetByIdAsync(long orderId, CancellationToken cancellationToken = default)
    {
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.OrderGet,
            new { OrderId = orderId },
            cancellationToken);

        // Thứ tự result set: header, dòng hàng, serial, thanh toán, lịch sử trạng thái, nhân viên tư vấn.
        var header = await results.ReadFirstOrDefaultAsync<OrderHeaderRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn hàng Id = {orderId}.");

        // Kiểm tra phạm vi trên chính header vừa đọc (khách: đơn của mình; nhân viên: chi nhánh của mình).
        EnsureViewScope(header);

        var items = await results.ReadListAsync<OrderItemRow>(cancellationToken);
        var serials = await results.ReadListAsync<OrderSerialRow>(cancellationToken);
        var payments = await results.ReadListAsync<OrderPaymentRow>(cancellationToken);
        var history = await results.ReadListAsync<OrderStatusHistoryRow>(cancellationToken);
        var salesStaff = await results.ReadListAsync<OrderSalesStaffRow>(cancellationToken);

        return new OrderDetailDto
        {
            Order = ToHeader(header),
            Items = items.Select(ToItem).ToList(),
            Serials = serials.Select(ToSerial).ToList(),
            Payments = payments.Select(ToPayment).ToList(),
            StatusHistory = history.Select(ToStatusHistory).ToList(),
            SalesStaff = salesStaff.Select(ToSalesStaff).ToList()
        };
    }

    /// <summary>
    /// Xem chi tiết đơn: khách bị ép theo <c>CustomerId</c> trong token, nhân viên theo <c>BranchId</c>
    /// (Admin xem được mọi chi nhánh) — BRD 2.2.
    /// </summary>
    private void EnsureViewScope(OrderHeaderRow header)
    {
        if (_currentUser.IsInRole(RoleCodes.Customer))
        {
            _guard.EnsureCustomerOwns(header);

            return;
        }

        _guard.EnsureStaffBranch(header);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hủy đơn (UC-06 khách hủy, UC-20 Quản lý duyệt hủy)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<OrderCancelResultDto> CancelByCustomerAsync(
        long orderId,
        CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = RequireReason(request.Reason, "hủy đơn");

        // Khách chỉ được hủy đơn của mình (403 nếu không phải).
        var header = await _guard.ForCustomerAsync(orderId, cancellationToken);

        // ORD-07 + Database/README.md mục 5.1: khách chỉ hủy khi đơn ở PendingPayment (0) hoặc Pending (1).
        // Proc chỉ kiểm tra chuyển 0,1,2,3,4,6 → 8 nên tầng API phải chặn trước, nếu không khách hủy được
        // cả đơn đã xác nhận hoặc đang giao.
        var status = (OrderStatus)header.Status;
        if (status is not OrderStatus.PendingPayment and not OrderStatus.Pending)
        {
            throw new ConflictException(
                $"Không thể hủy đơn ở trạng thái {header.Status} ({OrderStatusText(status)}); " +
                "khách chỉ hủy được đơn chờ thanh toán hoặc chờ xử lý — vui lòng liên hệ cửa hàng.");
        }

        return await CancelInternalAsync(orderId, reason, cancellationToken);
    }

    public async Task<OrderCancelResultDto> CancelApprovedAsync(
        long orderId,
        CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = RequireReason(request.Reason, "hủy đơn");

        // Quản lý/Admin hủy đơn đã xác nhận trở đi: chỉ trong chi nhánh của mình (Admin bỏ qua).
        await _guard.ForStaffAsync(orderId, cancellationToken);

        return await CancelInternalAsync(orderId, reason, cancellationToken);
    }

    /// <summary>
    /// Gọi <c>usp_Order_Cancel</c>: proc nhả tồn đang giữ, gỡ serial về InStock, trả lượt voucher, đảo công nợ
    /// và ghi nhật ký. Trả về <c>RefundRequired</c> để API biết đơn có phải hoàn tiền (PAY-04).
    /// </summary>
    private async Task<OrderCancelResultDto> CancelInternalAsync(long orderId, string reason, CancellationToken cancellationToken)
    {
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<OrderCancelRow>(
            StoredProcedureNames.OrderCancel,
            new { OrderId = orderId, Reason = reason, UserId = _currentUser.UserId },
            cancellationToken)
            ?? throw new ConflictException("Không hủy được đơn hàng.");

        return new OrderCancelResultDto
        {
            OrderId = row.OrderId,
            Status = row.Status,
            PaidAmount = row.PaidAmount,
            RefundRequired = row.RefundRequired
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Xác nhận, đóng gói, quét serial, giao nhận, hoàn tất (Database/README.md mục 5.2)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task ConfirmAsync(long orderId, CancellationToken cancellationToken = default)
    {
        await _guard.ForStaffAsync(orderId, cancellationToken);

        // Pending (1) → Confirmed (2). Chuyển sai trạng thái → SQL 50002 → 409.
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.OrderConfirm,
            new { OrderId = orderId, UserId = _currentUser.UserId },
            cancellationToken);
    }

    public async Task StartPackingAsync(long orderId, CancellationToken cancellationToken = default)
    {
        await _guard.ForStaffAsync(orderId, cancellationToken);

        // Confirmed (2) → Packing (3).
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.OrderStartPacking,
            new { OrderId = orderId, UserId = _currentUser.UserId },
            cancellationToken);
    }

    public async Task<IReadOnlyList<OrderSerialProgressDto>> AssignSerialAsync(
        long orderId,
        AssignSerialRequest request,
        CancellationToken cancellationToken = default)
    {
        await _guard.ForStaffAsync(orderId, cancellationToken);

        var serialNumber = RequireText(request.SerialNumber, SerialNumberMaxLength, "Số serial");

        // Proc chỉ cho quét khi đơn đang ở Packing (3); serial phải InStock và thuộc đúng chi nhánh xuất hàng của đơn.
        // Quét quá số lượng của dòng → 50006 (422); serial không thuộc đơn → 50001 (404).
        var progress = await _sqlExecutor.QueryAsync<OrderSerialProgressRow>(
            StoredProcedureNames.OrderAssignSerial,
            new
            {
                OrderId = orderId,
                SerialNumber = serialNumber,
                request.ProductId,
                UserId = _currentUser.UserId
            },
            cancellationToken);

        return progress.Select(row => new OrderSerialProgressDto
        {
            OrderItemId = row.OrderItemId,
            ProductId = row.ProductId,
            Quantity = row.Quantity,
            Assigned = row.Assigned
        }).ToList();
    }

    public async Task UnassignSerialAsync(
        long orderId,
        string serialNumber,
        string reason,
        long? productId,
        CancellationToken cancellationToken = default)
    {
        await _guard.ForStaffAsync(orderId, cancellationToken);

        var serial = RequireText(serialNumber, SerialNumberMaxLength, "Số serial");
        var note = RequireReason(reason, "gỡ serial");

        // Serial về InStock, ghi SerialStatusHistory và Audit (SER-02).
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.OrderUnassignSerial,
            new
            {
                OrderId = orderId,
                SerialNumber = serial,
                Reason = note,
                ProductId = productId,
                UserId = _currentUser.UserId
            },
            cancellationToken);
    }

    public async Task<OrderStatusChangeDto> MarkReadyAsync(long orderId, CancellationToken cancellationToken = default)
    {
        await _guard.ForStaffAsync(orderId, cancellationToken);

        // Yêu cầu đã quét đủ serial (thiếu → 50002 → 409).
        // Nhận tại cửa hàng → 4 (kèm hạn đến lấy theo cấu hình pickup.hold.days — ORD-02); giao tận nơi → 5.
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<OrderStatusRow>(
            StoredProcedureNames.OrderMarkReady,
            new { OrderId = orderId, UserId = _currentUser.UserId },
            cancellationToken)
            ?? throw new ConflictException("Không chuyển được đơn sang trạng thái đóng gói xong.");

        return new OrderStatusChangeDto { Status = row.Status };
    }

    public async Task MarkDeliveryFailedAsync(
        long orderId,
        DeliveryFailedRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = RequireReason(request.Reason, "giao thất bại");

        await _guard.ForStaffAsync(orderId, cancellationToken);

        // Shipping (5) → DeliveryFailed (6); hàng vẫn đang giữ (chờ giao lại hoặc hoàn về kho).
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.OrderMarkDeliveryFailed,
            new { OrderId = orderId, Reason = reason, UserId = _currentUser.UserId },
            cancellationToken);
    }

    public async Task RetryDeliveryAsync(long orderId, CancellationToken cancellationToken = default)
    {
        await _guard.ForStaffAsync(orderId, cancellationToken);

        // DeliveryFailed (6) → Shipping (5).
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.OrderRetryDelivery,
            new { OrderId = orderId, UserId = _currentUser.UserId },
            cancellationToken);
    }

    public async Task CompleteAsync(long orderId, CancellationToken cancellationToken = default)
    {
        await _guard.ForStaffAsync(orderId, cancellationToken);

        // ReadyForPickup (4) / Shipping (5) → Completed (7): proc trừ tồn thực tế, serial → Sold và bắt đầu
        // bảo hành (WAR-01), ghi nhận tiền COD/tiền mặt còn thiếu (PAY-01) và tính hoa hồng (KPI-01).
        // Đơn chuyển khoản/cổng thanh toán chưa thu đủ tiền → 50002 (409).
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.OrderComplete,
            new { OrderId = orderId, UserId = _currentUser.UserId },
            cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ánh xạ row (cột result set) → DTO trả cho client
    // ─────────────────────────────────────────────────────────────────────────

    private static CreatedOrderDto ToCreatedOrder(CreatedOrderRow row) => new()
    {
        OrderId = row.OrderId,
        OrderCode = row.OrderCode,
        Status = row.Status,
        GrandTotal = row.GrandTotal,
        ExpiresAt = row.ExpiresAt
    };

    private static OrderListItemDto ToListItem(OrderListRow row) => new()
    {
        Id = row.Id,
        OrderCode = row.OrderCode,
        CustomerId = row.CustomerId,
        CustomerName = row.CustomerName,
        CustomerPhone = row.CustomerPhone,
        BranchId = row.BranchId,
        Channel = row.Channel,
        FulfillmentType = row.FulfillmentType,
        Status = row.Status,
        GrandTotal = row.GrandTotal,
        PaidAmount = row.PaidAmount,
        PaymentMethod = row.PaymentMethod,
        PaymentStatus = row.PaymentStatus,
        CreatedAt = row.CreatedAt,
        ExpiresAt = row.ExpiresAt
    };

    private static OrderHeaderDto ToHeader(OrderHeaderRow row) => new()
    {
        Id = row.Id,
        OrderCode = row.OrderCode,
        CustomerId = row.CustomerId,
        CustomerName = row.CustomerName,
        CustomerPhone = row.CustomerPhone,
        BranchId = row.BranchId,
        BranchName = row.BranchName,
        Channel = row.Channel,
        FulfillmentType = row.FulfillmentType,
        Status = row.Status,
        RecipientName = row.RecipientName,
        RecipientPhone = row.RecipientPhone,
        ShippingAddress = row.ShippingAddress,
        SubTotal = row.SubTotal,
        DiscountTotal = row.DiscountTotal,
        ShippingFee = row.ShippingFee,
        GrandTotal = row.GrandTotal,
        PaidAmount = row.PaidAmount,
        PaymentMethod = row.PaymentMethod,
        PaymentStatus = row.PaymentStatus,
        VoucherId = row.VoucherId,
        ExpiresAt = row.ExpiresAt,
        CompletedAt = row.CompletedAt,
        Note = row.Note,
        CreatedAt = row.CreatedAt,
        CreatedBy = row.CreatedBy,
        UpdatedAt = row.UpdatedAt,
        UpdatedBy = row.UpdatedBy
    };

    private static OrderItemDto ToItem(OrderItemRow row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        Sku = row.Sku,
        ProductName = row.ProductName,
        UnitPrice = row.UnitPrice,
        DiscountAmount = row.DiscountAmount,
        Quantity = row.Quantity,
        LineTotal = row.LineTotal,
        WarrantyMonths = row.WarrantyMonths,
        RequiresSerial = row.RequiresSerial,
        PromotionId = row.PromotionId,
        SerialAssignedCount = row.SerialAssignedCount
    };

    private static OrderSerialDto ToSerial(OrderSerialRow row) => new()
    {
        SerialItemId = row.SerialItemId,
        OrderItemId = row.OrderItemId,
        SerialNumber = row.SerialNumber,
        Status = row.Status,
        WarrantyStartDate = row.WarrantyStartDate,
        WarrantyEndDate = row.WarrantyEndDate
    };

    private static OrderPaymentDto ToPayment(OrderPaymentRow row) => new()
    {
        Id = row.Id,
        PaymentType = row.PaymentType,
        Method = row.Method,
        Amount = row.Amount,
        Status = row.Status,
        Provider = row.Provider,
        ProviderTransactionId = row.ProviderTransactionId,
        ParentPaymentId = row.ParentPaymentId,
        ReturnRequestId = row.ReturnRequestId,
        PaidAt = row.PaidAt,
        CreatedAt = row.CreatedAt,
        CreatedBy = row.CreatedBy
    };

    private static OrderStatusHistoryDto ToStatusHistory(OrderStatusHistoryRow row) => new()
    {
        FromStatus = row.FromStatus,
        ToStatus = row.ToStatus,
        Reason = row.Reason,
        ChangedBy = row.ChangedBy,
        ChangedAt = row.ChangedAt
    };

    private static OrderSalesStaffDto ToSalesStaff(OrderSalesStaffRow row) => new()
    {
        EmployeeId = row.EmployeeId,
        FullName = row.FullName,
        ShareRate = row.ShareRate,
        IsPrimary = row.IsPrimary
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Kiểm tra dữ liệu đầu vào (chặn trước khi chạm database để trả 400 rõ ràng)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Kiểm tra độ dài các trường thông tin nhận hàng theo đúng cột của bảng Orders.</summary>
    private static void ValidateContactInfo(string? recipientName, string? recipientPhone, string? shippingAddress, string? note)
    {
        ValidateLength(recipientName, RecipientNameMaxLength, "Tên người nhận");
        ValidateLength(recipientPhone, RecipientPhoneMaxLength, "Số điện thoại người nhận");
        ValidateLength(shippingAddress, ShippingAddressMaxLength, "Địa chỉ giao hàng");
        ValidateLength(note, NoteMaxLength, "Ghi chú");
    }

    /// <summary>Bắt buộc có lý do (ORD-08: hủy đơn và giao thất bại phải có lý do) và không vượt 300 ký tự.</summary>
    private static string RequireReason(string? reason, string action)
    {
        var value = RequireText(reason, ReasonMaxLength, $"Lý do {action}");

        return value;
    }

    /// <summary>Cắt khoảng trắng, bắt buộc khác rỗng và giới hạn độ dài theo cột tương ứng.</summary>
    private static string RequireText(string? value, int maxLength, string fieldName)
    {
        var text = Normalize(value);

        if (text is null)
        {
            throw new BadRequestException($"{fieldName} không được để trống.");
        }

        ValidateLength(text, maxLength, fieldName);

        return text;
    }

    private static void ValidateLength(string? value, int maxLength, string fieldName)
    {
        if (value is not null && value.Length > maxLength)
        {
            throw new BadRequestException($"{fieldName} tối đa {maxLength} ký tự.");
        }
    }

    /// <summary>Chuỗi rỗng/khoảng trắng → <c>null</c> để proc dùng giá trị mặc định của tham số.</summary>
    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Tên trạng thái đơn bằng tiếng Việt — dùng cho thông báo lỗi (BRD mục 6.4).</summary>
    private static string OrderStatusText(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "Chờ thanh toán",
        OrderStatus.Pending => "Chờ xử lý",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Packing => "Đang đóng gói",
        OrderStatus.ReadyForPickup => "Chờ khách nhận hàng",
        OrderStatus.Shipping => "Đang giao hàng",
        OrderStatus.DeliveryFailed => "Giao thất bại",
        OrderStatus.Completed => "Đã hoàn tất",
        OrderStatus.Cancelled => "Đã hủy",
        OrderStatus.PartiallyReturned => "Trả một phần",
        OrderStatus.Returned => "Đã trả hàng",
        _ => "Không xác định"
    };
}
