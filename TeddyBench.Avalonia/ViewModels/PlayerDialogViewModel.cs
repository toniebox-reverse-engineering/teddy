using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TeddyBench.Avalonia.Models;
using TeddyBench.Avalonia.Services;
using TonieFile;

namespace TeddyBench.Avalonia.ViewModels;

public partial class PlayerDialogViewModel : ViewModelBase
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly TonieTrackInfoService _trackInfoService;
    private readonly string _tonieFilePath;
    private readonly string? _tonieHash;
    private readonly Window? _window;
    private TimeSpan? _pendingSeekPosition = null;

    public PlayerDialogViewModel(string tonieFilePath, string displayName, TonieTrackInfoService trackInfoService, string? tonieHash = null, Window? window = null)
    {
        _tonieFilePath = tonieFilePath;
        _tonieHash = tonieHash;
        _window = window;
        _trackInfoService = trackInfoService;
        Title = $"Player for: {displayName}";

        _audioPlayer = new AudioPlayerService();

        // Hook up audio player events
        _audioPlayer.PlaybackStateChanged += (s, state) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                PlaybackState = state;
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsStopped));
                OnPropertyChanged(nameof(CanStop));
                OnPropertyChanged(nameof(PlayPauseButtonText));
                OnPropertyChanged(nameof(PlayPauseIcon));
                OnPropertyChanged(nameof(PlayPauseTooltip));

                // If we have a pending seek and playback just started, apply it now
                if (state == PlaybackState.Playing && _pendingSeekPosition.HasValue)
                {
                    _audioPlayer.Seek(_pendingSeekPosition.Value);
                    _pendingSeekPosition = null;
                }
            });
        };

        _audioPlayer.PositionChanged += (s, position) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CurrentPosition = position;
                _isUpdatingPosition = true;
                PositionSeconds = position.TotalSeconds;
                _isUpdatingPosition = false;
                OnPropertyChanged(nameof(CurrentPositionText));

                // Update current track based on position
                UpdateCurrentTrack(position);
            });
        };

        _audioPlayer.PlaybackFinished += (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Reset to beginning
                CurrentPosition = TimeSpan.Zero;
                _isUpdatingPosition = true;
                PositionSeconds = 0;
                _isUpdatingPosition = false;

                // Clear current track highlight
                if (CurrentTrack != null)
                {
                    CurrentTrack.IsCurrentTrack = false;
                    CurrentTrack = null;
                }
                CurrentTrackText = "Not playing";

                // Update button states
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            });
        };

        _audioPlayer.DurationChanged += (s, duration) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                DurationSeconds = duration.TotalSeconds;
                OnPropertyChanged(nameof(DurationText));
            });
        };

        // Load track information
        LoadTracks();
    }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private PlaybackState _playbackState = PlaybackState.Stopped;

    [ObservableProperty]
    private TimeSpan _currentPosition = TimeSpan.Zero;

    private double _positionSeconds;
    private bool _isUpdatingPosition = false;

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            if (Math.Abs(_positionSeconds - value) < 0.001)
                return;

            // Check if this is a user-initiated change (not from position update)
            if (!_isUpdatingPosition && CanStop)
            {
                // User is dragging the slider - seek to new position
                SetProperty(ref _positionSeconds, value);
                SeekTo(value);
            }
            else
            {
                // Normal position update from playback
                SetProperty(ref _positionSeconds, value);
            }
        }
    }

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private ObservableCollection<TrackInfo> _tracks = new();

    [ObservableProperty]
    private TrackInfo? _currentTrack;

    [ObservableProperty]
    private string _currentTrackText = "Not playing";

    public bool IsPlaying => PlaybackState == PlaybackState.Playing;
    public bool IsPaused => PlaybackState == PlaybackState.Paused;
    public bool IsStopped => PlaybackState == PlaybackState.Stopped;
    public bool CanStop => IsPlaying || IsPaused;
    public string PlayPauseButtonText => IsPlaying ? "Pause" : "Play";
    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";
    public string PlayPauseTooltip => IsPlaying ? "Pause" : "Play";
    public string CurrentPositionText => FormatTime(CurrentPosition);
    public string DurationText => FormatTime(_audioPlayer?.Duration ?? TimeSpan.Zero);
    public bool CanGoPrevious => CurrentTrack != null && Tracks.Count > 0 && CurrentTrack.TrackNumber > 1;
    public bool CanGoNext => CurrentTrack != null && Tracks.Count > 0 && CurrentTrack.TrackNumber < Tracks.Count;

    private void LoadTracks()
    {
        try
        {
            // Ensure track info is saved to customTonies.json (for metadata display elsewhere)
            // This reads the audio, calculates tracks, and caches them if not already cached
            _trackInfoService.EnsureTrackInfo(_tonieFilePath, _tonieHash);

            // Read audio again for playback positions (we need granule positions for seeking)
            var tonie = TonieAudio.FromFile(_tonieFilePath, readAudio: true);
            var positions = tonie.ParsePositions();

            // Get total duration from CalculateStatistics which reads the highest granule
            TimeSpan totalDuration = TimeSpan.Zero;
            try
            {
                tonie.CalculateStatistics(out _, out _, out _, out _, out _, out _, out ulong highestGranule);
                totalDuration = TimeSpan.FromSeconds((double)highestGranule / 48000.0);
            }
            catch (Exception)
            {
                // Error calculating statistics - continue with zero duration
            }

            // Convert granules to TimeSpan and deduplicate
            var distinctPositions = new List<TimeSpan>();
            var seenPositions = new HashSet<ulong>();

            for (int i = 0; i < positions.Length; i++)
            {
                // Skip duplicate positions
                if (!seenPositions.Add(positions[i]))
                    continue;

                // Convert granule to TimeSpan (48000 Hz sample rate)
                var totalSeconds = (double)positions[i] / 48000.0;
                var timeSpan = TimeSpan.FromSeconds(totalSeconds);
                distinctPositions.Add(timeSpan);
            }

            // Add tracks based on distinct positions with calculated durations
            // All tracks except the last one - calculate duration from next track start
            for (int i = 0; i < distinctPositions.Count - 1; i++)
            {
                var trackStart = distinctPositions[i];
                var trackEnd = distinctPositions[i + 1];
                var trackDuration = trackEnd - trackStart;

                Tracks.Add(new TrackInfo
                {
                    TrackNumber = i + 1,
                    Position = trackStart,
                    DisplayText = $"Track {i + 1} ({FormatTime(trackDuration)})"
                });
            }

            // Add the last track only if it's not the final position marker
            // (The final position in ParsePositions represents the END of audio, not a track start)
            if (distinctPositions.Count > 0)
            {
                var lastTrackStart = distinctPositions[distinctPositions.Count - 1];

                // If we have a valid total duration, calculate the last track duration
                // Skip if duration would be zero (meaning this is just the final position marker)
                if (totalDuration > TimeSpan.Zero)
                {
                    var lastTrackDuration = totalDuration - lastTrackStart;

                    // Only add if duration is greater than zero
                    if (lastTrackDuration > TimeSpan.Zero)
                    {
                        string displayText = $"Track {distinctPositions.Count} ({FormatTime(lastTrackDuration)})";

                        Tracks.Add(new TrackInfo
                        {
                            TrackNumber = distinctPositions.Count,
                            Position = lastTrackStart,
                            DisplayText = displayText
                        });
                    }
                }
                else
                {
                    // No total duration info - add the track without duration display
                    string displayText = $"Track {distinctPositions.Count}";

                    Tracks.Add(new TrackInfo
                    {
                        TrackNumber = distinctPositions.Count,
                        Position = lastTrackStart,
                        DisplayText = displayText
                    });
                }
            }
        }
        catch (Exception)
        {
            // If we can't load tracks, just show one entry
            Tracks.Add(new TrackInfo
            {
                TrackNumber = 1,
                Position = TimeSpan.Zero,
                DisplayText = "Track 1"
            });
        }
    }

    [RelayCommand]
    private void PlayPause()
    {
        try
        {
            if (IsPlaying)
            {
                _audioPlayer.Pause();
            }
            else if (IsPaused)
            {
                _audioPlayer.Resume();
            }
            else
            {
                // Start playback
                _audioPlayer.Play(_tonieFilePath);
                // Duration will be set automatically via DurationChanged event
            }
        }
        catch (Exception)
        {
            // Playback error
        }
    }

    [RelayCommand]
    private void Stop()
    {
        try
        {
            _audioPlayer.Stop();

            // Reset position to 00:00
            CurrentPosition = TimeSpan.Zero;
            _isUpdatingPosition = true;
            PositionSeconds = 0;
            _isUpdatingPosition = false;
            OnPropertyChanged(nameof(CurrentPositionText));

            // Clear current track highlight
            if (CurrentTrack != null)
            {
                CurrentTrack.IsCurrentTrack = false;
                CurrentTrack = null;
            }
            CurrentTrackText = "Not playing";

            // Update button states
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }
        catch (Exception)
        {
            // Stop error
        }
    }

    [RelayCommand]
    private void SeekTo(double seconds)
    {
        try
        {
            _audioPlayer.Seek(TimeSpan.FromSeconds(seconds));
        }
        catch (Exception)
        {
            // Seek error
        }
    }

    [RelayCommand]
    private void SeekToTrack(TimeSpan position)
    {
        try
        {
            // If stopped, start playback first and queue the seek
            if (IsStopped)
            {
                _pendingSeekPosition = position;
                _audioPlayer.Play(_tonieFilePath);
                // Duration will be set automatically via DurationChanged event
            }
            else
            {
                // Already playing, seek immediately
                _audioPlayer.Seek(position);
            }
        }
        catch (Exception)
        {
            // Seek to track error
        }
    }

    [RelayCommand]
    private void PreviousTrack()
    {
        try
        {
            if (CurrentTrack == null || Tracks.Count == 0)
                return;

            // Find previous track
            var currentIndex = Tracks.IndexOf(CurrentTrack);
            if (currentIndex > 0)
            {
                var previousTrack = Tracks[currentIndex - 1];
                SeekToTrack(previousTrack.Position);
            }
        }
        catch (Exception)
        {
            // Previous track error
        }
    }

    [RelayCommand]
    private void NextTrack()
    {
        try
        {
            if (CurrentTrack == null || Tracks.Count == 0)
                return;

            // Find next track
            var currentIndex = Tracks.IndexOf(CurrentTrack);
            if (currentIndex < Tracks.Count - 1)
            {
                var nextTrack = Tracks[currentIndex + 1];
                SeekToTrack(nextTrack.Position);
            }
        }
        catch (Exception)
        {
            // Next track error
        }
    }

    [RelayCommand]
    private void Close()
    {
        Cleanup();
        _window?.Close();
    }

    public void Cleanup()
    {
        // Stop playback and dispose
        _audioPlayer.Stop();
        _audioPlayer.Dispose();
    }

    private void UpdateCurrentTrack(TimeSpan position)
    {
        if (Tracks.Count == 0)
        {
            CurrentTrack = null;
            CurrentTrackText = "Not playing";
            return;
        }

        // Find which track we're currently in based on position
        TrackInfo? newCurrentTrack = null;

        for (int i = Tracks.Count - 1; i >= 0; i--)
        {
            if (position >= Tracks[i].Position)
            {
                newCurrentTrack = Tracks[i];
                break;
            }
        }

        // If position is before the first track, default to first track
        if (newCurrentTrack == null && Tracks.Count > 0)
        {
            newCurrentTrack = Tracks[0];
        }

        // Update if changed
        if (CurrentTrack != newCurrentTrack)
        {
            // Clear previous track highlight
            if (CurrentTrack != null)
            {
                CurrentTrack.IsCurrentTrack = false;
            }

            CurrentTrack = newCurrentTrack;

            // Set new track highlight
            if (CurrentTrack != null)
            {
                CurrentTrack.IsCurrentTrack = true;
                CurrentTrackText = $"Currently playing: Track {CurrentTrack.TrackNumber}";
            }
            else
            {
                CurrentTrackText = "Not playing";
            }

            // Update button states
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return time.ToString(@"h\:mm\:ss");
        }
        else
        {
            return time.ToString(@"m\:ss");
        }
    }
}

public partial class TrackInfo : ObservableObject
{
    public int TrackNumber { get; set; }
    public TimeSpan Position { get; set; }
    public string DisplayText { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isCurrentTrack;
}
