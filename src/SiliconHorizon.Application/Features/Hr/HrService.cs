using System.Globalization;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Hr.Dto;

namespace SiliconHorizon.Application.Features.Hr;

/// <summary>
/// Hiện thực use case Nhân sự — xếp ca và chấm công qua stored procedure
/// (Database/06_sp_hr_audit_reports.sql, nhóm <c>usp_Schedule_*</c> / <c>usp_Attendance_*</c>).
/// </summary>
/// <remarks>
/// Nguyên tắc nghiệp vụ:
/// <list type="bullet">
/// <item><b>Ca làm theo chi nhánh</b>: ca luôn được tạo trong chi nhánh của token (<c>@BranchId</c> ép từ
/// <c>ICurrentUser</c>) và nhân viên được xếp ca phải thuộc chi nhánh đó (kiểm tra qua
/// <see cref="HrAccessGuard"/>) — vì các proc chỉ ghi dữ liệu, không kiểm tra phạm vi người gọi (BRD 2.2).</item>
/// <item><b>Chấm công vào/ra dùng chính nhân viên đang đăng nhập</b> (<c>EmployeeId</c> trong token), không
/// nhận <c>employeeId</c> từ client; ca được chọn là ca hôm nay chưa chấm công, giờ bắt đầu sớm nhất.</item>
/// <item><b>Điều chỉnh công bắt buộc có lý do</b> (quên chấm, sai giờ): proc lưu <c>Source = 1</c>
/// (ManualAdjust), <c>AdjustedBy</c>, <c>AdjustReason</c> và ghi nhật ký <c>AttendanceAdjusted</c> kèm
/// giá trị cũ/mới (HR-06).</item>
/// <item><b>Xóa ca</b> chỉ khi ca chưa có chấm công; proc trả 50002 → 409 khi đã có dữ liệu công.</item>
/// <item>Không mở transaction ở tầng Application: mỗi proc tự quản lý giao dịch của nó.</item>
/// <item>Ngày làm việc tính theo giờ Việt Nam (UTC+7) — <c>fn_VnDate</c>; giờ vào/ra lưu UTC.</item>
/// </list>
/// </remarks>
public sealed class HrService : IHrService
{
    /// <summary><c>@Reason nvarchar(300)</c> của usp_Attendance_Adjust (Attendances.AdjustReason).</summary>
    private const int ReasonMaxLength = 300;

    /// <summary>Định dạng tháng của <c>@YearMonth char(7)</c>.</summary>
    private const string YearMonthFormat = "yyyy-MM";

    private readonly ISqlExecutor _sqlExecutor;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HrAccessGuard _guard;

    public HrService(
        ISqlExecutor sqlExecutor,
        IDateTimeProvider dateTimeProvider,
        HrAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _dateTimeProvider = dateTimeProvider;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleItemDto>> GetSchedulesAsync(
        ScheduleListQuery query,
        CancellationToken cancellationToken = default)
    {
        // Nhân viên bị ép EmployeeId từ token; Quản lý bị ép BranchId từ token; chỉ Admin được lọc tùy chọn.
        var scope = _guard.ResolveScope(query.EmployeeId, query.BranchId);
        var (fromDate, toDate) = ResolveDateRange(query.FromDate, query.ToDate);

        var rows = await _sqlExecutor.QueryAsync<ScheduleRow>(
            StoredProcedureNames.ScheduleList,
            new
            {
                BranchId = scope.BranchId,
                EmployeeId = scope.EmployeeId,
                FromDate = ToSqlDate(fromDate),
                ToDate = ToSqlDate(toDate)
            },
            cancellationToken);

        return rows.Select(ScheduleItemDto.FromRow).ToList();
    }

    /// <inheritdoc />
    public async Task<CreatedScheduleDto> AssignScheduleAsync(
        AssignScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EmployeeId <= 0)
        {
            throw new BadRequestException("Thiếu nhân viên cần xếp ca.");
        }

        if (request.ShiftId <= 0)
        {
            throw new BadRequestException("Thiếu ca làm việc (shiftId).");
        }

        var workDate = request.WorkDate
            ?? throw new BadRequestException("Thiếu ngày làm việc (workDate).");

        // Ca làm theo chi nhánh: @BranchId ép từ token, không nhận từ client (BRD 2.2).
        var branchId = _guard.RequireBranchId();

        // Chặn sớm việc xếp ca cho nhân viên của chi nhánh khác (proc không kiểm tra).
        await _guard.EnsureEmployeeInBranchAsync(request.EmployeeId, branchId, cancellationToken);

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<CreatedScheduleRow>(
            StoredProcedureNames.ScheduleAssign,
            new
            {
                request.EmployeeId,
                request.ShiftId,
                BranchId = branchId,
                WorkDate = ToSqlDate(workDate),
                UserId = _guard.RequireUserId()
            },
            cancellationToken)
            ?? throw new ConflictException("Không xếp được ca làm việc.");

        return new CreatedScheduleDto { ScheduleId = row.ScheduleId };
    }

