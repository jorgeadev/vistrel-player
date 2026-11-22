namespace Vistrel.Core;

/// <summary>
/// File types the player opens. Keep in sync with the file type associations in
/// src/Vistrel.App/Package.appxmanifest.
/// </summary>
public static class MediaFormats
{
    public static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".ts", ".m2ts", ".mts", ".mpg", ".mpeg", ".flv", ".3gp", ".ogv",
    };

    public static readonly IReadOnlySet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wav", ".wma",
    };

    public static readonly IReadOnlySet<string> SubtitleExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".vtt", ".ass", ".ssa",
    };

    public static bool IsPlayable(string path)
    {
        var ext = Path.GetExtension(path);
        return VideoExtensions.Contains(ext) || AudioExtensions.Contains(ext);
    }

    public static bool IsSubtitle(string path) => SubtitleExtensions.Contains(Path.GetExtension(path));
}
