using System.Text.Json;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Inventory.Dto;
using SiliconHorizon.Domain.Entities.Inventory;

namespace SiliconHorizon.Application.Features.Inventory;

/// <summary>
/// Hiện thực use case nhà cung cấp (SUP-01..03).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>CRUD danh mục nhà cung cấp</b>: EF Core qua <see cref="IReadRepository{TEntity}"/>/
/// <see cref="IWriteRepository{TEntity}"/> — đúng ghi chú "EF <c>Suppliers</c>" trong
/// <c>src/SiliconHorizon.Application/Features/README.md</c>. Bảng nhỏ, ít thay đổi nên đọc toàn bộ rồi
/// lọc/phân trang trong bộ nhớ (giống <c>CatalogReferenceService</c>).</item>
/// <item><b>Trả hàng nhà cung cấp</b>: <c>usp_SupplierReturn_Create</c> — serial trả phải ở trạng thái
/// Defective tại chi nhánh của người gọi, hàng không serial thì trừ tồn thực tế, và có thể ghi giảm công nợ.</item>
/// <item><b>Thanh toán &amp; công nợ</b>: <c>usp_SupplierPayment_Record</c>, <c>usp_Supplier_GetLedger</c> —
/// sổ <c>SupplierLedger</c> là append-only: dương = tăng nợ phải trả, âm = giảm nợ (thanh toán / trả hàng).</item>
/// </list>
/// </remarks>
public sealed class SupplierService : ISupplierService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Suppliers.Code varchar(30).</summary>
    private const int CodeMaxLength = 30;

    /// <summary>Suppliers.Name nvarchar(200).</summary>
    private const int NameMaxLength = 200;

    /// <summary>Suppliers.TaxCode varchar(20).</summary>
    private const int TaxCodeMaxLength = 20;

    /// <summary>Suppliers.Phone varchar(20).</summary>
    private const int PhoneMaxLength = 20;

    /// <summary>Suppliers.Address nvarchar(300).</summary>
    private const int AddressMaxLength = 300;

    /// <summary>SerialItems.SerialNumber varchar(100).</summary>
    private const int SerialNumberMaxLength = 100;

    /// <summary>usp_Supplier_GetLedger tự cắt @PageSize về tối đa 200.</summary>
    private const int LedgerPageSizeMax = 200;

    private readonly IReadRepository<Supplier> _readRepository;
    private readonly IWriteRepository<Supplier> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ISqlExecutor _sqlExecutor;
    private readonly InventoryAccessGuard _guard;

    public SupplierService(
        IReadRepository<Supplier> readRepository,
        IWriteRepository<Supplier> writeRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        ISqlExecutor sqlExecutor,
        InventoryAccessGuard guard)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _sqlExecutor = sqlExecutor;
        _guard = guard;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Danh mục nhà cung cấp (SUP-01) — EF Core
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PagedResult<SupplierDto>> ListAsync(
        SupplierListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();
        var suppliers = await _readRepository.ListAsync(cancellationToken);

        var filtered = suppliers
            .Where(supplier => query.IsActive is null || supplier.IsActive == query.IsActive)
            .Where(supplier => string.IsNullOrEmpty(keyword)
                               || supplier.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                               || supplier.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(supplier => supplier.Name)
            .ToList();

        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 1 : Math.Min(query.PageSize, LedgerPageSizeMax);

        var items = filtered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(SupplierDto.FromEntity)
            .ToList();

        return PagedResult<SupplierDto>.Create(items, pageNumber, pageSize, filtered.Count);
    }

    /// <inheritdoc />
    public async Task<SupplierDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => SupplierDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var code = RequireText(request.Code, CodeMaxLength, "Mã nhà cung cấp").ToUpperInvariant();
        var name = RequireText(request.Name, NameMaxLength, "Tên nhà cung cấp");
        var taxCode = NormalizeOptional(request.TaxCode, TaxCodeMaxLength, "Mã số thuế");
        var phone = NormalizeOptional(request.Phone, PhoneMaxLength, "Điện thoại");
        var address = NormalizeOptional(request.Address, AddressMaxLength, "Địa chỉ");

        if (request.PaymentTermDays < 0)
        {
            throw new BadRequestException("Số ngày được nợ không được âm.");
        }

        // UQ_Suppliers_Code: chặn trùng mã trước khi ghi để trả 409 thay vì lỗi SQL thô.
        var existing = await _readRepository.ListAsync(cancellationToken);
        if (existing.Any(supplier => string.Equals(supplier.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"Mã nhà cung cấp {code} đã tồn tại.");
        }

        var supplier = new Supplier
        {
            Code = code,
            Name = name,
            TaxCode = taxCode,
            Phone = phone,
            Address = address,
            PaymentTermDays = request.PaymentTermDays,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(supplier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SupplierDto.FromEntity(supplier);
    }

    /// <inheritdoc />
    public async Task<SupplierDto> UpdateAsync(
        long id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplier = await GetOrThrowAsync(id, cancellationToken);

        if (request.PaymentTermDays < 0)
        {
            throw new BadRequestException("Số ngày được nợ không được âm.");
        }

        // Không đổi Code (đang được PurchaseReceipts/SupplierLedger tham chiếu) và không xóa cứng:
        // ngừng hợp tác chỉ đặt IsActive = false, proc nhập kho sẽ từ chối nhà cung cấp đã ngừng hoạt động.
        supplier.Name = RequireText(request.Name, NameMaxLength, "Tên nhà cung cấp");
        supplier.TaxCode = NormalizeOptional(request.TaxCode, TaxCodeMaxLength, "Mã số thuế");
        supplier.Phone = NormalizeOptional(request.Phone, PhoneMaxLength, "Điện thoại");
        supplier.Address = NormalizeOptional(request.Address, AddressMaxLength, "Địa chỉ");
        supplier.PaymentTermDays = request.PaymentTermDays;
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = _clock.UtcNow;
        supplier.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SupplierDto.FromEntity(supplier);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trả hàng nhà cung cấp (SUP-02) — stored procedure
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<CreatedSupplierReturnDto> CreateSupplierReturnAsync(
        CreateSupplierReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        // Hàng trả đi từ chi nhánh của người gọi — ép từ token, KHÔNG nhận branchId từ client.
        var branchId = _guard.RequireBranchId();

        if (request.SupplierId <= 0)
        {
            throw new BadRequestException("Thiếu nhà cung cấp nhận hàng trả.");
        }

        if (request.Items.Count == 0)
        {
            throw new BadRequestException("Phiếu trả hàng phải có ít nhất một dòng hàng.");
        }

        if (request.CreditAmount < 0)
        {
            throw new BadRequestException("Số tiền ghi giảm công nợ không được âm.");
        }

        // Serial trả phải đang ở trạng thái Defective (7) tại chi nhánh — proc kiểm tra lại và chuyển sang
        // ReturnedToSupplier (8); hàng không serial thì proc trừ tồn thực tế.
        var itemsJson = BuildSupplierReturnItemsJson(request.Items);

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CreatedSupplierReturnRow>(
            StoredProcedureNames.SupplierReturnCreate,
            new
            {
                request.SupplierId,
                BranchId = branchId,
                ItemsJson = itemsJson,
                request.CreditAmount,
                UserId = _currentUser.UserId
            },
            cancellationToken)
            ?? throw new ConflictException("Không tạo được phiếu trả hàng nhà cung cấp.");

        return new CreatedSupplierReturnDto { SupplierReturnId = row.SupplierReturnId, ReturnCode = row.ReturnCode };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Thanh toán & công nợ phải trả (SUP-03) — stored procedure
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SupplierPaymentResultDto> RecordPaymentAsync(
        long supplierId,
        RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new BadRequestException("Số tiền thanh toán phải lớn hơn 0.");
        }

        // Proc khóa sổ công nợ (UPDLOCK) và chặn thanh toán vượt công nợ hiện tại (50006 → 409);
        // người thanh toán lấy từ token, không nhận từ client.
        var balanceAfter = await _sqlExecutor.ExecuteScalarAsync<decimal?>(
            StoredProcedureNames.SupplierPaymentRecord,
            new
            {
                SupplierId = supplierId,
                request.Amount,
                request.PurchaseReceiptId,
                UserId = _currentUser.UserId
            },
            cancellationToken);

        return new SupplierPaymentResultDto { BalanceAfter = balanceAfter ?? 0m };
    }

    /// <inheritdoc />
    public async Task<SupplierLedgerDto> GetLedgerAsync(
        long supplierId,
        SupplierLedgerQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 1 : Math.Min(query.PageSize, LedgerPageSizeMax);

        // usp_Supplier_GetLedger trả 2 result set: (1) công nợ hiện tại, (2) các dòng sao kê + TotalCount.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.SupplierGetLedger,
            new { SupplierId = supplierId, PageNumber = pageNumber, PageSize = pageSize },
            cancellationToken);

        var balance = await results.ReadFirstOrDefaultAsync<SupplierBalanceRow>(cancellationToken);
        var entries = await results.ReadListAsync<SupplierLedgerEntryRow>(cancellationToken);

        return new SupplierLedgerDto
        {
            SupplierId = supplierId,
            Balance = balance?.Balance ?? 0m,
            Entries = entries.Select(SupplierLedgerEntryDto.FromRow).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = entries.Count > 0 ? entries[0].TotalCount : 0
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Đọc nhà cung cấp theo Id; không tồn tại → 404.</summary>
    private async Task<Supplier> GetOrThrowAsync(long id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy nhà cung cấp Id = {id}.");

    /// <summary>Chuỗi rỗng/khoảng trắng → <c>null</c>; vượt độ dài cột → 400.</summary>
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
    /// Dựng <c>@ItemsJson</c> cho <c>usp_SupplierReturn_Create</c>:
    /// <c>[{"productId":1,"quantity":2,"serials":["SN001","SN002"]}]</c>.
    /// Hàng không quản lý theo serial không được kèm serial (proc báo lỗi 50004 nếu kèm).
    /// </summary>
    private static string BuildSupplierReturnItemsJson(IReadOnlyList<SupplierReturnItemInput> items)
    {
        var lines = new List<ReturnLine>(items.Count);

        foreach (var item in items)
        {
            if (item.ProductId <= 0)
            {
                throw new BadRequestException("Có dòng trả hàng thiếu sản phẩm.");
            }

            if (item.Quantity <= 0)
            {
                throw new BadRequestException($"Sản phẩm {item.ProductId}: số lượng trả phải lớn hơn 0.");
            }

            lines.Add(new ReturnLine(
                item.ProductId,
                item.Quantity,
                NormalizeSerials(item.Serials, item.Quantity, item.ProductId)));
        }

        var duplicates = lines
            .GroupBy(line => line.ProductId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new BadRequestException(
                $"Một sản phẩm xuất hiện nhiều lần trong phiếu trả (productId: {string.Join(", ", duplicates)}).");
        }

        return JsonSerializer.Serialize(
            lines.Select(line => new { productId = line.ProductId, quantity = line.Quantity, serials = line.Serials }),
            JsonOptions);
    }

    /// <summary>
    /// Chuẩn hóa danh sách serial trả: không kèm → <c>null</c>; nếu có thì phải đủ bằng số lượng,
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

    /// <summary>Một dòng trả hàng đã kiểm tra, sẵn sàng serialize.</summary>
    private sealed record ReturnLine(long ProductId, int Quantity, string[]? Serials);
}




