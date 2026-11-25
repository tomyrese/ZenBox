using System.Text.Json;
using ZenBox.Models;
using ZenBox.Services;
using ZenBox.ViewModels;

namespace ZenBox.Tests;

public sealed class RemoteProtocolTests
{
    [Fact]
    public void RemoteCommandPayload_ShouldDeserializeRealtimeSearchAndTouchpadFields()
    {
        const string json = """
            {
                "action": "SEARCH_REALTIME",
                "target": "YOUTUBE",
                "text": "Nhạc Trịnh Công Sơn",
                "value": 75,
                "dx": 14.5,
                "dy": -8.2
            }
            """;

        var payload = JsonSerializer.Deserialize<RemoteCommandPayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Equal("SEARCH_REALTIME", payload.Action);
        Assert.Equal("YOUTUBE", payload.Target);
        Assert.Equal("Nhạc Trịnh Công Sơn", payload.Text);
        Assert.Equal(75, payload.Value);
        Assert.Equal(14.5, payload.Dx);
        Assert.Equal(-8.2, payload.Dy);
    }

    [Fact]
    public void MainViewModel_HandleRemoteCommand_ShouldSyncRealtimeOfflineSearchPresetsAndStateSnapshot()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ZenBox_RemoteTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var err = new ErrorHandlingService(tempDir);
            var cfg = new ConfigService(err, tempDir);
            var net = new NetworkMonitorService();
            var audio = new SystemAudioService();
            var web = new WebBrowserService(err, net, cfg);
            var media = new MediaLibraryService(err);

            using var mainVm = new MainViewModel(err, cfg, net, audio, web, media);

            // 1. Gửi lệnh tìm kiếm realtime vào Kho Phim Offline từ điện thoại
            mainVm.HandleRemoteCommand(new RemoteCommandPayload
            {
                Action = "SEARCH_REALTIME",
                Target = "OFFLINE",
                Text = "Bố Già"
            });

            Assert.Equal(AppSection.OfflineMovies, mainVm.ActiveSection);
            Assert.Equal("Bố Già", mainVm.MediaLibraryVm.SearchKeyword);

            // 2. Đổi cấu hình âm thanh EQ & Bảo vệ mắt & Hẹn giờ từ điện thoại
            mainVm.HandleRemoteCommand(new RemoteCommandPayload
            {
                Action = "SET_AUDIO_PRESET",
                Target = "CinemaSurround"
            });
            mainVm.HandleRemoteCommand(new RemoteCommandPayload
            {
                Action = "SET_EYECARE",
                Target = "WarmSoft"
            });
            mainVm.HandleRemoteCommand(new RemoteCommandPayload
            {
                Action = "SET_SLEEP_TIMER",
                Value = 30
            });

            var snapshot = mainVm.BuildRemoteStateSnapshot();
            Assert.Equal("STATE", snapshot.Type);
            Assert.Equal("OfflineMovies", snapshot.ActiveSection);
            Assert.Equal("Kho Phim Offline", snapshot.ActiveTitle);
            Assert.Equal("CinemaSurround", snapshot.AudioPreset);
            Assert.Equal("WarmSoft", snapshot.EyeCare);
            Assert.True(mainVm.IsSleepTimerActive);

            // 3. Quay lại Trang Chủ bằng lệnh HOME từ điện thoại
            mainVm.HandleRemoteCommand(new RemoteCommandPayload { Action = "HOME" });
            Assert.Equal(AppSection.Home, mainVm.ActiveSection);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
