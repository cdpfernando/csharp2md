using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Relations.Resolution;

/// <summary>
/// T28/RELR-37: every C2M-RELR-* diagnostic the resolver can emit is provoked here, once, and pinned
/// for code, severity and scope (the relation's own fact id) - no diagnostic outside the declared
/// seven-code set is possible, and none of them ever changes the run's exit code on their own.
/// </summary>
public sealed class RelationResolverDiagnosticsTests
{
    private static readonly ImmutableHashSet<string> DeclaredCodes =
        ["C2M-RELR-001", "C2M-RELR-002", "C2M-RELR-003", "C2M-RELR-004", "C2M-RELR-005", "C2M-RELR-006", "C2M-RELR-007"];

    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();
    private static readonly ISymbolIndex EmptyIndex = SymbolIndexBuilder.Build([], [], [], []);
    private static readonly HashSet<FactId> NoKnownFacts = [];

    [Fact]
    public void Resolve_NoCandidateFound_EmitsC2MRELR001AtInformationScopedToTheRelation()
    {
        var resolution = ResolveOne(RelationResolver.Default, Claim("references", "Nothing"), EmptyIndex, NoKnownFacts);

        AssertSingleDiagnostic(resolution, "C2M-RELR-001", DiagnosticSeverity.Information);
    }

    [Fact]
    public void Resolve_AmbiguousCandidates_EmitsC2MRELR002AtInformationScopedToTheRelation()
    {
        var legacy = Symbol("Dup", "global::Acme.Legacy.Dup", "First.cs");
        var current = Symbol("Dup", "global::Acme.Current.Dup", "Second.cs");
        var index = SymbolIndexBuilder.Build([legacy, current], [], [], []);

        var resolution = ResolveOne(RelationResolver.Default, Claim("references", "Dup"), index, NoKnownFacts);

        AssertSingleDiagnostic(resolution, "C2M-RELR-002", DiagnosticSeverity.Information);
    }

    [Fact]
    public void Resolve_UndeterminableReceiverType_EmitsC2MRELR003AtInformationScopedToTheRelation()
    {
        var claim = Claim("calls", "target.Go") with { ReceiverText = "target", ReceiverTypeText = null, MemberName = "Go" };

        var resolution = ResolveOne(RelationResolver.Default, claim, EmptyIndex, NoKnownFacts);

        AssertSingleDiagnostic(resolution, "C2M-RELR-003", DiagnosticSeverity.Information);
    }

    [Fact]
    public void Resolve_InterpolatedSqlTarget_EmitsC2MRELR004AtInformationScopedToTheRelation()
    {
        var claim = Claim("reads", "dynamic-table") with
        {
            Partition = RelationPartition.Data,
            UnresolvedReason = Csharp2Md.Core.Analysis.DataAccess.Sql.SqlTextAnalyzer.DynamicSqlReason,
        };

        var resolution = ResolveOne(RelationResolver.Default, claim, EmptyIndex, NoKnownFacts);

        AssertSingleDiagnostic(resolution, "C2M-RELR-004", DiagnosticSeverity.Information);
    }

    [Fact]
    public void Resolve_ConventionNamedDatabaseTarget_EmitsC2MRELR005AtInformationScopedToTheRelation()
    {
        var claim = Claim("maps-to", "Orders") with
        {
            Partition = RelationPartition.Data,
            Details = [
                new RelationDetail("target_text", "Orders"),
                new RelationDetail("mapping", Csharp2Md.Core.Analysis.DataAccess.DatabaseMappingResolver.ConventionMapping),
            ],
        };

        var resolution = ResolveOne(RelationResolver.Default, claim, EmptyIndex, NoKnownFacts);

        AssertSingleDiagnostic(resolution, "C2M-RELR-005", DiagnosticSeverity.Information);
    }

    [Fact]
    public void Resolve_AThrowingStrategy_EmitsC2MRELR006AtWarningScopedToTheRelation()
    {
        // Terminated by UnresolvedStrategy per RELR-12/RELR-09: the throw is discarded, not the whole
        // resolution, so a second, expected C2M-RELR-001 also lands once the chain continues - the test
        // isolates the 006 entry rather than assuming it is the only diagnostic.
        var throwing = new ThrowingStrategy();
        var resolver = new RelationResolver([throwing, new UnresolvedStrategy()]);

        var resolution = ResolveOne(resolver, Claim("references", "Whatever"), EmptyIndex, NoKnownFacts);

        Assert.Equal(1, throwing.CallCount);
        var fact = Assert.Single(resolution.Facts);
        var diagnostic = Assert.Single(resolution.Diagnostics, static d => d.Code == "C2M-RELR-006");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(fact.Header.Id, diagnostic.ScopeId);
    }

