namespace Vistrel.Core.Playback;

public enum TrackKind
{
    Audio,
    Subtitle,
}

/// <summary>An audio or subtitle track of the current media, as reported by the engine.</summary>
/// <param name="Id">Engine-specific id used to select the track. <see cref="MediaTrack.DisabledId"/> means "off".</param>
/// <param name="Name">Display name, e.g. "Track 1 - [English]".</param>
public sealed record MediaTrack(int Id, string Name, TrackKind Kind)
{
    /// <summary>Id that turns subtitles off (or mutes the audio track selection).</summary>
    public const int DisabledId = -1;

    public bool IsDisabled => Id == DisabledId;
}
