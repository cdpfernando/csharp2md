using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using FactualDocumentDetectionContext = Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext;
using FactualProjectDetectionContext = Csharp2Md.Core.Detection.Contracts.ProjectDetectionContext;

namespace Csharp2Md.Core.Tests.Detection;

public sealed class DetectorHostTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Program.cs");

    [Fact]
    public void ProjectHost_InvokesOnlyDetectorsDeclaringProjectLevel()
    {
        var supported = new ProjectDetector("io.csharp2md.supported", [DetectorLevel.Project], "Supported");
        var unsupported = new ProjectDetector("io.csharp2md.unsupported", [DetectorLevel.Document], "Unsupported");
        var host = new DetectorHost([unsupported, supported]);

        var result = host.DetectProject(ProjectContext());

        Assert.Equal(1, supported.InvocationCount);
        Assert.Equal(0, unsupported.InvocationCount);
        Assert.Equal(["Supported"], result.Facts.OfType<SymbolFact>().Select(static fact => fact.SymbolKind).ToArray());
        Assert.Equal("C2M-DETECTOR-001", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void DocumentHost_InvokesOnlyDetectorsDeclaringDocumentLevel()
    {
        var supported = new DocumentDetector("io.csharp2md.supported", [DetectorLevel.Document], "Supported");
        var unsupported = new DocumentDetector("io.csharp2md.unsupported", [DetectorLevel.Project], "Unsupported");
        var host = new DetectorHost(documentDetectors: [unsupported, supported]);

        var result = host.DetectDocument(DocumentContext());

        Assert.Equal(1, supported.InvocationCount);
        Assert.Equal(0, unsupported.InvocationCount);
        Assert.Equal(["Supported"], result.Facts.OfType<SymbolFact>().Select(static fact => fact.SymbolKind).ToArray());
        Assert.Equal("C2M-DETECTOR-001", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void SuccessfulFact_ReceivesDescriptorIdentityAndVersionProvenance()
    {
        var detector = new DocumentDetector("io.csharp2md.http", [DetectorLevel.Document], "Http");
        var host = new DetectorHost(documentDetectors: [detector]);

        var fact = Assert.Single(host.DetectDocument(DocumentContext()).Facts);

        var provenance = Assert.Single(fact.Header.Provenance);
        Assert.Equal(detector.Descriptor.Id, provenance.DetectorId);
        Assert.Equal("1.0.0", provenance.DetectorVersion);
    }

    [Fact]
    public void SuccessfulDiagnostic_ReceivesDescriptorExtensionIdentity()
    {
        var detector = new DocumentDetector(
            "io.csharp2md.http",
            [DetectorLevel.Document],
            "Http",
            diagnostic: DetectorDiagnostic("HTTP001"));
        var host = new DetectorHost(documentDetectors: [detector]);

        var diagnostic = Assert.Single(host.DetectDocument(DocumentContext()).Diagnostics);

        Assert.Equal(detector.Descriptor.Id, diagnostic.ExtensionId);
        Assert.Equal("HTTP001", diagnostic.Code);
    }

    [Fact]
    public void UnsupportedReturnedFactKind_DiscardsTheWholeInvocation()
    {
        var detector = new DocumentDetector(
            "io.csharp2md.symbols",
            [DetectorLevel.Document],
            "Unsupported",
            supportedKinds: [FactKind.Relation]);
        var host = new DetectorHost(documentDetectors: [detector]);

        var result = host.DetectDocument(DocumentContext());

        Assert.Empty(result.Facts);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-DETECTOR-002", diagnostic.Code);
        Assert.Equal(detector.Descriptor.Id, diagnostic.ExtensionId);
    }

    [Fact]
    public void ThrowingDetector_DiscardsItsInvocationAndAddsOneScopedDiagnostic()
    {
        var detector = new DocumentDetector(
            "io.csharp2md.throwing",
            [DetectorLevel.Document],
            "Throwing",
            exception: new InvalidOperationException("controlled partial failure"));
        var host = new DetectorHost(documentDetectors: [detector]);

        var result = host.DetectDocument(DocumentContext());

        Assert.Empty(result.Facts);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-DETECTOR-003", diagnostic.Code);
        Assert.Equal(DocumentId.ToFactId(), diagnostic.ScopeId);
        Assert.DoesNotContain("controlled partial failure", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherDetectorsContinueInCanonicalDescriptorOrderAfterFailure()
    {
        var order = new List<string>();
        var late = new DocumentDetector("io.csharp2md.zeta", [DetectorLevel.Document], "Zeta", order: order);
        var throwing = new DocumentDetector(
            "io.csharp2md.middle",
            [DetectorLevel.Document],
            "Middle",
            exception: new InvalidOperationException(),
            order: order);
        var early = new DocumentDetector("io.csharp2md.alpha", [DetectorLevel.Document], "Alpha", order: order);
        var host = new DetectorHost(documentDetectors: [late, throwing, early]);

        var result = host.DetectDocument(DocumentContext());

        Assert.Equal(["io.csharp2md.alpha", "io.csharp2md.middle", "io.csharp2md.zeta"], order.ToArray());
        Assert.Equal(["Alpha", "Zeta"], result.Facts.OfType<SymbolFact>().Select(static fact => fact.SymbolKind).ToArray());
        Assert.Equal("C2M-DETECTOR-003", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void FailureInOneDocumentScope_DoesNotPreventTheNextScope()
    {
        var detector = new DocumentDetector(
            "io.csharp2md.scope",
            [DetectorLevel.Document],
            "Scoped",
            exceptionOnInvocation: 1);
        var host = new DetectorHost(documentDetectors: [detector]);

        var first = host.DetectDocument(DocumentContext());
        var second = host.DetectDocument(DocumentContext());

        Assert.Equal("C2M-DETECTOR-003", Assert.Single(first.Diagnostics).Code);
        Assert.Equal(["Scoped"], second.Facts.OfType<SymbolFact>().Select(static fact => fact.SymbolKind).ToArray());
        Assert.Empty(second.Diagnostics);
    }

    [Fact]
    public void DuplicateStableDetectorIdentityWithinOneLevel_IsRejectedBeforeExecution()
    {
        var first = new ProjectDetector("io.csharp2md.duplicate", [DetectorLevel.Project], "First");
        var second = new ProjectDetector("io.csharp2md.duplicate", [DetectorLevel.Project], "Second");

        var exception = Assert.Throws<ArgumentException>(() => new DetectorHost([first, second]));

        Assert.Contains("registered more than once", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, first.InvocationCount);
        Assert.Equal(0, second.InvocationCount);
    }

    [Fact]
    public void SameStableIdentityAcrossGranularities_RoutesEachDeclaredContract()
    {
        var project = new ProjectDetector("io.csharp2md.combined", [DetectorLevel.Project], "Project");
        var document = new DocumentDetector("io.csharp2md.combined", [DetectorLevel.Document], "Document");
        var host = new DetectorHost([project], [document]);

        var projectResult = host.DetectProject(ProjectContext());
        var documentResult = host.DetectDocument(DocumentContext());

        Assert.Equal(["Project"], projectResult.Facts.OfType<SymbolFact>().Select(static fact => fact.SymbolKind).ToArray());
        Assert.Equal(["Document"], documentResult.Facts.OfType<SymbolFact>().Select(static fact => fact.SymbolKind).ToArray());
        Assert.Equal(1, project.InvocationCount);
        Assert.Equal(1, document.InvocationCount);
    }

    private static FactualProjectDetectionContext ProjectContext()
    {
        var (project, target, index) = IndexedFacts();
        return new FactualProjectDetectionContext(project, [target], [], index);
    }

    private static FactualDocumentDetectionContext DocumentContext()
    {
        var (project, target, index) = IndexedFacts();
        var document = new DocumentFact(
            Header(DocumentId.ToFactId(), FactKind.Document),
            DocumentId,
            ProjectId,
            "Program.cs",
            [],
            []);
        return new FactualDocumentDetectionContext(project, target, document, [], index);
    }

    private static (ProjectFact Project, TargetFact Target, SolutionAnalysisIndex Index) IndexedFacts()
    {
        var target = new TargetFact(
            Header(TargetId.ToFactId(), FactKind.Target),
            TargetId,
            ProjectId,
            "net10.0",
            new TargetEvaluationDetails("Exe", "App", "App", [], [], [], [], [], "preview", "enable", []));
        var project = new ProjectFact(
            Header(ProjectId.ToFactId(), FactKind.Project),
            ProjectId,
            "App",
            "src/App/App.csproj",
            [TargetId],
            [DocumentId]);
        var index = SolutionAnalysisIndex.Build(
            [project],
            [new TargetAnalysisIndexInput(target, [], [])]);
        return (project, target, index);
    }

    private static SymbolFact Symbol(string symbolKind)
    {
        var id = SymbolFactId.CreateSyntactic(ProjectId, "Program.cs", "class", symbolKind);
        return new SymbolFact(
            Header(id.ToFactId(), FactKind.Symbol),
            id,
            DocumentId,
            symbolKind,
            false,
            [],
            [],
            []);
    }

    private static AnalysisDiagnostic DetectorDiagnostic(string code) =>
        AnalysisDiagnostic.Create(
            code,
            DiagnosticSeverity.Information,
            DiagnosticStage.Detector,
            DocumentId.ToFactId(),
            "Detector observation.");

    private static FactHeader Header(FactId id, FactKind kind) =>
        FactHeader.Create(id, kind, FactResolution.Exact);

    private sealed class ProjectDetector(
        string id,
        ImmutableArray<DetectorLevel> levels,
        string symbolKind) : IProjectFactDetector
    {
        public DetectorDescriptor Descriptor { get; } = DetectorDescriptor.Create(
            DetectorId.Create(id), "1.0.0", levels, [FactKind.Symbol]);

        public int InvocationCount { get; private set; }

        public DetectorResult Detect(FactualProjectDetectionContext context)
        {
            InvocationCount++;
            return DetectorResult.Create([Symbol(symbolKind)]);
        }
    }

    private sealed class DocumentDetector : IDocumentFactDetector
    {
        private readonly string _id;
        private readonly string _symbolKind;
        private readonly AnalysisDiagnostic? _diagnostic;
        private readonly Exception? _exception;
        private readonly int? _exceptionOnInvocation;
        private readonly List<string>? _order;

        public DocumentDetector(
            string id,
            ImmutableArray<DetectorLevel> levels,
            string symbolKind,
            AnalysisDiagnostic? diagnostic = null,
            Exception? exception = null,
            int? exceptionOnInvocation = null,
            List<string>? order = null,
            ImmutableArray<FactKind> supportedKinds = default)
        {
            _id = id;
            _symbolKind = symbolKind;
            _diagnostic = diagnostic;
            _exception = exception;
            _exceptionOnInvocation = exceptionOnInvocation;
            _order = order;
            Descriptor = DetectorDescriptor.Create(
                DetectorId.Create(id),
                "1.0.0",
                levels,
                supportedKinds.IsDefault ? [FactKind.Symbol] : supportedKinds);
        }

        public DetectorDescriptor Descriptor { get; }

        public int InvocationCount { get; private set; }

        public DetectorResult Detect(FactualDocumentDetectionContext context)
        {
            InvocationCount++;
            _order?.Add(_id);
            if (_exception is not null || _exceptionOnInvocation == InvocationCount)
            {
                throw _exception ?? new InvalidOperationException("controlled scope failure");
            }

            return DetectorResult.Create(
                [Symbol(_symbolKind)],
                _diagnostic is null ? [] : [_diagnostic]);
        }
    }
}
