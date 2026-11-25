using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ZenBox.Tests;

public sealed class UiComplianceTests
{
    private static readonly string[] ForbiddenTechnicalTerms =
    [
        "Settings",
        "Network Error",
        "Cache",
        "Buffer",
        "Exception"
    ];

    private static string FindZenBoxProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ZenBox", "ZenBox.csproj");
            if (File.Exists(candidate))
            {
                return Path.Combine(dir.FullName, "ZenBox");
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc của project ZenBox.");
    }

    [Fact]
    public void AllXamlFiles_ShouldNeverHaveFontSizeSmallerThan16()
    {
        var projectRoot = FindZenBoxProjectRoot();
        var xamlFiles = Directory.GetFiles(projectRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                        !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(xamlFiles);

        var directFontSizeRegex = new Regex(@"FontSize\s*=\s*""([0-9]+(?:\.[0-9]+)?)""", RegexOptions.IgnoreCase);

        foreach (var file in xamlFiles)
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetRelativePath(projectRoot, file);

            // Kiểm tra bằng Regex thuộc tính FontSize="..."
            foreach (Match match in directFontSizeRegex.Matches(content))
            {
                var size = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                Assert.True(size >= 16.0, $"Phát hiện FontSize={size} (< 16) tại {fileName}");
            }

            // Kiểm tra bằng XML Parser cho cả Setter Property="FontSize" Value="..."
            var doc = XDocument.Parse(content);
            foreach (var element in doc.Descendants())
            {
                var fontSizeAttr = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "FontSize");
                if (fontSizeAttr is not null &&
                    double.TryParse(fontSizeAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedAttrSize))
                {
                    Assert.True(parsedAttrSize >= 16.0, $"XML Parser phát hiện FontSize={parsedAttrSize} (< 16) tại {fileName}");
                }

                if (element.Name.LocalName == "Setter")
                {
                    var prop = element.Attribute("Property")?.Value;
                    var val = element.Attribute("Value")?.Value;
                    if (string.Equals(prop, "FontSize", StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var setterSize))
                    {
                        Assert.True(setterSize >= 16.0, $"Style Setter phát hiện FontSize={setterSize} (< 16) tại {fileName}");
                    }
                }
            }
        }
    }

    [Fact]
    public void AllXamlFiles_ShouldNotContainForbiddenEnglishTechnicalTerms()
    {
        var projectRoot = FindZenBoxProjectRoot();
        var xamlFiles = Directory.GetFiles(projectRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                        !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(xamlFiles);

        var standaloneForbiddenRegex = new Regex(
            @"\b(Settings|Network\s+Error|Cache|Buffer|Exception)\b",
            RegexOptions.IgnoreCase);

        var inspectedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Text", "Content", "Header", "Title", "ToolTip"
        };

        foreach (var file in xamlFiles)
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetRelativePath(projectRoot, file);

            // 1. Quét Regex độc lập trên toàn bộ nội dung XAML
            var regexMatch = standaloneForbiddenRegex.Match(content);
            Assert.False(
                regexMatch.Success,
                $"Phát hiện từ tiếng Anh kỹ thuật cấm '{regexMatch.Value}' trong tệp {fileName}");

            // 2. Quét XML Parser trên toàn bộ thuộc tính hiển thị người dùng
            var doc = XDocument.Parse(content);
            foreach (var element in doc.Descendants())
            {
                foreach (var attr in element.Attributes())
                {
                    if (!inspectedAttributes.Contains(attr.Name.LocalName) ||
                        attr.Value.StartsWith("{", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (var forbidden in ForbiddenTechnicalTerms)
                    {
                        Assert.DoesNotContain(
                            forbidden,
                            attr.Value,
                            StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
    }
}
