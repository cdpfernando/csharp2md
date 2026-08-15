using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Graph;
using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Detection;

/// <summary>
/// Input to a document-level detector.
/// </summary>
/// <remarks>
/// <paramref name="SemanticModel"/> is nullable on purpose: detectors degrade alongside the
/// renderer, so a project that failed to restore still yields whatever syntax alone can prove
/// (AD-002). The service catalog is deliberately absent — mapping a logical name to a specific
/// catalog service is not defined by the spec, so detectors classify resolution and record the
/// raw target, leaving correlation to the graph builder.
/// </remarks>
public sealed record DocumentDetectionContext(
    ServiceName SourceService,
    string DocumentPath,
    SyntaxTree SyntaxTree,
    SemanticModel? SemanticModel,
    ConfigIndex ConfigIndex);

/// <summary>Detects dependency signals visible within one source document (AD-004).</summary>
public interface IDocumentDependencyDetector
{
    IEnumerable<DependencySignal> Detect(DocumentDetectionContext context);
}
