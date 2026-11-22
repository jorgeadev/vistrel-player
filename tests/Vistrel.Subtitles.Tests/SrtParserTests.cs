using Vistrel.Subtitles;
using Xunit;

namespace Vistrel.Subtitles.Tests;

public class SrtParserTests
{
    private const string Sample = "﻿1\r\n00:00:01,000 --> 00:00:03,500\r\nHello there.\r\n\r\n2\r\n00:00:04,000 --> 00:00:06,000\r\nTwo\r\nlines\r\n\r\n";

    [Fact]
    public void Parses_standard_file_with_bom_and_crlf()
    {
        var cues = SrtParser.Parse(Sample);
        Assert.Equal(2, cues.Count);
        Assert.Equal(new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3.5), "Hello there."), cues[0]);
        Assert.Equal("Two\nlines", cues[1].Text);
    }

    [Fact]
    public void Accepts_dot_separator_short_millis_and_missing_numbers()
    {
        var cues = SrtParser.Parse("00:00:01.5 --> 00:00:02.05\nShort millis\n");
        var cue = Assert.Single(cues);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), cue.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(2050), cue.End);
    }

    [Fact]
    public void Handles_cues_without_blank_separator()
    {
        var cues = SrtParser.Parse("00:00:01,000 --> 00:00:02,000\nA\n00:00:03,000 --> 00:00:04,000\nB\n");
        Assert.Equal(["A", "B"], cues.Select(c => c.Text));
    }

    [Fact]
    public void Skips_malformed_and_backwards_cues()
    {
        var cues = SrtParser.Parse("garbage\n\n1\n00:00:05,000 --> 00:00:04,000\nBackwards\n\n2\n00:00:06,000 --> 00:00:07,000\nGood\n");
        Assert.Equal("Good", Assert.Single(cues).Text);
    }

    [Fact]
    public void Sorts_cues_by_start_time()
    {
        var cues = SrtParser.Parse("00:00:09,000 --> 00:00:10,000\nLate\n\n00:00:01,000 --> 00:00:02,000\nEarly\n");
        Assert.Equal(["Early", "Late"], cues.Select(c => c.Text));
    }

    [Fact]
    public void Enforces_limits_on_hostile_input()
    {
        var limits = new SubtitleParseLimits { MaxCues = 2, MaxCueTextLength = 5, MaxLinesPerCue = 2, MaxInputLength = 10_000 };
        var block = "00:00:01,000 --> 00:00:02,000\nline one is long\nline2\nline3\n\n";
        var cues = SrtParser.Parse(string.Concat(Enumerable.Repeat(block, 5)), limits);
        Assert.Equal(2, cues.Count);
        Assert.All(cues, c => Assert.True(c.Text.Length <= 5));

        Assert.Throws<SubtitleFormatException>(() => SrtParser.Parse(new string('x', 10_001), limits));
    }

    [Fact]
    public void Write_then_parse_round_trips()
    {
        var original = SrtParser.Parse(Sample);
        var again = SrtParser.Parse(SrtParser.Write(original));
        Assert.Equal(original, again);
    }

    [Fact]
    public void Write_formats_long_times()
    {
        var srt = SrtParser.Write([new SubtitleCue(TimeSpan.FromHours(26.5), TimeSpan.FromHours(26.5) + TimeSpan.FromSeconds(1), "x")]);
        Assert.Contains("26:30:00,000 --> 26:30:01,000", srt, StringComparison.Ordinal);
    }
}
