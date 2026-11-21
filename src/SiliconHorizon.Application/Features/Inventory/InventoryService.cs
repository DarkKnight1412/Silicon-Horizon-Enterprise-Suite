using System.Text.Json;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Inventory.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Inventory;

/// <summary>
/// Hiện thực use case kho + serial qua stored procedure (Database/04_sp_inventory_suppliers.sql).
/// </summary>
/// <remarks>
/// Nguyên tắc nghiệp vụ:
/// <list type="bullet">
/// <item><b>Tồn kho</b>: <c>Physical</c> = số đơn vị đang ở kho, <c>Reserved</c> = đang giữ cho đơn,
/// <c>Available = Physical - Reserved</c>; chỉ proc được sửa tồn, API chỉ đọc qua <c>usp_Stock_GetLevels</c>.</item>
/// <item><b>StockMovements là sổ append-only</b>: mỗi bút toán do proc ghi, không có endpoint sửa/xóa;
/// thẻ kho chỉ để tra cứu lịch sử biến động (INV-03).</item>
/// <item><b>Serial không được đổi trạng thái tuỳ tiện</b>: chỉ 3 chuyển đổi thủ công được phép
/// (InStock→Defective, Defective→Scrapped, Defective→InStock) và bắt buộc có lý do; rời/vào InStock thì tồn thực tế ±1.</item>
/// <item><b>Kiểm kê lệch với hàng quản lý theo serial KHÔNG được duyệt bằng chênh lệch số lượng</b>:
/// phải xử lý từng serial bằng <c>usp_Serial_ChangeStatus</c> rồi kiểm kê lại (proc chủ động chặn).</item>
/// <item>Mọi thao tác ghi đều ép <c>@BranchId</c>/<c>@FromBranchId</c> từ token qua <see cref="InventoryAccessGuard"/>.</item>
/// <item>Không mở transaction ở tầng Application: mỗi proc đã tự quản lý giao dịch và khóa dòng tồn kho (NFR-01).</item>
/// </list>
/// </remarks>
public sealed class InventoryService : IInventoryService
{
    /// <summary>Tham số JSON của proc đọc bằng <c>OPENJSON</c> — dùng camelCase như tài liệu proc mô tả.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>SerialItems.SerialNumber varchar(100).</summary>
    private const int SerialNumberMaxLength = 100;

    /// <summary>PurchaseReceipts.SupplierInvoiceNo varchar(50).</summary>
    private const int SupplierInvoiceNoMaxLength = 50;

    /// <summary>@Keyword của usp_Stock_GetLevels là nvarchar(100).</summary>
    private const int KeywordMaxLength = 100;

    /// <summary>StockTakeItems.Reason / SerialStatusHistory.Note / @Note là nvarchar(300).</summary>
    private const int ReasonMaxLength = 300;

    /// <summary>usp_Stock_GetLevels và usp_Stock_GetMovements tự cắt @PageSize về tối đa 200.</summary>
    private const int StockPageSizeMax = 200;

    /// <summary>usp_PurchaseReceipt_List tự cắt @PageSize về tối đa 100.</summary>
    private const int ReceiptPageSizeMax = 100;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;
    private readonly InventoryAccessGuard _guard;

