using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Csharp2Md.Core.Discovery;

/// <summary>
/// Extracts the list of .csproj paths referenced by a solution file. Plain text/XML parsing only —
/// no MSBuild evaluation, keeping Stage 1 cheap (mirrors <c>ProjectIdentityReader</c>'s own
/// constraint). Paths are resolved to absolute, relative to the solution file's directory.
/// </summary>
public static partial class SolutionProjectPaths
{
    public static IReadOnlyList<string> Read(string solutionPath) =>
        Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            ? ReadSlnx(solutionPath)
            : ReadClassicSln(solutionPath);

    private static IReadOnlyList<string> ReadSlnx(string solutionPath)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var document = XDocument.Load(solutionPath);

        return document.Descendants("Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(path => path is not null && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFullPath(Path.Combine(solutionDir, path!)))
            .ToList();
    }

    private static IReadOnlyList<string> ReadClassicSln(string solutionPath)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var paths = new List<string>();

        foreach (var line in File.ReadLines(solutionPath))
        {
            var match = ClassicProjectLine().Match(line);
            if (match.Success && match.Groups["path"].Value.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(Path.GetFullPath(Path.Combine(solutionDir, match.Groups["path"].Value)));
            }
        }

        return paths;
    }

    // Project("{TypeGuid}") = "Name", "RelativePath", "{ProjectGuid}" — stable .sln format.
    [GeneratedRegex("^Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"(?<path>[^\"]+)\"")]
    private static partial Regex ClassicProjectLine();
}
