using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using ZenBox.Models;
using ZenBox.Services;

namespace ZenBox.ViewModels;

public sealed partial class MediaPlayerViewModel : ObservableObject, IDisposable
{
    private static bool _vlcCoreInitialized;
    private static readonly object VlcInitLock = new();

    private readonly ErrorHandlingService _errorService;
    private readonly Action<bool> _backToLibraryCallback;
    private readonly DispatcherTimer _cursorWatchTimer;

    private LibVLC? _libVlc;
    private MediaItem? _pendingMediaToStart;
    private bool _isUserDraggingSlider;
    private NativePoint _lastCursorPoint;
    private DateTime _lastMouseMoveUtc = DateTime.UtcNow;
    private bool _disposed;

    [ObservableProperty]
    private MediaPlayer? _vlcPlayer;

    [ObservableProperty]
    private MediaItem? _currentMedia;

    [ObservableProperty]
    private string _mediaTitle = string.Empty;

    [ObservableProperty]
    private bool _isAudioTrack;

    [ObservableProperty]
    private bool _isVideoTrack = true;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _playPauseButtonText = "Tạm dừng";

    [ObservableProperty]
    private string _currentTimeText = "00:00";

    [ObservableProperty]
    private string _totalDurationText = "00:00";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isControlsVisible = true;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    public MediaPlayerViewModel(ErrorHandlingService errorService, Action<bool> backToLibraryCallback)
    {
        _errorService = errorService;
        _backToLibraryCallback = backToLibraryCallback;

        GetCursorPos(out _lastCursorPoint);

        _cursorWatchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _cursorWatchTimer.Tick += OnCursorWatchTick;
    }

    private void OnCursorWatchTick(object? sender, EventArgs e)
    {
        if (GetCursorPos(out var current))
        {
            if (Math.Abs(current.X - _lastCursorPoint.X) > 3 || Math.Abs(current.Y - _lastCursorPoint.Y) > 3)
            {
                _lastCursorPoint = current;
                _lastMouseMoveUtc = DateTime.UtcNow;
                if (!IsControlsVisible)
                {
                    IsControlsVisible = true;
                }
                return;
            }
        }

        if (IsPlaying && IsVideoTrack && !_isUserDraggingSlider)
        {
            if ((DateTime.UtcNow - _lastMouseMoveUtc).TotalSeconds >= 3.0)
            {
                IsControlsVisible = false;
            }
        }
    }

    public MediaPlayer? EnsurePlayerReady()
    {
        if (_libVlc is not null && VlcPlayer is not null)
        {
            return VlcPlayer;
        }

        lock (VlcInitLock)
        {
            if (!_vlcCoreInitialized)
            {
                var vlcLibPath = ResolveLibVlcDirectory();
                if (!string.IsNullOrWhiteSpace(vlcLibPath))
                {
                    Core.Initialize(vlcLibPath);
                }
                else
                {
                    Core.Initialize();
                }
                _vlcCoreInitialized = true;
            }
        }

        _libVlc = new LibVLC("--avcodec-hw=any", "--quiet", "--no-video-title-show");
        var player = new MediaPlayer(_libVlc);

        player.Playing += (_, _) => RunOnUi(() =>
        {
            IsPlaying = true;
            PlayPauseButtonText = "Tạm dừng";
            _lastMouseMoveUtc = DateTime.UtcNow;
        });

        player.Paused += (_, _) => RunOnUi(() =>
        {
            IsPlaying = false;
            PlayPauseButtonText = "Tiếp tục phát";
            IsControlsVisible = true;
        });

        player.Stopped += (_, _) => RunOnUi(() =>
        {
            IsPlaying = false;
            PlayPauseButtonText = "Phát lại";
            IsControlsVisible = true;
        });

        player.EndReached += (_, _) => RunOnUi(() =>
        {
            IsPlaying = false;
            PlayPauseButtonText = "Phát lại";
            ProgressValue = 1000;
            IsControlsVisible = true;
        });

        player.LengthChanged += (_, e) => RunOnUi(() =>
        {
            TotalDurationText = FormatTime(e.Length);
        });

        player.TimeChanged += (_, e) => RunOnUi(() =>
        {
            CurrentTimeText = FormatTime(e.Time);
            var total = player.Length;
            if (!_isUserDraggingSlider && total > 0)
            {
                ProgressValue = Math.Clamp((double)e.Time / total * 1000.0, 0, 1000);
            }
        });

        player.EncounteredError += (_, _) => RunOnUi(() =>
        {
            var path = CurrentMedia?.FilePath ?? string.Empty;
            _errorService.ReportError(
                ErrorCategory.MediaFileCorrupted,
                $"LibVLC EncounteredError playing: {path}",
                null,
                () =>
                {
                    if (CurrentMedia is not null)
                    {
                        PlayMedia(CurrentMedia);
                        StartPendingPlayback();
                    }
                });
        });

        VlcPlayer = player;
        return VlcPlayer;
    }

