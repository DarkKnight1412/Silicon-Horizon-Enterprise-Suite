using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiliconHorizon.Domain.Entities.Hr;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình nhóm nhân sự: Employees, Shifts, EmployeeSchedules, Attendances.</summary>
public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmployeeCode).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Phone).HasColumnType("varchar(20)");
        builder.Property(x => x.Position).IsRequired();               // 0 Cashier ... 3 Manager
        builder.Property(x => x.Status).HasDefaultValue(EmployeeStatus.Active);
        builder.Property(x => x.HireDate).HasColumnType("date");
        builder.Property(x => x.TerminatedDate).HasColumnType("date");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.EmployeeCode).IsUnique();
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
    }
}

/// <summary>Cấu hình bảng dbo.Shifts.</summary>
public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StartTime).HasColumnType("time(0)");
        builder.Property(x => x.EndTime).HasColumnType("time(0)");

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

/// <summary>Cấu hình bảng dbo.EmployeeSchedules (duy nhất theo nhân viên + ngày + ca).</summary>
public class EmployeeScheduleConfiguration : IEntityTypeConfiguration<EmployeeSchedule>
{
    public void Configure(EntityTypeBuilder<EmployeeSchedule> builder)
    {
        builder.ToTable("EmployeeSchedules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkDate).HasColumnType("date");

        builder.HasIndex(x => new { x.EmployeeId, x.WorkDate, x.ShiftId }).IsUnique();

        builder.HasOne(x => x.Employee).WithMany(x => x.Schedules).HasForeignKey(x => x.EmployeeId);
        builder.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
    }
}

/// <summary>Cấu hình bảng dbo.Attendances.</summary>
public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("Attendances");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkDate).HasColumnType("date");
        builder.Property(x => x.CheckInAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.CheckOutAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.Source).HasDefaultValue(AttendanceSource.Device);
        builder.Property(x => x.AdjustReason).HasMaxLength(300);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        builder.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId);
    }
}