    /// <inheritdoc />
    public async Task RemoveScheduleAsync(long scheduleId, CancellationToken cancellationToken = default)
    {
        if (scheduleId <= 0)
        {
            throw new BadRequestException("Thiếu ca làm việc cần xóa.");
        }

        // usp_Schedule_Remove không có tham số chi nhánh → phải kiểm tra phạm vi trước khi gọi.
        await _guard.EnsureScheduleInBranchAsync(scheduleId, cancellationToken);

        // Proc từ chối (50002 → 409) khi ca đã có dữ liệu chấm công.
        await _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.ScheduleRemove,
            new { ScheduleId = scheduleId },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttendanceCheckInDto> CheckInAsync(CancellationToken cancellationToken = default)
    {
        // Chấm công cho CHÍNH nhân viên đang đăng nhập — KHÔNG nhận employeeId từ client.
        var employeeId = _guard.RequireEmployeeId();

        // Proc tự chọn ca hôm nay chưa chấm công có giờ bắt đầu sớm nhất; chưa được xếp ca → 400 (50004).
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<AttendanceCheckInRow>(
            StoredProcedureNames.AttendanceCheckIn,
            new { EmployeeId = employeeId },
            cancellationToken)
            ?? throw new ConflictException("Không chấm công vào được.");

        return AttendanceCheckInDto.FromRow(row);
    }

    /// <inheritdoc />
    public async Task<AttendanceCheckOutDto> CheckOutAsync(CancellationToken cancellationToken = default)
    {
        var employeeId = _guard.RequireEmployeeId();

        // Không có ca nào đang chờ chấm công ra → proc ném 50002 (→ 409).
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<AttendanceCheckOutRow>(
            StoredProcedureNames.AttendanceCheckOut,
            new { EmployeeId = employeeId },
            cancellationToken)
            ?? throw new ConflictException("Không chấm công ra được.");

        return AttendanceCheckOutDto.FromRow(row);
    }

    /// <inheritdoc />
    public async Task<AttendanceAdjustResultDto> AdjustAttendanceAsync(
        AdjustAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EmployeeId <= 0)
        {
            throw new BadRequestException("Thiếu nhân viên cần điều chỉnh công.");
        }

        var workDate = request.WorkDate
            ?? throw new BadRequestException("Thiếu ngày công cần điều chỉnh (workDate).");

        if (request.ScheduleId.HasValue && request.ScheduleId.Value <= 0)
        {
            throw new BadRequestException("scheduleId không hợp lệ.");
        }

        // Điều chỉnh công BẮT BUỘC có lý do (HR-06) — proc cũng chặn lại bằng 50004.
        var reason = RequireText(request.Reason, ReasonMaxLength, "Lý do điều chỉnh");

        if (request.CheckInAt is null && request.CheckOutAt is null)
        {
            throw new BadRequestException("Phải có giờ vào hoặc giờ ra sau khi điều chỉnh.");
        }

        if (request.CheckInAt is { } checkIn && request.CheckOutAt is { } checkOut && checkOut < checkIn)
        {
            throw new BadRequestException("Giờ ra phải sau giờ vào.");
        }

        // Quản lý chỉ điều chỉnh công cho nhân viên thuộc chi nhánh của mình (proc không kiểm tra).
        var branchId = _guard.RequireBranchId();
        await _guard.EnsureEmployeeInBranchAsync(request.EmployeeId, branchId, cancellationToken);

        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<AttendanceAdjustRow>(
            StoredProcedureNames.AttendanceAdjust,
            new
            {
                request.EmployeeId,
                WorkDate = ToSqlDate(workDate),
                ScheduleId = request.ScheduleId,
                CheckInAt = request.CheckInAt,
                CheckOutAt = request.CheckOutAt,
                Reason = reason,
                UserId = _guard.RequireUserId()
            },
            cancellationToken)
            ?? throw new ConflictException("Không điều chỉnh được công.");

        return AttendanceAdjustResultDto.FromRow(row);
    }

