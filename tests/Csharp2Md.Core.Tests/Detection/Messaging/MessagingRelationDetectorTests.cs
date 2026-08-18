using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Detection.Messaging;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Detection.Messaging;

public sealed class MessagingRelationDetectorTests
{
    private const string Publish = "messaging-publish";
    private const string Subscribe = "messaging-subscribe";

    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Program.cs");

    public static TheoryData<string, string, bool, string?> Cases => new()
    {
        // FACT-46: a confirmed publish, generic argument inferred from the constructed message.
        { "await _bus.PublishAsync(new OrderPlaced(System.Guid.NewGuid()));", Publish, true, "OrderPlaced" },

        // FACT-46: a confirmed subscribe with an explicit type argument.
        { "_bus.Subscribe<OrderPlaced>(Handle);", Subscribe, true, "OrderPlaced" },

        // Inference through a variable works the same as an explicit construction.
        { "var message = new PaymentProcessed(System.Guid.NewGuid()); await _bus.PublishAsync(message);", Publish, true, "PaymentProcessed" },

        // A document that both publishes and subscribes records both, independently.
        { "_bus.Subscribe<OrderPlaced>(Handle); await _bus.PublishAsync(new PaymentProcessed(System.Guid.NewGuid()));", Subscribe, true, "OrderPlaced" },
        { "_bus.Subscribe<OrderPlaced>(Handle); await _bus.PublishAsync(new PaymentProcessed(System.Guid.NewGuid()));", Publish, true, "PaymentProcessed" },

        // FACT-46: confirmation also works through a concrete type that implements the contract.
        { "var concrete = new Contoso.EventBus(); concrete.PublishAsync(new OrderPlaced(System.Guid.NewGuid()));", Publish, true, "OrderPlaced" },

        // Not a publish or subscribe call at all.
        { "System.Threading.Tasks.Task.Delay(0);", Publish, false, null },
        { "System.Threading.Tasks.Task.Delay(0);", Subscribe, false, null },

        // FACT-46: a name-only lookalike (non-generic, unrelated type) emits nothing.
        { "var logger = new Contoso.Logger(); logger.Publish();", Publish, false, null },

        // FACT-46: a generic method literally named Publish, but not declared on any interface.
        { "var util = new Contoso.Utility(); util.Publish(42);", Publish, false, null },

        // FACT-46: an interface method named Publish<T> that does not use T as the message parameter.
        { "var reporter = (Contoso.IReporter)new Contoso.Reporter(); reporter.Publish<OrderPlaced>(\"x\");", Publish, false, null },

        // FACT-46: an interface method named Subscribe<T> whose handler never references T.
        { "var timer = (Contoso.ITimer)new Contoso.Timer(); timer.Subscribe<OrderPlaced>(() => { });", Subscribe, false, null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Integration")]
    public void Detect_UsesConfirmedMessagingContractEvidence(
        string source,
        string relationKind,
        bool expected,
        string? topic)
    {
        var facts = Detect(Context(source));
        var matches = facts.Where(fact => fact.RelationKind == relationKind).ToArray();

        if (!expected)
        {
            Assert.Empty(matches);
            return;
        }

        var fact = Assert.Single(matches, item => item.Details.Any(detail =>
            detail.Key == "topic" && detail.Value == topic));
        Assert.Equal(FactResolution.Exact, fact.Header.Resolution);
        Assert.Equal(RelationPartition.Events, fact.Partition);
        Assert.NotEmpty(fact.Header.Evidence);
        Assert.Contains(fact.Header.Provenance, static provenance =>
            provenance.DetectorId?.Value == "id1:detector;name=io.csharp2md.messaging"
            && provenance.DetectorVersion == "1.0.0");

        // FACT-47: a single document proves only its own side of the exchange.
        Assert.Null(fact.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));
    }

    /// <summary>FACT-46: repeated identical publishes are never collapsed.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_RepeatedIdenticalPublishes_KeepDistinctIdentities()
    {
        var facts = Detect(Context(
            "await _bus.PublishAsync(new OrderPlaced(System.Guid.NewGuid())); await _bus.PublishAsync(new OrderPlaced(System.Guid.NewGuid()));"))
            .Where(fact => fact.RelationKind == Publish)
            .ToArray();

        Assert.Equal(2, facts.Length);
        Assert.Equal(2, facts.Select(static fact => fact.RelationId.Value).Distinct().Count());
    }

    /// <summary>FACT-35/FACT-46: without a semantic document nothing can be confirmed.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_WithoutSemanticDocument_EmitsNothing()
    {
        var context = Context("await _bus.PublishAsync(new OrderPlaced(System.Guid.NewGuid()));") with { SemanticDocument = null };
        Assert.Empty(new DetectorHost(documentDetectors: [new MessagingRelationDetector()]).DetectDocument(context).Facts);
    }

    private static RelationFact[] Detect(Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext context) =>
        new DetectorHost(documentDetectors: [new MessagingRelationDetector()])
            .DetectDocument(context)
            .Facts
            .OfType<RelationFact>()
            .ToArray();

    private static Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext Context(string body)
    {
        var stubsTree = CSharpSyntaxTree.ParseText(FrameworkStubs, path: "FrameworkStubs.cs");
        var tree = CSharpSyntaxTree.ParseText(
            BodyUsings + "\n" + Preamble + "\n" + body,
            path: "Program.cs");
        var compilation = CSharpCompilation.Create(
            "MessagingDetection",
            [stubsTree, tree],
            TestCompilation.PlatformReferences,
            new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.ConsoleApplication));
        var model = compilation.GetSemanticModel(tree);
        var project = new ProjectFact(
            Header(ProjectId.ToFactId(), FactKind.Project),
            ProjectId,
            "App",
            "src/App/App.csproj",
            [TargetId],
            [DocumentId]);
        var target = new TargetFact(
            Header(TargetId.ToFactId(), FactKind.Target),
            TargetId,
            ProjectId,
            "net10.0");
        var document = new DocumentFact(
            Header(DocumentId.ToFactId(), FactKind.Document),
            DocumentId,
            ProjectId,
            "Program.cs",
            [],
            []);
        var index = SolutionAnalysisIndex.Build(
            [project],
            [new TargetAnalysisIndexInput(target, [], [])]);
        return new Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext(
            project,
            target,
            document,
            [],
            index,
            new SemanticDetectionDocument(tree, model));
    }

