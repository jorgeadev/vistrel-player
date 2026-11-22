namespace Vistrel.Core.Playback;

public static class PlaybackLimits
{
    public const int MaxVolume = 200;
    public const float MinRate = 0.25f;
    public const float MaxRate = 4f;

    public static int ClampVolume(int volume) => Math.Clamp(volume, 0, MaxVolume);

    public static float ClampRate(float rate) =>
        float.IsFinite(rate) ? Math.Clamp(rate, MinRate, MaxRate) : 1f;

    /// <summary>Largest subtitle or audio delay accepted in either direction.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(10);

    public static TimeSpan ClampDelay(TimeSpan delay) =>
        delay > MaxDelay ? MaxDelay : delay < -MaxDelay ? -MaxDelay : delay;

    /// <summary>Formats a delay for the on-screen message, e.g. "+300 ms", "−1.5 s", "0 ms".</summary>
    public static string FormatDelay(TimeSpan delay)
    {
        var ms = (long)Math.Round(delay.TotalMilliseconds);
        var sign = ms > 0 ? "+" : ms < 0 ? "−" : "";
        var abs = Math.Abs(ms);
        return abs < 1000
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{sign}{abs} ms")
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{sign}{abs / 1000.0:0.0##} s");
    }

    /// <summary>Clamps a seek target into the playable range of the current media.</summary>
    public static TimeSpan ClampPosition(TimeSpan position, TimeSpan duration)
    {
        if (position < TimeSpan.Zero) return TimeSpan.Zero;
        if (duration > TimeSpan.Zero && position > duration) return duration;
        return position;
    }
}