    /// <inheritdoc />
    public async Task<AttendanceMonthDto> GetMonthlyAttendanceAsync(
        AttendanceMonthlyQuery query,
        CancellationToken cancellationToken = default)
    {
        var yearMonth = RequireYearMonth(query.YearMonth);

        // Nhân viên bị ép EmployeeId từ token; Quản lý bị ép BranchId từ token; chỉ Admin được lọc tùy chọn.
        var scope = _guard.ResolveScope(query.EmployeeId, query.BranchId);

        // 2 result set: chi tiết từng ngày công, tổng hợp theo nhân viên.
        await using var results = await _sqlExecutor.QueryMultipleAsync(
            StoredProcedureNames.AttendanceGetMonthly,
            new
            {
                YearMonth = yearMonth,
                EmployeeId = scope.EmployeeId,
                BranchId = scope.BranchId
            },
            cancellationToken);

        var days = await results.ReadListAsync<AttendanceDayRow>(cancellationToken);
        var employees = await results.ReadListAsync<AttendanceSummaryRow>(cancellationToken);

        return new AttendanceMonthDto
        {
            YearMonth = yearMonth,
            Days = days.Select(AttendanceDayDto.FromRow).ToList(),
            Employees = employees.Select(AttendanceSummaryDto.FromRow).ToList()
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Khoảng ngày xem bảng xếp ca. Bỏ trống thì mặc định trọn <b>tháng hiện tại theo giờ Việt Nam</b>
    /// (dùng <see cref="IDateTimeProvider.DisplayTimeZone"/>, khớp <c>dbo.fn_VnDate</c> của SQL).
    /// </summary>
    private (DateOnly FromDate, DateOnly ToDate) ResolveDateRange(DateOnly? fromDate, DateOnly? toDate)
    {
        var vnToday = DateOnly.FromDateTime(_dateTimeProvider.ToDisplayTime(_dateTimeProvider.UtcNow));
        var firstOfMonth = new DateOnly(vnToday.Year, vnToday.Month, 1);
        var lastOfMonth = new DateOnly(
            vnToday.Year,
            vnToday.Month,
            DateTime.DaysInMonth(vnToday.Year, vnToday.Month));

        var from = fromDate ?? firstOfMonth;
        var to = toDate ?? lastOfMonth;

        if (to < from)
        {
            throw new BadRequestException("toDate phải lớn hơn hoặc bằng fromDate.");
        }

        return (from, to);
    }

    /// <summary>
    /// Kiểm tra <c>yearMonth</c> theo định dạng <c>yyyy-MM</c> (khớp <c>@YearMonth char(7)</c>) để trả 400
    /// rõ ràng thay vì để proc ném 50004 hoặc trả về kết quả rỗng khó hiểu.
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

    /// <summary>Bắt buộc có nội dung và không vượt độ dài cột (Attendances.AdjustReason nvarchar(300)).</summary>
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

    /// <summary>
    /// Đổi <see cref="DateOnly"/> thành <see cref="DateTime"/> nửa đêm khi truyền vào tham số <c>@... date</c>
    /// của proc (SQL Server tự cắt về kiểu <c>date</c>). Dùng DateTime thay vì DateOnly cho tham số để không
    /// phụ thuộc việc provider ADO.NET có ánh xạ kiểu DateOnly hay không.
    /// </summary>
    private static DateTime ToSqlDate(DateOnly value) => value.ToDateTime(TimeOnly.MinValue);
}
