using SiliconHorizon.Domain.Entities.Hr;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Ca làm việc trả ra API — CRUD bằng EF Core trên <c>dbo.Shifts</c>
/// (Database/README.md mục 10: <c>/api/admin/shifts</c>). Danh mục ca dùng cho
/// <c>usp_Schedule_Assign</c> khi xếp ca nhân viên (HR-04).
/// </summary>
public class ShiftDto
{
    public int Id { get; init; }

    /// <summary>Mã ca (varchar(20), duy nhất — <c>UQ_Shifts_Code</c>): MORNING, AFTERNOON, FULL...</summary>
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>Giờ bắt đầu (SQL <c>time(0)</c>, serialized dạng <c>HH:mm:ss</c>).</summary>
    public TimeOnly StartTime { get; init; }

    /// <summary>Giờ kết thúc (SQL <c>time(0)</c>).</summary>
    public TimeOnly EndTime { get; init; }

    public static ShiftDto FromEntity(Shift shift) => new()
    {
        Id = shift.Id,
        Code = shift.Code,
        Name = shift.Name,
        StartTime = shift.StartTime,
        EndTime = shift.EndTime
    };
}

/// <summary>Tham số lọc danh sách ca làm — GET /api/v1/admin/shifts.</summary>
public class ShiftListQuery
{
    /// <summary>Tìm theo mã hoặc tên ca.</summary>
    public string? Keyword { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Yêu cầu tạo ca làm — POST /api/v1/admin/shifts.
/// <c>endTime</c> phải sau <c>startTime</c> (không hỗ trợ ca qua đêm, khớp dữ liệu khởi tạo trong
/// Database/table_and_data.sql mục 13).
/// </summary>
public class CreateShiftRequest
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật ca làm — PUT /api/v1/admin/shifts/{id}.
/// Không đổi <c>code</c> để giữ tham chiếu từ <c>EmployeeSchedules</c>.
/// </summary>
public class UpdateShiftRequest
{
    public string? Name { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}
