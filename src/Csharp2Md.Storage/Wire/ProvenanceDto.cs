using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Wire;

/// <summary>
/// The generator version and build identity, all five version axes, and the deterministic parameters
/// that shaped this package -- the manifest's provenance block (GCPC-056..GCPC-060). Two runs of the same
/// generator build over the same input publish byte-identical provenance: every field here is either a
/// fact about the running build (reflection over the loaded assembly) or a pure function of the document,
/// never a timestamp or a runtime measurement -- those stay in <c>measurements.json</c> alone.
/// </summary>
public sealed record ProvenanceDto(
    string GeneratorVersion,
    string BuildIdentity,
    int SchemaVersion,
    int TaxonomyVersion,
    int ObservationSchemaVersion,
    int ExtractorSetVersion,
    int ClassifierSetVersion,
    int ArtifactCeilingBytes,
    string TokenEstimatorId,
    string DocumentPolicyVersion,
    string AllowlistDigest)
{
    /// <summary>
    /// Mirrors <c>Csharp2Md.Analysis.Inventory.SupportedDocumentPolicy.Version</c>. Storage cannot
    /// reference that type (it is internal to the Analysis assembly, and no public channel yet carries it
    /// onto <c>FactualSnapshot</c>/<c>WireDocument</c>); this constant is kept in step with it by
    /// convention until such a channel exists.
    /// </summary>
    public const string DefaultDocumentPolicyVersion = "supported-document-policy/1";

    /// <summary>
    /// The digest of an empty document allowlist. No channel yet carries the allowlist an analysis run
    /// actually used (T12's `AnalysisRequest` allowlist is consumed entirely inside Analysis) onto
    /// <c>WireDocument</c>, so every provenance published today reports the empty-allowlist digest. A
    /// caller that supplies the real allowlist recomputes it with <see cref="ComputeAllowlistDigest"/>.
    /// </summary>
    public static string EmptyAllowlistDigest { get; } = ComputeAllowlistDigest([]);

    /// <summary>A deterministic digest of a document allowlist, ordinal-sorted so input order never
    /// changes the result.</summary>
    public static string ComputeAllowlistDigest(IEnumerable<string> allowlist)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        var normalized = string.Join('\n', allowlist.OrderBy(static entry => entry, StringComparer.Ordinal));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>
    /// The provenance of the running generator build: its version and a reproducible build identity (the
    /// loaded assembly's module version id, stable across compilations of the same source under the
    /// deterministic-build default the .NET SDK already applies), the five taxonomy version axes, and the
    /// derived ceiling with its token estimator.
    /// </summary>
    public static ProvenanceDto Current() => Current(CeilingCalculator.Derive(), EmptyAllowlistDigest);

    /// <summary>
    /// The provenance of the running generator build, using the caller-supplied ceiling and allowlist
    /// digest instead of the derived defaults (T52: the CLI's own <c>--reading-budget-tokens</c>,
    /// <c>--max-file-reads-per-scenario</c> and <c>--allowlist</c> reach here, so a run's published
    /// provenance reflects the values that actually shaped it, not always the default).
    /// </summary>
    public static ProvenanceDto Current(CeilingCalculation ceiling, string allowlistDigest)
    {
        ArgumentNullException.ThrowIfNull(allowlistDigest);

        var assembly = typeof(ProvenanceDto).Assembly;
        var generatorVersion = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        var buildIdentity = assembly.ManifestModule.ModuleVersionId.ToString();
        var versions = TaxonomyTables.Default.Versions;

        return new ProvenanceDto(
            generatorVersion,
            buildIdentity,
            versions.SchemaVersion,
            versions.TaxonomyVersion,
            versions.ObservationSchemaVersion,
            versions.ExtractorSetVersion,
            versions.ClassifierSetVersion,
            ceiling.CeilingBytes,
            ceiling.TokenEstimatorId,
            DefaultDocumentPolicyVersion,
            allowlistDigest);
    }
}
