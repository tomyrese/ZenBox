using System.IO;

namespace ZenBox.Models;

public sealed class AppConfig
{
    public string MoviesFolderPath { get; set; } = GetDefaultVideosFolder();
    public string MusicFolderPath { get; set; } = GetDefaultMusicFolder();
    public bool StartWithWindows { get; set; }
    public int RemotePort { get; set; } = 8989;
    public double? FloatingButtonLeft { get; set; }
    public double? FloatingButtonTop { get; set; }

    private static string GetDefaultVideosFolder()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        return string.IsNullOrWhiteSpace(path) ? AppContext.BaseDirectory : path;
    }

    private static string GetDefaultMusicFolder()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        return string.IsNullOrWhiteSpace(path) ? AppContext.BaseDirectory : path;
    }
}