    public void PlayMedia(MediaItem item)
    {
        CurrentMedia = item;
        MediaTitle = item.Title;
        IsAudioTrack = !item.IsVideo;
        IsVideoTrack = item.IsVideo;
        CurrentTimeText = "00:00";
        TotalDurationText = "00:00";
        ProgressValue = 0;
        IsControlsVisible = true;
        _lastMouseMoveUtc = DateTime.UtcNow;
        GetCursorPos(out _lastCursorPoint);

        if (!File.Exists(item.FilePath))
        {
            _pendingMediaToStart = null;
            _errorService.ReportError(ErrorCategory.MediaFileCorrupted, $"File does not exist: {item.FilePath}");
            return;
        }

        try
        {
            EnsurePlayerReady();
            _pendingMediaToStart = item;
        }
        catch (Exception ex)
        {
            _pendingMediaToStart = null;
            _errorService.ReportError(ErrorCategory.MediaFileCorrupted, $"Prepare PlayMedia exception: {item.FilePath}", ex);
        }
    }

    // Được gọi sau khi VideoView đã gắn HWND thực tế vào MediaPlayer, đảm bảo không bao giờ bung cửa sổ Direct3D rời hay đen hình
    public void StartPendingPlayback()
    {
        var item = _pendingMediaToStart;
        if (item is null)
        {
            return;
        }

        _pendingMediaToStart = null;
        try
        {
            EnsurePlayerReady();
            if (_libVlc is null || VlcPlayer is null)
            {
                return;
            }

            _cursorWatchTimer.Start();

            using var media = new Media(_libVlc, new Uri(item.FilePath));
            media.AddOption(":avcodec-hw=any");
            if (!VlcPlayer.Play(media))
            {
                _errorService.ReportError(ErrorCategory.MediaFileCorrupted, $"Cannot start playback: {item.FilePath}");
            }
        }
        catch (Exception ex)
        {
            _errorService.ReportError(ErrorCategory.MediaFileCorrupted, $"StartPendingPlayback exception: {item.FilePath}", ex);
        }
    }

    public void StopPlayback()
    {
        _pendingMediaToStart = null;
        _cursorWatchTimer.Stop();
        IsControlsVisible = true;
        try
        {
            if (VlcPlayer is not null && (VlcPlayer.IsPlaying || VlcPlayer.State is VLCState.Paused or VLCState.Opening))
            {
                VlcPlayer.Stop();
            }
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, "Stop VLC playback", ex);
        }
    }

    public void NotifyUserActivity()
    {
        _lastMouseMoveUtc = DateTime.UtcNow;
        IsControlsVisible = true;
    }

    public void BeginSliderDrag()
    {
        _isUserDraggingSlider = true;
        NotifyUserActivity();
    }

    public void CompleteSliderDrag(double newSliderValue)
    {
        _isUserDraggingSlider = false;
        if (VlcPlayer is { Length: > 0 } player)
        {
            var targetTime = (long)(newSliderValue / 1000.0 * player.Length);
            player.Time = Math.Clamp(targetTime, 0, player.Length);
        }
        NotifyUserActivity();
    }

    [RelayCommand]
    public void TogglePlayPause()
    {
        NotifyUserActivity();
        if (VlcPlayer is null)
        {
            return;
        }

        if (VlcPlayer.State is VLCState.Ended or VLCState.Stopped && CurrentMedia is not null)
        {
            PlayMedia(CurrentMedia);
            StartPendingPlayback();
            return;
        }

        VlcPlayer.Pause();
    }

    [RelayCommand]
    private void Rewind15Seconds()
    {
        NotifyUserActivity();
        if (VlcPlayer is { Length: > 0 } player)
        {
            player.Time = Math.Max(0, player.Time - 15_000);
        }
    }

    [RelayCommand]
    private void Forward15Seconds()
    {
        NotifyUserActivity();
        if (VlcPlayer is { Length: > 0 } player)
        {
            player.Time = Math.Min(player.Length, player.Time + 15_000);
        }
    }

    [RelayCommand]
    private void BackToLibrary()
    {
        var wasVideo = CurrentMedia?.IsVideo ?? true;
        StopPlayback();
        _backToLibraryCallback(wasVideo);
    }

    private static string? ResolveLibVlcDirectory()
    {
        var candidateRoots = new List<string> { AppContext.BaseDirectory };
        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string nativeDirs)
        {
            candidateRoots.AddRange(
                nativeDirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        foreach (var root in candidateRoots)
        {
            var winX64Dir = Path.Combine(root, "libvlc", "win-x64");
            if (File.Exists(Path.Combine(winX64Dir, "libvlc.dll")))
            {
                return winX64Dir;
            }

            if (File.Exists(Path.Combine(root, "libvlc.dll")))
            {
                return root;
            }
        }

        return null;
    }

    private static string FormatTime(long milliseconds)
    {
        if (milliseconds <= 0)
        {
            return "00:00";
        }

        var span = TimeSpan.FromMilliseconds(milliseconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }

    private static void RunOnUi(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            _ = dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cursorWatchTimer.Stop();
        VlcPlayer?.Dispose();
        _libVlc?.Dispose();
    }
}
