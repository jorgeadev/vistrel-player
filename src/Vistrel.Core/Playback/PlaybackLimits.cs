namespace Vistrel.Core.Playback;

public static class PlaybackLimits
{
    public const int MaxVolume = 200;
    public const float MinRate = 0.25f;
    public const float MaxRate = 4f;

    public static int ClampVolume(int volume) => Math.Clamp(volume, 0, MaxVolume);

    public static float ClampRate(float rate) =>
        float.IsFinite(rate) ? Math.Clamp(rate, MinRate, MaxRate) : 1f;

    /// <summary>Clamps a seek target into the playable range of the current media.</summary>
    public static TimeSpan ClampPosition(TimeSpan position, TimeSpan duration)
    {
        if (position < TimeSpan.Zero) return TimeSpan.Zero;
        if (duration > TimeSpan.Zero && position > duration) return duration;
        return position;
    }
}
