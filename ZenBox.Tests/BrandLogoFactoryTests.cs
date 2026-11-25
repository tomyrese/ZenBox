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
        Assert.True(vm.OnlineTiles.Count >= 10);
        Assert.Equal(3, vm.OfflineAndSystemTiles.Count);
        Assert.All(vm.OnlineTiles, tile => Assert.NotNull(tile.LogoImage));
        Assert.All(vm.OfflineAndSystemTiles, tile => Assert.NotNull(tile.LogoImage));
    }
}
