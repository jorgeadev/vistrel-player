namespace Vistrel.App;

/// <summary>Where the app keeps its own files. Everything stays on this PC.</summary>
internal static class AppPaths
{
    /// <summary>
    /// Packaged (MSIX): the app's private LocalState folder, removed on uninstall.
    /// Unpackaged dev runs: %LOCALAPPDATA%\Vistrel.
    /// </summary>
    public static string DataDirectory { get; } = ResolveDataDirectory();

    public static string LogDirectory => Ensure(Path.Combine(DataDirectory, "logs"));

    public static string HistoryFile => Path.Combine(DataDirectory, "history.json");

    private static string ResolveDataDirectory()
    {
        string root;
        try
        {
            root = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }
        catch (InvalidOperationException)
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vistrel");
        }
        return Ensure(root);
    }

    private static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}
