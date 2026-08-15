using Csharp2Md.Core;
using Csharp2Md.Core.Graph;
using Csharp2Md.Core.Output;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Output;

public sealed class MermaidWriterTests
{
    private static DependencyEdge Edge(
        string source,
        string target,
        CommunicationType communication,
        ResolutionKind resolution = ResolutionKind.Unresolved) =>
        new(new ServiceName(source),
            new ServiceName(target),
            communication,
            resolution,
            [new SourceLocation($"{source}/Client.cs", 12)]);

    /// <summary>A graph exercising all five of P2-12's communication types.</summary>
    private static DependencyGraph AllFiveTypes() =>
        new(
        [
            Edge("Acme.Orders", "PaymentService", CommunicationType.SincronoBloqueante, ResolutionKind.HardCoded),
            Edge("Acme.Orders", "NotifyService", CommunicationType.AssincronoFireAndForget, ResolutionKind.Dynamic),
            Edge("Acme.Orders", "Acme.Payments", CommunicationType.PubSubEvento, ResolutionKind.NotApplicable),
            Edge("Acme.Payments", "Ledger", CommunicationType.StreamingBidirecional),
            Edge("Acme.Payments", "Acme.Shared.Contracts", CommunicationType.DirectReference, ResolutionKind.NotApplicable),
        ]);

    // P2-13: every edge is labeled with its communication type, in spec.md's spelling.
    [Theory]
    [InlineData(CommunicationType.SincronoBloqueante, "sincrono-bloqueante")]
    [InlineData(CommunicationType.AssincronoFireAndForget, "assincrono-fire-and-forget")]
    [InlineData(CommunicationType.PubSubEvento, "pub-sub-evento")]
    [InlineData(CommunicationType.StreamingBidirecional, "streaming-bidirecional")]
    [InlineData(CommunicationType.DirectReference, "direct-reference")]
    public void Render_EdgeLabel_IsTheCommunicationTypeInSpecSpelling(
        CommunicationType communication,
        string expected)
    {
        var mermaid = MermaidWriter.Render(new DependencyGraph([Edge("Acme.Orders", "Acme.Payments", communication)]));

        Assert.Contains($"svc0 -->|{expected}| svc1", mermaid, StringComparison.Ordinal);
    }

    // P2-13: *every* edge is labeled — no edge may be drawn bare.
    [Fact]
    public void Render_AllFiveCommunicationTypes_LabelsEveryEdgeWithNoneLeftBare()
    {
        var lines = MermaidWriter.Render(AllFiveTypes())
            .Split('\n')
            .Where(line => line.Contains("-->", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(5, lines.Count);
        Assert.All(lines, line => Assert.Contains("-->|", line, StringComparison.Ordinal));
        Assert.Equal(
            new[]
            {
                "sincrono-bloqueante",
                "assincrono-fire-and-forget",
                "pub-sub-evento",
                "streaming-bidirecional",
                "direct-reference",
            },
            lines.Select(line => line.Split('|')[1]));
    }

    // "Derived from the graph object": the arrow runs source -> target, not the reverse.
    [Fact]
    public void Render_Edge_PointsFromSourceToTarget()
    {
        var mermaid = MermaidWriter.Render(
            new DependencyGraph([Edge("Acme.Orders", "Acme.Payments", CommunicationType.PubSubEvento)]));

        Assert.Contains("svc0[\"Acme.Orders\"]", mermaid, StringComparison.Ordinal);
        Assert.Contains("svc1[\"Acme.Payments\"]", mermaid, StringComparison.Ordinal);
        Assert.Contains("svc0 -->|pub-sub-evento| svc1", mermaid, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ServiceInvolvedInSeveralEdges_IsDeclaredOnceAndReused()
    {
        var mermaid = MermaidWriter.Render(AllFiveTypes());
        var declarations = mermaid.Split('\n').Where(line => line.Contains("[\"", StringComparison.Ordinal)).ToList();

        Assert.Equal(6, declarations.Count);
        Assert.Single(declarations, line => line.Contains("[\"Acme.Orders\"]", StringComparison.Ordinal));
        Assert.Equal(3, mermaid.Split('\n').Count(line => line.StartsWith("    svc0 -->", StringComparison.Ordinal)));
    }

    // A raw target may be an address or a topic name (AD-005), so identifiers are generated and the
    // punctuation stays inside the quoted label where Mermaid tolerates it.
    [Fact]
    public void Render_TargetContainingUrlPunctuation_KeepsItInTheLabelAndOutOfTheIdentifier()
    {
        var mermaid = MermaidWriter.Render(
            new DependencyGraph(
                [Edge("Acme.Orders", "http://payments:8080/api", CommunicationType.SincronoBloqueante)]));

        Assert.Contains("svc1[\"http://payments:8080/api\"]", mermaid, StringComparison.Ordinal);
        Assert.Contains("svc0 -->|sincrono-bloqueante| svc1", mermaid, StringComparison.Ordinal);
    }

    // A quote would close the label early and a '#' would open an entity: both break the diagram.
    [Fact]
    public void Render_ServiceNameWithMermaidSignificantCharacters_IsEscaped()
    {
        var mermaid = MermaidWriter.Render(
            new DependencyGraph(
                [Edge("Acme.Orders", "say \"hi\" #now", CommunicationType.SincronoBloqueante)]));

        Assert.Contains("svc1[\"say #quot;hi#quot; #35;now\"]", mermaid, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hi\"", mermaid, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_AlwaysOpensWithAFlowchartHeader()
    {
        Assert.StartsWith("graph LR\n", MermaidWriter.Render(AllFiveTypes()), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_CreatesTheDiagramFileBeneathTheOutputRoot()
    {
        var root = Directory.CreateTempSubdirectory("csharp2md-mermaid-").FullName;
        try
        {
            var path = MermaidWriter.Write(AllFiveTypes(), root);

            Assert.Equal(Path.Combine(root, "dependencies.mmd"), path);
            Assert.Equal(MermaidWriter.Render(AllFiveTypes()), File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // P2-13's emitted diagram, reviewed by hand and pinned.
    [Fact]
    public Task Render_DiagramShape_MatchesApprovedSnapshot() =>
        Verifier.Verify(MermaidWriter.Render(AllFiveTypes()), "mmd").UseDirectory("snapshots");
}
