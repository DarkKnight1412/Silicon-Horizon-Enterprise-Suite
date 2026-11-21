using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Hr;

/// <summary>
/// Nhóm vai trò dùng cho <c>[Authorize(Roles = ...)]</c> của module Nhân sự (xếp ca, chấm công),
/// đối chiếu cột "Quyền" ở Database/README.md mục 8 và ma trận BRD 2.2:
/// <c>NV</c> = mọi nhân viên, <c>QL</c> = quản lý chi nhánh, <c>AD</c> = admin.
/// </summary>
/// <remarks>
/// Chưa gắn <c>[RequirePermission]</c> (mã <c>hr.schedule.manage</c>, <c>hr.attendance.adjust</c> nằm ở
/// <see cref="PermissionCodes"/>) vì <c>dbo.Permissions</c> chỉ được seed khi chạy thêm
/// Database/07_seed_admin_user_and_permissions.sql — gắn sớm sẽ trả 403 cho mọi người. Cùng cách làm
/// với <c>OrderRoleGroups</c>/<c>InventoryRoles</c> của các module đã có.
/// </remarks>
public static class HrRoles
{
    /// <summary>QL, AD — xếp ca và xóa ca chưa có chấm công (usp_Schedule_Assign/Remove).</summary>
    public const string ScheduleApprovers = RoleCodes.Approvers;

    /// <summary>NV (xem ca của mình), QL (xem ca chi nhánh), AD — bảng xếp ca (usp_Schedule_List).</summary>
    public const string ScheduleViewers = RoleCodes.Staff + "," + RoleCodes.Admin;

    /// <summary>NV — chấm công vào/ra ca của chính mình (usp_Attendance_CheckIn/CheckOut).</summary>
    public const string AttendanceStaff = RoleCodes.Staff;

    /// <summary>NV (xem công của mình), QL (xem công chi nhánh), AD — bảng công tháng (usp_Attendance_GetMonthly).</summary>
    public const string AttendanceViewers = RoleCodes.Staff + "," + RoleCodes.Admin;

    /// <summary>QL, AD — điều chỉnh công kèm lý do, có ghi nhật ký (usp_Attendance_Adjust, HR-06).</summary>
    public const string AttendanceAdjusters = RoleCodes.Approvers;
}
