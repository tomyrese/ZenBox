using System.Text.Json.Serialization;

namespace ZenBox.Models;

public sealed class RemoteCommandPayload
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }
}
