namespace TeddyBench.Avalonia.Models;

/// <summary>
/// Represents the current state of audio playback
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// No audio is loaded or playing
    /// </summary>
    Stopped,

    /// <summary>
    /// Audio is currently playing
    /// </summary>
    Playing,

    /// <summary>
    /// Audio is paused
    /// </summary>
    Paused
}
