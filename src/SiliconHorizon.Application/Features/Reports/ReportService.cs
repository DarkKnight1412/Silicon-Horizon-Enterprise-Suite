using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Reports.Dto;

namespace SiliconHorizon.Application.Features.Reports;

/// <summary>
/// Hiện thực use case Báo cáo qua stored procedure
/// (Database/06_sp_hr_audit_reports.sql, nhóm <c>usp_Report_*</c>; Database/README.md mục 9).
/// </summary>
/// <remarks>
/// Nguyên tắc nghiệp vụ:
/// <list type="bullet">
/// <item><b>Tham số ngày</b>: <c>from</c>/<c>to</c> là ngày theo giờ Việt Nam và <b>bắt buộc</b> với báo cáo
/// doanh thu / bảo hành (proc không có giá trị mặc định); proc tự đổi sang UTC (<c>DATEADD(HOUR, -7, …)</c>)
/// và lấy trọn ngày kết thúc (+1 ngày). Ngày kết thúc luôn BAO GỒM.</item>
/// <item><b>Doanh thu</b> ghi nhận theo ngày hoàn tất đơn, không tính phí vận chuyển; lợi nhuận gộp trừ tiếp
/// giá vốn (serial lấy <c>UnitCost</c>, hàng không serial lấy giá nhập gần nhất).</item>
/// <item><b>Tồn kho</b> không có tham số ngày (số liệu hiện thời) — chỉ có <c>slowDays</c> để xác định
/// "hàng tồn lâu không xuất" (mặc định 90 ngày như proc).</item>
/// <item><b>Công nợ</b> không có tham số nào và chỉ Admin xem được (proc không lọc theo chi nhánh).</item>
/// <item>Phạm vi chi nhánh do <see cref="ReportAccessGuard"/> quyết định: Quản lý/thủ kho bị ép
/// <c>@BranchId</c> từ token, Admin xem toàn hệ thống (BRD 2.2).</item>
/// <item>Không mở transaction: mọi proc báo cáo chỉ đọc dữ liệu.</item>
/// </list>
/// </remarks>
public sealed class ReportService : IReportService
{
    /// <summary>Giá trị hợp lệ của <c>@GroupBy varchar(10)</c>.</summary>
    private const string GroupByDay = "day";

    private const string GroupByMonth = "month";

    private const string GroupByBranch = "branch";

    private const string GroupByCategory = "category";

    /// <summary>Mặc định của <c>@SlowDays</c> trong usp_Report_Inventory.</summary>
    private const int DefaultSlowDays = 90;

    private static readonly string[] AllowedGroupBy = { GroupByDay, GroupByMonth, GroupByBranch, GroupByCategory };

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ReportAccessGuard _guard;