    private static FactHeader Header(FactId id, FactKind kind) =>
        FactHeader.Create(id, kind, FactResolution.Exact);

    private const string BodyUsings = """
        using System.Threading.Tasks;
        using Acme.Contracts;
        """;

    private const string Preamble = """
        Acme.Contracts.IEventBus _bus = new Acme.Contracts.EventBus();
        static Task Handle(Acme.Contracts.OrderPlaced placed, System.Threading.CancellationToken token) => Task.CompletedTask;
        """;

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

                void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler);
            }

            public sealed class EventBus : IEventBus
            {
                public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) => Task.CompletedTask;

                public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) { }
            }
        }

        namespace Contoso
        {
            public sealed class Logger
            {
                public void Publish() { }
            }

            public sealed class Utility
            {
                public void Publish<T>(T item) { }
            }

            public interface IReporter
            {
                void Publish<T>(string message);
            }

            public sealed class Reporter : IReporter
            {
                public void Publish<T>(string message) { }
            }

            public interface ITimer
            {
                void Subscribe<T>(Action handler);
            }

            public sealed class Timer : ITimer
            {
                public void Subscribe<T>(Action handler) { }
            }

            public sealed class EventBus : Acme.Contracts.IEventBus
            {
                public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) => Task.CompletedTask;

                public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) { }
            }
        }
        """;
}
