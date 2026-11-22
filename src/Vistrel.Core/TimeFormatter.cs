using System.Globalization;

namespace Vistrel.Core;

public static class TimeFormatter
{
    /// <summary>
    /// Formats a media time the way players show it: "4:05" under an hour, "1:02:03" above.
    /// Negative or invalid values show as "0:00".
    /// </summary>
    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        var totalHours = (long)time.TotalHours;
        return totalHours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{totalHours}:{time.Minutes:00}:{time.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{time.Minutes}:{time.Seconds:00}");
    }
}
