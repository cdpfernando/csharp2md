using System.Text.Json;
using Csharp2Md.Core;
using Csharp2Md.Core.Graph;
using Csharp2Md.Core.Output;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Output;

public sealed class DependencyJsonWriterTests
{
    private static DependencyEdge Edge(
        string source,
        string target,
        CommunicationType communication,
        ResolutionKind resolution) =>
        new(new ServiceName(source),
            new ServiceName(target),
            communication,
            resolution,
            [new SourceLocation($"{source}/Client.cs", 12)]);

    /// <summary>
    /// Every field of an edge as comparable values. Needed because <see cref="DependencyEdge"/>'s
    /// <c>Evidence</c> is an <c>IReadOnlyList</c>, which record equality compares by reference —
    /// record equality would fail even on a perfect round-trip.
    /// </summary>
    private static (ServiceName, ServiceName, CommunicationType, ResolutionKind, string) Fields(DependencyEdge edge) =>
        (edge.Source, edge.Target, edge.Communication, edge.Resolution, string.Join("|", edge.Evidence));

    /// <summary>A graph exercising all five of P2-12's communication types.</summary>
    private static DependencyGraph AllFiveTypes() =>
        new(
        [
            Edge("Acme.Orders", "PaymentService", CommunicationType.SincronoBloqueante, ResolutionKind.HardCoded),
            Edge("Acme.Orders", "NotifyService", CommunicationType.AssincronoFireAndForget, ResolutionKind.Dynamic),
            Edge("Acme.Orders", "Acme.Payments", CommunicationType.PubSubEvento, ResolutionKind.NotApplicable),
            Edge("Acme.Payments", "Ledger", CommunicationType.StreamingBidirecional, ResolutionKind.Unresolved),
            Edge("Acme.Payments", "Acme.Shared.Contracts", CommunicationType.DirectReference, ResolutionKind.NotApplicable),
        ]);

    // P2-11: every edge carries source service, target service, communication type, and resolution.
    [Fact]
    public void Serialize_Edge_EmitsSourceTargetCommunicationTypeAndResolution()
    {
        var json = DependencyJsonWriter.Serialize(
            new DependencyGraph(
            [
                Edge("Acme.Orders", "PaymentService", CommunicationType.SincronoBloqueante, ResolutionKind.HardCoded),
            ]));

        var edge = JsonDocument.Parse(json).RootElement.GetProperty("edges")[0];

        Assert.Equal("Acme.Orders", edge.GetProperty("source").GetString());
        Assert.Equal("PaymentService", edge.GetProperty("target").GetString());
        Assert.Equal("sincrono-bloqueante", edge.GetProperty("communicationType").GetString());
        Assert.Equal("hard-coded", edge.GetProperty("resolution").GetString());
    }

    // P2-11: *every* detected edge is written, not a filtered subset.
    [Fact]
    public void Serialize_EmitsEveryEdgeInTheGraph()
    {
        var json = DependencyJsonWriter.Serialize(AllFiveTypes());

        Assert.Equal(5, JsonDocument.Parse(json).RootElement.GetProperty("edges").GetArrayLength());
    }

    // P2-12: exactly these five spellings, direct-reference included. Written as spec strings, not
    // as C# identifiers and not as ordinals.
    [Theory]
    [InlineData(CommunicationType.SincronoBloqueante, "sincrono-bloqueante")]
    [InlineData(CommunicationType.AssincronoFireAndForget, "assincrono-fire-and-forget")]
    [InlineData(CommunicationType.PubSubEvento, "pub-sub-evento")]
    [InlineData(CommunicationType.StreamingBidirecional, "streaming-bidirecional")]
    [InlineData(CommunicationType.DirectReference, "direct-reference")]
    public void Serialize_CommunicationType_UsesTheSpecSpelling(CommunicationType communication, string expected)
    {
        var json = DependencyJsonWriter.Serialize(
            new DependencyGraph([Edge("Acme.Orders", "Target", communication, ResolutionKind.Unresolved)]));

        var edge = JsonDocument.Parse(json).RootElement.GetProperty("edges")[0];

        Assert.Equal(expected, edge.GetProperty("communicationType").GetString());
    }

    // P2-07, P2-08, P2-09: the three resolution classifications the spec names by string.
    [Theory]
    [InlineData(ResolutionKind.HardCoded, "hard-coded")]
    [InlineData(ResolutionKind.Dynamic, "dynamic")]
    [InlineData(ResolutionKind.Unresolved, "unresolved")]
    [InlineData(ResolutionKind.NotApplicable, "not-applicable")]
    public void Serialize_Resolution_UsesTheSpecSpelling(ResolutionKind resolution, string expected)
    {
        var json = DependencyJsonWriter.Serialize(
            new DependencyGraph(
                [Edge("Acme.Orders", "Target", CommunicationType.SincronoBloqueante, resolution)]));

        var edge = JsonDocument.Parse(json).RootElement.GetProperty("edges")[0];

        Assert.Equal(expected, edge.GetProperty("resolution").GetString());
    }

    // P2-12: all five values round-trip — the written spelling reads back as the same value.
    [Fact]
    public void Serialize_AllFiveCommunicationTypes_RoundTripBackToTheSameGraph()
    {
        var graph = AllFiveTypes();

        var restored = DependencyJsonWriter.Deserialize(DependencyJsonWriter.Serialize(graph));

        // Compared field-wise rather than with record equality: DependencyEdge.Evidence is an
        // IReadOnlyList, which records compare by reference, so record equality would fail here
        // even on a perfect round-trip.
        Assert.Equal(graph.Edges.Select(Fields), restored.Edges.Select(Fields));
    }

    [Fact]
    public void Serialize_EvidenceLocations_RoundTripIncludingWindowsDriveLetters()
    {
        var graph = new DependencyGraph(
        [
            new DependencyEdge(
                new ServiceName("Acme.Orders"),
                new ServiceName("PaymentService"),
                CommunicationType.SincronoBloqueante,
                ResolutionKind.HardCoded,
                [new SourceLocation(@"d:\repo\Acme.Orders\Client.cs", 42)]),
        ]);

        var restored = DependencyJsonWriter.Deserialize(DependencyJsonWriter.Serialize(graph));

        Assert.Equal(
            [new SourceLocation(@"d:\repo\Acme.Orders\Client.cs", 42)],
            Assert.Single(restored.Edges).Evidence);
    }

    [Fact]
    public void Write_CreatesDependenciesJsonBeneathTheOutputRoot()
    {
        var root = Directory.CreateTempSubdirectory("csharp2md-depjson-").FullName;
        try
        {
            var path = DependencyJsonWriter.Write(AllFiveTypes(), root);

            Assert.Equal(Path.Combine(root, "dependencies.json"), path);
            Assert.Equal(DependencyJsonWriter.Serialize(AllFiveTypes()), File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // P2-11's emitted artifact shape, reviewed by hand and pinned.
    [Fact]
    public Task Serialize_EmittedJsonShape_MatchesApprovedSnapshot() =>
        Verifier.Verify(DependencyJsonWriter.Serialize(AllFiveTypes()), "json").UseDirectory("snapshots");
}
