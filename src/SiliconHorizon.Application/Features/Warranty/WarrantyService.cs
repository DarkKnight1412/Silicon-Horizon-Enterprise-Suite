using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Warranty.Dto;

namespace SiliconHorizon.Application.Features.Warranty;

/// <summary>
/// Hiện thực use case bảo hành qua stored procedure (Database/05_sp_warranty_returns_commission.sql).
/// </summary>
/// <remarks>
/// Nguyên tắc nghiệp vụ:
/// <list type="bullet">
/// <item><b>Vòng đời phiếu</b>: 0 Received → 1 Diagnosing → 2 SentToVendor → (3 Repaired | 4 Replaced | 5 Rejected)
/// → 6 ReturnedToCustomer. Chuyển sai cặp trạng thái do proc chặn → SQL 50002 → HTTP 409.</item>
/// <item><b>Hết hạn bảo hành vẫn tiếp nhận nhưng tự từ chối</b> (WAR-05): phiếu vào thẳng Rejected kèm lý do,
/// serial giữ nguyên Sold để không chiếm tồn RMA.</item>
/// <item><b>Đổi mới</b> yêu cầu serial thay thế đang InStock cùng sản phẩm: serial cũ → Replaced, serial mới → Sold
/// kế thừa hạn bảo hành còn lại, tồn thực tế của chi nhánh giảm 1.</item>
/// <item>Mọi thao tác ghi đều ép <c>@BranchId</c>/<c>@UserId</c> từ token qua <see cref="WarrantyAccessGuard"/>,
/// không nhận từ client (BRD 2.2).</item>
/// <item>Không mở transaction ở tầng Application: mỗi proc đã tự quản lý giao dịch (NFR-01).</item>
/// </list>
/// </remarks>
public sealed class WarrantyService : IWarrantyService
{
    /// <summary>SerialItems.SerialNumber varchar(100) / <c>@ReplacementSerialNumber varchar(100)</c>.</summary>
    private const int SerialNumberMaxLength = 100;

    /// <summary><c>@IssueDescription nvarchar(1000)</c> (WarrantyTickets.IssueDescription).</summary>
    private const int IssueDescriptionMaxLength = 1000;

    /// <summary><c>@ConditionNotes nvarchar(1000)</c> (WarrantyTickets.ConditionNotes).</summary>
    private const int ConditionNotesMaxLength = 1000;

    /// <summary><c>@RejectReason</c> / <c>@RejectionReason</c> nvarchar(500) (WarrantyTickets.RejectionReason).</summary>
    private const int RejectionReasonMaxLength = 500;

    /// <summary><c>@Note nvarchar(500)</c> (WarrantyTicketHistory.Note).</summary>
    private const int NoteMaxLength = 500;

    /// <summary>WarrantyTicketAttachments.Url varchar(500).</summary>
    private const int UrlMaxLength = 500;

    /// <summary><c>@Keyword nvarchar(100)</c> của usp_Warranty_List.</summary>
    private const int KeywordMaxLength = 100;

    /// <summary>usp_Warranty_List tự cắt <c>@PageSize</c> về tối đa 100 — dùng cùng mức để TotalPages khớp dữ liệu.</summary>
    private const int ListPageSizeMax = 100;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly WarrantyAccessGuard _guard;

