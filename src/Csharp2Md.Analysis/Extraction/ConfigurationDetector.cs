using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class ConfigurationDetector : IRegisteredContextDetector
{
    private static readonly BindingDiagnostic Bound = new("bound", "bound");

    public ObservationDraft? TryObserve(BoundOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        var key = occurrence.Node switch
        {
            ElementAccessExpressionSyntax access => TryKeyFromIndexer(occurrence, access),
            InvocationExpressionSyntax invocation => TryKeyFromInvocation(occurrence, invocation),
            _ => null,
        };
        if (key is null)
        {
            return null;
        }

        var literal = StructuralLiteral.Create(LiteralRole.ConfigurationKey, key, "key");
        return new ObservationDraft(
            occurrence.Owner,
            ObservationKind.Configuration,
            NormalizedPayload.Create([new PayloadEntry("key", literal)]),
            ObservationMaterializer.CreateLocator(occurrence.Document, occurrence.Node),
            EvidenceMethod.Semantic,
            Bound,
            occurrence.DocumentHash);
    }

    private static string? TryKeyFromIndexer(BoundOccurrence occurrence, ElementAccessExpressionSyntax access)
    {
        var symbol = occurrence.Model.GetSymbolInfo(access, occurrence.CancellationToken).Symbol;
        ITypeSymbol? surface = symbol switch
        {
            IPropertySymbol { IsIndexer: true } indexer => indexer.ContainingType,
            _ => occurrence.Model.GetTypeInfo(access.Expression, occurrence.CancellationToken).Type,
        };
        if (!IsConfigurationType(surface, occurrence.Compilation))
        {
            return null;
        }

        return TryFirstStringLiteral(access.ArgumentList.Arguments);
    }

    private static string? TryKeyFromInvocation(BoundOccurrence occurrence, InvocationExpressionSyntax invocation)
    {
        if (occurrence.Model.GetSymbolInfo(invocation, occurrence.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return null;
        }

        var name = method.Name;
        var receiver = method.ReceiverType ?? method.ContainingType;
        var matchesConfigurationCall = name is "GetSection" or "GetValue" or "GetConnectionString"
            && IsConfigurationType(receiver, occurrence.Compilation);
        var matchesConfigure = name is "Configure";
        var matchesOptions = IsOptionsType(method.ContainingType, occurrence.Compilation)
            || method.TypeArguments.Any(argument => IsOptionsType(argument, occurrence.Compilation));
        if (!matchesConfigurationCall && !matchesConfigure && !matchesOptions)
        {
            return null;
        }

        return TryFirstStringLiteral(invocation.ArgumentList.Arguments);
    }

    private static string? TryFirstStringLiteral(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        foreach (var argument in arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax literal
                && literal.Token.IsKind(SyntaxKind.StringLiteralToken)
                && !string.IsNullOrWhiteSpace(literal.Token.ValueText))
            {
                return literal.Token.ValueText;
            }
        }

        return null;
    }

    private static bool IsConfigurationType(ITypeSymbol? type, Compilation compilation)
    {
        var configuration = compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
        return IsSameOrImplements(type, configuration);
    }

    private static bool IsOptionsType(ITypeSymbol? type, Compilation compilation)
    {
        var options = compilation.GetTypeByMetadataName("Microsoft.Extensions.Options.IOptions`1");
        return IsSameOrImplements(type, options);
    }

    private static bool IsSameOrImplements(ITypeSymbol? type, INamedTypeSymbol? expected)
    {
        if (type is null || expected is null)
        {
            return false;
        }

        var comparer = SymbolEqualityComparer.Default;
        if (comparer.Equals(type.OriginalDefinition, expected))
        {
            return true;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (comparer.Equals(implemented.OriginalDefinition, expected))
            {
                return true;
            }
        }

        return false;
    }
}
