using SiliconHorizon.Domain.Common;
using SiliconHorizon.Domain.Entities.Common;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Domain.Entities.Hr;

/// <summary>dbo.Employees — hồ sơ nhân viên, luôn gắn với một <see cref="User"/> và một chi nhánh.</summary>
public class Employee : IAuditableEntity
{
    public long Id { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public long UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public EmployeePosition Position { get; set; }

    public int BranchId { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public DateOnly HireDate { get; set; }

    public DateOnly? TerminatedDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public User User { get; set; } = null!;

    public Branch Branch { get; set; } = null!;

    public ICollection<EmployeeSchedule> Schedules { get; set; } = new List<EmployeeSchedule>();
}

/// <summary>dbo.Shifts — ca làm việc (MORNING, AFTERNOON, FULL).</summary>
public class Shift
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}

/// <summary>dbo.EmployeeSchedules — xếp ca cho nhân viên (HR-04, UC-22).</summary>
public class EmployeeSchedule
{
    public long Id { get; set; }

    public long EmployeeId { get; set; }

    public int ShiftId { get; set; }

    public int BranchId { get; set; }

    public DateOnly WorkDate { get; set; }

    public Employee Employee { get; set; } = null!;

    public Shift Shift { get; set; } = null!;

    public Branch Branch { get; set; } = null!;
}

/// <summary>dbo.Attendances — chấm công vào/ra ca, có thể do quản lý điều chỉnh kèm lý do (HR-05..06).</summary>
public class Attendance : IAuditableEntity
{
    public long Id { get; set; }

    public long EmployeeId { get; set; }

    public long? ScheduleId { get; set; }

    public DateOnly WorkDate { get; set; }

    public DateTime? CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    public AttendanceSource Source { get; set; } = AttendanceSource.Device;

    public long? AdjustedBy { get; set; }

    public string? AdjustReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public Employee Employee { get; set; } = null!;

    public EmployeeSchedule? Schedule { get; set; }
}