    public WarrantyService(ISqlExecutor sqlExecutor, WarrantyAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task<ReceivedWarrantyTicketDto> ReceiveAsync(
        ReceiveWarrantyTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        // Tiếp nhận tại quầy: chi nhánh LUÔN ép từ token (BRD 2.2), KHÔNG nhận branchId từ body.
        var branchId = _guard.RequireBranchId();

        var serialNumber = NormalizeOptional(request.SerialNumber, SerialNumberMaxLength, "Số serial");
        var issueDescription = RequireText(request.IssueDescription, IssueDescriptionMaxLength, "Mô tả lỗi");

        // Proc yêu cầu @SerialNumber (hàng có serial) HOẶC @OrderItemId (hàng không serial) — chặn sớm để trả 400 rõ ràng.
        if (serialNumber is null && request.OrderItemId is not > 0)
        {
            throw new BadRequestException(
                "Phải cung cấp số serial (hàng quản lý theo serial) hoặc orderItemId (hàng không quản lý theo serial).");
        }

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<WarrantyReceiveResultRow>(
            StoredProcedureNames.WarrantyReceive,
            new
            {
                BranchId = branchId,
                IssueDescription = issueDescription,
                SerialNumber = serialNumber,
                ProductId = request.ProductId is > 0 ? request.ProductId : null,
                OrderItemId = request.OrderItemId is > 0 ? request.OrderItemId : null,
                ConditionNotes = NormalizeOptional(request.ConditionNotes, ConditionNotesMaxLength, "Tình trạng khi nhận"),
                RejectReason = NormalizeOptional(request.RejectReason, RejectionReasonMaxLength, "Lý do từ chối"),
                UserId = _guard.RequireUserId()
            },
            cancellationToken)
            ?? throw new ConflictException("Không tiếp nhận được phiếu bảo hành.");

        return ReceivedWarrantyTicketDto.FromRow(row);
    }

    /// <inheritdoc />
    public async Task ChangeStatusAsync(
        long ticketId,
        ChangeWarrantyStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            throw new BadRequestException("Thiếu phiếu bảo hành cần chuyển trạng thái.");
        }

        // Chỉ nhận trạng thái đích 1..6; 0 Received là trạng thái khởi tạo, không chuyển ngược về được.
        if (!WarrantyStatusText.IsAllowedTarget(request.ToStatus))
        {
            throw new BadRequestException(
                "Trạng thái đích chỉ nhận 1 (Chẩn đoán), 2 (Gửi hãng), 3 (Sửa xong), 4 (Đổi mới), 5 (Từ chối), 6 (Trả khách).");
        }

        var note = NormalizeOptional(request.Note, NoteMaxLength, "Ghi chú xử lý");
        var replacementSerial = NormalizeOptional(request.ReplacementSerialNumber, SerialNumberMaxLength, "Số serial thay thế");
        var rejectionReason = NormalizeOptional(request.RejectionReason, RejectionReasonMaxLength, "Lý do từ chối");

        // WAR-06/WAR-07: đổi mới bắt buộc có serial thay thế; từ chối bắt buộc có lý do (proc cũng kiểm tra lại).
        if (request.ToStatus == WarrantyStatusText.Replaced && replacementSerial is null)
        {
            throw new BadRequestException("Đổi mới bảo hành phải kèm số serial thay thế (replacementSerialNumber).");
        }

        if (request.ToStatus == WarrantyStatusText.Rejected && rejectionReason is null)
        {
            throw new BadRequestException("Từ chối bảo hành phải kèm lý do (rejectionReason).");
        }

        if (request.Fee is < 0)
        {
            throw new BadRequestException("Phí sửa chữa thu của khách không được âm.");
        }

        // Phạm vi chi nhánh: proc không kiểm tra chi nhánh của phiếu nên phải đọc phiếu trước (403 nếu khác chi nhánh).
        var ticket = await _guard.LoadTicketAsync(ticketId, customerId: null, cancellationToken);
        _guard.EnsureBranch(ticket.BranchId, "xử lý phiếu bảo hành");

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.WarrantyChangeStatus,
            new
            {
                TicketId = ticketId,
                request.ToStatus,
                Note = note,
                ReplacementSerialNumber = replacementSerial,
                RejectionReason = rejectionReason,
                request.Fee,
                UserId = _guard.RequireUserId()
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAttachmentAsync(
        long ticketId,
        AddWarrantyAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            throw new BadRequestException("Thiếu phiếu bảo hành cần đính kèm tài liệu.");
        }

        var url = RequireText(request.Url, UrlMaxLength, "Đường dẫn tài liệu");

        // Proc chỉ kiểm tra phiếu tồn tại, không kiểm tra chi nhánh → API phải chặn nhân viên chi nhánh khác.
        var ticket = await _guard.LoadTicketAsync(ticketId, customerId: null, cancellationToken);
        _guard.EnsureBranch(ticket.BranchId, "đính kèm tài liệu vào phiếu bảo hành");

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.WarrantyAddAttachment,
            new { TicketId = ticketId, Url = url },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<WarrantyTicketListItemDto>> ListAsync(
        WarrantyTicketListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Page(query.PageNumber, query.PageSize, ListPageSizeMax);

        // Nhân viên: @BranchId ép từ token (CustomerId = null). Khách: @CustomerId ép từ token (BranchId = null).
        var scope = _guard.ResolveReadScope();

        var rows = await _sqlExecutor.QueryAsync<WarrantyTicketListItemRow>(
            StoredProcedureNames.WarrantyList,
            new
            {
                scope.BranchId,
                scope.CustomerId,
                Status = query.Status,
                Keyword = NormalizeOptional(query.Keyword, KeywordMaxLength, "Từ khóa"),
                page.PageNumber,
                page.PageSize
            },
            cancellationToken);

        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;

        return PagedResult<WarrantyTicketListItemDto>.Create(
            rows.Select(WarrantyTicketListItemDto.FromRow).ToList(),
            page.PageNumber,
            page.PageSize,
            totalCount);
    }

    /// <inheritdoc />
    public async Task<WarrantyTicketDetailDto> GetAsync(long ticketId, CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            throw new BadRequestException("Thiếu phiếu bảo hành cần xem.");
        }

        // Khách: proc tự giới hạn theo @CustomerId (phiếu của người khác → 50001 → 404).
        // Nhân viên: đọc bằng @CustomerId = NULL rồi tự kiểm tra chi nhánh bên dưới.
        long? customerId = _guard.IsStaff ? null : _guard.RequireCustomerId();

        // 3 result set: header phiếu, lịch sử chuyển trạng thái, danh sách đính kèm.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.WarrantyGet,
            new { TicketId = ticketId, CustomerId = customerId },
            cancellationToken);

        var ticket = await results.ReadFirstOrDefaultAsync<WarrantyTicketRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phiếu bảo hành Id = {ticketId}.");

        if (_guard.IsStaff)
        {
            _guard.EnsureBranch(ticket.BranchId, "xem phiếu bảo hành");
        }

        var history = await results.ReadListAsync<WarrantyTicketHistoryRow>(cancellationToken);
        var attachments = await results.ReadListAsync<WarrantyTicketAttachmentRow>(cancellationToken);

        return new WarrantyTicketDetailDto
        {
            Ticket = WarrantyTicketDto.FromRow(ticket),
            History = history.Select(WarrantyTicketHistoryDto.FromRow).ToList(),
            Attachments = attachments
                .Select(row => new WarrantyTicketAttachmentDto { Id = row.Id, Url = row.Url })
                .ToList()
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

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
}