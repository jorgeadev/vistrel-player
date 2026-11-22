using Serilog;

namespace Vistrel.App;

/// <summary>
/// Local, rolling log files only. Nothing is sent anywhere; the logs stay on this PC
/// and the user can attach them to a bug report if they choose to.
/// </summary>
internal static class AppLog
{
    public static string Directory => AppPaths.LogDirectory;

    public static void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.File(
                Path.Combine(Directory, "vistrel-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
        Log.Information("Vistrel Player {Version} starting", typeof(AppLog).Assembly.GetName().Version);
    }
}
