using SiliconHorizon.Application.Abstractions.Time;

namespace SiliconHorizon.Infrastructure.Services;

/// <summary>
/// Nguồn thời gian mặc định: lấy giờ UTC (BRD mục 1.4) và hiển thị theo múi giờ Việt Nam.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);

    public TimeZoneInfo DisplayTimeZone => VietnamTimeZone;

    public DateTime ToDisplayTime(DateTime utcTime)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), VietnamTimeZone);

    /// <summary>.NET 6+ hỗ trợ mã IANA trên Windows; giữ nhánh dự phòng theo mã Windows.</summary>
    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
