using System.IO;
using System.Text.Json;
using ZenBox.Services;

namespace ZenBox.Tests;

public sealed class AssetVerificationTests
{
    [Fact]
    public void UBlockManifestJson_ShouldExistAndBeValidInSourceAndOutput()
    {
        // 1. Kiểm tra trong thư mục output (CopyToOutputDirectory=PreserveNewest)
        var outputManifestPath = Path.Combine(
            WebBrowserService.GetUBlockExtensionDirectory(),
            "manifest.json");

        Assert.True(File.Exists(outputManifestPath), $"Không tìm thấy manifest.json tại output: {outputManifestPath}");

        var jsonText = File.ReadAllText(outputManifestPath);
        using var doc = JsonDocument.Parse(jsonText);
        Assert.True(doc.RootElement.TryGetProperty("name", out var nameProp));
        Assert.False(string.IsNullOrWhiteSpace(nameProp.GetString()));
        Assert.True(doc.RootElement.TryGetProperty("version", out var versionProp));
        Assert.False(string.IsNullOrWhiteSpace(versionProp.GetString()));

        // 2. Kiểm tra trực tiếp trong thư mục nguồn ZenBox/Assets/Extensions/uBlock/manifest.json
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? sourceManifestPath = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ZenBox", "Assets", "Extensions", "uBlock", "manifest.json");
            if (File.Exists(candidate))
            {
                sourceManifestPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(sourceManifestPath);
        Assert.True(File.Exists(sourceManifestPath));
    }

    [Fact]
    public void ApplicationIcon_ShouldExistAndHaveValidIcoHeader()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? icoPath = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ZenBox", "Assets", "zenbox.ico");
            if (File.Exists(candidate))
            {
                icoPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(icoPath);
        var bytes = File.ReadAllBytes(icoPath!);
        Assert.True(bytes.Length > 1000);
        // Header chuẩn ICO: 00 00 01 00
        Assert.Equal(0, bytes[0]);
        Assert.Equal(0, bytes[1]);
        Assert.Equal(1, bytes[2]);
        Assert.Equal(0, bytes[3]);
    }

    [Theory]
    [InlineData("https://googleads.g.doubleclick.net/pagead/ads", true)]
    [InlineData("https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js", true)]
    [InlineData("https://ads.tiktok.com/api/v1/track", true)]
    [InlineData("https://an.facebook.com/placement", true)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("https://www.google.com/search?q=tin+tuc", false)]
    public void WebBrowserService_ShouldBlockKnownAdDomainsOnly(string url, bool expectedBlocked)
    {
        var actual = WebBrowserService.IsBlockedAdUri(url);
        Assert.Equal(expectedBlocked, actual);
    }
}
