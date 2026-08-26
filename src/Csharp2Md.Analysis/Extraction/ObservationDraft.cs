using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Extraction;

internal sealed record ObservationDraft(
    FactReference Owner,
    ObservationKind Kind,
    NormalizedPayload Payload,
    EvidenceLocator Locator,
    EvidenceMethod ExtractionMethod,
    BindingDiagnostic Diagnostic,
    DocumentHash DocumentHash);
