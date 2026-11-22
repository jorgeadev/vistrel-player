using Vistrel.Core.Playback;
using Xunit;

namespace Vistrel.Playback.Vlc.Tests;

/// <summary>
/// Plays a 3-second MKV (English + Spanish audio, one English subtitle) through the real
/// LibVLC engine with dummy outputs, so these run on CI machines without a display or sound card.
/// </summary>
public sealed class VlcTrackTests : IDisposable
{
    private static readonly string MediaDir = Path.Combine(AppContext.BaseDirectory, "Media");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly VlcPlaybackEngine _engine = new("--vout=dummy", "--aout=dummy", "--no-sub-autodetect-file");

    public VlcTrackTests()
    {
        _engine.Open(new Uri(Path.Combine(MediaDir, "two-audio-one-sub.mkv")));
        _engine.Pause(); // keep it on the first frames while the tests inspect tracks
        WaitUntil(() => RealTracks(TrackKind.Audio).Count == 2 && RealTracks(TrackKind.Subtitle).Count == 1,
            "the file's 2 audio tracks and 1 subtitle track to appear");
    }

    [Fact]
    public void Lists_audio_and_subtitle_tracks_with_names()
    {
        var audio = RealTracks(TrackKind.Audio);
        Assert.Contains(audio, t => t.Name.Contains("English", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(audio, t => t.Name.Contains("Spanish", StringComparison.OrdinalIgnoreCase));
        Assert.All(audio, t => Assert.Equal(TrackKind.Audio, t.Kind));

        // LibVLC lists a "Disable" entry for subtitles, which the UI shows as "Off".
        Assert.Contains(_engine.GetTracks(TrackKind.Subtitle), t => t.IsDisabled);
    }

    [Fact]
    public void Switches_audio_track()
    {
        var spanish = RealTracks(TrackKind.Audio).Single(t => t.Name.Contains("Spanish", StringComparison.OrdinalIgnoreCase));
        Assert.True(_engine.SelectTrack(TrackKind.Audio, spanish.Id));
        WaitUntil(() => _engine.GetSelectedTrackId(TrackKind.Audio) == spanish.Id, "the Spanish track to be selected");
    }

    [Fact]
    public void Turns_subtitles_on_and_off()
    {
        var sub = RealTracks(TrackKind.Subtitle).Single();
        Assert.True(_engine.SelectTrack(TrackKind.Subtitle, sub.Id));
        WaitUntil(() => _engine.GetSelectedTrackId(TrackKind.Subtitle) == sub.Id, "the subtitle to be selected");

        Assert.True(_engine.SelectTrack(TrackKind.Subtitle, MediaTrack.DisabledId));
        WaitUntil(() => _engine.GetSelectedTrackId(TrackKind.Subtitle) == MediaTrack.DisabledId, "subtitles to turn off");
    }

    [Fact]
    public void Loads_external_subtitle_file()
    {
        Assert.True(_engine.AddSubtitleFile(Path.Combine(MediaDir, "external.srt")));
        WaitUntil(() => RealTracks(TrackKind.Subtitle).Count == 2, "the external subtitle to be added as a second track");
    }

    [Fact]
    public void Rejects_non_subtitle_and_missing_files()
    {
        Assert.False(_engine.AddSubtitleFile(Path.Combine(MediaDir, "two-audio-one-sub.mkv")));
        Assert.False(_engine.AddSubtitleFile(Path.Combine(MediaDir, "missing.srt")));
    }

    [Fact]
    public void Sets_and_clamps_delays()
    {
        _engine.SubtitleDelay = TimeSpan.FromMilliseconds(300);
        Assert.Equal(TimeSpan.FromMilliseconds(300), _engine.SubtitleDelay);

        _engine.AudioDelay = TimeSpan.FromMilliseconds(-150);
        Assert.Equal(TimeSpan.FromMilliseconds(-150), _engine.AudioDelay);

        _engine.SubtitleDelay = TimeSpan.FromHours(2);
        Assert.Equal(PlaybackLimits.MaxDelay, _engine.SubtitleDelay);
    }

    [Fact]
    public void Raises_tracks_changed_when_a_track_is_selected()
    {
        using var raised = new ManualResetEventSlim();
        _engine.TracksChanged += (_, _) => raised.Set();
        var other = RealTracks(TrackKind.Audio).First(t => t.Id != _engine.GetSelectedTrackId(TrackKind.Audio));
        _engine.SelectTrack(TrackKind.Audio, other.Id);
        Assert.True(raised.Wait(Timeout, TestContext.Current.CancellationToken), "TracksChanged was not raised");
    }

    private List<MediaTrack> RealTracks(TrackKind kind) =>
        _engine.GetTracks(kind).Where(t => !t.IsDisabled).ToList();

    private static void WaitUntil(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            Thread.Sleep(50);
        }
        Assert.Fail($"Timed out waiting for {what}.");
    }

    public void Dispose() => _engine.Dispose();
}
