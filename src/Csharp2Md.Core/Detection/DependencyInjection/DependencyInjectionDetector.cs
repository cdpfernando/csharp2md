using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using FactDocumentDetectionContext = Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext;

namespace Csharp2Md.Core.Detection.DependencyInjection;

internal sealed class DependencyInjectionDetector : IDocumentFactDetector
{
    private const string DiNamespace = "Microsoft.Extensions.DependencyInjection";
    private const string ServiceCollectionTypeName = $"{DiNamespace}.IServiceCollection";
    private const string RegistrationKind = "di-registration";
    private const string ExpansionKind = "di-registration-expansion";

    private static readonly ImmutableDictionary<string, string> LifetimeMethods =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AddSingleton"] = "singleton",
            ["AddScoped"] = "scoped",
            ["AddTransient"] = "transient",
            ["AddKeyedSingleton"] = "singleton",
            ["AddKeyedScoped"] = "scoped",
            ["AddKeyedTransient"] = "transient",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public DetectorDescriptor Descriptor { get; } = DetectorDescriptor.Create(
        DetectorId.Create("io.csharp2md.dependency-injection"),
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
        foreach (var syntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semantic.SemanticModel.GetOperation(syntax) is not IInvocationOperation invocation)
            {
                continue;
            }

            Observe(context, semantic, invocation, observations);
        }

        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var facts = observations
            .OrderBy(static observation => observation.Evidence[0])
            .ThenBy(static observation => observation.Kind, StringComparer.Ordinal)
            .Select(observation => CreateFact(context, observation, ordinals))
            .ToImmutableArray();
        return DetectorResult.Create(facts);
    }

    private static void Observe(
        FactDocumentDetectionContext context,
        SemanticDetectionDocument semantic,
        IInvocationOperation invocation,
        ImmutableArray<Observation>.Builder observations)
    {
        var method = invocation.TargetMethod;
        if (!method.IsExtensionMethod)
        {
            return;
        }

        var definitionMethod = method.ReducedFrom ?? method;
        if (definitionMethod.Parameters.Length == 0
            || FullyQualified(definitionMethod.Parameters[0].Type) != ServiceCollectionTypeName)
        {
            return;
        }

        var evidence = EvidenceFor(context.Document, semantic.SyntaxTree, invocation.Syntax);
        var containingNamespace = definitionMethod.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!string.Equals(containingNamespace, DiNamespace, StringComparison.Ordinal))
        {
            var definitionEvidence = DefinitionEvidenceFor(context, semantic, definitionMethod);
            var declaringType = definitionMethod.ContainingType is { } type ? FullyQualified(type) : "<unknown>";
            observations.Add(new Observation(
                ExpansionKind,
                FactResolution.Exact,
                definitionEvidence is null ? [evidence] : [evidence, definitionEvidence.Value],
                [new RelationDetail("method", $"{ShortName(declaringType)}.{method.Name}")]));
            return;
        }

        if (!LifetimeMethods.TryGetValue(method.Name, out var lifetime))
        {
            return;
        }

        var details = new List<RelationDetail> { new("lifetime", lifetime) };
        var resolution = FactResolution.Exact;

        var (serviceType, implementationType) = TypeArgumentsOf(invocation);
        if (serviceType is not null)
        {
            details.Add(new RelationDetail("service", TypeDisplay(serviceType)));
            if (serviceType is INamedTypeSymbol { IsUnboundGenericType: true })
            {
                details.Add(new RelationDetail("open_generic", "true"));
            }
        }

        var factoryArgument = FindArgument(invocation, "implementationFactory");
        if (factoryArgument is not null)
        {
            details.Add(new RelationDetail("factory", factoryArgument.Value.Syntax.ToString()));
            resolution = FactResolution.Partial;
        }
        else if (implementationType is not null)
        {
            details.Add(new RelationDetail("implementation", TypeDisplay(implementationType)));
        }

        if (method.Name.StartsWith("AddKeyed", StringComparison.Ordinal))
        {
            var keyArgument = FindArgument(invocation, "serviceKey");
            var (keyKey, keyValue, keyResolution) = ExpressionValue(keyArgument, "key", "key_expression");
            details.Add(new RelationDetail(keyKey, keyValue));
            if (keyResolution == FactResolution.Partial)
            {
                resolution = FactResolution.Partial;
            }
        }

        observations.Add(new Observation(RegistrationKind, resolution, [evidence], details.ToImmutableArray()));
    }

    private static (ITypeSymbol? Service, ITypeSymbol? Implementation) TypeArgumentsOf(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (method.TypeArguments.Length == 2)
        {
            return (method.TypeArguments[0], method.TypeArguments[1]);
        }

        if (method.TypeArguments.Length == 1)
        {
            return (method.TypeArguments[0], method.TypeArguments[0]);
        }

        var serviceType = TypeOfArgument(FindArgument(invocation, "serviceType"));
        var implementationType = TypeOfArgument(FindArgument(invocation, "implementationType"));
        return (serviceType, implementationType);
    }

    private static ITypeSymbol? TypeOfArgument(IArgumentOperation? argument) =>
        argument?.Value is ITypeOfOperation typeOf ? typeOf.TypeOperand : null;

    private static Evidence? DefinitionEvidenceFor(
        FactDocumentDetectionContext context,
        SemanticDetectionDocument semantic,
        IMethodSymbol definitionMethod)
    {
        var reference = definitionMethod.DeclaringSyntaxReferences.FirstOrDefault();
        if (reference is null || reference.SyntaxTree != semantic.SyntaxTree)
        {
            return null;
        }

        return EvidenceFor(context.Document, semantic.SyntaxTree, reference.GetSyntax());
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
            evidence: observation.Evidence);
        return new RelationFact(
            header,
            id,
            context.Document.DocumentId.ToFactId(),
            null,
            RelationPartition.DependencyInjection,
            observation.Kind,
            "Dependency-injection registration does not identify a remote service target.",
            details);
    }

    private static IArgumentOperation? FindArgument(IInvocationOperation invocation, string parameterName) =>
        invocation.Arguments.FirstOrDefault(argument => string.Equals(argument.Parameter?.Name, parameterName, StringComparison.Ordinal));

    private static (string Key, string Value, FactResolution Resolution) ExpressionValue(
        IArgumentOperation? argument,
        string constantKey,
        string expressionKey)
    {
        var value = argument?.Value is IConversionOperation conversion ? conversion.Operand : argument?.Value;
        if (value?.ConstantValue is { HasValue: true, Value: string constant })
        {
            return (constantKey, constant, FactResolution.Exact);
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

    private static string TypeDisplay(ITypeSymbol type) => FullyQualified(type);

    private static string FullyQualified(ISymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);

    private static string ShortName(string fullyQualifiedTypeName)
    {
        var lastDot = fullyQualifiedTypeName.LastIndexOf('.');
        return lastDot < 0 ? fullyQualifiedTypeName : fullyQualifiedTypeName[(lastDot + 1)..];
    }

    private sealed record Observation(
        string Kind,
        FactResolution Resolution,
        ImmutableArray<Evidence> Evidence,
        ImmutableArray<RelationDetail> Details);
}
