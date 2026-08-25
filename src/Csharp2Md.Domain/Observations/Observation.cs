using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Domain.Observations;

public sealed class Observation
{
    public ObservationIdentity Identity { get; }

    public EvidenceLocator Locator { get; }

    public EvidenceMethod ExtractionMethod { get; }

    public BindingDiagnostic Diagnostic { get; }

    public DocumentHash DocumentHash { get; }

    public ExtractorVersion ExtractorVersion { get; }

    private Observation(
        ObservationIdentity identity,
        EvidenceLocator locator,
        EvidenceMethod extractionMethod,
        BindingDiagnostic diagnostic,
        DocumentHash documentHash,
        ExtractorVersion extractorVersion)
    {
        Identity = identity;
        Locator = locator;
        ExtractionMethod = extractionMethod;
        Diagnostic = diagnostic;
        DocumentHash = documentHash;
        ExtractorVersion = extractorVersion;
    }

    public static Observation Create(
        FactReference owner,
        ObservationKind kind,
        NormalizedPayload payload,
        int occurrenceOrdinal,
        EvidenceLocator locator,
        EvidenceMethod extractionMethod,
        BindingDiagnostic diagnostic,
        DocumentHash documentHash,
        ExtractorVersion extractorVersion)
    {
        RequireInitialized(owner, nameof(owner));
        RequireDefined(kind, nameof(kind));
        RequirePayload(payload, nameof(payload));
        RequireInitialized(locator, nameof(locator));
        RequireDefined(extractionMethod, nameof(extractionMethod));
        RequireInitialized(diagnostic, nameof(diagnostic));
        RequireInitialized(documentHash, nameof(documentHash));
        RequireInitialized(extractorVersion, nameof(extractorVersion));

        var identity = new ObservationIdentity(owner, kind, payload, occurrenceOrdinal);
        return new Observation(identity, locator, extractionMethod, diagnostic, documentHash, extractorVersion);
    }

    private static void RequireInitialized<T>(T value, string parameterName)
        where T : struct, IEquatable<T>
    {
        if (value.Equals(default(T)))
        {
            throw new ArgumentException($"An observation requires a {parameterName}.", parameterName);
        }
    }

    private static void RequireDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentException($"'{value}' is not a defined value required for {parameterName}.", parameterName);
        }
    }

    private static void RequirePayload(NormalizedPayload payload, string parameterName)
    {
        if (payload.Entries.IsDefault)
        {
            throw new ArgumentException($"An observation requires a {parameterName}.", parameterName);
        }
    }
}
