using System.Windows.Media;

namespace ZenBox.Models;

public sealed class MediaItem
{
    public required string Title { get; init; }
    public required string FilePath { get; init; }
    public required string FileSizeText { get; init; }
    public required string SubInfoText { get; init; }
    public required bool IsVideo { get; init; }
    public ImageSource? CoverImage { get; init; }
}
