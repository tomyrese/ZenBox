using System.IO;
using ZenBox.Services;

namespace ZenBox.Tests;

public sealed class MediaLibraryServiceTests
{
    [Fact]
    public async Task ScanMoviesAndMusic_ShouldFilterSupportedFormatsAndIgnoreJunkFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ZenBox_MediaTest_" + Guid.NewGuid().ToString("N"));
        var subFolder = Path.Combine(tempRoot, "ThuMucCon");
        Directory.CreateDirectory(subFolder);

        try
        {
            // Tạo các tệp giả lập: phim, nhạc và tệp rác
            await File.WriteAllBytesAsync(Path.Combine(tempRoot, "Phim_Gia_Dinh.mp4"), [0x00, 0x01, 0x02, 0x03]);
            await File.WriteAllBytesAsync(Path.Combine(subFolder, "Ky_Su_Du_Lich.mkv"), [0x04, 0x05, 0x06]);
            await File.WriteAllBytesAsync(Path.Combine(tempRoot, "Tinh_Ca_Mua_Xuan.mp3"), [0x49, 0x44, 0x33]);
            await File.WriteAllBytesAsync(Path.Combine(subFolder, "Nhac_Khong_Loi.flac"), [0x66, 0x4C, 0x61, 0x43]);
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "ghi_chu_rac.txt"), "Tệp văn bản không phải media");
            await File.WriteAllTextAsync(Path.Combine(subFolder, "anh_bia.jpg"), "Tệp ảnh rác");

            var errorService = new ErrorHandlingService(tempRoot);
            var mediaService = new MediaLibraryService(errorService);

            // 1. Kiểm tra quét Phim
            var movies = await mediaService.ScanMoviesAsync(tempRoot);
            Assert.Equal(2, movies.Count);
            Assert.Contains(movies, m => m.Title == "Phim_Gia_Dinh" && m.IsVideo && m.CoverImage is not null);
            Assert.Contains(movies, m => m.Title == "Ky_Su_Du_Lich" && m.IsVideo && m.CoverImage is not null);
            Assert.DoesNotContain(movies, m => m.FilePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(movies, m => m.FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));

            // 2. Kiểm tra quét Nhạc
            var music = await mediaService.ScanMusicAsync(tempRoot);
            Assert.Equal(2, music.Count);
            Assert.Contains(music, m => m.Title == "Tinh_Ca_Mua_Xuan" && !m.IsVideo && m.CoverImage is not null);
            Assert.Contains(music, m => m.Title == "Nhac_Khong_Loi" && !m.IsVideo && m.CoverImage is not null);
            Assert.DoesNotContain(music, m => m.FilePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(music, m => m.FilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