    public InventoryService(ISqlExecutor sqlExecutor, ICurrentUser currentUser, InventoryAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
        _guard = guard;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tồn kho (INV-02, INV-03)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PagedResult<StockLevelDto>> GetStockLevelsAsync(
        StockLevelQuery query,
        CancellationToken cancellationToken = default)
    {
        // Phạm vi chi nhánh ép từ token; Admin không gắn chi nhánh xem được toàn hệ thống (@BranchId = NULL).
        var branchId = _guard.BranchScope();
        var page = Page(query.PageNumber, query.PageSize, StockPageSizeMax);

        var rows = await _sqlExecutor.QueryAsync<StockLevelRow>(
            StoredProcedureNames.StockGetLevels,
            new
            {
                BranchId = branchId,
                ProductId = query.ProductId,
                CategoryId = query.CategoryId,
                Keyword = NormalizeOptional(query.Keyword, KeywordMaxLength, "Từ khóa"),
                BelowMinOnly = query.BelowMinOnly,
                PageNumber = page.PageNumber,
                PageSize = page.PageSize
            },
            cancellationToken);

        return PagedResult<StockLevelDto>.Create(
            rows.Select(StockLevelDto.FromRow).ToList(),
            page.PageNumber,
            page.PageSize,
            rows.Count > 0 ? rows[0].TotalCount : 0);
    }

    /// <inheritdoc />
    public async Task SetMinStockAsync(SetMinStockRequest request, CancellationToken cancellationToken = default)
    {
        // Mức tồn tối thiểu là cấu hình THEO CHI NHÁNH nên bắt buộc có chi nhánh trong token (QL/AD).
        var branchId = _guard.RequireBranchId();

        if (request.ProductId <= 0)
        {
            throw new BadRequestException("Thiếu sản phẩm cần đặt mức tồn tối thiểu.");
        }

        if (request.MinStock is < 0)
        {
            throw new BadRequestException("Mức tồn tối thiểu không được âm.");
        }

        // @MinStock = NULL: quay về dùng Products.MinStockDefault.
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.StockSetMin,
            new { BranchId = branchId, request.ProductId, request.MinStock },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<StockMovementDto>> GetStockMovementsAsync(
        StockMovementQuery query,
        CancellationToken cancellationToken = default)
    {
        // Proc yêu cầu @BranchId cụ thể (không nhận NULL) nên Admin không gắn chi nhánh cũng phải có chi nhánh.
        var branchId = _guard.RequireBranchId();

        if (query.ProductId <= 0)
        {
            throw new BadRequestException("Cần chỉ định sản phẩm để xem thẻ kho.");
        }

        var page = Page(query.PageNumber, query.PageSize, StockPageSizeMax);

        var rows = await _sqlExecutor.QueryAsync<StockMovementRow>(
            StoredProcedureNames.StockGetMovements,
            new
            {
                BranchId = branchId,
                ProductId = query.ProductId,
                FromDate = query.FromDate,
                ToDate = query.ToDate,
                PageNumber = page.PageNumber,
                PageSize = page.PageSize
            },
            cancellationToken);

        return PagedResult<StockMovementDto>.Create(
            rows.Select(StockMovementDto.FromRow).ToList(),
            page.PageNumber,
            page.PageSize,
            rows.Count > 0 ? rows[0].TotalCount : 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Nhập kho (INV-07..09, UC-13)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<CreatedPurchaseReceiptDto> CreatePurchaseReceiptAsync(
        CreatePurchaseReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        // Kho nhập về chi nhánh của thủ kho — ép từ token, KHÔNG nhận branchId từ client.
        var branchId = _guard.RequireBranchId();

        if (request.SupplierId <= 0)
        {
            throw new BadRequestException("Thiếu nhà cung cấp của phiếu nhập.");
        }

        if (request.Items.Count == 0)
        {
            throw new BadRequestException("Phiếu nhập phải có ít nhất một dòng hàng.");
        }

        // Sản phẩm RequiresSerial phải kèm đủ serial; proc kiểm tra lại rồi tạo phiếu + serial + tồn + công nợ trong 1 giao dịch.
        var itemsJson = BuildReceiptItemsJson(request.Items);

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CreatedPurchaseReceiptRow>(
            StoredProcedureNames.PurchaseReceiptCreate,
            new
            {
                request.SupplierId,
                BranchId = branchId,
                SupplierInvoiceNo = NormalizeOptional(
                    request.SupplierInvoiceNo,
                    SupplierInvoiceNoMaxLength,
                    "Số hóa đơn nhà cung cấp"),
                ItemsJson = itemsJson,
                UserId = _currentUser.UserId
            },
            cancellationToken)
            ?? throw new ConflictException("Không tạo được phiếu nhập kho.");

        return new CreatedPurchaseReceiptDto
        {
            ReceiptId = row.ReceiptId,
            ReceiptCode = row.ReceiptCode,
            TotalAmount = row.TotalAmount
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<PurchaseReceiptListItemDto>> ListPurchaseReceiptsAsync(
        PurchaseReceiptListQuery query,
        CancellationToken cancellationToken = default)
    {
        // Admin không gắn chi nhánh xem được tất cả phiếu nhập (@BranchId = NULL).
        var branchId = _guard.BranchScope();
        var page = Page(query.PageNumber, query.PageSize, ReceiptPageSizeMax);

        var rows = await _sqlExecutor.QueryAsync<PurchaseReceiptListItemRow>(
            StoredProcedureNames.PurchaseReceiptList,
            new
            {
                SupplierId = query.SupplierId,
                BranchId = branchId,
                FromDate = query.FromDate,
                ToDate = query.ToDate,
                PageNumber = page.PageNumber,
                PageSize = page.PageSize
            },
            cancellationToken);

        return PagedResult<PurchaseReceiptListItemDto>.Create(
            rows.Select(PurchaseReceiptListItemDto.FromRow).ToList(),
            page.PageNumber,
            page.PageSize,
            rows.Count > 0 ? rows[0].TotalCount : 0);
    }

    /// <inheritdoc />
    public async Task<PurchaseReceiptDetailDto> GetPurchaseReceiptAsync(
        long receiptId,
        CancellationToken cancellationToken = default)
    {
        // usp_PurchaseReceipt_Get trả 3 result set: header, dòng hàng, serial — đọc tuần tự đúng thứ tự.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.PurchaseReceiptGet,
            new { ReceiptId = receiptId },
            cancellationToken);

        var header = await results.ReadFirstOrDefaultAsync<PurchaseReceiptHeaderRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phiếu nhập Id = {receiptId}.");

        // Phiếu nhập thuộc chi nhánh nào thì chỉ nhân viên chi nhánh đó xem được (Admin xem tất cả).
        _guard.EnsureBranch(header.BranchId, "xem phiếu nhập");

        var items = await results.ReadListAsync<PurchaseReceiptItemRow>(cancellationToken);
        var serials = await results.ReadListAsync<PurchaseReceiptSerialRow>(cancellationToken);

        return new PurchaseReceiptDetailDto
        {
            Header = PurchaseReceiptHeaderDto.FromRow(header),
            Items = items.Select(PurchaseReceiptItemDto.FromRow).ToList(),
            Serials = serials.Select(PurchaseReceiptSerialDto.FromRow).ToList()
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chuyển kho nội bộ (INV-06, UC-14)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<CreatedStockTransferDto> CreateStockTransferAsync(
        CreateStockTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        // Chi nhánh ĐI ép từ token; client chỉ chọn chi nhánh đến.
        var fromBranchId = _guard.RequireBranchId();

        if (request.ToBranchId <= 0)
        {
            throw new BadRequestException("Thiếu chi nhánh nhận hàng của phiếu chuyển kho.");
        }

        if (request.ToBranchId == fromBranchId)
        {
            throw new BadRequestException("Chi nhánh đi và chi nhánh đến phải khác nhau.");
        }

        if (request.Items.Count == 0)
        {
            throw new BadRequestException("Phiếu chuyển kho phải có ít nhất một dòng hàng.");
        }

        // Phiếu tạo ở trạng thái Nháp (0) — CHƯA trừ tồn; chỉ khi gọi /ship proc mới trừ tồn chi nhánh đi.
        var itemsJson = BuildTransferItemsJson(request.Items);

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CreatedStockTransferRow>(
            StoredProcedureNames.StockTransferCreate,
            new
            {
                FromBranchId = fromBranchId,
                request.ToBranchId,
                ItemsJson = itemsJson,
                UserId = _currentUser.UserId
            },
            cancellationToken)
            ?? throw new ConflictException("Không tạo được phiếu chuyển kho.");

        return new CreatedStockTransferDto { TransferId = row.TransferId, TransferCode = row.TransferCode };
    }

    /// <inheritdoc />
    public async Task<StockTransferDetailDto> GetStockTransferAsync(
        long transferId,
        CancellationToken cancellationToken = default)
    {
        // usp_StockTransfer_Get trả 3 result set: header (kèm tên 2 chi nhánh), dòng hàng, serial.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.StockTransferGet,
            new { TransferId = transferId },
            cancellationToken);

        var header = await results.ReadFirstOrDefaultAsync<StockTransferHeaderRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phiếu chuyển kho Id = {transferId}.");

        _guard.EnsureTransferView(header);

        var items = await results.ReadListAsync<StockTransferItemRow>(cancellationToken);
        var serials = await results.ReadListAsync<StockTransferSerialRow>(cancellationToken);

        return new StockTransferDetailDto
        {
            Header = StockTransferHeaderDto.FromRow(header),
            Items = items.Select(StockTransferItemDto.FromRow).ToList(),
            Serials = serials.Select(StockTransferSerialDto.FromRow).ToList()
        };
    }

    /// <inheritdoc />
    public async Task ShipStockTransferAsync(long transferId, CancellationToken cancellationToken = default)
    {
        var header = await _guard.LoadTransferAsync(transferId, cancellationToken);

        // Chỉ chi nhánh ĐI được xuất kho: proc trừ tồn thực tế chi nhánh đi và chuyển serial InStock → InTransfer.
        _guard.EnsureTransferShip(header);

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.StockTransferShip,
            new { TransferId = transferId, UserId = _currentUser.UserId },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReceiveStockTransferAsync(long transferId, CancellationToken cancellationToken = default)
    {
        var header = await _guard.LoadTransferAsync(transferId, cancellationToken);

        // Chỉ chi nhánh ĐẾN được nhận hàng: proc cộng tồn chi nhánh đến và đổi BranchId của serial về chi nhánh đến.
        _guard.EnsureTransferReceive(header);

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.StockTransferReceive,
            new { TransferId = transferId, UserId = _currentUser.UserId },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task CancelStockTransferAsync(long transferId, CancellationToken cancellationToken = default)
    {
        var header = await _guard.LoadTransferAsync(transferId, cancellationToken);

        // Proc chỉ hủy được phiếu Nháp (0); nếu không còn nháp → SQL 50002 → 409.
        _guard.EnsureTransferCancel(header);

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.StockTransferCancel,
            new { TransferId = transferId, UserId = _currentUser.UserId },
            cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Kiểm kê (INV-04, INV-05, UC-15, UC-21)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<CreatedStockTakeDto> CreateStockTakeAsync(CancellationToken cancellationToken = default)
    {
        // Đợt kiểm kê luôn thuộc chi nhánh của thủ kho đang đứng (ép từ token).
        var branchId = _guard.RequireBranchId();

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CreatedStockTakeRow>(
            StoredProcedureNames.StockTakeCreate,
            new { BranchId = branchId, UserId = _currentUser.UserId },
            cancellationToken)
            ?? throw new ConflictException("Không mở được đợt kiểm kê.");

        return new CreatedStockTakeDto { StockTakeId = row.StockTakeId, StockTakeCode = row.StockTakeCode };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockTakeCountDto>> SubmitStockTakeCountsAsync(
        long stockTakeId,
        SubmitStockTakeCountsRequest request,
        CancellationToken cancellationToken = default)
    {
        // Proc không nhận chi nhánh nên API phải tự chặn: chỉ chi nhánh sở hữu đợt kiểm kê mới được nhập số đếm.
        var header = await _guard.LoadStockTakeAsync(stockTakeId, cancellationToken);
        _guard.EnsureStockTakeBranch(header, "nhập số đếm của đợt kiểm kê");

        var countsJson = request.Counts.Count == 0 ? null : BuildCountsJson(request.Counts);

        // @Finalize = 1: chuyển Đang kiểm kê (1) → Chờ duyệt (2); tồn CHƯA bị sửa, chỉ đổi khi Quản lý duyệt.
        var rows = await _sqlExecutor.QueryAsync<StockTakeCountRow>(
            StoredProcedureNames.StockTakeSubmitCounts,
            new
            {
                StockTakeId = stockTakeId,
                CountsJson = countsJson,
                Finalize = request.Finalize,
                UserId = _currentUser.UserId
            },
            cancellationToken);

        return rows.Select(StockTakeCountDto.FromRow).ToList();
    }

    /// <inheritdoc />
    public async Task ApproveStockTakeAsync(
        long stockTakeId,
        ApproveStockTakeRequest request,
        CancellationToken cancellationToken = default)
    {
        var header = await _guard.LoadStockTakeAsync(stockTakeId, cancellationToken);
        _guard.EnsureStockTakeBranch(header, "duyệt đợt kiểm kê");

        // Proc: chỉ duyệt khi phiếu ở Chờ duyệt (2); hàng RequiresSerial bị lệch thì buộc xử lý từng serial
        // (usp_Serial_ChangeStatus) rồi kiểm kê lại — API không tự điều chỉnh số lượng thay người dùng.
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.StockTakeApprove,
            new
            {
                StockTakeId = stockTakeId,
                Approve = request.Approve,
                UserId = RequireUserId(),
                Note = NormalizeOptional(request.Note, ReasonMaxLength, "Ghi chú duyệt kiểm kê")
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StockTakeDetailDto> GetStockTakeAsync(long stockTakeId, CancellationToken cancellationToken = default)
    {
        // usp_StockTake_Get trả 2 result set: header (SELECT * StockTakes) và các dòng kiểm kê.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.StockTakeGet,
            new { StockTakeId = stockTakeId },
            cancellationToken);

        var header = await results.ReadFirstOrDefaultAsync<StockTakeHeaderRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phiếu kiểm kê Id = {stockTakeId}.");

        _guard.EnsureStockTakeBranch(header, "xem đợt kiểm kê");

        var items = await results.ReadListAsync<StockTakeItemRow>(cancellationToken);

        return new StockTakeDetailDto
        {
            Header = StockTakeHeaderDto.FromRow(header),
            Items = items.Select(StockTakeItemDto.FromRow).ToList()
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Serial (SER-01..04)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SerialLookupDto> LookupSerialAsync(
        string serialNumber,
        long? productId = null,
        CancellationToken cancellationToken = default)
    {
        var serial = RequireText(serialNumber, SerialNumberMaxLength, "Số serial");

        // Khách chỉ tra cứu được hàng thuộc đơn của mình: ép @CustomerId từ token, KHÔNG nhận từ client.
        long? customerId = null;
        if (_currentUser.IsInRole(RoleCodes.Customer))
        {
            customerId = _currentUser.CustomerId
                ?? throw new ForbiddenException("Tài khoản khách hàng chưa gắn hồ sơ khách nên không tra cứu được serial.");
        }

        // 3 result set: thông tin serial, lịch sử trạng thái, phiếu bảo hành.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.SerialLookup,
            new { SerialNumber = serial, ProductId = productId, CustomerId = customerId },
            cancellationToken);

        var info = await results.ReadFirstOrDefaultAsync<SerialInfoRow>(cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy serial {serial}.");

        var history = await results.ReadListAsync<SerialStatusHistoryRow>(cancellationToken);
        var tickets = await results.ReadListAsync<SerialWarrantyTicketRow>(cancellationToken);

        return new SerialLookupDto
        {
            Serial = SerialInfoDto.FromRow(info),
            StatusHistory = history.Select(SerialStatusHistoryDto.FromRow).ToList(),
            WarrantyTickets = tickets.Select(SerialWarrantyTicketDto.FromRow).ToList()
        };
    }

    /// <inheritdoc />
    public async Task ChangeSerialStatusAsync(
        long serialItemId,
        ChangeSerialStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (serialItemId <= 0)
        {
            throw new BadRequestException("Thiếu serial cần đổi trạng thái.");
        }

        // Chỉ 3 chuyển đổi được phép thủ công; proc kiểm tra lại cặp (FromStatus, ToStatus) thực tế.
        if (request.ToStatus is not (InventoryStatusText.SerialInStock or InventoryStatusText.SerialDefective or InventoryStatusText.SerialScrapped))
        {
            throw new BadRequestException(
                "Chỉ được đổi trạng thái serial sang 0 (Trong kho), 7 (Hàng lỗi) hoặc 9 (Đã hủy).");
        }

        // Bắt buộc có lý do (proc cũng chặn) — nhật ký AUD-01 lưu nguyên văn lý do này.
        var reason = RequireText(request.Reason, ReasonMaxLength, "Lý do đổi trạng thái serial");

        // QL/AD: Quản lý phải gắn chi nhánh để biết đang thao tác trong chi nhánh nào (Admin bỏ qua).
        // Lưu ý: usp_Serial_ChangeStatus chỉ nhận @SerialItemId nên KHÔNG kiểm tra được chi nhánh của serial;
        // muốn siết chặt theo chi nhánh cần bổ sung proc đọc serial theo Id (xem báo cáo).
        if (!_currentUser.IsInRole(RoleCodes.Admin))
        {
            _ = _guard.RequireBranchId();
        }

        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.SerialChangeStatus,
            new
            {
                SerialItemId = serialItemId,
                request.ToStatus,
                Reason = reason,
                UserId = RequireUserId()
            },
            cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Người dùng đang gọi — <c>usp_Serial_ChangeStatus</c>/<c>usp_StockTake_Approve</c> yêu cầu @UserId NOT NULL.</summary>
    private long RequireUserId()
        => _currentUser.UserId
           ?? throw new ForbiddenException("Token thiếu định danh người dùng nên không thực hiện được thao tác này.");

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

    /// <summary>
    /// Dựng <c>@ItemsJson</c> cho <c>usp_PurchaseReceipt_Create</c>:
    /// <c>[{"productId":1,"quantity":2,"unitCost":1500000,"serials":["SN001","SN002"]}]</c>.
    /// Hàng không quản lý serial: <c>serials = null</c> để proc bỏ qua (proc báo lỗi nếu sản phẩm cần serial mà thiếu).
    /// </summary>
    private static string BuildReceiptItemsJson(IReadOnlyList<PurchaseReceiptItemInput> items)
    {
        var lines = new List<ReceiptLine>(items.Count);

        foreach (var item in items)
        {
            if (item.ProductId <= 0)
            {
                throw new BadRequestException("Có dòng nhập thiếu sản phẩm.");
            }

            if (item.Quantity <= 0)
            {
                throw new BadRequestException($"Sản phẩm {item.ProductId}: số lượng nhập phải lớn hơn 0.");
            }

            if (item.UnitCost < 0)
            {
                throw new BadRequestException($"Sản phẩm {item.ProductId}: đơn giá nhập không được âm.");
            }

            lines.Add(new ReceiptLine(
                item.ProductId,
                item.Quantity,
                item.UnitCost,
                NormalizeSerials(item.Serials, item.Quantity, item.ProductId)));
        }

        EnsureNoDuplicateProducts(lines.Select(line => line.ProductId), "phiếu nhập");

        return JsonSerializer.Serialize(
            lines.Select(line => new
            {
                productId = line.ProductId,
                quantity = line.Quantity,
                unitCost = line.UnitCost,
                serials = line.Serials
            }),
            JsonOptions);
    }

    /// <summary>Dựng <c>@ItemsJson</c> cho <c>usp_StockTransfer_Create</c> (không có đơn giá).</summary>
    private static string BuildTransferItemsJson(IReadOnlyList<StockTransferItemInput> items)
    {
        var lines = new List<TransferLine>(items.Count);

        foreach (var item in items)
        {
            if (item.ProductId <= 0)
            {
                throw new BadRequestException("Có dòng chuyển kho thiếu sản phẩm.");
            }

            if (item.Quantity <= 0)
            {
                throw new BadRequestException($"Sản phẩm {item.ProductId}: số lượng chuyển phải lớn hơn 0.");
            }

            lines.Add(new TransferLine(
                item.ProductId,
                item.Quantity,
                NormalizeSerials(item.Serials, item.Quantity, item.ProductId)));
        }

        EnsureNoDuplicateProducts(lines.Select(line => line.ProductId), "phiếu chuyển kho");

        return JsonSerializer.Serialize(
            lines.Select(line => new { productId = line.ProductId, quantity = line.Quantity, serials = line.Serials }),
            JsonOptions);
    }

    /// <summary>Dựng <c>@CountsJson</c> cho <c>usp_StockTake_SubmitCounts</c> — hàng serial bị lệch sẽ bị proc chặn khi duyệt.</summary>
    private static string BuildCountsJson(IReadOnlyList<StockTakeCountInput> counts)
    {
        var lines = new List<CountLine>(counts.Count);

        foreach (var count in counts)
        {
            if (count.ProductId <= 0)
            {
                throw new BadRequestException("Có dòng kiểm kê thiếu sản phẩm.");
            }

            if (count.CountedQty < 0)
            {
                throw new BadRequestException($"Sản phẩm {count.ProductId}: số lượng đếm không được âm.");
            }

            lines.Add(new CountLine(
                count.ProductId,
                count.CountedQty,
                NormalizeOptional(count.Reason, ReasonMaxLength, "Lý do lệch")));
        }

        EnsureNoDuplicateProducts(lines.Select(line => line.ProductId), "phiếu kiểm kê");

        return JsonSerializer.Serialize(
            lines.Select(line => new { productId = line.ProductId, countedQty = line.CountedQty, reason = line.Reason }),
            JsonOptions);
    }

    /// <summary>Proc từ chối một sản phẩm xuất hiện nhiều lần trong cùng chứng từ — chặn sớm để trả 400 rõ ràng.</summary>
    private static void EnsureNoDuplicateProducts(IEnumerable<long> productIds, string documentName)
    {
        var duplicates = productIds
            .GroupBy(productId => productId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new BadRequestException(
                $"Một sản phẩm xuất hiện nhiều lần trong {documentName} (productId: {string.Join(", ", duplicates)}).");
        }
    }

    /// <summary>
    /// Chuẩn hóa danh sách serial: không kèm serial → <c>null</c>; nếu có thì phải đủ bằng số lượng,
    /// không trùng, không rỗng và tối đa 100 ký tự (SerialItems.SerialNumber varchar(100)).
    /// </summary>
    private static string[]? NormalizeSerials(IReadOnlyList<string>? serials, int quantity, long productId)
    {
        if (serials is null || serials.Count == 0)
        {
            return null;
        }

        var cleaned = serials.Select(serial => serial?.Trim() ?? string.Empty).ToArray();

        if (cleaned.Any(string.IsNullOrEmpty))
        {
            throw new BadRequestException($"Sản phẩm {productId}: có số serial để trống.");
        }

        if (cleaned.Any(serial => serial.Length > SerialNumberMaxLength))
        {
            throw new BadRequestException($"Sản phẩm {productId}: số serial tối đa {SerialNumberMaxLength} ký tự.");
        }

        if (cleaned.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cleaned.Length)
        {
            throw new BadRequestException($"Sản phẩm {productId}: danh sách serial bị trùng.");
        }

        if (cleaned.Length != quantity)
        {
            throw new BadRequestException($"Sản phẩm {productId}: số serial ({cleaned.Length}) phải bằng số lượng ({quantity}).");
        }

        return cleaned;
    }

    /// <summary>Giá trị phân trang đã chuẩn hóa.</summary>
    private readonly record struct PageParams(int PageNumber, int PageSize);

    /// <summary>Một dòng phiếu nhập đã kiểm tra, sẵn sàng serialize.</summary>
    private sealed record ReceiptLine(long ProductId, int Quantity, decimal UnitCost, string[]? Serials);

    /// <summary>Một dòng phiếu chuyển kho đã kiểm tra.</summary>
    private sealed record TransferLine(long ProductId, int Quantity, string[]? Serials);

    /// <summary>Một dòng đếm kiểm kê đã kiểm tra.</summary>
    private sealed record CountLine(long ProductId, int CountedQty, string? Reason);
}







