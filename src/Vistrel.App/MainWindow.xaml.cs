using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Serilog;
using Vistrel.App.ViewModels;
using Vistrel.Core;
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
        Vm = new PlayerViewModel(DispatcherQueue);
        InitializeComponent();
        Log.Debug("MainWindow: XAML loaded");

        AppWindow.SetIcon("Assets/app.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        // Mica on Windows 11; acrylic on Windows 10, where Mica is not available.
        SystemBackdrop = MicaController.IsSupported() ? new MicaBackdrop() : new DesktopAcrylicBackdrop();
        Log.Debug("MainWindow: window configured");

        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.Title)) Title = Vm.Title;
        };
        Closed += OnClosed;
    }

    public PlayerViewModel Vm { get; }

    public static Visibility CollapsedWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

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

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Link;
            e.DragUIOverride.Caption = "Play";
        }
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        var file = items.OfType<StorageFile>().FirstOrDefault(f => MediaFormats.IsPlayable(f.Path));
        if (file is not null) OpenFile(file.Path);
        else Vm.ErrorMessage = "That file type isn't supported. Drop a video or audio file.";
    }

    private void ToggleFullscreen()
    {
        var full = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        AppWindow.SetPresenter(full ? AppWindowPresenterKind.Default : AppWindowPresenterKind.FullScreen);
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
        VideoView.MediaPlayer = null;
        _engine?.Dispose();
        Log.Information("Main window closed");
        App.Shutdown();
    }
}
