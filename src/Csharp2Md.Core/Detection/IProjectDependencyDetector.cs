using Csharp2Md.Core.Discovery;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Detection;

/// <summary>
/// Input to a project-level detector. Project and package references are properties of the
/// project file, not of any one document, which is why this contract is separate (AD-004).
/// </summary>
public sealed record ProjectDetectionContext(
    ServiceName SourceService,
    string ProjectPath,
    ServiceCatalog Catalog);

/// <summary>Detects dependency signals visible at project-reference granularity (AD-004).</summary>
public interface IProjectDependencyDetector
{
    IEnumerable<DependencySignal> Detect(ProjectDetectionContext context);
}
