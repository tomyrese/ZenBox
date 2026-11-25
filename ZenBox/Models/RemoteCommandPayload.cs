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

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }
}

public sealed class RemoteStateDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "STATE";

    [JsonPropertyName("serverName")]
    public string ServerName { get; init; } = Environment.MachineName;

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

    [JsonPropertyName("connectedClients")]
    public int ConnectedClients { get; set; }
}

public sealed class RemoteDiscoveryInfoDto
{
    [JsonPropertyName("app")]
    public string App { get; init; } = "ZenBox";

    [JsonPropertyName("serverName")]
    public string ServerName { get; init; } = Environment.MachineName;

    [JsonPropertyName("ip")]
    public string Ip { get; init; } = "127.0.0.1";

    [JsonPropertyName("allIps")]
    public IReadOnlyList<string> AllIps { get; init; } = [];

    [JsonPropertyName("port")]
    public int Port { get; init; } = 8989;

    [JsonPropertyName("connectedClients")]
    public int ConnectedClients { get; init; }

    [JsonPropertyName("state")]
    public RemoteStateDto? State { get; init; }
}
