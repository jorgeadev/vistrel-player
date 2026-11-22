using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace Vistrel.App;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        AppLog.Initialize();
        UnhandledException += (_, e) => Log.Error(e.Exception, "Unhandled UI exception");
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

        var startupFile = GetStartupFile(args);
        if (startupFile is not null) _window.OpenFile(startupFile);
    }

    /// <summary>
    /// The file the app was started with: from a file association / "Open with" (packaged),
    /// or from the command line (unpackaged runs and shell integration).
    /// </summary>
    private static string? GetStartupFile(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Kind == ExtendedActivationKind.File &&
                activation.Data is IFileActivatedEventArgs fileArgs &&
                fileArgs.Files.Count > 0 && fileArgs.Files[0] is IStorageFile file)
            {
                return file.Path;
            }
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "Could not read activation arguments");
        }

        var cmd = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(a => !a.StartsWith('-'));
        if (!string.IsNullOrWhiteSpace(cmd)) return cmd;
        return string.IsNullOrWhiteSpace(args.Arguments) ? null : args.Arguments.Trim('"');
    }

    internal static void Shutdown() => Log.CloseAndFlush();
}
