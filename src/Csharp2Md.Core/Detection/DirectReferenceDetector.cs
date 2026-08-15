using System.Xml;
using System.Xml.Linq;
using Csharp2Md.Core.Discovery;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Detection;

/// <summary>
/// Detects compile-time coupling between services: a project reference to another manifest
/// service's project (P2-04), or a package reference whose id matches another manifest service's
/// own <c>PackageId</c> (P2-04). References that match no manifest service are public libraries and
/// produce no edge (P2-05).
/// </summary>
/// <remarks>
/// Unlike the HTTP and gRPC detectors, this one **does** populate <c>TargetService</c>: P2-04
/// states the matching rule outright (same project file, or equal <c>PackageId</c>), so the target
/// is derived, not guessed.
/// <para>
/// Reads the project file as plain XML, matching <c>ProjectIdentityReader</c> - no MSBuild
/// evaluation, so detection stays as cheap as inventory.
/// </para>
/// </remarks>
public sealed class DirectReferenceDetector : IProjectDependencyDetector
{
    public IEnumerable<DependencySignal> Detect(ProjectDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        XDocument document;
        try
        {
            document = XDocument.Load(context.ProjectPath, LoadOptions.SetLineInfo);
        }
        catch (Exception exception) when (exception is XmlException or IOException)
        {
            // An unreadable project file is a load problem, already reported by SolutionLoader.
            return [];
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(context.ProjectPath)) ?? string.Empty;
        var signals = new List<DependencySignal>();

        foreach (var element in document.Descendants("ProjectReference"))
        {
            if (Include(element) is not { } include)
            {
                continue;
            }

            var referenced = Normalize(Path.Combine(projectDirectory, include.Replace('\\', Path.DirectorySeparatorChar)));

            if (OwnerOf(context.Catalog, service => service.ProjectPaths.Any(p => SamePath(p, referenced))) is { } owner)
            {
                Add(signals, context, owner, include, element);
            }
        }

        foreach (var element in document.Descendants("PackageReference"))
        {
            if (Include(element) is not { } include)
            {
                continue;
            }

            var packageId = new PackageId(include);

            if (OwnerOf(context.Catalog, service => service.PackageIds.Contains(packageId)) is { } owner)
            {
                Add(signals, context, owner, include, element);
            }
        }

        return signals;
    }

    private static void Add(
        List<DependencySignal> signals,
        ProjectDetectionContext context,
        ServiceDescriptor target,
        string rawTarget,
        XElement element)
    {
        // A reference within the same service is internal structure, not a service dependency.
        if (target.Name == context.SourceService)
        {
            return;
        }

        signals.Add(new DependencySignal(
            SourceService: context.SourceService,
            TargetService: target.Name,
            RawTarget: rawTarget,
            Kind: DependencyKind.DirectReference,
            Communication: CommunicationClassifier.Classify(DependencyKind.DirectReference, CallShape.NotApplicable),
            // Nothing was resolved through config: the reference names its target at compile time.
            Resolution: ResolutionKind.NotApplicable,
            Role: null,
            Location: new SourceLocation(context.ProjectPath, ((IXmlLineInfo)element).LineNumber)));
    }

    private static ServiceDescriptor? OwnerOf(ServiceCatalog catalog, Func<ServiceDescriptor, bool> predicate) =>
        catalog.Services.FirstOrDefault(predicate);

    private static string? Include(XElement element)
    {
        var include = element.Attribute("Include")?.Value.Trim();
        return string.IsNullOrEmpty(include) ? null : include;
    }

    private static string Normalize(string path) => Path.GetFullPath(path);

    private static bool SamePath(string left, string right) =>
        string.Equals(Normalize(left), right, StringComparison.OrdinalIgnoreCase);
}
