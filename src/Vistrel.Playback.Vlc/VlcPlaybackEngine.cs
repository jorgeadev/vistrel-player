using LibVLCSharp.Shared;
using Vistrel.Core.Playback;
using MediaTrack = Vistrel.Core.Playback.MediaTrack;

namespace Vistrel.Playback.Vlc;

/// <summary>
/// <see cref="IPlaybackEngine"/> backed by LibVLC. LibVLC raises its events on its own
/// threads and must never be called back into from inside an event handler, so every
/// handler here only copies state and re-raises; callers marshal to the UI thread.
/// </summary>
public sealed class VlcPlaybackEngine : IPlaybackEngine
{
    private readonly LibVLC _libVlc;
    private Media? _media;
    private Uri? _source;
    private PlaybackState _state = PlaybackState.Idle;
    private TimeSpan _duration;
    private bool _disposed;

    /// <param name="libVlcOptions">
    /// Extra LibVLC options. The WinUI video view supplies swap-chain options here.
    /// </param>
    public VlcPlaybackEngine(params string[] libVlcOptions)
    {
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC(enableDebugLogs: false, [.. SafeDefaults, .. libVlcOptions]);
        MediaPlayer = new MediaPlayer(_libVlc) { EnableHardwareDecoding = true };

        MediaPlayer.Opening += (_, _) => SetState(PlaybackState.Opening);
        // LibVLC sends no new Playing event after a buffering pause, so restore it when the cache fills.
        MediaPlayer.Buffering += (_, e) =>
        {
            if (e.Cache < 100f && _state is PlaybackState.Playing or PlaybackState.Opening) SetState(PlaybackState.Buffering);
            else if (e.Cache >= 100f && _state == PlaybackState.Buffering) SetState(PlaybackState.Playing);
        };
        MediaPlayer.Playing += (_, _) => SetState(PlaybackState.Playing);
        MediaPlayer.Paused += (_, _) => SetState(PlaybackState.Paused);
        MediaPlayer.Stopped += (_, _) => SetState(PlaybackState.Stopped);
        MediaPlayer.EndReached += (_, _) => SetState(PlaybackState.Ended);
        MediaPlayer.EncounteredError += (_, _) =>
        {
            SetState(PlaybackState.Error);
            ErrorOccurred?.Invoke(this, new PlaybackError("This file could not be played. It may be damaged or use an unsupported format.", _source));
        };
        MediaPlayer.ESAdded += (_, e) => RaiseTracksChanged(e.Type);
        MediaPlayer.ESDeleted += (_, e) => RaiseTracksChanged(e.Type);
        MediaPlayer.ESSelected += (_, e) => RaiseTracksChanged(e.Type);
        MediaPlayer.TimeChanged +=(_, e) => PositionChanged?.Invoke(this, TimeSpan.FromMilliseconds(e.Time));
        MediaPlayer.LengthChanged += (_, e) =>
        {
            _duration = TimeSpan.FromMilliseconds(Math.Max(0, e.Length));
            DurationChanged?.Invoke(this, _duration);
        };
    }

    /// <summary>
    /// Options applied to every LibVLC instance. They keep the player quiet and self-contained:
    /// no interface plugins, no network lookups for album art or metadata.
    /// </summary>
    private static readonly string[] SafeDefaults =
    [
        "--no-video-title-show",   // the app shows its own title
        "--no-osd",
        "--no-snapshot-preview",
        "--no-stats",
        "--no-lua",                // no script plugins (smaller attack surface)
        "--no-metadata-network-access",
    ];

    /// <summary>The underlying LibVLC player; the WinUI VideoView attaches to it.</summary>
    public MediaPlayer MediaPlayer { get; }

    public PlaybackState State => _state;
    public TimeSpan Position => TimeSpan.FromMilliseconds(Math.Max(0, MediaPlayer.Time));
    public TimeSpan Duration => _duration;
    public bool IsSeekable => MediaPlayer.IsSeekable;

