using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using FactDocumentDetectionContext = Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext;

namespace Csharp2Md.Core.Detection.Http;

internal sealed class HttpRelationDetector : IDocumentFactDetector
{
    private const string HttpClientTypeName = "System.Net.Http.HttpClient";
    private const string HttpClientFactoryTypeName = "Microsoft.Extensions.Http.IHttpClientFactory";
    private const string RequestKind = "http-request";
    private const string NamedClientKind = "http-named-client";
    private const string BaseAddressKind = "http-base-address";
    private const string TimeoutKind = "http-timeout";
    private const string HeaderKind = "http-header";

    private static readonly string[] NonRequestMembers = ["Dispose", "CancelPendingRequests"];

    private static readonly ImmutableDictionary<string, string> RequestMethods =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GetAsync"] = "GET",
            ["GetStringAsync"] = "GET",
            ["GetByteArrayAsync"] = "GET",
            ["GetStreamAsync"] = "GET",
            ["PostAsync"] = "POST",
            ["PutAsync"] = "PUT",
            ["DeleteAsync"] = "DELETE",
            ["PatchAsync"] = "PATCH",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public DetectorDescriptor Descriptor { get; } = DetectorDescriptor.Create(
        DetectorId.Create("io.csharp2md.http"),
        "1.0.0",
        [DetectorLevel.Document],
        [FactKind.Relation]);

    public DetectorResult Detect(FactDocumentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.SemanticDocument is not { } semantic)
        {
            return DetectorResult.Create();
        }

        var root = semantic.SyntaxTree.GetRoot();
        var observations = ImmutableArray.CreateBuilder<Observation>();
        var consumedCreateClientCalls = new HashSet<SyntaxNode>();

        foreach (var syntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semantic.SemanticModel.GetOperation(syntax) is not IInvocationOperation invocation)
            {
                continue;
            }

