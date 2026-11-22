using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Serilog;
using Vistrel.Core;
using Vistrel.Core.Playback;

namespace Vistrel.App.ViewModels;

/// <summary>
/// UI state for the player. Engine events arrive on LibVLC threads and are marshalled
/// to the UI thread here, so the view can bind directly.
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject
{
    private static readonly TimeSpan SmallSeek = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LargeSeek = TimeSpan.FromSeconds(30);

    private readonly DispatcherQueue _dispatcher;
    private IPlaybackEngine? _engine;
    private bool _updatingFromEngine;

    private static readonly TimeSpan SubtitleDelayStep = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan AudioDelayStep = TimeSpan.FromMilliseconds(50);
    private readonly DispatcherQueueTimer _osdTimer;

    public PlayerViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        _osdTimer = dispatcher.CreateTimer();
        _osdTimer.Interval = TimeSpan.FromSeconds(1.6);
        _osdTimer.IsRepeating = false;
        _osdTimer.Tick += (_, _) => IsOsdVisible = false;
    }

    /// <summary>Short on-screen message such as "Subtitle delay +300 ms".</summary>
    [ObservableProperty] public partial string OsdText { get; set; } = "";
    [ObservableProperty] public partial bool IsOsdVisible { get; set; }

    [ObservableProperty] public partial string Title { get; set; } = "Vistrel Player";
    [ObservableProperty] public partial bool HasMedia { get; set; }
    [ObservableProperty] public partial bool IsPlaying { get; set; }
    [ObservableProperty] public partial double PositionSeconds { get; set; }
    [ObservableProperty] public partial double DurationSeconds { get; set; } = 1;
    [ObservableProperty] public partial string PositionText { get; set; } = "0:00";
    [ObservableProperty] public partial string DurationText { get; set; } = "0:00";
    [ObservableProperty] public partial double Volume { get; set; } = 100;
    [ObservableProperty] public partial bool IsMuted { get; set; }
    [ObservableProperty] public partial string RateText { get; set; } = "1×";
    [ObservableProperty] public partial string? ErrorMessage { get; set; }

    public bool HasError => ErrorMessage is not null;
    public string PlayPauseGlyph => IsPlaying ? "" : ""; // Pause / Play
    public string PlayPauseLabel => IsPlaying ? "Pause (Space)" : "Play (Space)";
    public string MuteGlyph => IsMuted || Volume <= 0 ? "" : ""; // Mute / Volume

    /// <summary>Connects the engine once the video surface is ready.</summary>
    public void Attach(IPlaybackEngine engine)
    {
        _engine = engine;
        engine.Volume = (int)Volume;
        engine.StateChanged += (_, s) => OnUi(() => ApplyState(s));
        engine.PositionChanged += (_, p) => OnUi(() => ApplyPosition(p));
        engine.DurationChanged += (_, d) => OnUi(() => ApplyDuration(d));
        engine.ErrorOccurred += (_, e) =>
        {
            Log.Warning("Playback error for {Source}: {Message}", e.Source, e.Message);
            OnUi(() => ErrorMessage = e.Message);
        };
    }

    public void Open(string path)
    {
        if (_engine is null) return;
        ErrorMessage = null;
        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            ErrorMessage = $"\"{path}\" is not a valid file path.";
            return;
        }
        Log.Information("Opening {Path}", path);
        Title = $"{Path.GetFileName(path)} – Vistrel Player";
        HasMedia = true;
        ApplyPosition(TimeSpan.Zero);
        _engine.Open(uri);
    }

    // ---- Tracks and delays ----

    public IReadOnlyList<MediaTrack> GetTracks(TrackKind kind) =>
        _engine is null ? [] : _engine.GetTracks(kind).Where(t => !t.IsDisabled).ToList();

    public int GetSelectedTrackId(TrackKind kind) => _engine?.GetSelectedTrackId(kind) ?? MediaTrack.DisabledId;

    public void SelectTrack(TrackKind kind, int id)
    {
        if (_engine is null) return;
        _engine.SelectTrack(kind, id);
        var name = id == MediaTrack.DisabledId ? "Off" : GetTracks(kind).FirstOrDefault(t => t.Id == id)?.Name ?? "Off";
        ShowOsd(kind == TrackKind.Audio ? $"Audio: {name}" : $"Subtitles: {name}");
    }

    /// <summary>Selects the next track; for subtitles the cycle includes "Off".</summary>
    public void CycleTrack(TrackKind kind)
    {
        var ids = GetTracks(kind).Select(t => t.Id).ToList();
        if (kind == TrackKind.Subtitle) ids.Add(MediaTrack.DisabledId);
        if (ids.Count == 0) { ShowOsd(kind == TrackKind.Audio ? "No audio tracks" : "No subtitles"); return; }
        var next = ids[(ids.IndexOf(GetSelectedTrackId(kind)) + 1) % ids.Count];
        SelectTrack(kind, next);
    }

    public void AddSubtitleFile(string path)
    {
        if (_engine is null || !HasMedia)
        {
            ErrorMessage = "Open a video first, then add its subtitle file.";
            return;
        }
        if (_engine.AddSubtitleFile(path))
        {
            Log.Information("Added subtitle file {Path}", path);
            ShowOsd($"Subtitles: {Path.GetFileName(path)}");
        }
        else
        {
            ErrorMessage = $"\"{Path.GetFileName(path)}\" couldn't be loaded. Use an .srt, .vtt, .ass or .ssa file under 50 MB.";
        }
    }

    [RelayCommand]
    private void SubtitlesEarlier() => AdjustSubtitleDelay(-SubtitleDelayStep);

    [RelayCommand]
    private void SubtitlesLater() => AdjustSubtitleDelay(SubtitleDelayStep);

    [RelayCommand]
    private void ResetSubtitleDelay() => AdjustSubtitleDelay(null);

    [RelayCommand]
    private void AudioEarlier() => AdjustAudioDelay(-AudioDelayStep);

    [RelayCommand]
    private void AudioLater() => AdjustAudioDelay(AudioDelayStep);

    [RelayCommand]
    private void ResetAudioDelay() => AdjustAudioDelay(null);

    [RelayCommand]
    private void CycleSubtitles() => CycleTrack(TrackKind.Subtitle);

    [RelayCommand]
    private void CycleAudio() => CycleTrack(TrackKind.Audio);

    /// <param name="step">Amount to add, or null to reset to zero.</param>
    private void AdjustSubtitleDelay(TimeSpan? step)
    {
        if (_engine is null || !HasMedia) return;
        _engine.SubtitleDelay = step is { } s ? _engine.SubtitleDelay + s : TimeSpan.Zero;
        ShowOsd($"Subtitle delay {PlaybackLimits.FormatDelay(_engine.SubtitleDelay)}");
    }

    private void AdjustAudioDelay(TimeSpan? step)
    {
        if (_engine is null || !HasMedia) return;
        _engine.AudioDelay = step is { } s ? _engine.AudioDelay + s : TimeSpan.Zero;
        ShowOsd($"Audio delay {PlaybackLimits.FormatDelay(_engine.AudioDelay)}");
    }

    public void ShowOsd(string text)
    {
        OsdText = text;
        IsOsdVisible = true;
        _osdTimer.Stop();
        _osdTimer.Start();
    }

    // ---- Transport ----

    [RelayCommand]
    private void TogglePlayPause() => _engine?.TogglePlayPause();

    [RelayCommand]
    private void SeekForward() => _engine?.SeekRelative(SmallSeek);

    [RelayCommand]
    private void SeekBackward() => _engine?.SeekRelative(-SmallSeek);

    [RelayCommand]
    private void JumpForward() => _engine?.SeekRelative(LargeSeek);

    [RelayCommand]
    private void JumpBackward() => _engine?.SeekRelative(-LargeSeek);

    [RelayCommand]
    private void VolumeUp()
    {
        Volume = Math.Min(PlaybackLimits.MaxVolume, Volume + 5);
        ShowOsd($"Volume {Volume:0}%");
    }

    [RelayCommand]
    private void VolumeDown()
    {
        Volume = Math.Max(0, Volume - 5);
        ShowOsd($"Volume {Volume:0}%");
    }

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void SetRate(string rate)
    {
        if (_engine is null || !float.TryParse(rate, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r)) return;
        _engine.Rate = r;
        RateText = $"{PlaybackLimits.ClampRate(r).ToString(System.Globalization.CultureInfo.CurrentCulture)}×";
        ShowOsd($"Speed {RateText}");
    }

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    partial void OnPositionSecondsChanged(double value)
    {
        PositionText = TimeFormatter.Format(TimeSpan.FromSeconds(value));
        // Only a user drag or keyboard change on the seek bar seeks; engine updates do not.
        if (!_updatingFromEngine) _engine?.Seek(TimeSpan.FromSeconds(value));
    }

    partial void OnVolumeChanged(double value)
    {
        if (_engine is not null) _engine.Volume = (int)value;
        OnPropertyChanged(nameof(MuteGlyph));
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (_engine is not null) _engine.IsMuted = value;
        OnPropertyChanged(nameof(MuteGlyph));
    }

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(PlayPauseLabel));
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void ApplyState(PlaybackState state)
    {
        Log.Debug("Playback state: {State} at {Position}", state, PositionText);
        IsPlaying = state is PlaybackState.Playing or PlaybackState.Buffering or PlaybackState.Opening;
        if (state == PlaybackState.Ended) ApplyPosition(TimeSpan.FromSeconds(DurationSeconds));
    }

    private void ApplyPosition(TimeSpan position)
    {
        _updatingFromEngine = true;
        try { PositionSeconds = Math.Min(position.TotalSeconds, DurationSeconds); }
        finally { _updatingFromEngine = false; }
    }

    private void ApplyDuration(TimeSpan duration)
    {
        DurationSeconds = Math.Max(1, duration.TotalSeconds);
        DurationText = TimeFormatter.Format(duration);
    }

    private void OnUi(Action action) => _dispatcher.TryEnqueue(() => action());
}
