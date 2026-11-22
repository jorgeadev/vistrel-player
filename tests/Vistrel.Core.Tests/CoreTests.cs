using Vistrel.Core;
using Vistrel.Core.Playback;
using Xunit;

namespace Vistrel.Core.Tests;

public class TimeFormatterTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(245, "4:05")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-10, "0:00")]
    [InlineData(36000 * 3, "30:00:00")]
    public void Formats_like_a_player(int seconds, string expected) =>
        Assert.Equal(expected, TimeFormatter.Format(TimeSpan.FromSeconds(seconds)));
}

public class PlaybackLimitsTests
{
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(150, 150)]
    [InlineData(999, PlaybackLimits.MaxVolume)]
    public void Volume_is_clamped(int input, int expected) =>
        Assert.Equal(expected, PlaybackLimits.ClampVolume(input));

    [Theory]
    [InlineData(0.1f, PlaybackLimits.MinRate)]
    [InlineData(1.5f, 1.5f)]
    [InlineData(10f, PlaybackLimits.MaxRate)]
    [InlineData(float.NaN, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    public void Rate_is_clamped(float input, float expected) =>
        Assert.Equal(expected, PlaybackLimits.ClampRate(input));

    [Fact]
    public void Seek_target_stays_inside_media()
    {
        var duration = TimeSpan.FromMinutes(10);
        Assert.Equal(TimeSpan.Zero, PlaybackLimits.ClampPosition(TimeSpan.FromSeconds(-3), duration));
        Assert.Equal(duration, PlaybackLimits.ClampPosition(TimeSpan.FromMinutes(11), duration));
        Assert.Equal(TimeSpan.FromMinutes(4), PlaybackLimits.ClampPosition(TimeSpan.FromMinutes(4), duration));
        // Unknown duration (live streams): only the lower bound applies.
        Assert.Equal(TimeSpan.FromHours(5), PlaybackLimits.ClampPosition(TimeSpan.FromHours(5), TimeSpan.Zero));
    }
}

public class MediaFormatsTests
{
    [Theory]
    [InlineData(@"C:\v\movie.MKV", true)]
    [InlineData("clip.mp4", true)]
    [InlineData("song.flac", true)]
    [InlineData("notes.txt", false)]
    [InlineData("subs.srt", false)]
    [InlineData("noextension", false)]
    public void Recognises_playable_files(string path, bool expected) =>
        Assert.Equal(expected, MediaFormats.IsPlayable(path));

    [Fact]
    public void Recognises_subtitles() => Assert.True(MediaFormats.IsSubtitle("movie.en.SRT"));
}

public class DelayTests
{
    [Fact]
    public void Delay_is_clamped_both_ways()
    {
        Assert.Equal(PlaybackLimits.MaxDelay, PlaybackLimits.ClampDelay(TimeSpan.FromHours(1)));
        Assert.Equal(-PlaybackLimits.MaxDelay, PlaybackLimits.ClampDelay(TimeSpan.FromHours(-1)));
        Assert.Equal(TimeSpan.FromMilliseconds(-300), PlaybackLimits.ClampDelay(TimeSpan.FromMilliseconds(-300)));
    }

    [Theory]
    [InlineData(0, "0 ms")]
    [InlineData(300, "+300 ms")]
    [InlineData(-100, "\u2212100 ms")]
    [InlineData(1500, "+1.5 s")]
    [InlineData(-2250, "\u22122.25 s")]
    public void Delay_formats_for_display(int ms, string expected) =>
        Assert.Equal(expected, PlaybackLimits.FormatDelay(TimeSpan.FromMilliseconds(ms)));
}
