using ZenBox.Models;
using ZenBox.Services;
using ZenBox.ViewModels;

namespace ZenBox.Tests;

public sealed class BrandLogoFactoryTests
{
    [Fact]
    public void GetLogo_ShouldGenerateValidFrozenImageForEveryAppSection()
    {
        foreach (var section in Enum.GetValues<AppSection>())
        {
            var logo = BrandLogoFactory.GetLogo(section);
            Assert.NotNull(logo);
            Assert.True(logo.IsFrozen, $"Logo cho {section} phải ở trạng thái Frozen để render nhanh trên GPU.");
        }
    }

    [Fact]
    public void HomeViewModel_ShouldInitializeAllOnlineAndOfflineTilesWithoutException()
    {
        var vm = new HomeViewModel(_ => { });
        Assert.Equal(14, vm.OnlineTiles.Count);
        Assert.Equal(3, vm.OfflineAndSystemTiles.Count);
        Assert.All(vm.OnlineTiles, tile => Assert.NotNull(tile.LogoImage));
        Assert.All(vm.OfflineAndSystemTiles, tile => Assert.NotNull(tile.LogoImage));
    }

    [Fact]
    public void AudioPresetCatalog_ShouldProvideTenBandEqualizerGainsForEveryPreset()
    {
        foreach (var preset in Enum.GetValues<AudioPreset>())
        {
            Assert.False(string.IsNullOrWhiteSpace(AudioPresetCatalog.GetDisplayName(preset)));
            Assert.False(string.IsNullOrWhiteSpace(AudioPresetCatalog.GetDescription(preset)));
            var gains = AudioPresetCatalog.GetTenBandGains(preset);
            Assert.Equal(10, gains.Length);
        }
    }

    [Fact]
    public void SettingsViewModel_ShouldUpdateActiveButtonFlagsAndHaveIconsForAllSoundPresets()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ZenBox_SettingsTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var err = new ErrorHandlingService(tempDir);
            var cfg = new ConfigService(err, tempDir);
            var net = new NetworkMonitorService();
            var audio = new SystemAudioService();
            var web = new WebBrowserService(err, net, cfg);

            var vm = new SettingsViewModel(cfg, audio, web, _ => { }, _ => { }, _ => { }, () => { });

            // 1. Mọi lựa chọn Âm thanh đều phải có Icon Vector đi kèm
            Assert.Equal(6, vm.SoundPresets.Count);
            Assert.All(vm.SoundPresets, p =>
            {
                Assert.NotNull(p.IconGeometry);
                Assert.NotNull(p.IconBrush);
                Assert.NotNull(p.IconBgBrush);
            });

            // 2. Nút Bảo vệ mắt cập nhật đúng trạng thái chọn
            vm.SetEyeCareModeCommand.Execute("WarmSoft");
            Assert.True(vm.IsEyeCareWarmSoft);
            Assert.False(vm.IsEyeCareOff);
            Assert.False(vm.IsEyeCareDeepNight);

            // 3. Nút Khung hình, Tốc độ, Giới hạn RAM & Hẹn giờ cập nhật đúng trạng thái chọn
            vm.SetAspectRatioCommand.Execute("21:9");
            Assert.True(vm.IsAspect219);
            Assert.False(vm.IsAspectAuto);

            vm.SetPlaybackSpeedCommand.Execute("1.25");
            Assert.True(vm.IsSpeed125);
            Assert.False(vm.IsSpeed100);

            vm.SetMaxCachedAppsCommand.Execute("6");
            Assert.True(vm.IsMaxApps6);
            Assert.False(vm.IsMaxApps4);

            vm.SetSleepTimerMinutesCommand.Execute("30");
            Assert.True(vm.IsSleep30);
            Assert.False(vm.IsSleep0);

            web.Dispose();
            net.Dispose();
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
