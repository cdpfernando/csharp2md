using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class MessageOperationDetector : IRegisteredContextDetector
{
    private static readonly NormalizedPayload EmptyPayload = NormalizedPayload.Create([]);
    private static readonly BindingDiagnostic Bound = new("bound", "bound");
    private static readonly HashSet<string> MessageNames =
    [
        "Publish",
        "PublishAsync",
        "Send",
        "SendAsync",
        "Subscribe",
    ];

    public ObservationDraft? TryObserve(BoundOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (occurrence.Node is not InvocationExpressionSyntax invocation)
        {
            return null;
        }

        if (occurrence.Model.GetSymbolInfo(invocation, occurrence.CancellationToken).Symbol is not IMethodSymbol method
            || !MessageNames.Contains(method.Name))
        {
            return null;
        }

        return new ObservationDraft(
            occurrence.Owner,
            ObservationKind.MessageOperation,
            EmptyPayload,
            ObservationMaterializer.CreateLocator(occurrence.Document, invocation),
            EvidenceMethod.Semantic,
            Bound,
            occurrence.DocumentHash);
    }
}
