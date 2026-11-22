using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vistrel.Subtitles;

/// <summary>
/// Tolerant SubRip (.srt) parser. Accepts CRLF/LF/CR line endings, a byte-order mark,
/// missing or wrong cue numbers, "," or "." before milliseconds, and 1-3 digit milliseconds.
/// Malformed blocks are skipped rather than failing the whole file.
/// </summary>
public static partial class SrtParser
{
    [GeneratedRegex(
        @"^\s*(\d{1,3}):(\d{1,2}):(\d{1,2})[,.](\d{1,3})\s*-->\s*(\d{1,3}):(\d{1,2}):(\d{1,2})[,.](\d{1,3})",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex TimingLine();

    public static IReadOnlyList<SubtitleCue> Parse(string content, SubtitleParseLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        limits ??= SubtitleParseLimits.Default;
        if (content.Length > limits.MaxInputLength)
            throw new SubtitleFormatException($"Subtitle file is too large ({content.Length:N0} characters).");

        var lines = content.TrimStart('﻿').ReplaceLineEndings("\n").Split('\n');
        var cues = new List<SubtitleCue>();
        var text = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var match = TimingLine().Match(lines[i]);
            if (!match.Success) continue;

            var start = ToTime(match, 1);
            var end = ToTime(match, 5);

            text.Clear();
            var lineCount = 0;
            for (i++; i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]); i++)
            {
                // A timing line right after another means the previous cue had no blank separator.
                if (TimingLine().IsMatch(lines[i])) { i--; break; }
                if (lineCount++ >= limits.MaxLinesPerCue) continue;
                if (text.Length > 0) text.Append('\n');
                text.Append(lines[i].TrimEnd());
            }

            if (end <= start || text.Length == 0) continue;
            var cueText = text.Length > limits.MaxCueTextLength
                ? text.ToString(0, limits.MaxCueTextLength)
                : text.ToString();

            cues.Add(new SubtitleCue(start, end, cueText));
            if (cues.Count >= limits.MaxCues) break;
        }

        cues.Sort((a, b) => a.Start.CompareTo(b.Start));
        return cues;
    }

    private static TimeSpan ToTime(Match m, int first)
    {
        int Part(int offset) => int.Parse(m.Groups[first + offset].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);

        var msText = m.Groups[first + 3].Value;
        // "5" means 500 ms and "05" means 50 ms, as in a decimal fraction.
        var ms = int.Parse(msText.PadRight(3, '0'), NumberStyles.None, CultureInfo.InvariantCulture);
        return new TimeSpan(0, Part(0), Part(1), Part(2), ms);
    }

    /// <summary>Writes cues back out as a standard .srt document (used later for AI-generated subtitles).</summary>
    public static string Write(IEnumerable<SubtitleCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        var sb = new StringBuilder();
        var n = 1;
        foreach (var cue in cues)
        {
            sb.Append(n++).Append('\n')
              .Append(Stamp(cue.Start)).Append(" --> ").Append(Stamp(cue.End)).Append('\n')
              .Append(cue.Text).Append("\n\n");
        }
        return sb.ToString();

        static string Stamp(TimeSpan t) => string.Create(CultureInfo.InvariantCulture,
            $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}");
    }
}
