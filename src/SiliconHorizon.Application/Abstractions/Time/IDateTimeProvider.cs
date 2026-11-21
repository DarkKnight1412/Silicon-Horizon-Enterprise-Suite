namespace SiliconHorizon.Application.Abstractions.Time;

/// <summary>
/// Nguồn thời gian của hệ thống. Mọi thời điểm được lưu theo UTC, hiển thị theo múi giờ Việt Nam (BRD mục 1.4).
/// Tách thành interface để test có thể giả lập thời gian.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    DateOnly UtcToday { get; }

    /// <summary>Múi giờ dùng để hiển thị (mặc định Asia/Ho_Chi_Minh).</summary>
    TimeZoneInfo DisplayTimeZone { get; }

    DateTime ToDisplayTime(DateTime utcTime);
}