    public ReportService(ISqlExecutor sqlExecutor, ReportAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task<RevenueReportDto> GetRevenueAsync(
        RevenueReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = RequireDateRange(query.FromDate, query.ToDate, "doanh thu");
        var groupBy = ResolveGroupBy(query.GroupBy);

        // Quản lý bị ép @BranchId từ token; chỉ Admin được truyền giá trị lọc (null = toàn hệ thống).
        var branchId = _guard.ResolveBranchScope(query.BranchId);

        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.ReportRevenue,
            new
            {
                FromDate = ToSqlDate(fromDate),
                ToDate = ToSqlDate(toDate),
                BranchId = branchId,
                GroupBy = groupBy
            },
            cancellationToken);

        // groupBy = 'category' thì proc RETURN sớm với 1 result set theo danh mục; các giá trị khác
        // trả 1 result set gộp theo ngày/tháng/chi nhánh.
        if (groupBy == GroupByCategory)
        {
            var categories = await results.ReadListAsync<RevenueCategoryRow>(cancellationToken);

            return new RevenueReportDto
            {
                GroupBy = groupBy,
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                Categories = categories.Select(RevenueCategoryDto.FromRow).ToList()
            };
        }

        var groups = await results.ReadListAsync<RevenueGroupRow>(cancellationToken);

        return new RevenueReportDto
        {
            GroupBy = groupBy,
            FromDate = fromDate,
            ToDate = toDate,
            BranchId = branchId,
            Groups = groups.Select(RevenueGroupDto.FromRow).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<InventoryReportDto> GetInventoryAsync(
        InventoryReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var slowDays = ResolveSlowDays(query.SlowDays);
        var branchId = _guard.ResolveBranchScope(query.BranchId);

        // 2 result set: giá trị tồn theo chi nhánh/danh mục, hàng tồn lâu không xuất.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.ReportInventory,
            new { BranchId = branchId, SlowDays = slowDays },
            cancellationToken);

        var values = await results.ReadListAsync<InventoryValueRow>(cancellationToken);
        var slowMoving = await results.ReadListAsync<SlowMovingStockRow>(cancellationToken);

        return new InventoryReportDto
        {
            BranchId = branchId,
            SlowDays = slowDays,
            Values = values.Select(InventoryValueDto.FromRow).ToList(),
            SlowMoving = slowMoving.Select(SlowMovingStockDto.FromRow).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SerialStatusCountDto>> GetSerialStatusAsync(
        SerialStatusReportQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsurePositiveOrNull(query.ProductId, "productId");
        EnsurePositiveOrNull(query.BranchId, "branchId");

        var branchId = _guard.ResolveBranchScope(query.BranchId);

        // Một result set duy nhất: Status, Quantity (chỉ các trạng thái đang có serial).
        var rows = await _sqlExecutor.QueryAsync<SerialStatusCountRow>(
            StoredProcedureNames.ReportSerialStatus,
            new { ProductId = query.ProductId, BranchId = branchId },
            cancellationToken);

        return rows.Select(SerialStatusCountDto.FromRow).ToList();
    }

    /// <inheritdoc />
    public async Task<DebtReportDto> GetDebtAsync(CancellationToken cancellationToken = default)
    {
        // usp_Report_Debt không có tham số nào (không lọc chi nhánh/ngày) → chỉ Admin gọi được
        // (ràng buộc ở [Authorize] của controller).
        // 2 result set: công nợ phải thu của khách, công nợ phải trả nhà cung cấp.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.ReportDebt,
            parameters: null,
            cancellationToken: cancellationToken);

        var customers = await results.ReadListAsync<CustomerDebtRow>(cancellationToken);
        var suppliers = await results.ReadListAsync<SupplierDebtRow>(cancellationToken);

        return new DebtReportDto
        {
            Customers = customers.Select(CustomerDebtDto.FromRow).ToList(),
            Suppliers = suppliers.Select(SupplierDebtDto.FromRow).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<WarrantyReportDto> GetWarrantyAsync(
        WarrantyReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = RequireDateRange(query.FromDate, query.ToDate, "bảo hành");
        var branchId = _guard.ResolveBranchScope(query.BranchId);

        // 3 result set: số phiếu theo trạng thái, top 20 sản phẩm lỗi nhiều, thời gian xử lý trung bình.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.ReportWarranty,
            new
            {
                FromDate = ToSqlDate(fromDate),
                ToDate = ToSqlDate(toDate),
                BranchId = branchId
            },
            cancellationToken);

        var statuses = await results.ReadListAsync<WarrantyStatusCountRow>(cancellationToken);
        var products = await results.ReadListAsync<WarrantyTopProductRow>(cancellationToken);
        var handling = await results.ReadFirstOrDefaultAsync<WarrantyHandlingRow>(cancellationToken);

        return new WarrantyReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            BranchId = branchId,
            Statuses = statuses.Select(WarrantyStatusCountDto.FromRow).ToList(),
            TopDefectiveProducts = products.Select(WarrantyTopProductDto.FromRow).ToList(),
            AvgHandlingDays = handling?.AvgHandlingDays
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>from</c>/<c>to</c> là <b>bắt buộc</b> (<c>@FromDate</c>/<c>@ToDate</c> của proc không có giá trị mặc
    /// định). Ngày theo giờ Việt Nam; proc tự lấy trọn ngày kết thúc nên <c>to</c> luôn BAO GỒM.
    /// </summary>
    private static (DateOnly FromDate, DateOnly ToDate) RequireDateRange(
        DateOnly? fromDate,
        DateOnly? toDate,
        string reportName)
    {
        var from = fromDate
            ?? throw new BadRequestException(
                $"Thiếu tham số from của báo cáo {reportName} (định dạng yyyy-MM-dd).");

        var to = toDate
            ?? throw new BadRequestException(
                $"Thiếu tham số to của báo cáo {reportName} (định dạng yyyy-MM-dd).");

        if (to < from)
        {
            throw new BadRequestException($"Tham số to phải lớn hơn hoặc bằng from của báo cáo {reportName}.");
        }

        return (from, to);
    }

    /// <summary>Chỉ nhận <c>day</c>/<c>month</c>/<c>branch</c>/<c>category</c> (proc cũng chặn bằng 50004); mặc định <c>day</c>.</summary>
    private static string ResolveGroupBy(string? value)
    {
        var text = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(text))
        {
            return GroupByDay;
        }

        if (Array.IndexOf(AllowedGroupBy, text) < 0)
        {
            throw new BadRequestException("groupBy phải là day, month, branch hoặc category.");
        }

        return text;
    }

    /// <summary><c>@SlowDays</c> phải là số ngày dương; bỏ trống thì dùng mặc định 90 của proc.</summary>
    private static int ResolveSlowDays(int? slowDays)
    {
        if (!slowDays.HasValue)
        {
            return DefaultSlowDays;
        }

        if (slowDays.Value < 1)
        {
            throw new BadRequestException("slowDays phải là số ngày dương (mặc định 90).");
        }

        return slowDays.Value;
    }

    /// <summary>Tham số id tùy chọn của proc là số dương; 0 hoặc âm là dữ liệu sai (400).</summary>
    private static void EnsurePositiveOrNull(long? value, string fieldName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new BadRequestException($"{fieldName} phải là số dương.");
        }
    }

    /// <summary>Bản dùng cho <c>int?</c> (<c>branchId</c>) — cùng quy tắc với bản <c>long?</c> ở trên.</summary>
    private static void EnsurePositiveOrNull(int? value, string fieldName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new BadRequestException($"{fieldName} phải là số dương.");
        }
    }

    /// <summary>
    /// Đổi <see cref="DateOnly"/> thành <see cref="DateTime"/> nửa đêm khi truyền vào tham số <c>@... date</c>
    /// của proc (SQL Server tự cắt về kiểu <c>date</c>). Dùng DateTime thay vì DateOnly cho tham số để không
    /// phụ thuộc việc provider ADO.NET có ánh xạ kiểu DateOnly hay không.
    /// </summary>
    private static DateTime ToSqlDate(DateOnly value) => value.ToDateTime(TimeOnly.MinValue);
}
