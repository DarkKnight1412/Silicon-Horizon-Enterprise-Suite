using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Security;
using SiliconHorizon.Domain.Entities.Hr;

namespace SiliconHorizon.Application.Features.Hr;

/// <summary>
/// Chốt chặn phạm vi dữ liệu của module Nhân sự (Database/README.md mục 1.1, BRD 2.2):
/// nhân viên chỉ thao tác dữ liệu của mình, Quản lý chi nhánh chỉ trong chi nhánh mình,
/// Admin xem toàn hệ thống.
/// </summary>
/// <remarks>
/// Vì sao cần lớp này: <c>usp_Schedule_Assign</c>/<c>usp_Attendance_Adjust</c> chỉ <b>ghi</b> dữ liệu,
/// chúng không kiểm tra người gọi có thuộc chi nhánh của dữ liệu hay không; <c>usp_Schedule_Remove</c>
/// thậm chí không có tham số chi nhánh. Nếu API truyền thẳng giá trị client gửi lên thì Quản lý chi nhánh A
/// có thể xóa ca/điều chỉnh công của chi nhánh B bằng cách đoán id.
/// <para>
/// <c>employeeId</c>/<c>branchId</c> LUÔN lấy từ token (<see cref="ICurrentUser"/>), KHÔNG bao giờ nhận từ client.
/// </para>
/// <para>
/// Hai truy vấn kiểm tra bên dưới đọc hồ sơ nhân viên và ca làm qua <see cref="IReadRepository{TEntity}"/>
/// (abstraction của tầng Application, Infrastructure hiện thực bằng EF Core) vì <b>không có stored procedure
/// nào trả về chi nhánh của một ca theo id</b>; các proc còn lại vẫn gọi qua <c>ISqlExecutor</c>.
/// </para>
/// </remarks>
public sealed class HrAccessGuard
{
    private readonly IReadRepository<Employee> _employeeRepository;
    private readonly IReadRepository<EmployeeSchedule> _scheduleRepository;
    private readonly ICurrentUser _currentUser;

    public HrAccessGuard(
        IReadRepository<Employee> employeeRepository,
        IReadRepository<EmployeeSchedule> scheduleRepository,
        ICurrentUser currentUser)
    {
        _employeeRepository = employeeRepository;
        _scheduleRepository = scheduleRepository;
        _currentUser = currentUser;
    }

    /// <summary>Admin — xem toàn hệ thống và không bị giới hạn chi nhánh khi lọc dữ liệu.</summary>
    public bool IsAdmin => _currentUser.IsInRole(RoleCodes.Admin);

    /// <summary>Quản lý chi nhánh — chỉ xem/xếp ca và điều chỉnh công trong chi nhánh của mình.</summary>
    public bool IsManager => _currentUser.IsInRole(RoleCodes.Manager);

    /// <summary>
    /// Quyết định giá trị lọc thực sự truyền vào <c>@EmployeeId</c>/<c>@BranchId</c> của
    /// <c>usp_Schedule_List</c> và <c>usp_Attendance_GetMonthly</c> theo vai trò người gọi.
    /// </summary>
    /// <param name="requestedEmployeeId">Giá trị client gửi (chỉ được dùng khi người gọi là Admin).</param>
    /// <param name="requestedBranchId">Giá trị client gửi (chỉ được dùng khi người gọi là Admin).</param>
    public HrScope ResolveScope(long? requestedEmployeeId, int? requestedBranchId)
    {
        // Admin: lọc tùy chọn theo nhân viên/chi nhánh; không truyền gì = toàn hệ thống.
        if (IsAdmin)
        {
            return new HrScope(requestedEmployeeId, requestedBranchId);
        }

        // Quản lý chi nhánh: luôn bị ép @BranchId từ token. Có thể lọc thêm một nhân viên, nhưng proc luôn
        // lọc kèm chi nhánh nên không xem được dữ liệu của chi nhánh khác.
        if (IsManager)
        {
            return new HrScope(requestedEmployeeId, RequireBranchId());
        }

        // Nhân viên (TN/TV/TK): chỉ xem ca và công của chính mình — ép @EmployeeId từ token,
        // bỏ qua employeeId/branchId do client gửi.
        return new HrScope(RequireEmployeeId(), null);
    }

    /// <summary>Người dùng đang gọi — <c>@UserId</c> của usp_Schedule_Assign/usp_Attendance_Adjust (ghi nhật ký).</summary>
    public long RequireUserId()
        => _currentUser.UserId
           ?? throw new ForbiddenException("Token thiếu định danh người dùng nên không thực hiện được thao tác này.");

    /// <summary>Hồ sơ nhân viên trong token — chấm công vào/ra bắt buộc phải có (thiếu → 403).</summary>
    public long RequireEmployeeId()
        => _currentUser.EmployeeId
           ?? throw new ForbiddenException(
               "Tài khoản chưa gắn hồ sơ nhân viên nên không thực hiện được nghiệp vụ chấm công.");

    /// <summary>Chi nhánh của người gọi — thiếu → 403 vì không xác định được phạm vi (BRD 2.2).</summary>
    public int RequireBranchId()
        => _currentUser.BranchId
           ?? throw new ForbiddenException(
               "Tài khoản chưa được gắn chi nhánh nên không thao tác được dữ liệu nhân sự.");

    /// <summary>
    /// Nhân viên được xếp ca / bị điều chỉnh công phải thuộc chi nhánh của người gọi —
    /// nếu không → 403 (ca làm việc theo chi nhánh, BRD 2.2).
    /// </summary>
    /// <param name="employeeId">Nhân viên đích (lấy từ body request, luôn bị kiểm tra theo chi nhánh trong token).</param>
    /// <param name="branchId">Chi nhánh trong token — xem <see cref="RequireBranchId"/>.</param>
    public async Task EnsureEmployeeInBranchAsync(
        long employeeId,
        int branchId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy nhân viên Id = {employeeId}.");

        if (employee.BranchId != branchId)
        {
            throw new ForbiddenException(
                $"Nhân viên {employeeId} thuộc chi nhánh {employee.BranchId} nên bạn (chi nhánh {branchId}) không thao tác được.");
        }
    }

    /// <summary>
    /// Ca cần xóa phải thuộc chi nhánh của người gọi — nếu không → 403.
    /// <c>usp_Schedule_Remove</c> không có tham số chi nhánh nên đây là chốt chặn duy nhất.
    /// </summary>
    public async Task EnsureScheduleInBranchAsync(long scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy ca làm việc Id = {scheduleId}.");

        var branchId = RequireBranchId();

        if (schedule.BranchId != branchId)
        {
            throw new ForbiddenException(
                $"Ca làm việc {scheduleId} thuộc chi nhánh {schedule.BranchId} nên bạn (chi nhánh {branchId}) không xóa được.");
        }
    }
}

/// <summary>Phạm vi truy vấn xếp ca / bảng công đã kiểm tra theo vai trò (<c>null</c> = không lọc).</summary>
public readonly record struct HrScope(long? EmployeeId, int? BranchId);


