using System.Text.Json;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Returns.Dto;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Returns;

/// <summary>
/// Hiện thực use case đổi trả qua stored procedure (Database/05_sp_warranty_returns_commission.sql).
/// </summary>
/// <remarks>
/// Nguyên tắc nghiệp vụ:
/// <list type="bullet">
/// <item><b>Vòng đời yêu cầu</b>: 0 Requested → (1 Approved | 2 Rejected) → 3 GoodsReceived → 4 Refunded → 5 Closed.
/// Chỉ duyệt/từ chối được khi đang Requested, chỉ kiểm tra hàng khi đã Approved, chỉ hoàn tiền sau khi đã
/// nhận và kiểm tra hàng (proc báo 50002 → 409 khi sai bước).</item>
/// <item><b>Hạn đổi trả</b>: chỉ nhận yêu cầu trong <c>SystemSettings['return.window.days']</c> (mặc định 7 ngày)
/// kể từ ngày hoàn tất đơn; quá hạn → 50006 → 409 (RET-02).</item>
/// <item><b>Tiền hoàn</b>: giá dòng sau khuyến mãi trừ phần voucher phân bổ theo tỷ lệ, KHÔNG hoàn phí vận chuyển;
/// phần vượt quá số đã thu chỉ được cấn trừ công nợ khi đơn mua chịu (RET-05).</item>
/// <item><b>Thu hồi hoa hồng</b>: <c>usp_Return_Refund</c> gọi <c>usp_Internal_ClawbackCommission</c> để ghi bút toán
/// âm (Clawback) theo tỷ lệ số tiền đã hoàn trên doanh thu đơn (KPI-04) — API không tự tính.</item>
/// <item>Không mở transaction ở tầng Application: mỗi proc đã tự quản lý giao dịch và khóa dòng đơn/tồn (NFR-01).</item>
/// </list>
/// </remarks>
public sealed class ReturnService : IReturnService
{
    /// <summary>Tham số JSON của proc đọc bằng <c>OPENJSON</c> — dùng camelCase như tài liệu proc mô tả.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary><c>@Reason nvarchar(500)</c> (ReturnRequests.Reason).</summary>
    private const int ReasonMaxLength = 500;

    /// <summary><c>@Note nvarchar(300)</c> của usp_Return_Review.</summary>
    private const int NoteMaxLength = 300;

    /// <summary>SerialItems.SerialNumber varchar(100) — proc so khớp serial bằng <c>CAST(... AS varchar(100))</c>.</summary>
    private const int SerialNumberMaxLength = 100;

    /// <summary>Phương thức hoàn tiền lớn nhất hợp lệ (4 = Công nợ, xem <see cref="PaymentMethod"/>).</summary>
    private const byte MaxPaymentMethod = (byte)PaymentMethod.Credit;

    /// <summary>usp_Return_List tự cắt <c>@PageSize</c> về tối đa 100 — dùng cùng mức để TotalPages khớp dữ liệu.</summary>
    private const int ListPageSizeMax = 100;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ReturnAccessGuard _guard;

