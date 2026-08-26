using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class MessageOperationDetector : IRegisteredContextDetector
{
    private static readonly BindingDiagnostic Bound = new("bound", "bound");
    private static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;
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
            MessagePayload(occurrence, method, invocation),
            ObservationMaterializer.CreateLocator(occurrence.Document, invocation),
            EvidenceMethod.Semantic,
            Bound,
            occurrence.DocumentHash);
    }

    private static NormalizedPayload MessagePayload(
        BoundOccurrence occurrence,
        IMethodSymbol method,
        InvocationExpressionSyntax invocation)
    {
        var entries = new List<PayloadEntry>
        {
            new("method-name", StructuralLiteral.Create(LiteralRole.ProtocolName, method.Name, "method-name")),
        };
        var receiver = method.ReceiverType ?? method.ContainingType;
        if (receiver is not null)
        {
            entries.Add(
                new PayloadEntry(
                    "target-type",
                    StructuralLiteral.Create(LiteralRole.ProtocolName, receiver.ToDisplayString(Qualified), "target-type")));
        }

        var eventType = method.TypeArguments.FirstOrDefault()
            ?? (invocation.ArgumentList.Arguments.Count > 0
                ? occurrence.Model.GetTypeInfo(
                    invocation.ArgumentList.Arguments[0].Expression,
                    occurrence.CancellationToken).Type
                : null);
        if (eventType is INamedTypeSymbol named && !named.IsAnonymousType)
        {
            entries.Add(
                new PayloadEntry(
                    "type-argument",
                    StructuralLiteral.Create(LiteralRole.ProtocolName, named.ToDisplayString(Qualified), "type-argument")));
        }

        return NormalizedPayload.Create(entries);
    }
}
