using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Detection.DependencyInjection;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Detection.DependencyInjection;

public sealed class DependencyInjectionDetectorTests
{
    private const string Expansion = "di-registration-expansion";
    private const string Registration = "di-registration";

    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Program.cs");

    private const string LocalExtension =
        "public static class ClockRegistrations { public static IServiceCollection AddClockServices(this IServiceCollection services) => services; }";

    private const string NameOnlyExtension =
        "public static class Sneaky { public static IServiceCollection AddSingleton(this IServiceCollection services, int count) => services; }";

    public static TheoryData<string, string, bool, string?, string?, FactResolution?> Cases => new()
    {
        // FACT-44: lifetimes.
        { "services.AddSingleton<IClock, SystemClock>();", Registration, true, "lifetime", "singleton", FactResolution.Exact },
        { "services.AddScoped<IClock, SystemClock>();", Registration, true, "lifetime", "scoped", FactResolution.Exact },
        { "services.AddTransient<IClock, SystemClock>();", Registration, true, "lifetime", "transient", FactResolution.Exact },

        // FACT-44: service and implementation identity.
        { "services.AddSingleton<IClock, SystemClock>();", Registration, true, "service", "IClock", FactResolution.Exact },
        { "services.AddSingleton<IClock, SystemClock>();", Registration, true, "implementation", "SystemClock", FactResolution.Exact },
        { "services.AddSingleton<SystemClock>();", Registration, true, "implementation", "SystemClock", FactResolution.Exact },
        { "services.AddSingleton(typeof(IClock), typeof(SystemClock));", Registration, true, "service", "IClock", FactResolution.Exact },

        // FACT-44 + FACT-43 edge case: a factory hides the implementation type, so the fact stays partial.
        { "services.AddSingleton<IClock>(provider => new SystemClock());", Registration, true, "factory", "provider => new SystemClock()", FactResolution.Partial },

        // FACT-44: open generics.
        { "services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));", Registration, true, "open_generic", "true", FactResolution.Exact },
        { "services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));", Registration, true, "service", "IRepository<>", FactResolution.Exact },

        // FACT-44: keyed services, exact for a constant key and partial for an irreducible one.
        { "services.AddKeyedSingleton<IClock, SystemClock>(\"primary\");", Registration, true, "key", "primary", FactResolution.Exact },
        { "services.AddKeyedScoped<IClock, SystemClock>(Key()); static object Key() => \"primary\";", Registration, true, "key_expression", "Key()", FactResolution.Partial },

        // FACT-44: navigable local registration expansion.
        { $"services.AddClockServices(); {LocalExtension}", Expansion, true, "method", "ClockRegistrations.AddClockServices", FactResolution.Exact },

        // FACT-46: name-only lookalikes on a foreign service-collection type emit nothing.
        { "var bag = new Contoso.ServiceBag(); bag.AddSingleton<IClock, SystemClock>();", Registration, false, null, null, null },
        { "var bag = new Contoso.ServiceBag(); bag.AddSingleton<IClock, SystemClock>();", Expansion, false, null, null, null },

        // FACT-46: an unrelated extension call emits nothing.
        { "var bag = new Contoso.Bag(); bag.AddScoped<IClock, SystemClock>();", Registration, false, null, null, null },
        { "var bag = new Contoso.Bag(); bag.AddScoped<IClock, SystemClock>();", Expansion, false, null, null, null },

        // FACT-46: passing the collection to a plain method is not a registration.
        { "Configure(services); static void Configure(IServiceCollection collection) { }", Registration, false, null, null, null },
        { "Configure(services); static void Configure(IServiceCollection collection) { }", Expansion, false, null, null, null },

        // FACT-46: a user method merely named AddSingleton is an expansion, never a framework registration.
        { $"services.AddSingleton(1); {NameOnlyExtension}", Registration, false, null, null, null },
        { $"services.AddSingleton(1); {NameOnlyExtension}", Expansion, true, "method", "Sneaky.AddSingleton", FactResolution.Exact },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Integration")]
    public void Detect_UsesConfirmedServiceCollectionEvidence(
        string source,
        string relationKind,
        bool expected,
        string? detailKey,
        string? detailValue,
        FactResolution? resolution)
    {
        var facts = Detect(Context(source));
        var matches = facts.Where(fact => fact.RelationKind == relationKind).ToArray();

        if (!expected)
        {
            Assert.Empty(matches);
            return;
        }

        var fact = Assert.Single(matches, item => item.Details.Any(detail =>
            detail.Key == detailKey && detail.Value == detailValue));
        Assert.Equal(resolution, fact.Header.Resolution);
        Assert.Equal(RelationPartition.DependencyInjection, fact.Partition);
        Assert.NotEmpty(fact.Header.Evidence);
        Assert.Contains(fact.Header.Provenance, static provenance =>
            provenance.DetectorId?.Value == "id1:detector;name=io.csharp2md.dependency-injection"
            && provenance.DetectorVersion == "1.0.0");

        // FACT-47: a registration names an in-process type, never a proven remote target.
        Assert.Null(fact.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));
    }