    public ReturnService(ISqlExecutor sqlExecutor, ReturnAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task<CreatedReturnDto> CreateAsync(
        long orderId,
        CreateReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        // Khách chỉ đổi trả được đơn của mình: ép @CustomerId từ token, KHÔNG nhận customerId từ body.
        var customerId = _guard.RequireCustomerId();

        if (orderId <= 0)
        {
            throw new BadRequestException("Thiếu đơn hàng cần đổi trả.");
        }

        var reason = RequireText(request.Reason, ReasonMaxLength, "Lý do đổi trả");
        var itemsJson = BuildItemsJson(request.Items);

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CreatedReturnRow>(
            StoredProcedureNames.ReturnCreate,
            new
            {
                OrderId = orderId,
                CustomerId = customerId,
                Reason = reason,
                ItemsJson = itemsJson,
                UserId = _guard.RequireUserId()
            },
            cancellationToken)
            ?? throw new ConflictException("Không tạo được yêu cầu đổi trả.");

        return CreatedReturnDto.FromRow(row);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReturnListItemDto>> ListAsync(
        ReturnListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Page(query.PageNumber, query.PageSize, ListPageSizeMax);

        // Nhân viên: @BranchId ép từ token (CustomerId = null). Khách: @CustomerId ép từ token (BranchId = null).
        var scope = _guard.ResolveReadScope();

        var rows = await _sqlExecutor.QueryAsync<ReturnListItemRow>(
            StoredProcedureNames.ReturnList,
            new
            {
                scope.BranchId,
                scope.CustomerId,
                Status = query.Status,
                page.PageNumber,
                page.PageSize
            },
            cancellationToken);

        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;

        return PagedResult<ReturnListItemDto>.Create(
            rows.Select(ReturnListItemDto.FromRow).ToList(),
            page.PageNumber,
            page.PageSize,
            totalCount);
    }

    /// <inheritdoc />
    public async Task<ReturnRequestDetailDto> GetAsync(long returnRequestId, CancellationToken cancellationToken = default)
    {
        if (returnRequestId <= 0)
        {
            throw new BadRequestException("Thiếu yêu cầu đổi trả cần xem.");
        }

        // Khách: proc tự giới hạn theo @CustomerId (yêu cầu của người khác → 50001 → 404).
        // Nhân viên: đọc bằng @CustomerId = NULL rồi tự kiểm tra chi nhánh bên dưới.
        long? customerId = _guard.IsStaff ? null : _guard.RequireCustomerId();

        // 2 result set: header yêu cầu, danh sách dòng hàng trả.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.ReturnGet,
            new { ReturnRequestId = returnRequestId, CustomerId = customerId },
            cancellationToken);

        var request = await results.ReadFirstOrDefaultAsync<ReturnRequestRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu đổi trả Id = {returnRequestId}.");

        if (_guard.IsStaff)
        {
            _guard.EnsureBranch(request.BranchId, "xem yêu cầu đổi trả");
        }

        var items = await results.ReadListAsync<ReturnItemRow>(cancellationToken);

        return new ReturnRequestDetailDto
        {
            Request = ReturnRequestDto.FromRow(request),
            Items = items.Select(ReturnItemDto.FromRow).ToList()
        };
    }

    /// <inheritdoc />
    public async Task ReviewAsync(
        long returnRequestId,
        ReviewReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (returnRequestId <= 0)
        {
            throw new BadRequestException("Thiếu yêu cầu đổi trả cần duyệt.");
        }

        // RET-04: từ chối bắt buộc có lý do (proc cũng chặn bằng 50004).
        var note = NormalizeOptional(request.Note, NoteMaxLength, "Lý do");

        if (!request.Approve && note is null)
        {
            throw new BadRequestException("Từ chối đổi trả phải kèm lý do (note).");
        }

        // QL/AD: proc không kiểm tra chi nhánh của yêu cầu nên phải đọc yêu cầu trước (403 nếu khác chi nhánh).
        var header = await _guard.LoadRequestAsync(returnRequestId, customerId: null, cancellationToken);
        _guard.EnsureBranch(header.BranchId, "duyệt yêu cầu đổi trả");

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.ReturnReview,
            new
            {
                ReturnRequestId = returnRequestId,
                request.Approve,
                Note = note,
                UserId = _guard.RequireUserId()
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task InspectAsync(
        long returnRequestId,
        InspectReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (returnRequestId <= 0)
        {
            throw new BadRequestException("Thiếu yêu cầu đổi trả cần kiểm tra hàng.");
        }

        var resultsJson = BuildResultsJson(request.Results);

        // TK/AD: proc chỉ kiểm tra trạng thái yêu cầu (phải Approved) nên API phải chặn thủ kho chi nhánh khác.
        var header = await _guard.LoadRequestAsync(returnRequestId, customerId: null, cancellationToken);
        _guard.EnsureBranch(header.BranchId, "nhận hàng và kiểm tra hàng trả");

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.ReturnInspect,
            new
            {
                ReturnRequestId = returnRequestId,
                ResultsJson = resultsJson,
                UserId = _guard.RequireUserId()
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReturnRefundResultDto> RefundAsync(
        long returnRequestId,
        RefundReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (returnRequestId <= 0)
        {
            throw new BadRequestException("Thiếu yêu cầu đổi trả cần hoàn tiền.");
        }

        if (request.Method > MaxPaymentMethod)
        {
            throw new BadRequestException($"Phương thức hoàn tiền chỉ nhận 0..{MaxPaymentMethod} (PaymentMethod).");
        }

        if (request.Amount is < 0)
        {
            throw new BadRequestException("Số tiền hoàn không được âm.");
        }

        // QL/AD: chỉ hoàn tiền sau khi hàng đã được nhận & kiểm tra (GoodsReceived) — proc kiểm tra lại (50002).
        var header = await _guard.LoadRequestAsync(returnRequestId, customerId: null, cancellationToken);
        _guard.EnsureBranch(header.BranchId, "hoàn tiền cho yêu cầu đổi trả");

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<ReturnRefundResultRow>(
            StoredProcedureNames.ReturnRefund,
            new
            {
                ReturnRequestId = returnRequestId,
                Method = request.Method,
                request.Amount,
                UserId = _guard.RequireUserId()
            },
            cancellationToken)
            ?? throw new ConflictException("Không hoàn tiền được cho yêu cầu đổi trả này.");

        return ReturnRefundResultDto.FromRow(row);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dựng <c>@ItemsJson</c> cho <c>usp_Return_Create</c>:
    /// <c>[{"orderItemId":12,"quantity":1,"serialNumbers":["SN001"]}]</c> — hàng không quản lý theo serial thì
    /// <c>serialNumbers = null</c>. Việc so số serial với số lượng trả do proc kiểm tra (API không biết
    /// sản phẩm có quản lý serial hay không).
    /// </summary>
    private static string BuildItemsJson(IReadOnlyList<ReturnItemInput> items)
    {
        if (items.Count == 0)
        {
            throw new BadRequestException("Yêu cầu đổi trả phải có ít nhất một dòng hàng.");
        }

        var lines = new List<ReturnLine>(items.Count);

        foreach (var item in items)
        {
            if (item.OrderItemId <= 0)
            {
                throw new BadRequestException("Có dòng trả thiếu orderItemId (dòng hàng của đơn gốc).");
            }

            if (item.Quantity <= 0)
            {
                throw new BadRequestException($"Dòng hàng {item.OrderItemId}: số lượng trả phải lớn hơn 0.");
            }

            lines.Add(new ReturnLine(
                item.OrderItemId,
                item.Quantity,
                NormalizeSerials(item.SerialNumbers, item.OrderItemId)));
        }

        if (lines.Select(line => line.OrderItemId).Distinct().Count() != lines.Count)
        {
            throw new BadRequestException("Một dòng hàng của đơn chỉ được xuất hiện một lần trong yêu cầu đổi trả.");
        }

        return JsonSerializer.Serialize(
            lines.Select(line => new
            {
                orderItemId = line.OrderItemId,
                quantity = line.Quantity,
                serialNumbers = line.SerialNumbers
            }),
            JsonOptions);
    }

    /// <summary>
    /// Dựng <c>@ResultsJson</c> cho <c>usp_Return_Inspect</c>: <c>[{"returnItemId":1,"result":1}]</c>
    /// (1 đạt, 2 không đạt). Proc yêu cầu nhập đủ tất cả dòng của yêu cầu.
    /// </summary>
    private static string BuildResultsJson(IReadOnlyList<InspectReturnItemInput> results)
    {
        if (results.Count == 0)
        {
            throw new BadRequestException("Phải nhập kết quả kiểm tra cho tất cả dòng hàng trả.");
        }

        var lines = new List<InspectionLine>(results.Count);

        foreach (var result in results)
        {
            if (result.ReturnItemId <= 0)
            {
                throw new BadRequestException("Có dòng kiểm tra thiếu returnItemId.");
            }

            if (result.Result is not (ReturnStatusText.InspectionPass or ReturnStatusText.InspectionFail))
            {
                throw new BadRequestException(
                    $"Dòng hàng trả {result.ReturnItemId}: kết quả kiểm tra chỉ nhận 1 (đạt) hoặc 2 (không đạt).");
            }

            lines.Add(new InspectionLine(result.ReturnItemId, result.Result));
        }

        if (lines.Select(line => line.ReturnItemId).Distinct().Count() != lines.Count)
        {
            throw new BadRequestException("Một dòng hàng trả xuất hiện nhiều lần trong kết quả kiểm tra.");
        }

        return JsonSerializer.Serialize(
            lines.Select(line => new { returnItemId = line.ReturnItemId, result = line.Result }),
            JsonOptions);
    }

    /// <summary>
    /// Chuẩn hóa danh sách serial của một dòng trả: không kèm serial → <c>null</c>; có thì không được rỗng,
    /// không trùng và tối đa 100 ký tự (varchar(100)).
    /// </summary>
    private static string[]? NormalizeSerials(IReadOnlyList<string>? serials, long orderItemId)
    {
        if (serials is null || serials.Count == 0)
        {
            return null;
        }

        var cleaned = serials.Select(serial => serial?.Trim() ?? string.Empty).ToArray();

        if (cleaned.Any(string.IsNullOrEmpty))
        {
            throw new BadRequestException($"Dòng hàng {orderItemId}: có số serial để trống.");
        }

        if (cleaned.Any(serial => serial.Length > SerialNumberMaxLength))
        {
            throw new BadRequestException($"Dòng hàng {orderItemId}: số serial tối đa {SerialNumberMaxLength} ký tự.");
        }

        if (cleaned.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cleaned.Length)
        {
            throw new BadRequestException($"Dòng hàng {orderItemId}: danh sách serial bị trùng.");
        }

        return cleaned;
    }

    /// <summary>Chuẩn hóa phân trang và cắt theo giới hạn proc để TotalPages khớp dữ liệu trả về.</summary>
    private static PageParams Page(int pageNumber, int pageSize, int maxPageSize)
        => new(pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 1 : Math.Min(pageSize, maxPageSize));

    /// <summary>Chuỗi rỗng/khoảng trắng → <c>null</c> (proc dùng giá trị mặc định); vượt độ dài cột → 400.</summary>
    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > maxLength)
        {
            throw new BadRequestException($"{fieldName} tối đa {maxLength} ký tự.");
        }

        return text;
    }

    /// <summary>Bắt buộc có nội dung và không vượt độ dài cột.</summary>
    private static string RequireText(string? value, int maxLength, string fieldName)
        => NormalizeOptional(value, maxLength, fieldName)
           ?? throw new BadRequestException($"{fieldName} không được để trống.");

    /// <summary>Giá trị phân trang đã chuẩn hóa.</summary>
    private readonly record struct PageParams(int PageNumber, int PageSize);

    /// <summary>Một dòng hàng trả đã kiểm tra, sẵn sàng serialize.</summary>
    private sealed record ReturnLine(long OrderItemId, int Quantity, string[]? SerialNumbers);

    /// <summary>Một dòng kết quả kiểm tra đã kiểm tra, sẵn sàng serialize.</summary>
    private sealed record InspectionLine(long ReturnItemId, byte Result);
}