namespace ZenBox.Models;

public sealed class AppConfig
{
    public string MoviesFolderPath { get; set; } = GetDefaultVideosFolder();
    public string MusicFolderPath { get; set; } = GetDefaultMusicFolder();
    public bool StartWithWindows { get; set; }
    public int RemotePort { get; set; } = 8989;
    public double? FloatingButtonLeft { get; set; }
    public double? FloatingButtonTop { get; set; }

    // Cài đặt Âm thanh & Xem phim
    public AudioPreset SoundPreset { get; set; } = AudioPreset.Balanced;
    public string DefaultAspectRatio { get; set; } = "AUTO";
    public double DefaultPlaybackSpeed { get; set; } = 1.0;
    public bool AutoResumeVideos { get; set; } = true;

    // Bảo vệ mắt & Hiệu năng
    public EyeCareMode EyeCare { get; set; } = EyeCareMode.Off;
    public bool SmartMemorySaver { get; set; } = true;
    public int MaxCachedWebApps { get; set; } = 4;

    // Lưu vị trí xem dở của từng tệp phim (FilePath -> Milliseconds)
    public Dictionary<string, long> ResumePositions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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