            ObserveRequest(context, semantic, invocation, observations, consumedCreateClientCalls);
            ObserveHeader(context, semantic, invocation, observations);
        }

        foreach (var syntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (consumedCreateClientCalls.Contains(syntax)
                || semantic.SemanticModel.GetOperation(syntax) is not IInvocationOperation invocation)
            {
                continue;
            }

            ObserveNamedClientCreation(context, semantic, invocation, observations);
        }

        foreach (var syntax in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (semantic.SemanticModel.GetOperation(syntax) is not ISimpleAssignmentOperation assignment)
            {
                continue;
            }

            ObserveAssignment(context, semantic, assignment, observations);
        }

        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var facts = observations
            .OrderBy(static observation => observation.Evidence)
            .ThenBy(static observation => observation.Kind, StringComparer.Ordinal)
            .Select(observation => CreateFact(context, observation, ordinals))
            .ToImmutableArray();
        return DetectorResult.Create(facts);
    }

    private static void ObserveRequest(
        FactDocumentDetectionContext context,
        SemanticDetectionDocument semantic,
        IInvocationOperation invocation,
        ImmutableArray<Observation>.Builder observations,
        HashSet<SyntaxNode> consumedCreateClientCalls)
    {
        var method = invocation.TargetMethod;
        if (!RequestMethods.TryGetValue(method.Name, out var httpMethod)
            || NonRequestMembers.Contains(method.Name, StringComparer.Ordinal)
            || invocation.Instance?.Type is not { } receiverType
            || !IsHttpClient(receiverType))
        {
            return;
        }

        var details = new List<RelationDetail> { new("http_method", httpMethod) };
        if (TryNamedClientOf(invocation.Instance) is { } named)
        {
            details.Add(new RelationDetail("client", $"named:{named.Name}"));
            consumedCreateClientCalls.Add(named.Syntax);
        }
        else
        {
            details.Add(new RelationDetail("client", $"typed:{invocation.Instance.Syntax}"));
        }

        var routeArgument = invocation.Arguments.FirstOrDefault();
        var (routeKey, routeValue, resolution) = ExpressionValue(routeArgument, "route", "route_expression");
        details.Add(new RelationDetail(routeKey, routeValue));

        observations.Add(new Observation(
            RequestKind,
            resolution,
            EvidenceFor(context.Document, semantic.SyntaxTree, invocation.Syntax),
            details.ToImmutableArray()));
    }

    private static void ObserveHeader(
        FactDocumentDetectionContext context,
        SemanticDetectionDocument semantic,
        IInvocationOperation invocation,
        ImmutableArray<Observation>.Builder observations)
    {
        if (invocation.TargetMethod.Name != "Add"
            || invocation.Instance is not IPropertyReferenceOperation { Property.Name: "DefaultRequestHeaders" } headers
            || headers.Instance?.Type is not { } receiverType
            || !IsHttpClient(receiverType))
        {
            return;
        }

        var (nameKey, nameValue, nameResolution) = ExpressionValue(
            invocation.Arguments.ElementAtOrDefault(0), "header_name", "header_name_expression");
        var (valueKey, valueValue, valueResolution) = ExpressionValue(
            invocation.Arguments.ElementAtOrDefault(1), "header_value", "header_value_expression");

        observations.Add(new Observation(
            HeaderKind,
            nameResolution == FactResolution.Exact && valueResolution == FactResolution.Exact
                ? FactResolution.Exact
                : FactResolution.Partial,
            EvidenceFor(context.Document, semantic.SyntaxTree, invocation.Syntax),
            [new RelationDetail(nameKey, nameValue), new RelationDetail(valueKey, valueValue)]));
    }

    private static void ObserveNamedClientCreation(
        FactDocumentDetectionContext context,
        SemanticDetectionDocument semantic,
        IInvocationOperation invocation,
        ImmutableArray<Observation>.Builder observations)
    {
        if (TryNamedClientOf(invocation) is not { } named)
        {
            return;
        }

        observations.Add(new Observation(
            NamedClientKind,
            FactResolution.Exact,
            EvidenceFor(context.Document, semantic.SyntaxTree, invocation.Syntax),
            [new RelationDetail("client_name", named.Name)]));
    }

    private static void ObserveAssignment(
        FactDocumentDetectionContext context,
        SemanticDetectionDocument semantic,
        ISimpleAssignmentOperation assignment,
        ImmutableArray<Observation>.Builder observations)
    {
        if (assignment.Target is not IPropertyReferenceOperation property
            || property.Instance?.Type is not { } receiverType
            || !IsHttpClient(receiverType))
        {
            return;
        }

        var evidence = EvidenceFor(context.Document, semantic.SyntaxTree, assignment.Syntax);
        if (property.Property.Name == "BaseAddress")
        {
            var uriOperand = UriConstructorArgument(assignment.Value)?.Value ?? assignment.Value;
            var (key, value, resolution) = ExpressionValue(uriOperand, "base_url", "base_url_expression");
            observations.Add(new Observation(BaseAddressKind, resolution, evidence, [new RelationDetail(key, value)]));
        }
        else if (property.Property.Name == "Timeout")
        {
            observations.Add(new Observation(
                TimeoutKind,
                FactResolution.Exact,
                evidence,
                [new RelationDetail("timeout", assignment.Value.Syntax.ToString())]));
        }
    }

    /// <summary>
    /// <c>BaseAddress = new Uri("...")</c>: the literal is inside the constructor call, not the
    /// assignment's own constant value, so it needs its own unwrap distinct from a boxing conversion.
    /// </summary>
    private static IArgumentOperation? UriConstructorArgument(IOperation value) =>
        value is IObjectCreationOperation { Arguments: [var first, ..] } ? first : null;

    private static (string Name, SyntaxNode Syntax)? TryNamedClientOf(IOperation operation)
    {
        if (operation is not IInvocationOperation { TargetMethod.Name: "CreateClient" } invocation
            || FullyQualified(invocation.TargetMethod.ContainingType) != HttpClientFactoryTypeName)
        {
            return null;
        }

        return invocation.Arguments.FirstOrDefault()?.Value.ConstantValue is { HasValue: true, Value: string name }
            ? (name, invocation.Syntax)
            : null;
    }

    private static bool IsHttpClient(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (FullyQualified(current) == HttpClientTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private static RelationFact CreateFact(
        FactDocumentDetectionContext context,
        Observation observation,
        Dictionary<string, int> ordinals)
    {
        var details = observation.Details.Distinct().Order().ToImmutableArray();
        var claim = string.Join('|', details.Select(static detail => $"{detail.Key}={detail.Value}"));
        var ordinalKey = $"{observation.Kind}\0{claim}";
        var ordinal = ordinals.GetValueOrDefault(ordinalKey) + 1;
        ordinals[ordinalKey] = ordinal;
        var id = RelationFactId.Create(context.Document.DocumentId.ToFactId(), observation.Kind, claim, ordinal);
        var header = FactHeader.Create(
            id.ToFactId(),
            FactKind.Relation,
            observation.Resolution,
            evidence: [observation.Evidence]);
        return new RelationFact(
            header,
            id,
            context.Document.DocumentId.ToFactId(),
            null,
            RelationPartition.Http,
            observation.Kind,
            "HTTP client source observation does not identify a remote service target.",
            details);
    }

    private static (string Key, string Value, FactResolution Resolution) ExpressionValue(
        IArgumentOperation? argument,
        string constantKey,
        string expressionKey) =>
        ExpressionValue(argument?.Value, constantKey, expressionKey);

    private static (string Key, string Value, FactResolution Resolution) ExpressionValue(
        IOperation? operand,
        string constantKey,
        string expressionKey)
    {
        var value = operand is IConversionOperation conversion ? conversion.Operand : operand;
        if (value?.ConstantValue is { HasValue: true, Value: string constant })
        {
            return (constantKey, constant, FactResolution.Exact);
        }

        return (expressionKey, operand?.Syntax.ToString() ?? "<missing>", FactResolution.Partial);
    }

    private static Evidence EvidenceFor(DocumentFact document, SyntaxTree syntaxTree, SyntaxNode node)
    {
        var span = syntaxTree.GetLineSpan(node.Span);
        return new Evidence(
            document.DocumentId,
            document.RelativePath,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }

    private static string FullyQualified(ISymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);

    private sealed record Observation(
        string Kind,
        FactResolution Resolution,
        Evidence Evidence,
        ImmutableArray<RelationDetail> Details);
}
