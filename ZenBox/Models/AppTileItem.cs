using System.Windows.Media;

namespace ZenBox.Models;

public sealed class AppTileItem
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required Geometry IconGeometry { get; init; }
    public required Brush BadgeBrush { get; init; }
    public required Brush GlowBrush { get; init; }
    public required AppSection Section { get; init; }
    public string? TargetUrl { get; init; }
    public bool RequiresInternet { get; init; }
}
