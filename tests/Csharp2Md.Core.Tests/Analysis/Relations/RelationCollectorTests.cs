using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Analysis.Relations;

public sealed class RelationCollectorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();

    // Every one of the 10 in-scope kinds, its expected RelationPartition, and its observed target
    // text (the "http-call" case uses the pipe-delimited encoding SyntaxFactExtractor actually emits).
    public static TheoryData<string, RelationPartition, string> AllKinds => new()
    {
        { "calls", RelationPartition.Structural, "paymentsClient.AuthorizePayment" },
        { "creates", RelationPartition.Structural, "PaymentAuthorizer" },
        { "references", RelationPartition.Structural, "IEventBus" },
        { "inherits", RelationPartition.Inheritance, "PaymentsBase" },
        { "implements", RelationPartition.Inheritance, "IDisposable" },
        { "publishes", RelationPartition.Events, "PaymentProcessed" },
        { "subscribes", RelationPartition.Events, "OrderPlaced" },
        { "handles", RelationPartition.Events, "OrderPlaced" },
        { "http-client", RelationPartition.Http, "PaymentService" },
        { "http-call", RelationPartition.Http, "http_method=POST|route=payments/authorize" },
    };

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void CreateClaims_OneCandidatePerKind_MapsToTheDocumentedPartition(
        string relationKind, RelationPartition expectedPartition, string observedTarget)
    {
        var candidates = ImmutableArray.Create(Candidate(relationKind, observedTarget, FactResolution.Syntactic));

        var claims = RelationCollector.CreateClaims(DocumentId, "Worker.cs", candidates);

        var claim = Assert.Single(claims);
        Assert.Equal(relationKind, claim.Kind);
        Assert.Equal(expectedPartition, claim.Partition);
    }

    [Fact]
    public void CreateClaims_EveryClaim_CarriesEvidenceAndNoResolvedTargetYet()
    {
        var candidates = ImmutableArray.CreateRange(
            AllKinds.Select(row => Candidate((string)row[0]!, (string)row[2]!, FactResolution.Syntactic)));

        var claims = RelationCollector.CreateClaims(DocumentId, "Worker.cs", candidates);

        Assert.Equal(candidates.Length, claims.Length);
        Assert.All(claims, claim =>
        {
            Assert.NotEqual(default, claim.Evidence);
            Assert.Null(claim.TargetId);
            Assert.Null(claim.ProducerMethod);
            // RELR-16: resolving what a claim could not resolve - and saying why - is
            // RelationResolver's job in pass two; the collector no longer states a reason at all.
            Assert.Null(claim.UnresolvedReason);
        });
    }

    [Fact]
    public void CreateClaims_HttpCallObservedTarget_SplitsIntoSeparateHttpMethodAndRouteDetails()
    {
        var candidates = ImmutableArray.Create(
            Candidate("http-call", "http_method=POST|route=payments/authorize", FactResolution.Syntactic));

        var claim = Assert.Single(RelationCollector.CreateClaims(DocumentId, "Worker.cs", candidates));

        Assert.Equal(2, claim.Details.Length);
        Assert.Contains(claim.Details, detail => detail is { Key: "http_method", Value: "POST" });
        Assert.Contains(claim.Details, detail => detail is { Key: "route", Value: "payments/authorize" });
        Assert.DoesNotContain(claim.Details, detail => detail.Key == "target_text");
    }

    [Fact]
    public void CreateClaims_HttpCallRouteSpansMultipleLines_PreservesTheRawTextWithoutThrowing()
    {
        // Regression guard: a non-literal route argument (e.g. an object-initializer expression) is
        // captured verbatim by SyntaxFactExtractor and can span multiple lines. Claim construction
        // must not throw over that shape, and the "route" detail must keep the original raw text -
        // canonicalizing it for a claim fingerprint is RelationResolver's concern (T21), not this
        // collector's, since claims no longer mint a RelationFactId here.
        var multiLineRoute = "new PayrollProposalFilters {\n  PageNumber = 1,\n  PageSize = 20\n}";
        var candidates = ImmutableArray.Create(
            Candidate("http-call", $"http_method=GET|route={multiLineRoute}", FactResolution.Syntactic));

        var claim = Assert.Single(RelationCollector.CreateClaims(DocumentId, "Worker.cs", candidates));

        Assert.Contains(claim.Details, detail => detail is { Key: "route" } && detail.Value == multiLineRoute);
    }

    [Fact]
    public void CreateClaims_NonHttpCallKind_WrapsObservedTargetAsOneTargetTextDetail()
    {
        var candidates = ImmutableArray.Create(Candidate("creates", "PaymentAuthorizer", FactResolution.Syntactic));

        var claim = Assert.Single(RelationCollector.CreateClaims(DocumentId, "Worker.cs", candidates));

        var detail = Assert.Single(claim.Details);
        Assert.Equal("target_text", detail.Key);
        Assert.Equal("PaymentAuthorizer", detail.Value);
    }

    [Fact]
    public void CreateClaims_TwoCandidatesSameKindAndTarget_BothSurviveAsDistinctClaims()
    {
        // Ordinal-disambiguated identity now belongs to RelationResolver (T21) - not this collector -
        // so what this layer must still prove is that neither occurrence is deduplicated away: both
        // claims survive, distinguished by the evidence of their own call site.
        var candidates = ImmutableArray.Create(
            Candidate("calls", "paymentsClient.AuthorizePayment", FactResolution.Syntactic, startLine: 10),
            Candidate("calls", "paymentsClient.AuthorizePayment", FactResolution.Syntactic, startLine: 20));

        var claims = RelationCollector.CreateClaims(DocumentId, "Worker.cs", candidates);

        Assert.Equal(2, claims.Length);
        Assert.All(claims, claim => Assert.Equal("calls", claim.Kind));
        Assert.All(claims, claim => Assert.Single(claim.Details, detail => detail is { Key: "target_text", Value: "paymentsClient.AuthorizePayment" }));
        Assert.Equal(2, claims.Select(claim => claim.Evidence).Distinct().Count());
        Assert.Contains(claims, claim => claim.Evidence.StartLine == 10);
        Assert.Contains(claims, claim => claim.Evidence.StartLine == 20);
    }

    private static SyntacticRelationCandidate Candidate(
        string relationKind, string observedTarget, FactResolution resolution, int startLine = 1) =>
        new(OwnerId, relationKind, observedTarget, resolution, startLine, 1, startLine, 10);

    // T13: RelationCollector.RefineClaims - semantic refinement merged into the baseline claim by
    // FactResolutionAlgebra.Stronger (AD-018), replacing the old fact-level rank merge.

    [Fact]
    public void RefineClaims_PublishAsyncThroughVariable_DiscoversAPublishesClaimThatCreateClaimsCannot()
    {
        const string source = """
            using Acme.Contracts;

            namespace App;

            public sealed class Worker(IEventBus bus)
            {
                public async System.Threading.Tasks.Task RunAsync()
                {
                    var message = new PaymentProcessed(System.Guid.NewGuid());
                    await bus.PublishAsync(message);
                }
            }
            """;
        var (extraction, model) = Compile(source);

        // spec.md's Assumptions table: no explicit <T> and the argument isn't an object-creation
        // expression, so the syntax-only pass yields no "publishes" candidate for this call at all.
        Assert.DoesNotContain(extraction.RelationCandidates, candidate => candidate.RelationKind == "publishes");
        var baseline = RelationCollector.CreateClaims(extraction.Document.DocumentId, "Worker.cs", extraction.RelationCandidates);
        Assert.DoesNotContain(baseline, claim => claim.Kind == "publishes");

        var refined = RelationCollector.RefineClaims(extraction.Document.DocumentId, "Worker.cs", extraction.RelationCandidates, model);

        var publish = Assert.Single(refined, claim => claim.Kind == "publishes");
        Assert.Contains(publish.Details, detail => detail is { Key: "target_text", Value: "PaymentProcessed" });
    }

    [Fact]
    public void RefineClaims_FirstBaseListEntryNamedLikeAnInterfaceButActuallyAClass_ReplacesTheHeuristicGuessAndKeepsTheStrongerShapeConfidence()
    {
        // "IRepository" satisfies the syntax-only naming heuristic (starts with I + uppercase), so the
        // baseline guesses "implements" at FactResolution.Heuristic even though it is really a class -
        // exactly the case semantic refinement exists to correct.
        const string source = """
            namespace App;

            public class IRepository { }

            public sealed class Worker : IRepository
            {
            }
            """;
        var (extraction, model) = Compile(source);

        var baseline = RelationCollector.CreateClaims(extraction.Document.DocumentId, "Worker.cs", extraction.RelationCandidates);
        var refined = RelationCollector.RefineClaims(extraction.Document.DocumentId, "Worker.cs", extraction.RelationCandidates, model);

        var baselineClaim = Assert.Single(baseline);
        Assert.Equal("implements", baselineClaim.Kind);
        Assert.Equal(FactResolution.Heuristic, baselineClaim.ShapeConfidence);

        var refinedClaim = Assert.Single(refined);
        Assert.Equal("inherits", refinedClaim.Kind);
        Assert.Equal(FactResolution.Syntactic, refinedClaim.ShapeConfidence);
    }

    [Fact]
    public void RefineClaims_UnresolvedBaseListEntry_LeavesTheBaselineClaimUntouchedRatherThanDroppingIt()
    {
        const string source = """
            namespace App;

            public sealed class Worker : UndeclaredBase
            {
            }
            """;
        var (extraction, model) = Compile(source);

        var baseline = RelationCollector.CreateClaims(extraction.Document.DocumentId, "Worker.cs", extraction.RelationCandidates);
        var refined = RelationCollector.RefineClaims(extraction.Document.DocumentId, "Worker.cs", extraction.RelationCandidates, model);

        var baselineClaim = Assert.Single(baseline);
        var refinedClaim = Assert.Single(refined);
        Assert.Equal(baselineClaim.Kind, refinedClaim.Kind);
        Assert.Equal(baselineClaim.Partition, refinedClaim.Partition);
        Assert.Equal(baselineClaim.ShapeConfidence, refinedClaim.ShapeConfidence);
        Assert.True(baselineClaim.Details.SequenceEqual(refinedClaim.Details));
    }

    private static (SyntaxFactExtraction Extraction, SemanticModel Model) Compile(string workerSource, string relativePath = "Worker.cs")
    {
        var stubsTree = CSharpSyntaxTree.ParseText(FrameworkStubs, path: "FrameworkStubs.cs");
        var tree = CSharpSyntaxTree.ParseText(workerSource, path: relativePath);
        var compilation = CSharpCompilation.Create(
            "RelationCollectorRefineTests",
            [stubsTree, tree],
            Csharp2Md.Core.Tests.TestCompilation.PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var extraction = SyntaxFactExtractor.Extract(ProjectId, relativePath, workerSource);
        return (extraction, model);
    }

    private const string FrameworkStubs = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Acme.Contracts
        {
            public sealed record OrderPlaced(Guid OrderId);

            public sealed record PaymentProcessed(Guid PaymentId);

            public interface IEventBus
            {
                Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default);
                void Publish<TEvent>(TEvent message);
                void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler);
            }
        }
        """;
}
