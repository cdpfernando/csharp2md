using Csharp2Md.Core.Analysis.Classification;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using FactDocumentDetectionContext = Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext;

namespace Csharp2Md.Core.Detection.AspNetCore;

internal sealed class AspNetCoreDetector : IDocumentFactDetector
{
    private const string AspNetCoreNamespace = "Microsoft.AspNetCore.";
    private const string MvcNamespace = "Microsoft.AspNetCore.Mvc";
    private const string FiltersNamespace = "Microsoft.AspNetCore.Mvc.Filters";
    private const string BuilderNamespace = "Microsoft.AspNetCore.Builder";
    private const string AuthorizationNamespace = "Microsoft.AspNetCore.Authorization";
    private const string HealthChecksNamespace = "Microsoft.AspNetCore.Diagnostics.HealthChecks";

    private static readonly ImmutableDictionary<string, string> HttpAttributes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"{MvcNamespace}.HttpGetAttribute"] = "GET",
            [$"{MvcNamespace}.HttpPostAttribute"] = "POST",
            [$"{MvcNamespace}.HttpPutAttribute"] = "PUT",
            [$"{MvcNamespace}.HttpDeleteAttribute"] = "DELETE",
            [$"{MvcNamespace}.HttpPatchAttribute"] = "PATCH",
            [$"{MvcNamespace}.HttpHeadAttribute"] = "HEAD",
            [$"{MvcNamespace}.HttpOptionsAttribute"] = "OPTIONS",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static readonly ImmutableDictionary<string, string> MinimalApiMethods =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MapGet"] = "GET",
            ["MapPost"] = "POST",
            ["MapPut"] = "PUT",
            ["MapDelete"] = "DELETE",
            ["MapPatch"] = "PATCH",
            ["MapMethods"] = "MULTIPLE",
            ["Map"] = "ANY",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public DetectorDescriptor Descriptor { get; } = DetectorDescriptor.Create(
        DetectorId.Create("io.csharp2md.aspnet-core"),
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
        DetectControllers(context, semantic.SemanticModel, root, observations);
        DetectInvocations(context, semantic.SemanticModel, root, observations);

        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var facts = observations
            .OrderBy(static observation => observation.Evidence)
            .ThenBy(static observation => observation.Kind, StringComparer.Ordinal)
            .Select(observation => CreateFact(context, observation, ordinals))
            .ToImmutableArray();
        return DetectorResult.Create(facts);
    }

    private static void DetectControllers(
        FactDocumentDetectionContext context,
        SemanticModel semanticModel,
        SyntaxNode root,
        ImmutableArray<Observation>.Builder observations)
    {
        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type || !IsController(type))
            {
                continue;
            }

            observations.Add(new Observation(
                "aspnet-controller",
                FactResolution.Exact,
                EvidenceFor(context.Document, semanticModel.SyntaxTree, declaration),
                [new RelationDetail("controller", FullyQualified(type))]));
            AddMetadataObservations(context, semanticModel.SyntaxTree, type, declaration, "controller", observations);

            foreach (var methodDeclaration in declaration.Members.OfType<MethodDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol method || !IsAction(method))
                {
                    continue;
                }

                var route = Route(method.GetAttributes());
                var httpMethod = HttpMethod(method.GetAttributes());
                var details = new List<RelationDetail>
                {
                    new("action", method.Name),
                    new("controller", FullyQualified(type)),
                    new("http_method", httpMethod ?? "ANY"),
                };
                if (route is not null)
                {
                    details.Add(new RelationDetail("route", route));
                }

                observations.Add(new Observation(
                    ProjectClassifier.HttpEndpointRelationKind,
                    FactResolution.Exact,
                    EvidenceFor(context.Document, semanticModel.SyntaxTree, methodDeclaration),
                    details.ToImmutableArray()));
                AddMetadataObservations(context, semanticModel.SyntaxTree, method, methodDeclaration, "action", observations);
            }
        }
    }

    private static void DetectInvocations(
        FactDocumentDetectionContext context,
        SemanticModel semanticModel,
        SyntaxNode root,
        ImmutableArray<Observation>.Builder observations)
    {
        foreach (var syntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetOperation(syntax) is not IInvocationOperation invocation)
            {
                continue;
            }

            var method = invocation.TargetMethod;
            var containingNamespace = method.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            var evidence = EvidenceFor(context.Document, semanticModel.SyntaxTree, syntax);

            if (MinimalApiMethods.TryGetValue(method.Name, out var httpMethod)
                && string.Equals(containingNamespace, BuilderNamespace, StringComparison.Ordinal))
            {
                var routeArgument = FindArgument(invocation, "pattern") ?? invocation.Arguments.FirstOrDefault();
                var (routeKey, routeValue, resolution) = ExpressionValue(routeArgument);
                observations.Add(new Observation(
                    ProjectClassifier.HttpEndpointRelationKind,
                    resolution,
                    evidence,
                    [
                        new RelationDetail("endpoint_style", "minimal-api"),
                        new RelationDetail("http_method", httpMethod),
                        new RelationDetail(routeKey, routeValue),
                    ]));
            }

            if (method.Name == "MapHealthChecks"
                && string.Equals(containingNamespace, HealthChecksNamespace, StringComparison.Ordinal))
            {
                var routeArgument = FindArgument(invocation, "pattern") ?? invocation.Arguments.FirstOrDefault();
                var (routeKey, routeValue, resolution) = ExpressionValue(routeArgument);
                observations.Add(new Observation(
                    "aspnet-health-check",
                    resolution,
                    evidence,
                    [new RelationDetail(routeKey, routeValue)]));
            }

            if (method.Name == "RequireAuthorization"
                && string.Equals(containingNamespace, BuilderNamespace, StringComparison.Ordinal))
            {
                var policy = FindArgument(invocation, "policyNames") ?? invocation.Arguments.FirstOrDefault();
                var (policyKey, policyValue, resolution) = ExpressionValue(policy, "policy", "policy_expression");
                observations.Add(new Observation(
                    "aspnet-authorization",
                    resolution,
                    evidence,
                    [new RelationDetail(policyKey, policyValue)]));
            }

            if (method.Name == "AddPolicy"
                && containingNamespace.StartsWith(AuthorizationNamespace, StringComparison.Ordinal))
            {
                var (policyKey, policyValue, resolution) = ExpressionValue(
                    FindArgument(invocation, "name") ?? invocation.Arguments.FirstOrDefault(),
                    "policy",
                    "policy_expression");
                observations.Add(new Observation(
                    "aspnet-policy",
                    resolution,
                    evidence,
                    [new RelationDetail(policyKey, policyValue)]));
            }

            if (method.Name is "AddEndpointFilter" or "AddEndpointFilterFactory"
                && string.Equals(containingNamespace, BuilderNamespace, StringComparison.Ordinal))
            {
                var filter = invocation.Arguments.LastOrDefault();
                observations.Add(new Observation(
                    "aspnet-filter",
                    FactResolution.Exact,
                    evidence,
                    [new RelationDetail("filter", filter?.Value.Type is null ? filter?.Syntax.ToString() ?? "unknown" : FullyQualified(filter.Value.Type))]));
            }

            if (IsEntrypoint(method, invocation))
            {
                observations.Add(new Observation(
                    "aspnet-entrypoint",
                    FactResolution.Exact,
                    evidence,
                    [new RelationDetail("operation", method.Name)]));
            }
        }
    }

    private static void AddMetadataObservations(
        FactDocumentDetectionContext context,
        SyntaxTree syntaxTree,
        ISymbol symbol,
        SyntaxNode declaration,
        string appliesTo,
        ImmutableArray<Observation>.Builder observations)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var type = attribute.AttributeClass;
            var typeName = type is null ? string.Empty : FullyQualified(type);
            if (typeName == $"{AuthorizationNamespace}.AuthorizeAttribute")
            {
                var policy = attribute.NamedArguments
                    .FirstOrDefault(static pair => pair.Key == "Policy")
                    .Value.Value as string;
                observations.Add(new Observation(
                    "aspnet-authorization",
                    FactResolution.Exact,
                    EvidenceFor(context.Document, syntaxTree, attribute.ApplicationSyntaxReference?.GetSyntax() ?? declaration),
                    [
                        new RelationDetail("applies_to", appliesTo),
                        new RelationDetail("policy", policy ?? "default"),
                    ]));
            }
            else if (typeName == $"{AuthorizationNamespace}.AllowAnonymousAttribute")
            {
                observations.Add(new Observation(
                    "aspnet-authorization",
                    FactResolution.Exact,
                    EvidenceFor(context.Document, syntaxTree, attribute.ApplicationSyntaxReference?.GetSyntax() ?? declaration),
                    [
                        new RelationDetail("applies_to", appliesTo),
                        new RelationDetail("policy", "allow-anonymous"),
                    ]));
            }

            if (type is not null && Implements(type, $"{FiltersNamespace}.IFilterMetadata"))
            {
                observations.Add(new Observation(
                    "aspnet-filter",
                    FactResolution.Exact,
                    EvidenceFor(context.Document, syntaxTree, attribute.ApplicationSyntaxReference?.GetSyntax() ?? declaration),
                    [
                        new RelationDetail("applies_to", appliesTo),
                        new RelationDetail("filter", typeName),
                    ]));
            }
        }
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
            "ASP.NET source observation does not identify a remote service target.",
            details);
    }

    private static bool IsController(INamedTypeSymbol type) =>
        Inherits(type, $"{MvcNamespace}.ControllerBase")
        || HasAttribute(type, $"{MvcNamespace}.ApiControllerAttribute");

    private static bool IsAction(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary
        && method.DeclaredAccessibility == Accessibility.Public
        && !method.IsStatic
        && !HasAttribute(method, $"{MvcNamespace}.NonActionAttribute");

    private static bool IsEntrypoint(IMethodSymbol method, IInvocationOperation invocation) =>
        method.Name == "CreateBuilder"
            && FullyQualified(method.ContainingType) == $"{BuilderNamespace}.WebApplication"
        || method.Name == "Run"
            && invocation.Instance?.Type is { } instance
            && FullyQualified(instance) == $"{BuilderNamespace}.WebApplication";

    private static bool Inherits(INamedTypeSymbol type, string metadataName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (FullyQualified(current) == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Implements(INamedTypeSymbol type, string metadataName) =>
        type.AllInterfaces.Any(@interface => FullyQualified(@interface) == metadataName);

    private static bool HasAttribute(ISymbol symbol, string metadataName) =>
        symbol.GetAttributes().Any(attribute => attribute.AttributeClass is { } type && FullyQualified(type) == metadataName);

    private static string? HttpMethod(ImmutableArray<AttributeData> attributes) =>
        attributes.Select(static attribute => attribute.AttributeClass)
            .OfType<INamedTypeSymbol>()
            .Select(FullyQualified)
            .Select(type => HttpAttributes.GetValueOrDefault(type))
            .FirstOrDefault(static value => value is not null);

    private static string? Route(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is not { } type)
            {
                continue;
            }

            var typeName = FullyQualified(type);
            if (typeName != $"{MvcNamespace}.RouteAttribute" && !HttpAttributes.ContainsKey(typeName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.FirstOrDefault().Value is string route)
            {
                return route;
            }
        }

        return null;
    }

    private static IArgumentOperation? FindArgument(IInvocationOperation invocation, string parameterName) =>
        invocation.Arguments.FirstOrDefault(argument => string.Equals(argument.Parameter?.Name, parameterName, StringComparison.Ordinal));

    private static (string Key, string Value, FactResolution Resolution) ExpressionValue(
        IArgumentOperation? argument,
        string constantKey = "route",
        string expressionKey = "route_expression")
    {
        if (argument?.Value.ConstantValue is { HasValue: true, Value: string constant })
        {
            return (constantKey, constant, FactResolution.Exact);
        }

        if (argument?.Value is IArrayCreationOperation { Initializer.ElementValues: [{ ConstantValue: { HasValue: true, Value: string firstElement } }, ..] })
        {
            return (constantKey, firstElement, FactResolution.Exact);
        }

        return (expressionKey, argument?.Value.Syntax.ToString() ?? "<missing>", FactResolution.Partial);
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
