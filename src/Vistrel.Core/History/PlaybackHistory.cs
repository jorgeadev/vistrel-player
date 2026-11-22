using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vistrel.Core.History;

/// <summary>A file the user played: where they stopped and when.</summary>
/// <param name="Path">Full path of the media file.</param>
/// <param name="Position">Where to resume; <see cref="TimeSpan.Zero"/> means start from the beginning.</param>
public sealed record HistoryEntry(string Path, TimeSpan Position, TimeSpan Duration, DateTimeOffset LastPlayed)
{
    public bool HasResumePoint => Position > TimeSpan.Zero;
}

/// <summary>
/// Resume positions and recently played files, kept in one small JSON file on this PC.
/// Thread-safe: the UI records positions while saves run on a background thread.
/// </summary>
public sealed class PlaybackHistory
{
    /// <summary>Most entries kept; the oldest are dropped first.</summary>
    public const int MaxEntries = 200;

    /// <summary>Positions before this are not worth resuming.</summary>
    public static readonly TimeSpan MinResumePosition = TimeSpan.FromSeconds(10);

    /// <summary>Clips shorter than this always start from the beginning.</summary>
    public static readonly TimeSpan MinResumeDuration = TimeSpan.FromMinutes(1);

    /// <summary>Stopping this close to the end (or in the last 5%) counts as finished.</summary>
    public static readonly TimeSpan EndMargin = TimeSpan.FromSeconds(15);

    /// <summary>Resuming rewinds a little so the viewer gets their bearings.</summary>
    public static readonly TimeSpan ResumeRewind = TimeSpan.FromSeconds(3);

    /// <summary>History files larger than this are ignored as damaged.</summary>
    private const long MaxFileBytes = 2 * 1024 * 1024;

    private readonly string _filePath;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, HistoryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    private PlaybackHistory(string filePath, TimeProvider? time)
    {
        _filePath = filePath;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Why the saved history couldn't be read, if it couldn't. The history then starts empty.</summary>
    public string? LoadError { get; private set; }

    /// <summary>Reads the history file. A missing, oversized or damaged file gives an empty history.</summary>
    public static PlaybackHistory Load(string filePath, TimeProvider? time = null)
    {
        var history = new PlaybackHistory(filePath, time);
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists) return history;
            if (info.Length > MaxFileBytes)
            {
                history.LoadError = $"History file is too large ({info.Length:N0} bytes) and was ignored.";
                return history;
            }

            var dto = JsonSerializer.Deserialize(File.ReadAllText(filePath), HistoryJsonContext.Default.HistoryFileDto);
            foreach (var e in dto?.Entries ?? [])
            {
                if (string.IsNullOrWhiteSpace(e.Path) || e.PositionMs < 0 || e.DurationMs < 0) continue;
                history._entries[e.Path] = new HistoryEntry(e.Path,
                    TimeSpan.FromMilliseconds(e.PositionMs), TimeSpan.FromMilliseconds(e.DurationMs), e.LastPlayed);
            }
            history.Prune();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            history._entries.Clear();
            history.LoadError = $"History file could not be read and was ignored: {ex.Message}";
        }
        return history;
    }

    /// <summary>Remembers how far the user got in a file.</summary>
    public void Record(string path, TimeSpan position, TimeSpan duration)
    {
        var key = Normalize(path);
        if (key is null) return;

        if (position < MinResumePosition || IsFinished(position, duration)) position = TimeSpan.Zero;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

        lock (_lock)
        {
            _entries[key] = new HistoryEntry(key, position, duration, _time.GetUtcNow());
            Prune();
        }
    }

    /// <summary>Where to resume this file, or null to start from the beginning.</summary>
    public TimeSpan? GetResumePosition(string path)
    {
        var key = Normalize(path);
        if (key is null) return null;
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var e) || !e.HasResumePoint) return null;
            if (e.Duration > TimeSpan.Zero && e.Duration < MinResumeDuration) return null;
            var target = e.Position - ResumeRewind;
            return target > TimeSpan.Zero ? target : TimeSpan.Zero;
        }
    }

    /// <summary>Most recently played first.</summary>
    public IReadOnlyList<HistoryEntry> GetRecent(int count)
    {
        lock (_lock)
        {
            return _entries.Values.OrderByDescending(e => e.LastPlayed).Take(Math.Max(0, count)).ToList();
        }
    }

    public void Remove(string path)
    {
        var key = Normalize(path);
        if (key is null) return;
        lock (_lock) _entries.Remove(key);
    }

    public void Clear()
    {
        lock (_lock) _entries.Clear();
    }

    /// <summary>
    /// Writes the history to disk. The file is written to a temporary name first and then swapped in,
    /// so a crash or power loss mid-save never leaves a half-written history.
    /// </summary>
    public void Save()
    {
        HistoryFileDto dto;
        lock (_lock)
        {
            dto = new HistoryFileDto(1, _entries.Values
                .OrderByDescending(e => e.LastPlayed)
                .Select(e => new HistoryEntryDto(e.Path, (long)e.Position.TotalMilliseconds, (long)e.Duration.TotalMilliseconds, e.LastPlayed))
                .ToList());
        }

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var temp = _filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(dto, HistoryJsonContext.Default.HistoryFileDto));
        File.Move(temp, _filePath, overwrite: true);
    }

    private static bool IsFinished(TimeSpan position, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return false;
        var margin = TimeSpan.FromTicks(Math.Max(EndMargin.Ticks, duration.Ticks / 20)); // 15 s or 5%
        return position >= duration - margin;
    }

    private static string? Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return System.IO.Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    // Caller holds _lock (or is the loader, before the instance is shared).
    private void Prune()
    {
        if (_entries.Count <= MaxEntries) return;
        foreach (var old in _entries.Values.OrderByDescending(e => e.LastPlayed).Skip(MaxEntries).ToList())
            _entries.Remove(old.Path);
    }
}

internal sealed record HistoryFileDto(int Version, List<HistoryEntryDto> Entries);

internal sealed record HistoryEntryDto(string Path, long PositionMs, long DurationMs, DateTimeOffset LastPlayed);

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HistoryFileDto))]
internal sealed partial class HistoryJsonContext : JsonSerializerContext;