    [Fact]
    public void Resolve_AnAbsentTarget_EmitsC2MRELR007AtWarningScopedToTheRelation()
    {
        var missingTarget = DatabaseObjectFactId.Create(DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "ghost").ToFactId();
        var claim = Claim("maps-to", "ghost") with
        {
            TargetId = missingTarget,
            ProducerMethod = ResolutionMethod.Configured,
        };
        var knownFacts = new HashSet<FactId> { OwnerId };

        var resolution = ResolveOne(RelationResolver.Default, claim, EmptyIndex, knownFacts);

        AssertSingleDiagnostic(resolution, "C2M-RELR-007", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Resolve_EveryDiagnosticTheResolverCanEmit_UsesOnlyACodeFromTheDeclaredSet()
    {
        var claims = new[]
        {
            Claim("references", "Nothing"),
            Claim("calls", "target.Go") with { ReceiverText = "target", ReceiverTypeText = null, MemberName = "Go" },
            Claim("reads", "dynamic-table") with
            {
                Partition = RelationPartition.Data,
                UnresolvedReason = Csharp2Md.Core.Analysis.DataAccess.Sql.SqlTextAnalyzer.DynamicSqlReason,
            },
            Claim("maps-to", "Orders") with
            {
                Partition = RelationPartition.Data,
                Details = [
                    new RelationDetail("target_text", "Orders"),
                    new RelationDetail("mapping", Csharp2Md.Core.Analysis.DataAccess.DatabaseMappingResolver.ConventionMapping),
                ],
            },
        };
        var accumulator = new RelationClaimAccumulator();
        accumulator.Add(DocumentId, "Worker.cs", [200], [.. claims]);

        var resolution = RelationResolver.Default.Resolve(
            accumulator.ToSnapshot(), EmptyIndex, NoKnownFacts, CancellationToken.None);

        Assert.NotEmpty(resolution.Diagnostics);
        Assert.All(resolution.Diagnostics, diagnostic => Assert.Contains(diagnostic.Code, DeclaredCodes));
    }

    [Fact]
    public async Task AnalyzeAsync_ARunProducingOnlyRelrDiagnostics_ExitsZero()
    {
        var input = Directory.CreateTempSubdirectory("csharp2md-relr-diagnostics-input-").FullName;
        var output = Path.Combine(Path.GetTempPath(), $"csharp2md-relr-diagnostics-output-{Guid.NewGuid():N}");
        try
        {
            var directory = Path.Combine(input, "App");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(Path.Combine(directory, "C.cs"), """
                class Worker
                {
                    void Run()
                    {
                        Nonexistent target = new Nonexistent();
                        target.Go();
                    }
                }
                """);
            var request = Assert.IsType<AnalysisRequest>(AnalysisRequest.Create(input, output).Request);

            var result = await new AnalysisEngine().AnalyzeAsync(request);

            Assert.Equal(0, result.ExitCode);
            var diagnosticsText = File.ReadAllText(Path.Combine(output, "raw", "facts", "diagnostics.json"));
            Assert.Contains("C2M-RELR-", diagnosticsText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(input, recursive: true);
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static void AssertSingleDiagnostic(RelationResolution resolution, string code, DiagnosticSeverity severity)
    {
        var fact = Assert.Single(resolution.Facts);
        var diagnostic = Assert.Single(resolution.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal(severity, diagnostic.Severity);
        Assert.Equal(fact.Header.Id, diagnostic.ScopeId);
    }

    private static RelationResolution ResolveOne(
        RelationResolver resolver, RawRelation claim, ISymbolIndex index, IReadOnlySet<FactId> knownFacts)
    {
        var accumulator = new RelationClaimAccumulator();
        accumulator.Add(DocumentId, "Worker.cs", [40], [claim]);
        return resolver.Resolve(accumulator.ToSnapshot(), index, knownFacts, CancellationToken.None);
    }

    private sealed class ThrowingStrategy : IRelationResolutionStrategy
    {
        public int CallCount { get; private set; }

        public RelationResolutionOutcome TryResolve(RelationResolutionContext context)
        {
            CallCount++;
            throw new InvalidOperationException("boom");
        }
    }

    private static SymbolFact Symbol(string name, string fullyQualifiedName, string documentPath) =>
        new(
            FactHeader.Create(
                SymbolFactId.CreateSyntactic(ProjectId, documentPath, "class", fullyQualifiedName).ToFactId(),
                FactKind.Symbol, FactResolution.Syntactic),
            SymbolFactId.CreateSyntactic(ProjectId, documentPath, "class", fullyQualifiedName),
            DocumentFactId.Create(ProjectId, documentPath),
            "class",
            ContainsErrorSymbol: false,
            [], [], [],
            Semantics: null,
            Name: name,
            FullyQualifiedName: fullyQualifiedName,
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: $"class {name}",
            Arity: 0,
            ParameterTypes: []);

    private static RawRelation Claim(string kind, string targetText) => new()
    {
        Kind = kind,
        OwnerId = OwnerId,
        Evidence = new Evidence(DocumentId, "Worker.cs", 1, 1, 1, 10),
        ShapeConfidence = FactResolution.Syntactic,
        Partition = RelationPartition.Structural,
        Details = [new RelationDetail("target_text", targetText)],
        TargetText = targetText,
    };
}
