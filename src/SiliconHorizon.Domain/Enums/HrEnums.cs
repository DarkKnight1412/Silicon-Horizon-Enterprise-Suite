namespace SiliconHorizon.Domain.Enums;

/// <summary>Employees.Position — chức vụ nhân viên (BRD mục 2.1).</summary>
public enum EmployeePosition : byte
{
    Cashier = 0,
    TechAdvisor = 1,
    Storekeeper = 2,
    Manager = 3
}

/// <summary>Employees.Status.</summary>
public enum EmployeeStatus : byte
{
    Active = 0,
    Terminated = 1
}

/// <summary>Attendances.Source — chấm công bằng thiết bị hay do quản lý điều chỉnh (HR-06).</summary>
public enum AttendanceSource : byte
{
    Device = 0,
    ManualAdjust = 1
}
