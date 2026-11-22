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

    public PlayerViewModel(DispatcherQueue dispatcher) => _dispatcher = dispatcher;

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
    private void VolumeUp() => Volume = Math.Min(PlaybackLimits.MaxVolume, Volume + 5);

    [RelayCommand]
    private void VolumeDown() => Volume = Math.Max(0, Volume - 5);

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void SetRate(string rate)
    {
        if (_engine is null || !float.TryParse(rate, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r)) return;
        _engine.Rate = r;
        RateText = $"{PlaybackLimits.ClampRate(r).ToString(System.Globalization.CultureInfo.CurrentCulture)}×";
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
