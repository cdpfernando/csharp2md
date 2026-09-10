using Csharp2Md.Analysis.Classification;

namespace Csharp2Md.Analysis.Inventory;

/// <summary>
/// The policy category a document is decided into. Every accepted category is admitted with a
/// <c>Document</c> fact and a <c>source/</c> artifact (GCPC-027); every excluded category is not
/// (GCPC-028). Public because <see cref="DocumentPolicyCategoryTotal"/> carries it on the public
/// <c>FactualSnapshot</c>.
/// </summary>
public enum DocumentPolicyCategory
{
    CSharpSource,
    ProjectFile,
    Configuration,
    Conditional,
    Allowlisted,
    FrontendScript,
    Archive,
    StaticAsset,
    PackageManagementArtifact,
    BinaryOrCertificate,
    Unregistered,
}

/// <summary>
/// The accept-or-exclude decision for one document, and the category that decision falls into.
/// </summary>
internal readonly record struct PolicyDecision(bool Accepted, DocumentPolicyCategory Category);

/// <summary>
/// Decides, per document, whether the generator's supported-document policy admits it, and into
/// which category (GCPC-026..GCPC-030). The default supported set is C# source, each project's own
/// project file and <c>appsettings*.json</c>. An additional extension is admitted only when an
/// active classifier declares that it consumes it (GCPC-029), and a caller-supplied allowlist
/// re-includes named documents without admitting any other excluded class (GCPC-030). Everything
/// else falls into one of the default excluded categories.
/// </summary>
internal sealed class SupportedDocumentPolicy
{
    /// <summary>Carried into provenance once a later phase publishes it (GCPC-058).</summary>
    public const string Version = "supported-document-policy/1";

    private static readonly ImmutableHashSet<string> FrontendScriptExtensions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ".ts", ".tsx", ".js", ".jsx", ".map");

    private static readonly ImmutableHashSet<string> ArchiveExtensions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ".zip", ".tar", ".gz", ".tgz", ".7z", ".rar");

    private static readonly ImmutableHashSet<string> StaticAssetExtensions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".bmp", ".webp",
        ".woff", ".woff2", ".ttf", ".eot", ".otf", ".css");

    private static readonly ImmutableHashSet<string> PackageManagementFileNames = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "npm-shrinkwrap.json");

    private static readonly ImmutableHashSet<string> BinaryOrCertificateExtensions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ".dll", ".exe", ".pfx", ".pem", ".crt", ".cer", ".key", ".p12", ".der");

    private readonly ImmutableHashSet<string> _conditionalExtensions;
    private readonly ImmutableHashSet<string> _allowlistedRelativePaths;

    public SupportedDocumentPolicy(
        ClassifierCapabilityRegistry capabilityRegistry,
        ImmutableArray<string> allowlistedRelativePaths = default)
    {
        ArgumentNullException.ThrowIfNull(capabilityRegistry);
        _conditionalExtensions = capabilityRegistry.ConsumedExtensions()
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        _allowlistedRelativePaths = (allowlistedRelativePaths.IsDefault
                ? ImmutableArray<string>.Empty
                : allowlistedRelativePaths)
            .ToImmutableHashSet(PathComparer);
    }

    public PolicyDecision Decide(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (_allowlistedRelativePaths.Contains(relativePath))
        {
            return new PolicyDecision(true, DocumentPolicyCategory.Allowlisted);
        }

        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(relativePath);

        if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return new PolicyDecision(true, DocumentPolicyCategory.CSharpSource);
        }

        if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return new PolicyDecision(true, DocumentPolicyCategory.ProjectFile);
        }

        if (IsConfigurationDocument(fileName))
        {
            return new PolicyDecision(true, DocumentPolicyCategory.Configuration);
        }

        if (extension.Length > 0 && _conditionalExtensions.Contains(extension))
        {
            return new PolicyDecision(true, DocumentPolicyCategory.Conditional);
        }

        if (FrontendScriptExtensions.Contains(extension))
        {
            return new PolicyDecision(false, DocumentPolicyCategory.FrontendScript);
        }

        if (ArchiveExtensions.Contains(extension))
        {
            return new PolicyDecision(false, DocumentPolicyCategory.Archive);
        }

        if (StaticAssetExtensions.Contains(extension))
        {
            return new PolicyDecision(false, DocumentPolicyCategory.StaticAsset);
        }

        if (PackageManagementFileNames.Contains(fileName))
        {
            return new PolicyDecision(false, DocumentPolicyCategory.PackageManagementArtifact);
        }

        if (BinaryOrCertificateExtensions.Contains(extension))
        {
            return new PolicyDecision(false, DocumentPolicyCategory.BinaryOrCertificate);
        }

        return new PolicyDecision(false, DocumentPolicyCategory.Unregistered);
    }

    private static bool IsConfigurationDocument(string fileName) =>
        fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
