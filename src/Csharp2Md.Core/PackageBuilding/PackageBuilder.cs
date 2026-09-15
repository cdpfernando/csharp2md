using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.PackageBuilding;

internal sealed record PackageBudget(int MaximumArtifacts, long MaximumBytes)
{
    internal static PackageBudget Default { get; } = new(1_500, 64L * 1024 * 1024);
}

internal sealed class PackageBudgetExceededException : InvalidOperationException
{
    internal PackageBudgetExceededException(string limit) : base($"package-budget: '{limit}'.") { }
}

internal static class PackageBuilder
{
    internal static PackagePlan Build(RetrievalModel model, bool includeTests = false, PackageBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        budget ??= PackageBudget.Default;
        if (budget.MaximumArtifacts < 1 || budget.MaximumBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }

        var machine = MachineArtifactWriter.Write(model, includeTests);
        var payloads = machine.Artifacts.Where(artifact => artifact.Path.Value != "manifest.json")
            .ToImmutableArray().AddRange(MarkdownRenderer.Render(model, machine.Manifest));
        var retained = model.Solutions.Select(solution => solution.RetainedGraph).Where(graph => graph is not null).Cast<RetainedGraph>().ToArray();
        var extraction = retained.Length == 0
            ? new ExtractionMeasurements(0, 0)
            : new ExtractionMeasurements(retained.Sum(graph => graph.Measurements.RetainedCount), retained.Sum(graph => graph.Measurements.FilteredCount));
        var measurement = new PublicationMeasurements(extraction, payloads.Length + 3, [new FilteredCount("retention", extraction.FilteredCount)]);
        payloads = payloads.Add(Artifact("measurements.json", ArtifactFamily.Measurement, measurement, 1));
        payloads = payloads.Add(Artifact("certification.json", ArtifactFamily.Certification, new PackageCertification([]), 0));
        payloads = payloads.Add(Artifact("manifest.json", ArtifactFamily.Manifest, machine.Manifest, 1));
        var ordered = payloads.OrderBy(artifact => artifact.Path.Value, StringComparer.Ordinal).ToImmutableArray();
        var bytes = ordered.Sum(artifact => (long)artifact.Payload.Length);
        if (ordered.Length > budget.MaximumArtifacts)
        {
            throw new PackageBudgetExceededException("artifacts");
        }

        if (bytes > budget.MaximumBytes)
        {
            throw new PackageBudgetExceededException("bytes");
        }

        return new PackagePlan(machine.Manifest, ordered, Digest(ordered), measurement);
    }

    private static PlannedArtifact Artifact<T>(string path, ArtifactFamily family, T value, int records)
    {
        var payload = CanonicalJson.Write(value);
        return new PlannedArtifact(new RelativeArtifactPath(path), family, payload, records, Convert.ToHexStringLower(SHA256.HashData(payload.AsSpan())));
    }

    private static string Digest(IEnumerable<PlannedArtifact> artifacts)
    {
        var text = string.Join('\n', artifacts.Select(artifact => $"{artifact.Path.Value}:{artifact.ContentDigest}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
