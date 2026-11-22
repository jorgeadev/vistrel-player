namespace Vistrel.Core.Playback;

/// <summary>
/// The app's view of a media engine. The UI only talks to this interface, so the
/// LibVLC engine can later be swapped for another one (for example mpv) without UI changes.
/// Events may be raised on a background thread; UI code must marshal to its dispatcher.
/// </summary>
public interface IPlaybackEngine : IDisposable
{
    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    bool IsSeekable { get; }

    /// <summary>Volume in percent, 0 to <see cref="PlaybackLimits.MaxVolume"/>.</summary>
    int Volume { get; set; }
    bool IsMuted { get; set; }

    /// <summary>Playback speed, clamped to <see cref="PlaybackLimits.MinRate"/>..<see cref="PlaybackLimits.MaxRate"/>.</summary>
    float Rate { get; set; }

    /// <summary>Subtitle timing offset; positive shows subtitles later. Clamped by <see cref="PlaybackLimits.ClampDelay"/>.</summary>
    TimeSpan SubtitleDelay { get; set; }

    /// <summary>Audio timing offset; positive plays audio later. Clamped by <see cref="PlaybackLimits.ClampDelay"/>.</summary>
    TimeSpan AudioDelay { get; set; }

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<TimeSpan>? DurationChanged;
    event EventHandler<PlaybackError>? ErrorOccurred;

    /// <summary>Raised when tracks are added, removed or selected. Re-read the track lists afterwards.</summary>
    event EventHandler? TracksChanged;

    /// <summary>Tracks of the given kind in the current media, including "Disable" for subtitles.</summary>
    IReadOnlyList<MediaTrack> GetTracks(TrackKind kind);

    /// <summary>Id of the selected track, or <see cref="MediaTrack.DisabledId"/>.</summary>
    int GetSelectedTrackId(TrackKind kind);

    /// <summary>Selects a track by id; <see cref="MediaTrack.DisabledId"/> turns subtitles off. Returns false if the engine refused.</summary>
    bool SelectTrack(TrackKind kind, int id);

    /// <summary>Loads an external subtitle file for the current media and selects it.</summary>
    bool AddSubtitleFile(string path);

    /// <summary>Opens a local file or stream and starts playing it.</summary>
    void Open(Uri source);
    void Play();
    void Pause();
    void TogglePlayPause();
    void Stop();
    void Seek(TimeSpan position);
    void SeekRelative(TimeSpan offset);
}

public enum PlaybackState
{
    Idle,
    Opening,
    Buffering,
    Playing,
    Paused,
    Stopped,
    Ended,
    Error,
}

public sealed record PlaybackError(string Message, Uri? Source);
