using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class RouteDeclarationDetector : IRegisteredContextDetector
{
    private static readonly NormalizedPayload EmptyPayload = NormalizedPayload.Create([]);
    private static readonly BindingDiagnostic Bound = new("bound", "bound");
    private static readonly HashSet<string> RouteAttributeNames =
    [
        "HttpGet",
        "HttpPost",
        "HttpPut",
        "HttpDelete",
        "HttpPatch",
        "Route",
    ];
    private static readonly HashSet<string> MapMethodNames =
    [
        "MapGet",
        "MapPost",
        "MapPut",
        "MapDelete",
    ];

    public ObservationDraft? TryObserve(BoundOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        string? route;
        string? httpMethod;
        switch (occurrence.Node)
        {
            case AttributeSyntax attribute when IsRouteAttribute(occurrence, attribute):
                route = TryFirstStringLiteral(EnumerateAttributeArguments(attribute));
                httpMethod = TryHttpMethod(AttributeTypeName(occurrence, attribute));
                break;
            case InvocationExpressionSyntax invocation when IsMapInvocation(occurrence, invocation):
                route = TryFirstStringLiteral(EnumerateInvocationArguments(invocation));
                httpMethod = TryHttpMethod(MapMethodName(occurrence, invocation));
                break;
            default:
                return null;
        }

        var payload = CreatePayload(route, httpMethod);
        return new ObservationDraft(
            occurrence.Owner,
            ObservationKind.RouteDeclaration,
            payload,
            ObservationMaterializer.CreateLocator(occurrence.Document, occurrence.Node),
            EvidenceMethod.Semantic,
            Bound,
            occurrence.DocumentHash);
    }

    private static bool IsRouteAttribute(BoundOccurrence occurrence, AttributeSyntax attribute)
    {
        if (occurrence.Model.GetSymbolInfo(attribute, occurrence.CancellationToken).Symbol is IMethodSymbol constructor
            && RouteAttributeNames.Contains(TrimAttributeSuffix(constructor.ContainingType.Name)))
        {
            return true;
        }

        return RouteAttributeNames.Contains(TrimAttributeSuffix(attribute.Name.ToString()));
    }

    private static bool IsMapInvocation(BoundOccurrence occurrence, InvocationExpressionSyntax invocation) =>
        MapMethodName(occurrence, invocation) is not null;

    private static string? MapMethodName(BoundOccurrence occurrence, InvocationExpressionSyntax invocation) =>
        occurrence.Model.GetSymbolInfo(invocation, occurrence.CancellationToken).Symbol is IMethodSymbol method
        && MapMethodNames.Contains(method.Name)
            ? method.Name
            : null;

    private static string AttributeTypeName(BoundOccurrence occurrence, AttributeSyntax attribute)
    {
        if (occurrence.Model.GetSymbolInfo(attribute, occurrence.CancellationToken).Symbol is IMethodSymbol constructor)
        {
            return constructor.ContainingType.Name;
        }

        return attribute.Name.ToString();
    }

    private static NormalizedPayload CreatePayload(string? route, string? httpMethod)
    {
        var entries = new List<PayloadEntry>();
        if (httpMethod is not null)
        {
            entries.Add(
                new PayloadEntry(
                    "method-name",
                    StructuralLiteral.Create(LiteralRole.ProtocolName, httpMethod, "method-name")));
        }

        if (route is not null)
        {
            entries.Add(
                new PayloadEntry("route", StructuralLiteral.Create(LiteralRole.Route, route, "route")));
        }

        return entries.Count == 0 ? EmptyPayload : NormalizedPayload.Create(entries);
    }

    private static string? TryHttpMethod(string? rawName)
    {
        if (rawName is null)
        {
            return null;
        }

        var name = TrimAttributeSuffix(rawName);
        var simple = name.Contains('.', StringComparison.Ordinal) ? name[(name.LastIndexOf('.') + 1)..] : name;
        return simple switch
        {
            "HttpGet" or "MapGet" => "GET",
            "HttpPost" or "MapPost" => "POST",
            "HttpPut" or "MapPut" => "PUT",
            "HttpDelete" or "MapDelete" => "DELETE",
            "HttpPatch" => "PATCH",
            _ => null,
        };
    }

    private static IEnumerable<ExpressionSyntax> EnumerateAttributeArguments(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            yield break;
        }

        foreach (var argument in attribute.ArgumentList.Arguments)
        {
            yield return argument.Expression;
        }
    }

    private static IEnumerable<ExpressionSyntax> EnumerateInvocationArguments(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            yield return argument.Expression;
        }
    }

    private static string? TryFirstStringLiteral(IEnumerable<ExpressionSyntax> expressions)
    {
        foreach (var expression in expressions)
        {
            if (expression is LiteralExpressionSyntax literal
                && literal.Token.IsKind(SyntaxKind.StringLiteralToken)
                && !string.IsNullOrWhiteSpace(literal.Token.ValueText))
            {
                return literal.Token.ValueText;
            }
        }

        return null;
    }

    private static string TrimAttributeSuffix(string name)
    {
        const string suffix = "Attribute";
        return name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length
            ? name[..^suffix.Length]
            : name;
    }
}
