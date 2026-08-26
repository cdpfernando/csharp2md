using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ConfigurationDocumentReaderTests
{
    private const string FixtureSecret =
        "Server=orders-db.internal.acme.local;Database=Orders;User Id=orders_app;Password=appsettings-fixture-secret;";

    [Fact]
    [Trait("Requirement", "CDC-26")]
    public void Emit_NestedObject_UsesColonJoinedKeyPath()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Services": { "PaymentService": "https://payments.example" } }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observation = Assert.Single(ConfigurationObservations(context));

        Assert.Equal("Services:PaymentService", PayloadValue(observation, "key"));
        Assert.Equal(context.ConfigurationDocuments[0].Reference, observation.Identity.Owner);
    }

    [Fact]
    [Trait("Requirement", "CDC-26")]
    public void Emit_Array_UsesIndexAsPathSegment()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Hosts": [ "alpha", "beta" ] }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observations = ConfigurationObservations(context);

        Assert.Equal(["Hosts:0", "Hosts:1"], observations.Select(observation => PayloadValue(observation, "key")));
    }

    [Fact]
    [Trait("Requirement", "CDC-27")]
    public void Emit_LiteralLeaf_DoesNotCarryTheLeafValue()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Name": "visible-value" }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observation = Assert.Single(ConfigurationObservations(context));

        Assert.DoesNotContain(
            observation.Identity.Payload.Entries,
            entry => entry.Value.Value.Contains("visible-value", StringComparison.Ordinal));
        Assert.Equal("Name", PayloadValue(observation, "key"));
    }

    [Fact]
    [Trait("Requirement", "CDC-29")]
    public void Emit_EnvVarShapes_ResolveAsDynamic()
    {
        using var tree = new TempRoot();
        var context = Arrange(
            tree,
            """{ "A": "${NOTIFICATION_SERVICE_URL}", "B": "$NOTIFICATION_SERVICE_URL", "C": "%NOTIFICATION_SERVICE_URL%" }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observations = ConfigurationObservations(context);

        Assert.All(observations, observation => Assert.Equal("dynamic", PayloadValue(observation, "resolution")));
        Assert.All(
            observations,
            observation => Assert.DoesNotContain(observation.Identity.Payload.Entries, entry => entry.Key == "address"));
    }

    [Fact]
    [Trait("Requirement", "CDC-29")]
    public void Emit_NonEmptyLiteral_ResolvesAsLiteral()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Name": "orders" }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);

        Assert.Equal("literal", PayloadValue(Assert.Single(ConfigurationObservations(context)), "resolution"));
    }

    [Fact]
    [Trait("Requirement", "CDC-29")]
    public void Emit_EmptyAndNull_ResolveAsUnknown()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Empty": "", "Missing": null }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observations = ConfigurationObservations(context);

        Assert.Equal(2, observations.Length);
        Assert.All(observations, observation => Assert.Equal("unknown", PayloadValue(observation, "resolution")));
    }

    [Fact]
    [Trait("Requirement", "CDC-30")]
    public void Emit_AbsoluteUri_CarriesAddress()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Services": { "PaymentService": "https://payments.internal.acme.local:8443" } }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observation = Assert.Single(ConfigurationObservations(context));

        Assert.Equal("https://payments.internal.acme.local:8443", PayloadValue(observation, "address"));
        Assert.Equal("literal", PayloadValue(observation, "resolution"));
    }

    [Fact]
    [Trait("Requirement", "CDC-30")]
    public void Emit_NonUriLiteral_OmitsAddress()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Name": "orders" }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);

        Assert.DoesNotContain(
            Assert.Single(ConfigurationObservations(context)).Identity.Payload.Entries,
            entry => entry.Key == "address");
    }

    [Fact]
    [Trait("Requirement", "CDC-28")]
    [Trait("Requirement", "CDC-30")]
    public void Emit_SecretConnectionString_RecordsEvidenceAndOmitsValueAndAddress()
    {
        using var tree = new TempRoot();
        var json = $$"""{ "ConnectionStrings": { "OrdersDb": "{{FixtureSecret}}" } }""";
        var context = Arrange(tree, json);

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var snapshot = context.Accumulator.ToSnapshot();
        var observation = Assert.Single(ConfigurationObservations(context));

        Assert.Equal("ConnectionStrings:OrdersDb", PayloadValue(observation, "key"));
        Assert.Equal("literal", PayloadValue(observation, "resolution"));
        Assert.DoesNotContain(observation.Identity.Payload.Entries, entry => entry.Key == "address");
        Assert.DoesNotContain(observation.Identity.Payload.Entries, entry => entry.Key == "value");
        Assert.All(
            observation.Identity.Payload.Entries,
            entry => Assert.DoesNotContain(FixtureSecret, entry.Value.Value, StringComparison.Ordinal));
        Assert.All(
            snapshot.Diagnostics,
            record => Assert.DoesNotContain(FixtureSecret, record.Message, StringComparison.Ordinal));
        var secret = Assert.Single(snapshot.SuspectedSecrets);
        Assert.DoesNotContain(FixtureSecret, secret.Excerpt.Value, StringComparison.Ordinal);
        Assert.Contains("***", secret.Excerpt.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CDC-31")]
    public void Emit_MalformedJson_RecordsDiagnosticAndNoObservations()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, "{ not json");

        var count = ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var snapshot = context.Accumulator.ToSnapshot();

        Assert.Equal(0, count);
        Assert.Empty(ConfigurationObservations(context));
        var diagnostic = Assert.Single(
            snapshot.Diagnostics,
            record => string.Equals(record.Code, "malformed-configuration-document", StringComparison.Ordinal));
        Assert.Equal("App/appsettings.json", diagnostic.IdentityOrKey);
        Assert.False(Path.IsPathRooted(diagnostic.IdentityOrKey));
    }

    [Fact]
    [Trait("Requirement", "CDC-32")]
    public void Emit_Leaf_CarriesConfiguredEvidenceAndDocumentHash()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Name": "orders" }""");
        var expectedHash = ObservationMaterializer.HashFileBytes(
            Path.Combine(tree.Root, "App", "appsettings.json"));

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observation = Assert.Single(ConfigurationObservations(context));

        Assert.Equal(EvidenceMethod.Configured, observation.ExtractionMethod);
        Assert.Equal(expectedHash, observation.DocumentHash);
        Assert.Equal("App/appsettings.json", observation.Locator.RelativePath);
    }

    [Fact]
    [Trait("Requirement", "CDC-33")]
    public void Emit_UnsortedKeys_AssignsOrdinalsOverSortedKeyPaths()
    {
        using var tree = new TempRoot();
        var context = Arrange(tree, """{ "Zebra": "z", "Aardvark": "a" }""");

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observations = ConfigurationObservations(context);

        Assert.Equal(1, observations[0].Identity.OccurrenceOrdinal);
        Assert.Equal(2, observations[1].Identity.OccurrenceOrdinal);
        Assert.Equal("Aardvark", PayloadValue(observations[0], "key"));
        Assert.Equal("Zebra", PayloadValue(observations[1], "key"));
        Assert.Equal(
            observations.Select(observation => observation.Identity.OccurrenceOrdinal).Distinct().Count(),
            observations.Length);
    }

    [Fact]
    [Trait("Requirement", "CDC-26")]
    [Trait("Requirement", "CDC-29")]
    [Trait("Requirement", "CDC-30")]
    public void Emit_FixtureServicesAndConnectionStringsShapes_MatchDeclaredResolutions()
    {
        using var tree = new TempRoot();
        var json = $$"""
            {
              "ConnectionStrings": {
                "OrdersDb": "{{FixtureSecret}}"
              },
              "Services": {
                "PaymentService": "https://payments.internal.acme.local:8443",
                "NotificationService": "${NOTIFICATION_SERVICE_URL}"
              }
            }
            """;
        var context = Arrange(tree, json);

        ConfigurationDocumentReader.Emit(context, CancellationToken.None);
        var observations = ConfigurationObservations(context);
        var byKey = observations.ToDictionary(observation => PayloadValue(observation, "key"), StringComparer.Ordinal);

        Assert.Equal("literal", PayloadValue(byKey["Services:PaymentService"], "resolution"));
        Assert.Equal(
            "https://payments.internal.acme.local:8443",
            PayloadValue(byKey["Services:PaymentService"], "address"));
        Assert.Equal("dynamic", PayloadValue(byKey["Services:NotificationService"], "resolution"));
        Assert.DoesNotContain(
            byKey["Services:NotificationService"].Identity.Payload.Entries,
            entry => entry.Key == "address");
        Assert.Equal("literal", PayloadValue(byKey["ConnectionStrings:OrdersDb"], "resolution"));
        Assert.DoesNotContain(
            byKey["ConnectionStrings:OrdersDb"].Identity.Payload.Entries,
            entry => entry.Key == "address");
        Assert.All(
            observations,
            observation => Assert.DoesNotContain(FixtureSecret, observation.Identity.Payload.Entries.Select(entry => entry.Value.Value)));
    }

    [Fact]
    [Trait("Requirement", "CDC-34")]
    public void Emit_PathThatEscapesAuthorizedRoot_IsRejectedByTheGuard()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-cdc34-escape-");
        try
        {
            var root = Path.Combine(tree.FullName, "root");
            var outside = Path.Combine(tree.FullName, "outside");
            Directory.CreateDirectory(Path.Combine(root, "App"));
            Directory.CreateDirectory(outside);
            var target = Path.Combine(outside, "appsettings.json");
            File.WriteAllText(target, """{ "Name": "secret" }""");
            var symlink = Path.Combine(root, "App", "appsettings.json");
            File.CreateSymbolicLink(symlink, target);
            var context = ArrangeExisting(root, "App/appsettings.json");

            var exception = Assert.Throws<InvalidOperationException>(
                () => ConfigurationDocumentReader.Emit(context, CancellationToken.None));

            Assert.Contains("escapes the authorized root", exception.Message, StringComparison.Ordinal);
            Assert.Empty(ConfigurationObservations(context));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static PipelineContext Arrange(TempRoot tree, string json)
    {
        var app = Path.Combine(tree.Root, "App");
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(app, "appsettings.json"), json);
        return ArrangeExisting(tree.Root, "App/appsettings.json");
    }

    private static PipelineContext ArrangeExisting(string root, string relativePath)
    {
        var solution = Solution.Create(SolutionId.Create(WorkspaceIdentity.Create("t"), "App.slnx"));
        var project = Project.Create(ProjectId.Create(solution.Id, "App/App.csproj"));
        var document = Document.Create(project.Id, relativePath);
        var context = new PipelineContext(new SwallowingSession(), Path.Combine(root, "App.slnx"))
        {
            AuthorizedRoot = root,
            ConfigurationDocuments = [document],
        };
        context.Accumulator.AddFact(solution);
        context.Accumulator.AddFact(project);
        context.Accumulator.AddFact(document);
        return context;
    }

    private static Observation[] ConfigurationObservations(PipelineContext context) =>
        context.Accumulator.ToSnapshot().Observations
            .Where(observation =>
                observation.Identity.Kind is ObservationKind.Configuration
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "key"))
            .OrderBy(observation => observation.Identity.OccurrenceOrdinal)
            .ToArray();

    private static string PayloadValue(Observation observation, string key) =>
        Assert.Single(observation.Identity.Payload.Entries, entry => entry.Key == key).Value.Value;

    private sealed class TempRoot : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("csharp2md-cdc-config-");

        public string Root => _directory.FullName;

        public void Dispose() => _directory.Delete(recursive: true);
    }
}
