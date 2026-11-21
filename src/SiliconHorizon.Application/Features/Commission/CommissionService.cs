using System.Globalization;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Commission.Dto;

namespace SiliconHorizon.Application.Features.Commission;

/// <summary>
/// Hiện thực use case hoa hồng qua stored procedure (Database/05_sp_warranty_returns_commission.sql).
/// </summary>
/// <remarks>
/// Nguyên tắc nghiệp vụ:
/// <list type="bullet">
/// <item>Hoa hồng <b>chỉ phát sinh khi đơn hoàn tất</b> và chia theo tỷ lệ nhân sự tư vấn trong
/// <c>OrderSalesStaff</c> (KPI-02/KPI-03); đơn online không có nhân viên tư vấn thì không có bút toán nào.
/// API không tự tính hoa hồng — chỉ đọc/điều chỉnh qua proc.</item>
/// <item><b>Thu hồi hoa hồng (Clawback)</b> do <c>usp_Return_Refund</c> thực hiện khi khách đổi trả; bút toán
/// Clawback có <c>Amount</c> ÂM và gắn <c>ReturnRequestId</c> (KPI-04).</item>
/// <item><b>Kỳ hoa hồng phải đang mở mới ghi được bút toán mới</b>: điều chỉnh thủ công luôn ghi vào kỳ đang mở
/// của ngày hiện tại (<c>usp_Internal_GetOpenPeriod</c> tự tạo kỳ mới/tự nhảy sang tháng kế tiếp còn mở),
/// nên dữ liệu của kỳ đã chốt không bị sửa (KPI-05/KPI-06).</item>
/// <item>Phạm vi xem do <see cref="CommissionAccessGuard"/> quyết định: nhân viên xem của mình, Quản lý xem
/// theo chi nhánh, Admin xem toàn hệ thống.</item>
/// </list>
/// </remarks>
public sealed class CommissionService : ICommissionService
{
    /// <summary>Định dạng tháng của <c>@YearMonth char(7)</c>.</summary>
    private const string YearMonthFormat = "yyyy-MM";

    /// <summary><c>@Reason nvarchar(300)</c> của usp_Commission_Adjust.</summary>
    private const int ReasonMaxLength = 300;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly CommissionAccessGuard _guard;

    public CommissionService(ISqlExecutor sqlExecutor, CommissionAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task<CommissionSummaryDto> GetSummaryAsync(
        CommissionSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var yearMonth = RequireYearMonth(query.YearMonth);

        // Nhân viên bị ép EmployeeId từ token; Quản lý bị ép BranchId từ token; chỉ Admin được truyền giá trị lọc.
        var scope = _guard.ResolveScope(query.EmployeeId, query.BranchId);

        // 3 result set: kỳ hoa hồng của tháng, tổng hợp theo nhân viên, chi tiết từng bút toán.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.CommissionSummary,
            new { YearMonth = yearMonth, scope.EmployeeId, scope.BranchId },
            cancellationToken);

        var period = await results.ReadFirstOrDefaultAsync<CommissionPeriodRow>(cancellationToken);
        var employees = await results.ReadListAsync<CommissionSummaryRow>(cancellationToken);
        var entries = await results.ReadListAsync<CommissionEntryRow>(cancellationToken);

        return new CommissionSummaryDto
        {
            Period = period is null ? null : CommissionPeriodDto.FromRow(period),
            Employees = employees.Select(CommissionEmployeeSummaryDto.FromRow).ToList(),
            Entries = entries.Select(CommissionEntryDto.FromRow).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<CommissionAdjustmentResultDto> AdjustAsync(
        AdjustCommissionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EmployeeId <= 0)
        {
            throw new BadRequestException("Thiếu nhân viên cần điều chỉnh hoa hồng.");
        }

        if (request.OrderId <= 0)
        {
            throw new BadRequestException("Thiếu đơn hàng gắn với bút toán điều chỉnh.");
        }

        // Bút toán điều chỉnh bằng 0 là vô nghĩa — proc cũng chặn bằng 50004.
        if (request.Amount == 0)
        {
            throw new BadRequestException("Số tiền điều chỉnh phải khác 0.");
        }

        var reason = RequireText(request.Reason, ReasonMaxLength, "Lý do điều chỉnh");

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CommissionAdjustmentRow>(
            StoredProcedureNames.CommissionAdjust,
            new
            {
                request.EmployeeId,
                request.OrderId,
                request.Amount,
                Reason = reason,
                UserId = _guard.RequireUserId()
            },
            cancellationToken)
            ?? throw new ConflictException("Không tạo được bút toán điều chỉnh hoa hồng.");

        return new CommissionAdjustmentResultDto { EntryId = row.EntryId };
    }

    /// <inheritdoc />
    public async Task ClosePeriodAsync(int periodId, CancellationToken cancellationToken = default)
    {
        if (periodId <= 0)
        {
            throw new BadRequestException("Thiếu kỳ hoa hồng cần chốt.");
        }

        // Proc ném 50001 nếu kỳ không tồn tại (→ 404) và 50002 nếu kỳ đã chốt (→ 409).
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.CommissionClosePeriod,
            new { PeriodId = periodId, UserId = _guard.RequireUserId() },
            cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra <c>yearMonth</c> theo định dạng <c>yyyy-MM</c> (khớp <c>@YearMonth char(7)</c>)
    /// để trả 400 rõ ràng thay vì trả về kết quả rỗng khó hiểu.
    /// </summary>
    private static string RequireYearMonth(string? value)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            throw new BadRequestException("Thiếu tham số yearMonth (định dạng yyyy-MM, ví dụ 2026-11).");
        }

        if (!DateOnly.TryParseExact(text, YearMonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new BadRequestException("yearMonth phải theo định dạng yyyy-MM, ví dụ 2026-11.");
        }

        return text;
    }

    /// <summary>Bắt buộc có nội dung và không vượt độ dài cột.</summary>
    private static string RequireText(string? value, int maxLength, string fieldName)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            throw new BadRequestException($"{fieldName} không được để trống.");
        }

        if (text.Length > maxLength)
        {
            throw new BadRequestException($"{fieldName} tối đa {maxLength} ký tự.");
        }

        return text;
    }
}