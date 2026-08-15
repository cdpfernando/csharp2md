using System.Xml;
using System.Xml.Linq;

namespace Csharp2Md.Core.Discovery;

/// <summary>
/// Reads each project's effective <c>PackageId</c> via a plain XML read — no MSBuild evaluation,
/// keeping Stage 1 cheap. Enables internal-package matching (P2-04, P2-05).
/// </summary>
public static class ProjectIdentityReader
{
    // Returns PackageId (value object), not string — matches ServiceDescriptor.PackageIds' own
    // type. design.md's Interfaces sketch said `IReadOnlyList<string>`, but its own Data Models
    // section types ServiceDescriptor.PackageIds as IReadOnlyList<PackageId>, and the whole point
    // of PackageId as a value object (per P2-04's rationale) is to stop raw strings from being
    // compared silently — returning string here would undermine that immediately.
    public static IReadOnlyList<PackageId> ReadPackageIds(ServiceDescriptor service) =>
        service.ProjectPaths.Select(ReadPackageId).ToList();

    private static PackageId ReadPackageId(string projectPath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(projectPath);
        }
        catch (XmlException)
        {
            return new PackageId(FileNameIdentity(projectPath));
        }

        var explicitId = document.Descendants("PackageId").FirstOrDefault()?.Value.Trim();
        if (!string.IsNullOrEmpty(explicitId))
        {
            return new PackageId(explicitId);
        }

        var assemblyName = document.Descendants("AssemblyName").FirstOrDefault()?.Value.Trim();
        if (!string.IsNullOrEmpty(assemblyName))
        {
            return new PackageId(assemblyName);
        }

        return new PackageId(FileNameIdentity(projectPath));
    }

    // SDK default precedence when neither <PackageId> nor <AssemblyName> is set: both fall back to
    // the project file name without extension.
    private static string FileNameIdentity(string projectPath) => Path.GetFileNameWithoutExtension(projectPath);
}
