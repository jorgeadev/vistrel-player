using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Serilog;
using Vistrel.App.ViewModels;
using Vistrel.Core;
using Vistrel.Core.History;
using Vistrel.Core.Playback;
using MediaTrack = Vistrel.Core.Playback.MediaTrack;
using Vistrel.Playback.Vlc;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Vistrel.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "WinUI windows are not IDisposable; the engine is disposed in the Closed handler.")]
public sealed partial class MainWindow : Window
{
    private VlcPlaybackEngine? _engine;
    private string? _pendingFile;

    public MainWindow()
    {
        var history = PlaybackHistory.Load(AppPaths.HistoryFile);
        if (history.LoadError is not null) Log.Warning("{Error}", history.LoadError);
        Vm = new PlayerViewModel(DispatcherQueue, history);
        InitializeComponent();
        Log.Debug("MainWindow: XAML loaded");

        AppWindow.SetIcon("Assets/app.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        ConfigureTitleBar();
        Log.Debug("MainWindow: window configured");

        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.Title)) Title = Vm.Title;
        };
        Closed += OnClosed;
    }

    public PlayerViewModel Vm { get; }

    /// <summary>
    /// Draws our own title bar in the app's chrome colour instead of the system one, which follows the
    /// Windows light/dark setting and clashed with the dark player. The caption buttons are recoloured to match.
    /// </summary>
    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var bar = AppWindow.TitleBar;
        bar.PreferredHeightOption = TitleBarHeightOption.Tall; // 48 px caption buttons, room for the drag area
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonForegroundColor = Color.FromArgb(0xFF, 0xE6, 0xEA, 0xF0);
        bar.ButtonInactiveForegroundColor = Color.FromArgb(0xFF, 0x6B, 0x74, 0x84);
        bar.ButtonHoverBackgroundColor = Color.FromArgb(0xFF, 0x24, 0x29, 0x33);
        bar.ButtonHoverForegroundColor = Colors.White;
        bar.ButtonPressedBackgroundColor = Color.FromArgb(0xFF, 0x2E, 0x34, 0x40);
        bar.ButtonPressedForegroundColor = Colors.White;
        AppTitleBar.Height = 48;
    }

    public static Visibility CollapsedWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisibleWhen(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Opens a file now, or as soon as the video surface is ready.</summary>
    public void OpenFile(string path)
    {
        if (_engine is null) { _pendingFile = path; return; }
        Vm.Open(path);
    }

    private async void VideoView_Initialized(object? sender, InitializedEventArgs e)
    {
        // LibVLC must not be created on the UI thread: its startup waits on work that needs
        // the UI thread (deadlock), and a cold first start can take seconds while plugins load.
        Log.Debug("VideoView initialized; creating LibVLC");
        var options = e.SwapChainOptions;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _engine = await Task.Run(() => new VlcPlaybackEngine(options));
            Log.Information("Playback engine started in {Ms} ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is VLCException or DllNotFoundException or InvalidOperationException)
        {
            Log.Error(ex, "Could not start the playback engine");
            Vm.ErrorMessage = "The video engine failed to start. Reinstalling Vistrel Player usually fixes this.";
            return;
        }
        VideoView.MediaPlayer = _engine.MediaPlayer;
        Vm.Attach(_engine);
        Log.Information("Video surface ready");

        if (_pendingFile is not null)
        {
            Vm.Open(_pendingFile);
            _pendingFile = null;
        }
    }

    private async void Open_Click(object sender, RoutedEventArgs e) => await PickAndOpenAsync();

    private async Task PickAndOpenAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary };
        foreach (var ext in MediaFormats.VideoExtensions.Concat(MediaFormats.AudioExtensions)) picker.FileTypeFilter.Add(ext);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file is not null) OpenFile(file.Path);
    }

    private async Task PickSubtitleAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary };
        foreach (var ext in MediaFormats.SubtitleExtensions) picker.FileTypeFilter.Add(ext);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file is not null) Vm.AddSubtitleFile(file.Path);
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Link;
            e.DragUIOverride.Caption = "Open";
        }
    }

    /// <summary>A dropped video or audio file plays; a dropped subtitle file is added to the current video.</summary>
    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var files = (await e.DataView.GetStorageItemsAsync()).OfType<StorageFile>().ToList();

        var media = files.FirstOrDefault(f => MediaFormats.IsPlayable(f.Path));
        if (media is not null) OpenFile(media.Path);

        var subtitle = files.FirstOrDefault(f => MediaFormats.IsSubtitle(f.Path));
        if (subtitle is not null && media is null) Vm.AddSubtitleFile(subtitle.Path);

        if (media is null && subtitle is null)
            Vm.ErrorMessage = "That file type isn't supported. Drop a video, audio or subtitle file.";
    }

    // ---- Track menus (rebuilt each time they open, so they always match the current media) ----

    private void AudioMenu_Opening(object sender, object e)
    {
        AudioMenu.Items.Clear();
        AddTrackItems(AudioMenu, TrackKind.Audio, includeOff: false);
        AudioMenu.Items.Add(new MenuFlyoutSeparator());
        AudioMenu.Items.Add(Item("Play audio earlier (−50 ms)", Vm.AudioEarlierCommand));
        AudioMenu.Items.Add(Item("Play audio later (+50 ms)", Vm.AudioLaterCommand));
        AudioMenu.Items.Add(Item("Reset audio delay", Vm.ResetAudioDelayCommand));
    }

    private void SubtitleMenu_Opening(object sender, object e)
    {
        SubtitleMenu.Items.Clear();
        AddTrackItems(SubtitleMenu, TrackKind.Subtitle, includeOff: true);
        SubtitleMenu.Items.Add(new MenuFlyoutSeparator());
        var add = new MenuFlyoutItem { Text = "Add subtitle file…", Icon = new FontIcon { Glyph = "" } };
        add.Click += async (_, _) => await PickSubtitleAsync();
        SubtitleMenu.Items.Add(add);
        SubtitleMenu.Items.Add(new MenuFlyoutSeparator());
        SubtitleMenu.Items.Add(Item("Show subtitles earlier (G)", Vm.SubtitlesEarlierCommand));
        SubtitleMenu.Items.Add(Item("Show subtitles later (H)", Vm.SubtitlesLaterCommand));
        SubtitleMenu.Items.Add(Item("Reset subtitle delay", Vm.ResetSubtitleDelayCommand));
    }

    private void AddTrackItems(MenuFlyout menu, TrackKind kind, bool includeOff)
    {
        var tracks = Vm.GetTracks(kind);
        var selected = Vm.GetSelectedTrackId(kind);
        var group = kind.ToString();

        if (includeOff)
        {
            var off = new RadioMenuFlyoutItem { Text = "Off", GroupName = group, IsChecked = selected == MediaTrack.DisabledId };
            off.Click += (_, _) => Vm.SelectTrack(kind, MediaTrack.DisabledId);
            menu.Items.Add(off);
        }
        if (tracks.Count == 0)
        {
            menu.Items.Add(new MenuFlyoutItem
            {
                Text = kind == TrackKind.Audio ? "No audio tracks" : "No subtitles in this file",
                IsEnabled = false,
            });
        }
        foreach (var track in tracks)
        {
            var item = new RadioMenuFlyoutItem { Text = track.Name, GroupName = group, IsChecked = track.Id == selected };
            item.Click += (_, _) => Vm.SelectTrack(kind, track.Id);
            menu.Items.Add(item);
        }
    }

    private static MenuFlyoutItem Item(string text, System.Windows.Input.ICommand command) =>
        new() { Text = text, Command = command };

    private void ToggleFullscreen()
    {
        var full = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        AppWindow.SetPresenter(full ? AppWindowPresenterKind.Default : AppWindowPresenterKind.FullScreen);
        // Full screen shows only the video and controls; the title bar returns with the window.
        AppTitleBar.Visibility = full ? Visibility.Visible : Visibility.Collapsed;
        ControlBar.Visibility = Visibility.Visible;
        FullscreenIcon.Glyph = full ? "" : ""; // Full screen / Back to window
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();
    private void Video_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => ToggleFullscreen();

    // Keyboard shortcuts
    private void Accel_PlayPause(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.TogglePlayPauseCommand.Execute);
    private void Accel_SeekBack(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.SeekBackwardCommand.Execute);
    private void Accel_SeekForward(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.SeekForwardCommand.Execute);
    private void Accel_JumpBack(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.JumpBackwardCommand.Execute);
    private void Accel_JumpForward(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.JumpForwardCommand.Execute);
    private void Accel_VolumeUp(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.VolumeUpCommand.Execute);
    private void Accel_VolumeDown(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.VolumeDownCommand.Execute);
    private void Accel_Mute(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.ToggleMuteCommand.Execute);
    private void Accel_Fullscreen(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, _ => ToggleFullscreen());
    private void Accel_Open(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, _ => _ = PickAndOpenAsync());
    private void Accel_SubtitlesEarlier(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.SubtitlesEarlierCommand.Execute);
    private void Accel_SubtitlesLater(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.SubtitlesLaterCommand.Execute);
    private void Accel_CycleSubtitles(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.CycleSubtitlesCommand.Execute);
    private void Accel_CycleAudio(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.CycleAudioCommand.Execute);
    private void Accel_StartOver(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) => Handle(e, Vm.StartOverCommand.Execute);

    private void RecentFiles_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentFileItem item) OpenFile(item.Path);
    }

    private void Accel_ExitFullscreen(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
    {
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen) Handle(e, _ => ToggleFullscreen());
    }

    private static void Handle(KeyboardAcceleratorInvokedEventArgs e, Action<object?> action)
    {
        e.Handled = true;
        action(null);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Vm.SaveProgressNow(); // before the engine stops, while the position is still known
        VideoView.MediaPlayer = null;
        _engine?.Dispose();
        Log.Information("Main window closed");
        App.Shutdown();
    }
}