    /// <summary>FACT-44 edge case: several registrations for one service type are all preserved.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_MultipleImplementationsOfOneService_AreNotCollapsed()
    {
        var facts = Detect(Context(
            "services.AddScoped<IClock, SystemClock>(); services.AddScoped<IClock, FakeClock>();"));

        var implementations = facts
            .Where(fact => fact.RelationKind == Registration)
            .SelectMany(static fact => fact.Details)
            .Where(static detail => detail.Key == "implementation")
            .Select(static detail => detail.Value)
            .ToArray();

        Assert.Equal(["FakeClock", "SystemClock"], implementations.Order());
    }

    /// <summary>FACT-44: distinct registrations are never collapsed, even when textually identical.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_RepeatedIdenticalRegistrations_KeepDistinctIdentities()
    {
        var facts = Detect(Context(
            "services.AddSingleton<IClock, SystemClock>(); services.AddSingleton<IClock, SystemClock>();"))
            .Where(fact => fact.RelationKind == Registration)
            .ToArray();

        Assert.Equal(2, facts.Length);
        Assert.Equal(2, facts.Select(static fact => fact.RelationId.Value).Distinct().Count());
    }

    /// <summary>FACT-44: a local expansion links to the evidence of its own definition.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_LocalExpansion_CarriesDefinitionEvidence()
    {
        var source = $"services.AddClockServices();\n{LocalExtension}";
        var fact = Assert.Single(Detect(Context(source)), item => item.RelationKind == Expansion);

        var definitionLine = Array.FindIndex(
            (BodyUsings + "\nvar services = new ServiceCollection();\n" + source).Split('\n'),
            line => line.Contains("public static class ClockRegistrations", StringComparison.Ordinal)) + 1;
        Assert.Equal(2, fact.Header.Evidence.Length);
        Assert.Contains(fact.Header.Evidence, evidence => evidence.StartLine == definitionLine);
    }

    /// <summary>FACT-35/FACT-46: without a semantic document nothing can be confirmed.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_WithoutSemanticDocument_EmitsNothing()
    {
        var context = Context("services.AddSingleton<IClock, SystemClock>();") with { SemanticDocument = null };
        Assert.Empty(new DetectorHost(documentDetectors: [new DependencyInjectionDetector()]).DetectDocument(context).Facts);
    }

    private static RelationFact[] Detect(Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext context) =>
        new DetectorHost(documentDetectors: [new DependencyInjectionDetector()])
            .DetectDocument(context)
            .Facts
            .OfType<RelationFact>()
            .ToArray();

    private static Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext Context(string body)
    {
        var stubsTree = CSharpSyntaxTree.ParseText(FrameworkStubs, path: "FrameworkStubs.cs");
        var tree = CSharpSyntaxTree.ParseText(
            BodyUsings + "\nvar services = new ServiceCollection();\n" + body,
            path: "Program.cs");
        var compilation = CSharpCompilation.Create(
            "DependencyInjectionDetection",
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
        using System;
        using Microsoft.Extensions.DependencyInjection;
        """;

    private const string FrameworkStubs = """
        using System;

        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }

            public sealed class ServiceCollection : IServiceCollection { }

            public static class ServiceCollectionServiceExtensions
            {
                public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services) => services;
                public static IServiceCollection AddSingleton<TService>(this IServiceCollection services) => services;
                public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory) => services;
                public static IServiceCollection AddSingleton(this IServiceCollection services, Type serviceType, Type implementationType) => services;
                public static IServiceCollection AddScoped<TService, TImplementation>(this IServiceCollection services) => services;
                public static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services) => services;
                public static IServiceCollection AddKeyedSingleton<TService, TImplementation>(this IServiceCollection services, object? serviceKey) => services;
                public static IServiceCollection AddKeyedScoped<TService, TImplementation>(this IServiceCollection services, object? serviceKey) => services;
            }
        }

        public interface IClock { }
        public sealed class SystemClock : IClock { }
        public sealed class FakeClock : IClock { }
        public interface IRepository<T> { }
        public sealed class Repository<T> : IRepository<T> { }

        namespace Contoso
        {
            public interface IServiceCollection { }
            public sealed class ServiceBag : IServiceCollection { }
            public sealed class Bag { }

            public static class LookalikeExtensions
            {
                public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services) => services;
                public static Bag AddScoped<TService, TImplementation>(this Bag bag) => bag;
            }
        }
        """;
}
