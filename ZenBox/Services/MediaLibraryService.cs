using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZenBox.Models;

namespace ZenBox.Services;

public sealed class MediaLibraryService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".wav", ".m4a"
    };

    private readonly ErrorHandlingService _errorService;
    private readonly ImageSource _defaultVideoCover;
    private readonly ImageSource _defaultAudioCover;

    public MediaLibraryService(ErrorHandlingService errorService)
    {
        _errorService = errorService;
        _defaultVideoCover = CreateDefaultPoster(isVideo: true);
        _defaultAudioCover = CreateDefaultPoster(isVideo: false);
    }

    public static bool IsSupportedVideoFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrWhiteSpace(ext) && VideoExtensions.Contains(ext);
    }

    public static bool IsSupportedAudioFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrWhiteSpace(ext) && AudioExtensions.Contains(ext);
    }

    public Task<IReadOnlyList<MediaItem>> ScanMoviesAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanFolderInternal(folderPath, isVideo: true, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<MediaItem>> ScanMusicAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanFolderInternal(folderPath, isVideo: false, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<MediaItem> ScanFolderInternal(string folderPath, bool isVideo, CancellationToken cancellationToken)
    {
        var results = new List<MediaItem>();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return results;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folderPath, "*.*", options);
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.MediaFileCorrupted, $"Enumerate folder: {folderPath}", ex);
            return results;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var matches = isVideo ? IsSupportedVideoFile(file) : IsSupportedAudioFile(file);
            if (!matches)
            {
                continue;
            }

            try
            {
                var fileInfo = new FileInfo(file);
                if (!fileInfo.Exists)
                {
                    continue;
                }

                var sizeText = FormatFileSize(fileInfo.Length);
                if (isVideo)
                {
                    var thumb = TryExtractShellThumbnail(file) ?? _defaultVideoCover;
                    var extLabel = fileInfo.Extension.TrimStart('.').ToUpperInvariant();
                    results.Add(new MediaItem
                    {
                        Title = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        FileSizeText = sizeText,
                        SubInfoText = $"Phim {extLabel} • {sizeText}",
                        IsVideo = true,
                        CoverImage = thumb
                    });
                }
                else
                {
                    var (title, artist, albumCover) = ReadAudioMetadataAndCover(file);
                    var subInfo = string.IsNullOrWhiteSpace(artist)
                        ? $"Nhạc • {sizeText}"
                        : $"{artist} • {sizeText}";

                    results.Add(new MediaItem
                    {
                        Title = title,
                        FilePath = file,
                        FileSizeText = sizeText,
                        SubInfoText = subInfo,
                        IsVideo = false,
                        CoverImage = albumCover ?? _defaultAudioCover
                    });
                }
            }
            catch (Exception ex)
            {
                _errorService.LogError(ErrorCategory.MediaFileCorrupted, $"Inspect media file: {file}", ex);
            }
        }

        return results
            .OrderBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private (string Title, string? Artist, ImageSource? Cover) ReadAudioMetadataAndCover(string filePath)
    {
        var fallbackTitle = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var title = string.IsNullOrWhiteSpace(tagFile.Tag.Title) ? fallbackTitle : tagFile.Tag.Title.Trim();
            var artist = tagFile.Tag.FirstPerformer?.Trim();

            ImageSource? cover = null;
            var pictures = tagFile.Tag.Pictures;
            if (pictures is { Length: > 0 } && pictures[0].Data?.Data is { Length: > 0 } rawBytes)
            {
                using var ms = new MemoryStream(rawBytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 360;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                cover = bmp;
            }

            return (title, artist, cover);
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.MediaFileCorrupted, $"TagLib read audio: {filePath}", ex);
            return (fallbackTitle, null, null);
        }
    }

    private ImageSource? TryExtractShellThumbnail(string filePath)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var shellItemGuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref shellItemGuid, out var factory);
            if (hr != 0 || factory is null)
            {
                return null;
            }

            var desiredSize = new NativeSize { Width = 420, Height = 250 };
            hr = factory.GetImage(desiredSize, ShellImageFlags.ThumbnailOnly | ShellImageFlags.BiggerSizeOk, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
            {
                hr = factory.GetImage(desiredSize, ShellImageFlags.ResizeToFit, out hBitmap);
            }

            if (hr != 0 || hBitmap == IntPtr.Zero)
            {
                return null;
            }

            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.MediaFileCorrupted, $"Shell thumbnail extract: {filePath}", ex);
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
        }
    }

    private static ImageSource CreateDefaultPoster(bool isVideo)
    {
        var group = new DrawingGroup();
        var rect = new Rect(0, 0, 420, 250);

        var gradient = isVideo
            ? new LinearGradientBrush(
                Color.FromRgb(15, 23, 42),
                Color.FromRgb(14, 116, 144),
                new Point(0, 0),
                new Point(1, 1))
            : new LinearGradientBrush(
                Color.FromRgb(30, 27, 75),
                Color.FromRgb(56, 189, 248),
                new Point(0, 0),
                new Point(1, 1));

        group.Children.Add(new GeometryDrawing(gradient, null, new RectangleGeometry(rect, 16, 16)));

        // Vòng tròn sáng mờ ở trung tâm
        var glowBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
        group.Children.Add(new GeometryDrawing(
            glowBrush,
            new Pen(new SolidColorBrush(Color.FromArgb(110, 56, 189, 248)), 2.5),
            new EllipseGeometry(new Point(210, 125), 48, 48)));

        // Biểu tượng Vector sắc nét (Tam giác Phát cho Phim / Nốt nhạc đôi cho Nhạc)
        var iconBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));
        var iconGeometry = isVideo
            ? Geometry.Parse("M 196,98 L 236,125 L 196,152 Z")
            : Geometry.Parse("M 202,94 L 234,88 L 234,138 A 12,10 0 1 1 222,128 L 222,102 L 206,106 L 206,144 A 12,10 0 1 1 194,134 Z");

        group.Children.Add(new GeometryDrawing(iconBrush, null, iconGeometry));

        var drawingImage = new DrawingImage(group);
        drawingImage.Freeze();
        return drawingImage;
    }

    public static string FormatFileSize(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        const double gb = mb * 1024;

        if (bytes >= gb)
        {
            return $"{bytes / gb:0.0} GB";
        }

        if (bytes >= mb)
        {
            return $"{bytes / mb:0.0} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / kb:0} KB";
        }

        return $"{bytes} B";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [Flags]
    private enum ShellImageFlags
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        ThumbnailOnly = 0x08
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage([In, MarshalAs(UnmanagedType.Struct)] NativeSize size, [In] ShellImageFlags flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        [In] IntPtr pbc,
        [In] ref Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}
