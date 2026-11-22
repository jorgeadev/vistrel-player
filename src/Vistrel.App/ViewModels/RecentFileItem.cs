using Vistrel.Core;
using Vistrel.Core.History;

namespace Vistrel.App.ViewModels;

/// <summary>One row in the start screen's recent files list.</summary>
public sealed class RecentFileItem(HistoryEntry entry)
{
    public string Path { get; } = entry.Path;

    public string Name { get; } = System.IO.Path.GetFileName(entry.Path);

    public string Folder { get; } = System.IO.Path.GetDirectoryName(entry.Path) ?? "";

    /// <summary>"Resume at 42:10 of 1:58:00", or "Start from the beginning" once watched.</summary>
    public string Detail { get; } = entry.HasResumePoint
        ? entry.Duration > TimeSpan.Zero
            ? $"Resume at {TimeFormatter.Format(entry.Position)} of {TimeFormatter.Format(entry.Duration)}"
            : $"Resume at {TimeFormatter.Format(entry.Position)}"
        : "Start from the beginning";

    /// <summary>0-1 share watched, for the small progress bar.</summary>
    public double Progress { get; } = entry.HasResumePoint && entry.Duration > TimeSpan.Zero
        ? Math.Clamp(entry.Position / entry.Duration, 0, 1)
        : 0;

    public bool HasProgress => Progress > 0;
}
