using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Csharp2Md.Analysis.Inventory;

internal static partial class SolutionFileReader
{
    public static ImmutableArray<string> ReadProjectPaths(string solutionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        var fullPath = Path.GetFullPath(solutionPath);
        return Path.GetExtension(fullPath) switch
        {
            ".slnx" => ReadSlnx(fullPath),
            ".sln" => ReadSln(fullPath),
            _ => throw new ArgumentException(
                $"Unsupported solution file extension '{Path.GetExtension(fullPath)}'.",
                nameof(solutionPath)),
        };
    }

    private static ImmutableArray<string> ReadSlnx(string solutionPath)
    {
        var document = XDocument.Load(solutionPath);
        return
        [
            .. document.Descendants()
                .Where(element => element.Name.LocalName == "Project")
                .Select(element => element.Attribute("Path")?.Value)
                .OfType<string>(),
        ];
    }

    private static ImmutableArray<string> ReadSln(string solutionPath)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var line in File.ReadLines(solutionPath))
        {
            var match = SlnProjectPathPattern().Match(line);
            if (match.Success)
            {
                builder.Add(match.Groups["path"].Value);
            }
        }

        return builder.ToImmutable();
    }

    [GeneratedRegex(@"^Project\(""[^""]*""\)\s*=\s*""[^""]*"",\s*""(?<path>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex SlnProjectPathPattern();
}
