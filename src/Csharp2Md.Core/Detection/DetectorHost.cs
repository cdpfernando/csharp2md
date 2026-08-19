using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using FactualDocumentDetectionContext = Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext;
using FactualProjectDetectionContext = Csharp2Md.Core.Detection.Contracts.ProjectDetectionContext;

namespace Csharp2Md.Core.Detection;

internal sealed class DetectorHost
{
    private const string EngineId = "csharp2md.detector-host";
    private const string EngineVersion = "3.0.0";

    private readonly ImmutableArray<IProjectFactDetector> _projectDetectors;
    private readonly ImmutableArray<IDocumentFactDetector> _documentDetectors;

    public DetectorHost(
        IEnumerable<IProjectFactDetector>? projectDetectors = null,
        IEnumerable<IDocumentFactDetector>? documentDetectors = null)
    {
        _projectDetectors = Canonicalize(projectDetectors ?? []);
        _documentDetectors = Canonicalize(documentDetectors ?? []);
        RejectDuplicateDescriptors(_projectDetectors.Select(static detector => detector.Descriptor));
        RejectDuplicateDescriptors(_documentDetectors.Select(static detector => detector.Descriptor));
    }

    public DetectorResult DetectProject(FactualProjectDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Run(
            _projectDetectors,
            DetectorLevel.Project,
            context.Project.ProjectId.ToFactId(),
            detector => detector.Detect(context));
    }

    public DetectorResult DetectDocument(FactualDocumentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Run(
            _documentDetectors,
            DetectorLevel.Document,
            context.Document.DocumentId.ToFactId(),
            detector => detector.Detect(context));
    }

    private static DetectorResult Run<TDetector>(
        ImmutableArray<TDetector> detectors,
        DetectorLevel level,
        FactId scopeId,
        Func<TDetector, DetectorResult> detect)
        where TDetector : class
    {
        var facts = ImmutableArray.CreateBuilder<IFact>();
        var diagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
        foreach (var detector in detectors)
        {
            var descriptor = Descriptor(detector);
            if (!descriptor.SupportedLevels.Contains(level))
            {
                diagnostics.Add(HostDiagnostic(
                    "C2M-DETECTOR-001",
                    scopeId,
                    descriptor,
                    "Detector registration does not support the requested fact level."));
                continue;
            }

            try
            {
                var result = detect(detector);
                if (result.Facts.Any(fact => !descriptor.SupportedFactKinds.Contains(fact.Header.Kind)))
                {
                    diagnostics.Add(HostDiagnostic(
                        "C2M-DETECTOR-002",
                        scopeId,
                        descriptor,
                        "Detector returned a fact kind outside its descriptor contract."));
                    continue;
                }

                facts.AddRange(result.Facts.Select(fact => WithDescriptorProvenance(fact, descriptor)));
                diagnostics.AddRange(result.Diagnostics.Select(diagnostic => WithDescriptor(diagnostic, descriptor)));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(HostDiagnostic(
                    "C2M-DETECTOR-003",
                    scopeId,
                    descriptor,
                    "Detector invocation failed; its incomplete facts were discarded.",
                    exception.GetType().FullName ?? exception.GetType().Name));
            }
        }

        return DetectorResult.Create(facts, diagnostics);
    }

    private static DetectorDescriptor Descriptor<TDetector>(TDetector detector) where TDetector : class =>
        detector switch
        {
            IProjectFactDetector project => project.Descriptor,
            IDocumentFactDetector document => document.Descriptor,
            _ => throw new InvalidOperationException("Unsupported factual detector contract."),
        };

    private static IFact WithDescriptorProvenance(IFact fact, DetectorDescriptor descriptor)
    {
        var header = FactHeader.Create(
            fact.Header.Id,
            fact.Header.Kind,
            fact.Header.Resolution,
            fact.Header.Provenance.Append(new FactProvenance(
                EngineId,
                EngineVersion,
                descriptor.Id,
                descriptor.Version)),
            fact.Header.Evidence,
            fact.Header.DiagnosticIds);
        return fact switch
        {
            SolutionFact value => value with { Header = header },
            ProjectFact value => value with { Header = header },
            TargetFact value => value with { Header = header },
            DocumentFact value => value with { Header = header },
            SourceSectionFact value => value with { Header = header },
            SymbolFact value => value with { Header = header },
            ComponentFact value => value with { Header = header },
            RelationFact value => value with { Header = header },
            _ => throw new InvalidOperationException($"Unsupported factual type '{fact.GetType().FullName}'."),
        };
    }

    private static AnalysisDiagnostic WithDescriptor(
        AnalysisDiagnostic diagnostic,
        DetectorDescriptor descriptor) =>
        AnalysisDiagnostic.Create(
            diagnostic.Code,
            diagnostic.Severity,
            diagnostic.Stage,
            diagnostic.ScopeId,
            diagnostic.Message,
            diagnostic.Data,
            diagnostic.Evidence,
            descriptor.Id);

    private static AnalysisDiagnostic HostDiagnostic(
        string code,
        FactId scopeId,
        DetectorDescriptor descriptor,
        string message,
        string? exceptionType = null)
    {
        var data = new List<DiagnosticData>
        {
            new("detector_id", descriptor.Id.Value),
            new("detector_version", descriptor.Version),
        };
        if (exceptionType is not null)
        {
            data.Add(new DiagnosticData("exception_type", exceptionType));
        }

        return AnalysisDiagnostic.Create(
            code,
            DiagnosticSeverity.Warning,
            DiagnosticStage.Detector,
            scopeId,
            message,
            data,
            extensionId: descriptor.Id);
    }

    private static ImmutableArray<TDetector> Canonicalize<TDetector>(IEnumerable<TDetector> detectors)
        where TDetector : class =>
        detectors
            .OrderBy(static detector => Descriptor(detector).Id.Value, StringComparer.Ordinal)
            .ThenBy(static detector => Descriptor(detector).Version, StringComparer.Ordinal)
            .ToImmutableArray();

    private static void RejectDuplicateDescriptors(IEnumerable<DetectorDescriptor> descriptors)
    {
        var duplicate = descriptors
            .GroupBy(static descriptor => descriptor.Id)
            .FirstOrDefault(static group => group.Skip(1).Any());
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Detector '{duplicate.Key.Value}' is registered more than once.",
                nameof(descriptors));
        }
    }
}
