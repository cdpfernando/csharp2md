using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ProjectMetadataEmitterTests
{
    [Fact]
    [Trait("Requirement", "CDC-01")]
    [Trait("Requirement", "CDC-03")]
    public async Task Emit_CompiledExe_OwnsApplicationOutputKindLocatedInCsproj()
    {
        var context = await BindAsync(AcmeOrdersSolutionPath());
        try
        {
            ProjectMetadataEmitter.Emit(context, CancellationToken.None);
            var snapshot = context.Accumulator.ToSnapshot();
            var orders = ProjectNamed(snapshot, "Acme.Orders/Acme.Orders.csproj");
            var observation = Assert.Single(OutputKindObservations(snapshot, orders));

            Assert.Equal("application", PayloadValue(observation, "output-kind"));
            Assert.Equal(1, observation.Identity.OccurrenceOrdinal);
            Assert.Equal(EvidenceMethod.Configured, observation.ExtractionMethod);
            Assert.Equal(ObservationKind.Configuration, observation.Identity.Kind);
            Assert.Equal(orders.Reference, observation.Identity.Owner);
            Assert.Equal("Acme.Orders/Acme.Orders.csproj", observation.Locator.RelativePath);
            Assert.Equal(1, observation.Locator.Span.StartLine);
            Assert.Equal(1, observation.Locator.Span.StartColumn);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-01")]
    public async Task Emit_CompiledLibrary_OwnsLibraryOutputKind()
    {
        var context = await BindAsync(AcmeOrdersSolutionPath());
        try
        {
            ProjectMetadataEmitter.Emit(context, CancellationToken.None);
            var snapshot = context.Accumulator.ToSnapshot();
            var contracts = ProjectNamed(snapshot, "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
            var observation = Assert.Single(OutputKindObservations(snapshot, contracts));

            Assert.Equal("library", PayloadValue(observation, "output-kind"));
            Assert.Empty(ProjectReferenceObservations(snapshot, contracts));
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-01")]
    public async Task Emit_WinExe_OwnsApplicationOutputKind()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-cdc01-winexe-");
        try
        {
            var solutionPath = WriteSolution(
                tree.FullName,
                ("App", "WinExe", hostSource: "class Host { static void Main() {} }", references: []));
            var context = await BindAsync(solutionPath);
            try
            {
                ProjectMetadataEmitter.Emit(context, CancellationToken.None);
                var snapshot = context.Accumulator.ToSnapshot();
                var app = ProjectNamed(snapshot, "App/App.csproj");
                var observation = Assert.Single(OutputKindObservations(snapshot, app));

                Assert.Equal("application", PayloadValue(observation, "output-kind"));
            }
            finally
            {
                context.BoundSolution?.Dispose();
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-02")]
    [Trait("Requirement", "CDC-03")]
    public async Task Emit_InSolutionProjectReference_OwnsReferencedLogicalPath()
    {
        var context = await BindAsync(AcmeOrdersSolutionPath());
        try
        {
            ProjectMetadataEmitter.Emit(context, CancellationToken.None);
            var snapshot = context.Accumulator.ToSnapshot();
            var orders = ProjectNamed(snapshot, "Acme.Orders/Acme.Orders.csproj");
            var observation = Assert.Single(ProjectReferenceObservations(snapshot, orders));

            Assert.Equal(
                "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj",
                PayloadValue(observation, "project-reference"));
            Assert.Equal(EvidenceMethod.Configured, observation.ExtractionMethod);
            Assert.Equal("Acme.Orders/Acme.Orders.csproj", observation.Locator.RelativePath);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-04")]
    public async Task Emit_CalledTwice_YieldsTheSameObservationIdentities()
    {
        var context = await BindAsync(AcmeOrdersSolutionPath());
        try
        {
            ProjectMetadataEmitter.Emit(context, CancellationToken.None);
            var first = MetadataObservations(context.Accumulator.ToSnapshot())
                .Select(observation => observation.Identity)
                .ToArray();

            ProjectMetadataEmitter.Emit(context, CancellationToken.None);
            var second = MetadataObservations(context.Accumulator.ToSnapshot())
                .Select(observation => observation.Identity)
                .ToArray();

            Assert.NotEmpty(first);
            Assert.Equal(first.Length, second.Length);
            Assert.Equal(first.OrderBy(id => id.Owner.Id.Value).ThenBy(id => id.OccurrenceOrdinal),
                second.OrderBy(id => id.Owner.Id.Value).ThenBy(id => id.OccurrenceOrdinal));
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-05")]
    public async Task Emit_ProjectWithNoCompilation_EmitsNoMetadataAndLeavesDiagnostics()
    {
        var context = await BindAsync(AcmeOrdersSolutionPath());
        try
        {
            var before = context.Accumulator.ToSnapshot().Diagnostics.ToArray();
            Assert.Contains(before, record => string.Equals(record.Code, "unresolvable-sdk", StringComparison.Ordinal));

            ProjectMetadataEmitter.Emit(context, CancellationToken.None);
            var snapshot = context.Accumulator.ToSnapshot();
            var broken = ProjectNamed(snapshot, "Acme.Broken/Acme.Broken.csproj");

            Assert.Empty(MetadataObservations(snapshot, broken));
            Assert.Equal(before.Select(record => (record.Code, record.Message, record.IdentityOrKey)),
                snapshot.Diagnostics.Select(record => (record.Code, record.Message, record.IdentityOrKey)));
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-06")]
    public async Task Emit_TwoReferences_AssignsOrdinalsOverSortedPathsNotMsBuildOrder()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-cdc06-ordinal-");
        try
        {
            var solutionPath = WriteSolution(
                tree.FullName,
                ("Zebra", "Library", hostSource: "public class Z {}", references: []),
                ("Aardvark", "Library", hostSource: "public class A {}", references: []),
                ("App", "Exe", hostSource: "class Host { static void Main() {} }", references: ["Zebra", "Aardvark"]));
            var context = await BindAsync(solutionPath);
            try
            {
                ProjectMetadataEmitter.Emit(context, CancellationToken.None);
                var snapshot = context.Accumulator.ToSnapshot();
                var app = ProjectNamed(snapshot, "App/App.csproj");
                var outputKind = Assert.Single(OutputKindObservations(snapshot, app));
                var references = ProjectReferenceObservations(snapshot, app)
                    .OrderBy(observation => observation.Identity.OccurrenceOrdinal)
                    .ToArray();

                Assert.Equal(1, outputKind.Identity.OccurrenceOrdinal);
                Assert.Equal(2, references.Length);
                Assert.Equal(2, references[0].Identity.OccurrenceOrdinal);
                Assert.Equal(3, references[1].Identity.OccurrenceOrdinal);
                Assert.Equal("Aardvark/Aardvark.csproj", PayloadValue(references[0], "project-reference"));
                Assert.Equal("Zebra/Zebra.csproj", PayloadValue(references[1], "project-reference"));
                Assert.Equal(
                    references.Select(observation => observation.Identity.OccurrenceOrdinal).Distinct().Count(),
                    references.Length);
            }
            finally
            {
                context.BoundSolution?.Dispose();
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-07")]
    public async Task Emit_UnanalyzedProjectReference_RecordsDiagnosticAndNoObservation()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-cdc07-unanalyzed-");
        try
        {
            var ghostDir = Path.Combine(tree.FullName, "Ghost");
            Directory.CreateDirectory(ghostDir);
            File.WriteAllText(
                Path.Combine(ghostDir, "Ghost.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(ghostDir, "Ghost.cs"), "public class Ghost {}");
            var solutionPath = WriteSolution(
                tree.FullName,
                ("App", "Library", hostSource: "class Host {}", references: ["Ghost"]));
            var context = await BindAsync(solutionPath);
            try
            {
                ProjectMetadataEmitter.Emit(context, CancellationToken.None);
                var snapshot = context.Accumulator.ToSnapshot();
                var app = ProjectNamed(snapshot, "App/App.csproj");

                Assert.Empty(ProjectReferenceObservations(snapshot, app));
                var diagnostic = Assert.Single(
                    snapshot.Diagnostics,
                    record => string.Equals(record.Code, "unanalyzed-project-reference", StringComparison.Ordinal));
                Assert.Contains("Ghost", diagnostic.IdentityOrKey, StringComparison.Ordinal);
                Assert.False(Path.IsPathRooted(diagnostic.IdentityOrKey));
                Assert.DoesNotContain(
                    snapshot.Observations,
                    observation => observation.Identity.Payload.Entries.Any(
                        entry => entry.Key == "project-reference"
                            && entry.Value.Value.Contains("Ghost", StringComparison.Ordinal)));
            }
            finally
            {
                context.BoundSolution?.Dispose();
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static async Task<PipelineContext> BindAsync(string solutionPath)
    {
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
        await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
        return context;
    }

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static Project ProjectNamed(Csharp2Md.Analysis.Storage.FactualSnapshot snapshot, string logicalPath) =>
        Assert.Single(
            snapshot.Facts.OfType<Project>(),
            project => string.Equals(LogicalPath(project), logicalPath, StringComparison.Ordinal));

    private static string LogicalPath(Project project)
    {
        const string marker = ";path=";
        var id = project.Id.Value;
        var start = id.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, id);
        start += marker.Length;
        var end = id.IndexOf(';', start);
        var encoded = end < 0 ? id[start..] : id[start..end];
        return Uri.UnescapeDataString(encoded);
    }

    private static Observation[] MetadataObservations(
        Csharp2Md.Analysis.Storage.FactualSnapshot snapshot,
        Project? owner = null) =>
        snapshot.Observations
            .Where(observation =>
                observation.Identity.Kind is ObservationKind.Configuration
                && observation.Identity.Payload.Entries.Any(
                    entry => entry.Key is "output-kind" or "project-reference")
                && (owner is null || observation.Identity.Owner.Equals(owner.Reference)))
            .ToArray();

    private static Observation[] OutputKindObservations(
        Csharp2Md.Analysis.Storage.FactualSnapshot snapshot,
        Project owner) =>
        MetadataObservations(snapshot, owner)
            .Where(observation => observation.Identity.Payload.Entries.Any(entry => entry.Key == "output-kind"))
            .ToArray();

    private static Observation[] ProjectReferenceObservations(
        Csharp2Md.Analysis.Storage.FactualSnapshot snapshot,
        Project owner) =>
        MetadataObservations(snapshot, owner)
            .Where(observation => observation.Identity.Payload.Entries.Any(entry => entry.Key == "project-reference"))
            .ToArray();

    private static string PayloadValue(Observation observation, string key) =>
        Assert.Single(observation.Identity.Payload.Entries, entry => entry.Key == key).Value.Value;

    private static string WriteSolution(
        string root,
        params (string Name, string OutputType, string hostSource, string[] references)[] projects)
    {
        foreach (var (name, outputType, hostSource, references) in projects)
        {
            var projectDir = Path.Combine(root, name);
            Directory.CreateDirectory(projectDir);
            var referenceItems = string.Concat(
                references.Select(reference =>
                    $"    <ProjectReference Include=\"..\\{reference}\\{reference}.csproj\" />{Environment.NewLine}"));
            var outputElement = outputType is "Library"
                ? ""
                : $"    <OutputType>{outputType}</OutputType>{Environment.NewLine}";
            File.WriteAllText(
                Path.Combine(projectDir, $"{name}.csproj"),
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                {outputElement}    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                {referenceItems}  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDir, "Host.cs"), hostSource);
        }

        var entries = string.Join(
            Environment.NewLine,
            projects.Select(project => $"  <Project Path=\"{project.Name}/{project.Name}.csproj\" />"));
        var solutionPath = Path.Combine(root, "App.slnx");
        File.WriteAllText(
            solutionPath,
            $"""
            <Solution>
            {entries}
            </Solution>
            """);
        return solutionPath;
    }
}
