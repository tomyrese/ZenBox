using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ZenBox.Models;
using ZenBox.Services;
using ZenBox.ViewModels;

namespace ZenBox.Tests;

public sealed class RemoteProtocolTests
{
    [Fact]
    public void NetworkAddressHelper_ShouldExcludeRadminVpnRange()
    {
        var ips = NetworkAddressHelper.GetRealLanIpAddresses();
        Assert.NotEmpty(ips);
        Assert.All(ips, ip => Assert.False(ip.StartsWith("26."), $"Không được trả về IP Radmin VPN ảo: {ip}"));
    }

    [Fact]
    public async Task RemoteControlServer_ShouldSupportHttpDiscoveryAndMultipleSimultaneousPhones()
    {
        int freePort;
        using (var tempListener = new TcpListener(IPAddress.Loopback, 0))
        {
            tempListener.Start();
            freePort = ((IPEndPoint)tempListener.LocalEndpoint).Port;
        }

        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ZenBox_MultiPhone_" + Guid.NewGuid().ToString("N"));
        try
        {
            var err = new ErrorHandlingService(tempDir);
            string lastAction = string.Empty;
            string lastSearch = string.Empty;

            using var server = new RemoteControlServer(
                freePort,
                err,
                cmd =>
                {
                    lastAction = cmd.Action ?? string.Empty;
                    if (!string.IsNullOrEmpty(cmd.Text))
                    {
                        lastSearch = cmd.Text;
                    }
                },
                () => new RemoteStateDto
                {
                    ActiveSection = "YouTube",
                    ActiveTitle = "YouTube",
                    Volume = 72
                });

            server.Start();
            Assert.True(server.IsRunning);

            // 1. Kiểm tra dò tìm tự động qua HTTP GET /zenbox-info trên cùng cổng
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var infoJson = await http.GetStringAsync($"http://127.0.0.1:{freePort}/zenbox-info");
            var info = JsonSerializer.Deserialize<RemoteDiscoveryInfoDto>(infoJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(info);
            Assert.Equal("ZenBox", info.App);
            Assert.Equal(freePort, info.Port);

            // 2. Kết nối đồng thời 3 điện thoại qua WebSocket cùng một lúc
            using var phone1 = new ClientWebSocket();
            using var phone2 = new ClientWebSocket();
            using var phone3 = new ClientWebSocket();

            var wsUri = new Uri($"ws://127.0.0.1:{freePort}");
            await Task.WhenAll(
                phone1.ConnectAsync(wsUri, CancellationToken.None),
                phone2.ConnectAsync(wsUri, CancellationToken.None),
                phone3.ConnectAsync(wsUri, CancellationToken.None));

            await Task.Delay(120);
            Assert.Equal(3, server.ConnectedClientCount);

            // 3. Điện thoại 1 gõ tìm kiếm realtime tiếng Việt
            var cmdBytes = Encoding.UTF8.GetBytes("""{"action":"SEARCH_REALTIME","target":"YOUTUBE","text":"Sơn Tùng M-TP"}""");
            await phone1.SendAsync(cmdBytes, WebSocketMessageType.Text, true, CancellationToken.None);
            await Task.Delay(100);
            Assert.Equal("SEARCH_REALTIME", lastAction);
            Assert.Equal("Sơn Tùng M-TP", lastSearch);

            // 4. Điện thoại dự phòng gửi lệnh qua HTTP POST /zenbox-cmd trong khi 3 máy WebSocket vẫn đang giữ kết nối
            using var postContent = new StringContent("""{"action":"VOL_UP"}""", Encoding.UTF8, "application/json");
            var postResp = await http.PostAsync($"http://127.0.0.1:{freePort}/zenbox-cmd", postContent);
            Assert.True(postResp.IsSuccessStatusCode);
            Assert.Equal("VOL_UP", lastAction);
            Assert.Equal(3, server.ConnectedClientCount);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
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

            mainVm.HandleRemoteCommand(new RemoteCommandPayload
            {
                Action = "SEARCH_REALTIME",
                Target = "OFFLINE",
                Text = "Bố Già"
            });

            Assert.Equal(AppSection.OfflineMovies, mainVm.ActiveSection);
            Assert.Equal("Bố Già", mainVm.MediaLibraryVm.SearchKeyword);

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
