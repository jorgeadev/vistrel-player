namespace Vistrel.Subtitles;

/// <summary>One subtitle line (or block of lines) shown between <see cref="Start"/> and <see cref="End"/>.</summary>
public sealed record SubtitleCue(TimeSpan Start, TimeSpan End, string Text);

/// <summary>
/// Limits that keep a hostile or corrupt subtitle file from exhausting memory or CPU.
/// Subtitle files come from untrusted sources, so every parser must honour these.
/// </summary>
public sealed record SubtitleParseLimits
{
    public static SubtitleParseLimits Default { get; } = new();

    /// <summary>Largest input accepted, in characters (about 20 MB of text).</summary>
    public int MaxInputLength { get; init; } = 20_000_000;
    public int MaxCues { get; init; } = 100_000;
    public int MaxCueTextLength { get; init; } = 2_000;
    public int MaxLinesPerCue { get; init; } = 10;
}

public sealed class SubtitleFormatException(string message) : Exception(message);
