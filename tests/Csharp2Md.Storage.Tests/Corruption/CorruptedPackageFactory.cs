using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Reading;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Corruption;

/// <summary>
/// Builds one clean, self-consistent published package -- one fact, one source file, one fact-citing
/// catalog entry with a locator span, and one page-to-page cross-reference -- as the shared baseline every
/// defect class in <see cref="CorruptedPackageTests"/> mutates exactly one way (GCPC-065, GCPC-066). Each
/// mutation is proven, by construction, to differ from the clean package in only the one file it names.
/// </summary>
internal static class CorruptedPackageFactory
{
    internal const string SourceKey = "source/acme/Program.cs";
    internal const string SourceText = "class Program { static void Main() { } }";
    internal const string CatalogKey = "catalogs/components.json";
    internal const string EntryPointsPageKey = "pages/entry-points.md";
    internal const string ComponentsPageKey = "pages/components.md";
    internal const string ManifestKey = "manifest.json";

    private static readonly ManifestContext Context = new("s-corruption", "Acme.sln");
    private static readonly SolutionId AcmeSolution = SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln");

    /// <summary>Builds the clean package fresh under <paramref name="parentDirectory"/>\clean.</summary>
    internal static string BuildCleanPackage(string parentDirectory)
    {
        var (document, plan, projections) = BuildContent();

        // Prove the baseline is actually clean before anything mutates it: both validators the real
        // publication path runs must accept it as-is.
        ProjectionValidator.Validate(PublishedPackageView.From(document, plan), projections);

        var packageDirectory = Path.Combine(parentDirectory, "clean");
        PackageDirectoryWriter.Write(packageDirectory, document, plan, projections);
        return packageDirectory;
    }

    /// <summary>Copies <paramref name="cleanPackageDirectory"/> into a fresh sibling directory to mutate.</summary>
    internal static string CopyForMutation(string cleanPackageDirectory, string parentDirectory, string name)
    {
        var destination = Path.Combine(parentDirectory, name);
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(cleanPackageDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(cleanPackageDirectory, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return destination;
    }

    /// <summary>Every file relative path under a package directory, for asserting exactly one file differs.</summary>
    internal static IReadOnlyDictionary<string, byte[]> ReadAllFiles(string packageDirectory)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(packageDirectory, file).Replace('\\', '/');
            files[relative] = File.ReadAllBytes(file);
        }

        return files;
    }

    private static (WireDocument Document, LayoutPlan Plan, ImmutableArray<StagedFragment> Projections) BuildContent()
    {
        var snapshot = new FactualSnapshot([Solution.Create(AcmeSolution)], [], [], [], [], []);
        var document = PackageValidator.Validate(DomainMapper.ToWire(snapshot, Context)).Document;
        var plan = LayoutPlanner.Plan(document, int.MaxValue);

        var projections = ImmutableArray.Create(
            new StagedFragment(ArtifactRole.Payload, SourceKey, Utf8(SourceText)),
            new StagedFragment(
                ArtifactRole.Payload,
                CatalogKey,
                Utf8(
                    "{\"page\":\"" + CatalogKey + "\",\"artifact_key\":\"" + SourceKey
                    + "\",\"ordinal\":0,\"locator\":\"" + SourceKey
                    + ":1:1-1:5\",\"span\":{\"start_line\":1,\"start_column\":1,\"end_line\":1,\"end_column\":5}}")),
            new StagedFragment(
                ArtifactRole.Payload,
                EntryPointsPageKey,
                Utf8("{\"page\":\"" + EntryPointsPageKey + "\",\"artifact_key\":\"" + ComponentsPageKey + "\",\"ordinal\":0}")),
            new StagedFragment(ArtifactRole.Payload, ComponentsPageKey, Utf8("{\"page\":\"" + ComponentsPageKey + "\"}")));

        return (document, plan, projections);
    }

    private static ImmutableArray<byte> Utf8(string text) => Encoding.UTF8.GetBytes(text).ToImmutableArray();

    internal static string ReadText(string path) => File.ReadAllText(path);

    internal static void WriteText(string path, string text) => File.WriteAllText(path, text);

    internal static string ComputeSha256(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