    public int Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = PlaybackLimits.ClampVolume(value);
    }

    public bool IsMuted
    {
        get => MediaPlayer.Mute;
        set => MediaPlayer.Mute = value;
    }

    public float Rate
    {
        get => MediaPlayer.Rate;
        set => MediaPlayer.SetRate(PlaybackLimits.ClampRate(value));
    }

    // LibVLC stores delays in microseconds; one TimeSpan tick is 0.1 µs.
    public TimeSpan SubtitleDelay
    {
        get => TimeSpan.FromTicks(MediaPlayer.SpuDelay * 10);
        set => MediaPlayer.SetSpuDelay(PlaybackLimits.ClampDelay(value).Ticks / 10);
    }

    public TimeSpan AudioDelay
    {
        get => TimeSpan.FromTicks(MediaPlayer.AudioDelay * 10);
        set => MediaPlayer.SetAudioDelay(PlaybackLimits.ClampDelay(value).Ticks / 10);
    }

    /// <summary>Largest external subtitle file accepted; real subtitle files are far smaller.</summary>
    private const long MaxSubtitleFileBytes = 50L * 1024 * 1024;

    public event EventHandler? TracksChanged;
    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<TimeSpan>? DurationChanged;
    public event EventHandler<PlaybackError>? ErrorOccurred;

    public void Open(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _source = source;
        _duration = TimeSpan.Zero;
        var previous = _media;
        _media = new Media(_libVlc, source);
        MediaPlayer.Play(_media);
        previous?.Dispose();
    }

    public void Play()
    {
        if (_media is null) return;
        // After the end of the file, Play restarts from the beginning.
        if (_state is PlaybackState.Ended or PlaybackState.Stopped) MediaPlayer.Stop();
        MediaPlayer.Play();
    }

    public void Pause() => MediaPlayer.SetPause(true);

    public void TogglePlayPause()
    {
        if (_state == PlaybackState.Playing) Pause();
        else Play();
    }

    public void Stop() => MediaPlayer.Stop();

    public void Seek(TimeSpan position)
    {
        if (!MediaPlayer.IsSeekable) return;
        MediaPlayer.Time = (long)PlaybackLimits.ClampPosition(position, _duration).TotalMilliseconds;
    }

    public void SeekRelative(TimeSpan offset) => Seek(Position + offset);

    public IReadOnlyList<MediaTrack> GetTracks(TrackKind kind)
    {
        var descriptions = kind == TrackKind.Audio ? MediaPlayer.AudioTrackDescription : MediaPlayer.SpuDescription;
        return descriptions
            .Select(d => new MediaTrack(d.Id, string.IsNullOrWhiteSpace(d.Name) ? $"Track {d.Id}" : d.Name, kind))
            .ToArray();
    }

    public int GetSelectedTrackId(TrackKind kind) =>
        kind == TrackKind.Audio ? MediaPlayer.AudioTrack : MediaPlayer.Spu;

    public bool SelectTrack(TrackKind kind, int id) =>
        kind == TrackKind.Audio ? MediaPlayer.SetAudioTrack(id) : MediaPlayer.SetSpu(id);

    public bool AddSubtitleFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (_media is null || !Core.MediaFormats.IsSubtitle(path)) return false;

        var file = new FileInfo(path);
        if (!file.Exists || file.Length > MaxSubtitleFileBytes) return false;

        return MediaPlayer.AddSlave(MediaSlaveType.Subtitle, new Uri(file.FullName).AbsoluteUri, select: true);
    }

    private void RaiseTracksChanged(TrackType type)
    {
        if (type is TrackType.Audio or TrackType.Text) TracksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetState(PlaybackState state)
    {
        if (_state == state) return;
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        MediaPlayer.Stop();
        MediaPlayer.Dispose();
        _media?.Dispose();
        _libVlc.Dispose();
    }
}
