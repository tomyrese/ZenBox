using System.Text.Json.Serialization;

namespace ZenBox.Models;

public sealed class RemoteCommandPayload
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("value")]
    public int? Value { get; set; }

    [JsonPropertyName("dx")]
    public double? Dx { get; set; }

    [JsonPropertyName("dy")]
    public double? Dy { get; set; }
}

public sealed class RemoteStateDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "STATE";

    [JsonPropertyName("activeSection")]
    public string ActiveSection { get; init; } = "Home";

    [JsonPropertyName("activeTitle")]
    public string ActiveTitle { get; init; } = "Trang Chủ";

    [JsonPropertyName("volume")]
    public int Volume { get; init; }

    [JsonPropertyName("isMuted")]
    public bool IsMuted { get; init; }

    [JsonPropertyName("audioPreset")]
    public string AudioPreset { get; init; } = "Balanced";

    [JsonPropertyName("audioPresetLabel")]
    public string AudioPresetLabel { get; init; } = "Chuẩn cân bằng";

    [JsonPropertyName("eyeCare")]
    public string EyeCare { get; init; } = "Off";

    [JsonPropertyName("sleepTimer")]
    public string SleepTimer { get; init; } = "Hẹn giờ: Tắt";
}
