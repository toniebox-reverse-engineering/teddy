using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using TeddyBench.Avalonia.Models;
using TonieFile;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Cross-platform audio player service using LibVLC for reliable playback
/// </summary>
public class AudioPlayerService : IDisposable
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Timer? _positionTimer;
    private bool _isDisposed;

    /// <summary>
    /// Raised when playback state changes
    /// </summary>
    public event EventHandler<PlaybackState>? PlaybackStateChanged;

    /// <summary>
    /// Raised periodically during playback with current position
    /// </summary>
    public event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>
    /// Raised when playback finishes naturally (reaches end)
    /// </summary>
    public event EventHandler? PlaybackFinished;

    /// <summary>
    /// Raised when an error occurs during playback
    /// </summary>
    public event EventHandler<string>? PlaybackError;

    /// <summary>
    /// Raised when the duration of the media becomes available
    /// </summary>
    public event EventHandler<TimeSpan>? DurationChanged;

    /// <summary>
    /// Current playback state
    /// </summary>
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    /// <summary>
    /// Current playback position
    /// </summary>
    public TimeSpan CurrentPosition
    {
        get
        {
            if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                return TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            }
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Total duration of loaded audio
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            if (_mediaPlayer?.Media != null)
            {
                return TimeSpan.FromMilliseconds(_mediaPlayer.Length);
            }
            return TimeSpan.Zero;
        }
    }

    public AudioPlayerService()
    {
        Core.Initialize();
        _libVLC = new LibVLC();
    }

    /// <summary>
    /// Load and play a Tonie audio file
    /// </summary>
    /// <param name="tonieFilePath">Path to the Tonie file</param>
    public void Play(string tonieFilePath)
    {
        try
        {
            if (_libVLC == null)
            {
                OnPlaybackError("LibVLC not initialized");
                return;
            }

            // Stop any existing playback
            Stop();

            // Load the Tonie file
            var tonie = TonieAudio.FromFile(tonieFilePath, readAudio: true);
            if (tonie.Audio == null || tonie.Audio.Length == 0)
            {
                OnPlaybackError("No audio data in Tonie file");
                return;
            }

            // Create a temporary file for the audio (LibVLC works best with files)
            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, tonie.Audio);

            // Create media and player
            var media = new Media(_libVLC, tempFile, FromType.FromPath);
            _mediaPlayer = new MediaPlayer(media);

            // Hook up events
            _mediaPlayer.Playing += (s, e) =>
            {
                SetState(PlaybackState.Playing);
                StartPositionTimer();
            };

            _mediaPlayer.Paused += (s, e) => SetState(PlaybackState.Paused);

            _mediaPlayer.Stopped += (s, e) =>
            {
                SetState(PlaybackState.Stopped);
                StopPositionTimer();
            };

            _mediaPlayer.EndReached += (s, e) =>
            {
                StopPositionTimer();
                OnPlaybackFinished();
            };

            _mediaPlayer.EncounteredError += (s, e) =>
            {
                OnPlaybackError("VLC encountered an error during playback");
            };

            _mediaPlayer.LengthChanged += (s, e) =>
            {
                // Duration is now available
                var duration = TimeSpan.FromMilliseconds(e.Length);
                OnDurationChanged(duration);
            };

            // Start playback
            _mediaPlayer.Play();
        }
        catch (Exception ex)
        {
            OnPlaybackError($"Failed to start playback: {ex.Message}");
            Stop();
        }
    }

    /// <summary>
    /// Pause playback
    /// </summary>
    public void Pause()
    {
        if (_mediaPlayer != null && State == PlaybackState.Playing)
        {
            _mediaPlayer.Pause();
            StopPositionTimer();
        }
    }

    /// <summary>
    /// Resume playback
    /// </summary>
    public void Resume()
    {
        if (_mediaPlayer != null && State == PlaybackState.Paused)
        {
            _mediaPlayer.Play();
            StartPositionTimer();
        }
    }

    /// <summary>
    /// Stop playback and release resources
    /// </summary>
    public void Stop()
    {
        StopPositionTimer();

        if (_mediaPlayer != null)
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Media?.Dispose();
            _mediaPlayer.Dispose();
            _mediaPlayer = null;
        }

        SetState(PlaybackState.Stopped);
    }

    /// <summary>
    /// Seek to a specific position in the audio
    /// </summary>
    /// <param name="position">Target position</param>
    public void Seek(TimeSpan position)
    {
        if (_mediaPlayer == null || State == PlaybackState.Stopped)
        {
            return;
        }

        try
        {
            _mediaPlayer.Time = (long)position.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            OnPlaybackError($"Failed to seek: {ex.Message}");
        }
    }

    private void StartPositionTimer()
    {
        StopPositionTimer();
        _positionTimer = new Timer(_ =>
        {
            OnPositionChanged(CurrentPosition);
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(10));
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    private void SetState(PlaybackState newState)
    {
        if (State == newState)
            return;

        State = newState;
        PlaybackStateChanged?.Invoke(this, newState);
    }

    private void OnPositionChanged(TimeSpan position)
    {
        PositionChanged?.Invoke(this, position);
    }

    private void OnPlaybackFinished()
    {
        PlaybackFinished?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackError(string message)
    {
        PlaybackError?.Invoke(this, message);
    }

    private void OnDurationChanged(TimeSpan duration)
    {
        DurationChanged?.Invoke(this, duration);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Stop();
        _libVLC?.Dispose();
        _libVLC = null;
        _isDisposed = true;
    }
}
