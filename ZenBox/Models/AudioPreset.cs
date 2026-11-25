namespace ZenBox.Models;

public enum AudioPreset
{
    Balanced,
    CinemaSurround,
    ClearVoice,
    WarmBolero,
    BassBoost,
    NightSoft
}

public enum EyeCareMode
{
    Off,
    WarmSoft,
    DeepNight
}

public static class AudioPresetCatalog
{
    public static string GetDisplayName(AudioPreset preset) => preset switch
    {
        AudioPreset.Balanced => "Chuẩn cân bằng",
        AudioPreset.CinemaSurround => "Rạp chiếu phim",
        AudioPreset.ClearVoice => "Rõ lời thoại",
        AudioPreset.WarmBolero => "Nhạc trữ tình ấm",
        AudioPreset.BassBoost => "Tăng âm trầm",
        AudioPreset.NightSoft => "Ban đêm dịu êm",
        _ => "Chuẩn cân bằng"
    };

    public static string GetDescription(AudioPreset preset) => preset switch
    {
        AudioPreset.Balanced => "Âm thanh trung thực nguyên bản cho mọi nội dung",
        AudioPreset.CinemaSurround => "Tăng trầm sâu và âm cao sắc nét khi xem phim điện ảnh",
        AudioPreset.ClearVoice => "Làm nổi bật giọng nói biên tập viên và lời thoại phim",
        AudioPreset.WarmBolero => "Âm trung ngọt ngào, dải cao êm dịu cho nhạc trữ tình",
        AudioPreset.BassBoost => "Đẩy mạnh dải trầm uy lực cho nhạc sôi động",
        AudioPreset.NightSoft => "Giảm tiếng động lớn đột ngột để xem khuya yên tĩnh",
        _ => string.Empty
    };

    // 10 băng tần chuẩn LibVLC (31Hz, 63Hz, 125Hz, 250Hz, 500Hz, 1kHz, 2kHz, 4kHz, 8kHz, 16kHz)
    public static float[] GetTenBandGains(AudioPreset preset) => preset switch
    {
        AudioPreset.CinemaSurround => [5.5f, 6.0f, 3.5f, 0.5f, -1.0f, 1.5f, 3.0f, 4.5f, 5.0f, 4.5f],
        AudioPreset.ClearVoice => [-3.0f, -2.0f, -0.5f, 2.5f, 5.0f, 6.5f, 6.5f, 4.5f, 1.5f, 0.0f],
        AudioPreset.WarmBolero => [2.5f, 3.5f, 3.0f, 2.0f, 2.5f, 2.0f, 1.0f, 1.5f, 2.5f, 2.0f],
        AudioPreset.BassBoost => [7.5f, 8.0f, 6.5f, 3.5f, 0.5f, 0.0f, 1.0f, 2.0f, 2.5f, 2.0f],
        AudioPreset.NightSoft => [-4.5f, -3.5f, -1.0f, 1.5f, 3.5f, 4.5f, 3.5f, 1.0f, -1.5f, -3.0f],
        _ => [0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f]
    };

    // (Bass dB @ 100Hz, Mid dB @ 1800Hz, Treble dB @ 9000Hz) cho bộ lọc WebAudio trực tuyến
    public static (int BassDb, int MidDb, int TrebleDb) GetWebThreeBandGains(AudioPreset preset) => preset switch
    {
        AudioPreset.CinemaSurround => (6, 1, 5),
        AudioPreset.ClearVoice => (-3, 7, 2),
        AudioPreset.WarmBolero => (3, 3, 2),
        AudioPreset.BassBoost => (8, 0, 2),
        AudioPreset.NightSoft => (-5, 4, -3),
        _ => (0, 0, 0)
    };
}
