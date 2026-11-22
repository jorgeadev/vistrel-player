using Vistrel.Core.History;
using Xunit;

namespace Vistrel.Core.Tests;

public sealed class PlaybackHistoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vistrel-tests", Guid.NewGuid().ToString("N"));
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero));
    private string HistoryFile => Path.Combine(_dir, "history.json");
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private PlaybackHistory NewHistory() => PlaybackHistory.Load(HistoryFile, _time);

    [Fact]
    public void Resumes_a_little_before_where_the_user_stopped()
    {
        var h = NewHistory();
        h.Record(@"C:\Videos\movie.mkv", TimeSpan.FromMinutes(42), Hour);
        Assert.Equal(TimeSpan.FromMinutes(42) - PlaybackHistory.ResumeRewind, h.GetResumePosition(@"C:\Videos\movie.mkv"));
    }

    [Fact]
    public void Paths_match_case_insensitively_and_after_normalising()
    {
        var h = NewHistory();
        h.Record(@"C:\Videos\Sub\..\Movie.MKV", TimeSpan.FromMinutes(5), Hour);
        Assert.NotNull(h.GetResumePosition(@"c:\videos\movie.mkv"));
    }

    [Theory]
    [InlineData(5, 3600)]      // too early to be worth resuming
    [InlineData(3590, 3600)]   // within 15 s of the end: finished
    [InlineData(3500, 3600)]   // within the last 5%: finished
    [InlineData(30, 45)]       // clip shorter than a minute
    public void Does_not_resume_near_start_end_or_for_short_clips(int positionSeconds, int durationSeconds)
    {
        var h = NewHistory();
        h.Record(@"C:\v.mp4", TimeSpan.FromSeconds(positionSeconds), TimeSpan.FromSeconds(durationSeconds));
        Assert.Null(h.GetResumePosition(@"C:\v.mp4"));
    }

    [Fact]
    public void Finished_files_stay_in_recents_without_a_resume_point()
    {
        var h = NewHistory();
        h.Record(@"C:\done.mp4", Hour, Hour);
        var entry = Assert.Single(h.GetRecent(10));
        Assert.False(entry.HasResumePoint);
    }

    [Fact]
    public void Recent_files_are_newest_first()
    {
        var h = NewHistory();
        h.Record(@"C:\a.mp4", TimeSpan.FromMinutes(1), Hour);
        _time.Advance(TimeSpan.FromMinutes(1));
        h.Record(@"C:\b.mp4", TimeSpan.FromMinutes(1), Hour);
        _time.Advance(TimeSpan.FromMinutes(1));
        h.Record(@"C:\a.mp4", TimeSpan.FromMinutes(2), Hour);

        Assert.Equal([@"C:\a.mp4", @"C:\b.mp4"], h.GetRecent(10).Select(e => e.Path));
        Assert.Single(h.GetRecent(1));
    }

    [Fact]
    public void Keeps_at_most_the_newest_entries()
    {
        var h = NewHistory();
        for (var i = 0; i < PlaybackHistory.MaxEntries + 25; i++)
        {
            h.Record($@"C:\v\{i}.mp4", TimeSpan.FromMinutes(1), Hour);
            _time.Advance(TimeSpan.FromSeconds(1));
        }
        var recent = h.GetRecent(int.MaxValue);
        Assert.Equal(PlaybackHistory.MaxEntries, recent.Count);
        Assert.DoesNotContain(recent, e => e.Path == @"C:\v\0.mp4");
    }

    [Fact]
    public void Saves_and_loads_round_trip_without_leaving_temp_files()
    {
        var h = NewHistory();
        h.Record(@"C:\Videos\movie.mkv", TimeSpan.FromMinutes(42), Hour);
        h.Save();

        var reloaded = NewHistory();
        Assert.Null(reloaded.LoadError);
        Assert.Equal(h.GetResumePosition(@"C:\Videos\movie.mkv"), reloaded.GetResumePosition(@"C:\Videos\movie.mkv"));
        Assert.Equal(h.GetRecent(5), reloaded.GetRecent(5));
        Assert.Equal(["history.json"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"version":1,"entries":[{"path":"C:\\x.mp4","positionMs":-5,"durationMs":1000,"lastPlayed":"2026-01-01T00:00:00Z"}]}""")]
    public void Damaged_or_invalid_files_start_empty(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(HistoryFile, content);
        var h = NewHistory();
        Assert.Empty(h.GetRecent(10));
    }

    [Fact]
    public void Oversized_file_is_ignored()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(HistoryFile, new string(' ', 3 * 1024 * 1024));
        var h = NewHistory();
        Assert.NotNull(h.LoadError);
        Assert.Empty(h.GetRecent(10));
    }

    [Fact]
    public void Remove_and_clear_forget_files()
    {
        var h = NewHistory();
        h.Record(@"C:\a.mp4", TimeSpan.FromMinutes(5), Hour);
        h.Record(@"C:\b.mp4", TimeSpan.FromMinutes(5), Hour);
        h.Remove(@"C:\A.mp4");
        Assert.Equal([@"C:\b.mp4"], h.GetRecent(10).Select(e => e.Path));
        h.Clear();
        Assert.Empty(h.GetRecent(10));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ignores_blank_paths(string path)
    {
        var h = NewHistory();
        h.Record(path, TimeSpan.FromMinutes(5), Hour);
        Assert.Empty(h.GetRecent(10));
        Assert.Null(h.GetResumePosition(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
