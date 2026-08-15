using Csharp2Md.Core;
using Csharp2Md.Core.Graph;
using Csharp2Md.Core.Rendering;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Rendering;

public sealed class DependencySectionRendererTests
{
    private static DependencySignal Signal(
        DependencyKind kind,
        CommunicationType communication,
        string rawTarget,
        ResolutionKind resolution,
        ServiceName? targetService = null,
        MessagingRole? role = null) =>
        new(SourceService: new ServiceName("Acme.Orders"),
            TargetService: targetService,
            RawTarget: rawTarget,
            Kind: kind,
            Communication: communication,
            Resolution: resolution,
            Role: role,
            Location: new SourceLocation("Acme.Orders/OrderService.cs", 12));

    private static DependencySignal Http() => Signal(
        DependencyKind.Http, CommunicationType.SincronoBloqueante, "PaymentService", ResolutionKind.HardCoded);

    private static DependencySignal Grpc() => Signal(
        DependencyKind.Grpc, CommunicationType.StreamingBidirecional, "Payments", ResolutionKind.Dynamic);

    private static DependencySignal Message() => Signal(
        DependencyKind.Messaging, CommunicationType.PubSubEvento, "OrderPlaced", ResolutionKind.Unresolved,
        role: MessagingRole.Publish);

    private static DependencySignal DirectReference() => Signal(
        DependencyKind.DirectReference, CommunicationType.DirectReference,
        @"..\Acme.Shared.Contracts\Acme.Shared.Contracts.csproj", ResolutionKind.NotApplicable,
        targetService: new ServiceName("Acme.Shared.Contracts"));

    private static string Row(string markdown, int index) =>
        markdown.Split('\n').Where(line => line.StartsWith("| ", StringComparison.Ordinal)).ElementAt(index + 2);

    // P2-10: an HTTP dependency lists its communication type, target, and resolution.
    [Fact]
    public void Render_HttpDependency_ListsCommunicationTypeTargetAndResolution()
    {
        var markdown = DependencySectionRenderer.Render([Http()])!;

        Assert.Equal("| sincrono-bloqueante | PaymentService | hard-coded |", Row(markdown, 0));
    }

    // P2-10 for a gRPC dependency.
    [Fact]
    public void Render_GrpcDependency_ListsCommunicationTypeTargetAndResolution()
    {
        var markdown = DependencySectionRenderer.Render([Grpc()])!;

        Assert.Equal("| streaming-bidirecional | Payments | dynamic |", Row(markdown, 0));
    }

    // P2-10 as amended: a messaging signal's target is the topic / message type name, because its
    // counterpart service is not correlated until the graph is assembled.
    [Fact]
    public void Render_MessagingDependency_UsesTheTopicNameAsTheTarget()
    {
        var markdown = DependencySectionRenderer.Render([Message()])!;

        Assert.Equal("| pub-sub-evento | OrderPlaced | unresolved |", Row(markdown, 0));
    }

    // P2-04: a direct reference was correlated at detection time, so the target is the real service.
    [Fact]
    public void Render_DirectReferenceDependency_UsesTheCorrelatedServiceAsTheTarget()
    {
        var markdown = DependencySectionRenderer.Render([DirectReference()])!;

        Assert.Equal("| direct-reference | Acme.Shared.Contracts | not-applicable |", Row(markdown, 0));
    }

    [Fact]
    public void Render_SeveralDependencies_ListsEveryOne()
    {
        var markdown = DependencySectionRenderer.Render([Http(), Grpc(), Message(), DirectReference()])!;

        Assert.Equal(
            new[] { "sincrono-bloqueante", "streaming-bidirecional", "pub-sub-evento", "direct-reference" },
            Enumerable.Range(0, 4).Select(i => Row(markdown, i).Split('|')[1].Trim()));
    }

    // P2-10 scopes the section to files that have a dependency: a clean file gets no empty heading.
    [Fact]
    public void Apply_DocumentWithNoDetectedDependencies_OmitsTheSectionEntirely()
    {
        var document = DependencySectionRenderer.Apply(
            RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace), []);

        Assert.Null(document.DependencySection);
        Assert.DoesNotContain(DependencySectionRenderer.Heading, document.ToMarkdown(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_DocumentWithDependencies_PlacesTheSectionAfterTheBacklinkAndBeforeTheSource()
    {
        var document = DependencySectionRenderer.Apply(
            RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace), [Http()]);

        var markdown = document.ToMarkdown();

        Assert.Contains(DependencySectionRenderer.Heading, markdown, StringComparison.Ordinal);
        Assert.InRange(
            markdown.IndexOf(DependencySectionRenderer.Heading, StringComparison.Ordinal),
            markdown.IndexOf("[Index](", StringComparison.Ordinal),
            markdown.IndexOf("## Preamble", StringComparison.Ordinal));
    }

    // AD-002's invariant must survive the new section: it maps to no source bytes, so it must not
    // enter the section list, and the source must still reconstruct byte-for-byte.
    [Theory]
    [MemberData(nameof(SpanCoverageTests.Sources), MemberType = typeof(SpanCoverageTests))]
    public void Apply_WithTheSectionPresent_PreservesTheSpanCoverageInvariant(string name, string source)
    {
        var rendered = RenderTestSources.Render(source);
        var document = DependencySectionRenderer.Apply(rendered, [Http(), Message()]);

        Assert.NotNull(document.DependencySection);
        Assert.Equal(rendered.Sections.Count, document.Sections.Count);
        Assert.Equal(source, string.Concat(document.Sections.Select(section => section.Text)));

        var markdown = document.ToMarkdown();
        foreach (var section in document.Sections)
        {
            Assert.True(
                markdown.Contains(section.Text, StringComparison.Ordinal),
                $"{name}: section '{section.Title}' was altered once the dependency section was added");
        }
    }

    // P2-10's emitted shape across all four detector families, reviewed by hand and pinned.
    [Fact]
    public Task Apply_DocumentShapeWithEveryDependencyKind_MatchesApprovedSnapshot()
    {
        var document = DependencySectionRenderer.Apply(
            RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace),
            [Http(), Grpc(), Message(), DirectReference()]);

        return Verifier.Verify(document.ToMarkdown(), "md").UseDirectory("snapshots");
    }
}
