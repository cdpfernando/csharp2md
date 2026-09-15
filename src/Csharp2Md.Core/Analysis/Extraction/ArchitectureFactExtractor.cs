using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Csharp2Md.Core.Analysis.Inventory;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.Extraction;

internal sealed record ArchitectureExtractionInput(
    SolutionIdentity Solution,
    ProjectIdentity Project,
    AnalysisVariant Variant,
    ImmutableArray<InventoriedSourceDocument> Documents,
    string? ProjectFileContents,
    Compilation? Compilation);

internal sealed record ArchitectureExtractionResult(
    ImmutableArray<LogicalEntity> Entities,
    ImmutableArray<VariantOccurrence> Occurrences,
    ImmutableArray<EvidenceRecord> Evidence);

internal static class ArchitectureFactExtractor
{
    private const string ControllerBaseMetadata = "ControllerBase";
    private const string WebApplicationTypeName = "WebApplication";
    private const string CreateBuilderName = "CreateBuilder";

    public static ArchitectureExtractionResult Extract(ArchitectureExtractionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Solution);
        ArgumentNullException.ThrowIfNull(input.Project);
        ArgumentNullException.ThrowIfNull(input.Variant);

        var entities = new Dictionary<string, LogicalEntity>(StringComparer.Ordinal);
        var occurrences = new List<VariantOccurrence>();
        var evidence = new List<EvidenceRecord>();

        void AddEntity(LogicalEntity entity, LogicalLocator locator, string shapeDigest, string evidenceKey)
        {
            entities[entity.CanonicalKey] = entity;
            occurrences.Add(new VariantOccurrence(
                entity.CanonicalKey,
                input.Project,
                input.Variant,
                locator,
                shapeDigest,
                ImmutableArray.Create(evidenceKey)));
        }

        EvidenceRecord AddEvidence(string kind, LogicalLocator locator, string payload)
        {
            var digest = Sha256(kind + ":" + payload);
            var key = "evidence:" + digest;
            var record = new EvidenceRecord(key, DocumentKey(input.Solution, locator.RelativePath), input.Variant, locator.Span, digest);
            evidence.Add(record);
            return record;
        }

        var projectLocator = new LogicalLocator(input.Project.LogicalRelativePath, new SourceSpan(1, 1, 1, 1), input.Project);
        var solutionEvidence = AddEvidence("solution", projectLocator, input.Solution.CanonicalKey);
        AddEntity(
            new LogicalEntity(EntityKind.Solution, input.Solution.CanonicalKey, DisplayName(input.Solution.LogicalRelativePath), input.Solution.LogicalRelativePath),
            projectLocator,
            "solution",
            solutionEvidence.CanonicalKey);

        var projectEvidence = AddEvidence("project", projectLocator, input.Project.LogicalRelativePath);
        AddEntity(
            new LogicalEntity(EntityKind.Project, input.Project.CanonicalKey, DisplayName(input.Project.LogicalRelativePath), input.Project.LogicalRelativePath),
            projectLocator,
            "project",
            projectEvidence.CanonicalKey);

        foreach (var document in input.Documents.IsDefaultOrEmpty ? [] : input.Documents)
        {
            var locator = new LogicalLocator(document.RelativePath, new SourceSpan(1, 1, 1, 1), input.Project);
            var documentEvidence = AddEvidence(
                document.IsTest ? "document:test" : "document:production",
                locator,
                document.ContentDigest);
            var documentKey = CanonicalIdentity.CreateDocumentKey(input.Solution, document.RelativePath);
            AddEntity(
                new LogicalEntity(EntityKind.Document, documentKey, Path.GetFileName(document.RelativePath), document.RelativePath),
                locator,
                document.IsTest ? "document:test" : "document:production",
                documentEvidence.CanonicalKey);
        }

        var outputType = ReadOutputType(input.ProjectFileContents);
        var isExecutable = string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase);

        var hostBootstrap = false;
        var hasController = false;

        if (input.Compilation is not null)
        {
            foreach (var tree in input.Compilation.SyntaxTrees.OrderBy(tree => tree.FilePath, StringComparer.Ordinal))
            {
                var model = input.Compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();
                var relativePath = ToLogicalDocumentPath(input.Project, tree.FilePath);
                if (relativePath is null)
                {
                    continue;
                }

                foreach (var type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(type) is not INamedTypeSymbol typeSymbol)
                    {
                        continue;
                    }

                    var span = SpanOf(type);
                    var locator = new LogicalLocator(relativePath, span, input.Project);
                    var typeKey = CanonicalIdentity.CreateEntityKey(input.Solution, EntityKind.Symbol, typeSymbol.ToDisplayString());
                    var typeEvidence = AddEvidence("symbol:named-type", locator, typeSymbol.ToDisplayString());
                    AddEntity(
                        new LogicalEntity(EntityKind.Symbol, typeKey, typeSymbol.Name, typeSymbol.ToDisplayString()),
                        locator,
                        "symbol:named-type",
                        typeEvidence.CanonicalKey);

                    if (InheritsControllerBase(typeSymbol))
                    {
                        hasController = true;
                        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>()
                            .Where(static method => method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public)
                            .OrderBy(static method => method.Name, StringComparer.Ordinal))
                        {
                            if (!HasHttpBoundaryAttribute(method))
                            {
                                continue;
                            }

                            var methodSyntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
                            var methodSpan = methodSyntax is null ? span : SpanOf(methodSyntax);
                            var methodLocator = new LogicalLocator(relativePath, methodSpan, input.Project);
                            var entryKey = CanonicalIdentity.CreateEntityKey(input.Solution, EntityKind.EntryPoint, method.ToDisplayString());
                            var entryEvidence = AddEvidence("entrypoint:http-action", methodLocator, method.ToDisplayString());
                            AddEntity(
                                new LogicalEntity(EntityKind.EntryPoint, entryKey, method.Name, method.ToDisplayString()),
                                methodLocator,
                                "entrypoint:http-action",
                                entryEvidence.CanonicalKey);

                            var boundaryKey = CanonicalIdentity.CreateEntityKey(input.Solution, EntityKind.BoundaryOperation, "http:" + method.ToDisplayString());
                            var boundaryEvidence = AddEvidence("boundary:http", methodLocator, method.ToDisplayString());
                            AddEntity(
                                new LogicalEntity(EntityKind.BoundaryOperation, boundaryKey, method.Name, "http:" + method.ToDisplayString()),
                                methodLocator,
                                "boundary:http",
                                boundaryEvidence.CanonicalKey);
                        }
                    }
                }

                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                    {
                        continue;
                    }

                    if (string.Equals(method.Name, CreateBuilderName, StringComparison.Ordinal)
                        && method.ContainingType?.Name == WebApplicationTypeName)
                    {
                        hostBootstrap = true;
                        var locator = new LogicalLocator(relativePath, SpanOf(invocation), input.Project);
                        _ = AddEvidence("host:webapplication-createbuilder", locator, method.ToDisplayString());
                    }
                }

                foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol)
                    {
                        continue;
                    }

                    if (!string.Equals(methodSymbol.Name, "Main", StringComparison.Ordinal)
                        || !methodSymbol.IsStatic)
                    {
                        continue;
                    }

                    var locator = new LogicalLocator(relativePath, SpanOf(method), input.Project);
                    var entryKey = CanonicalIdentity.CreateEntityKey(input.Solution, EntityKind.EntryPoint, methodSymbol.ToDisplayString());
                    var entryEvidence = AddEvidence("entrypoint:main", locator, methodSymbol.ToDisplayString());
                    AddEntity(
                        new LogicalEntity(EntityKind.EntryPoint, entryKey, "Main", methodSymbol.ToDisplayString()),
                        locator,
                        "entrypoint:main",
                        entryEvidence.CanonicalKey);
                }
            }
        }

        // DEP-08: Deployment Units require approved project-file or host evidence — never name alone.
        var deploymentEvidenceKind = hostBootstrap
            ? "deployment:host-bootstrap"
            : isExecutable
                ? "deployment:output-type-exe"
                : null;
        if (deploymentEvidenceKind is not null)
        {
            var deploymentEvidence = AddEvidence(deploymentEvidenceKind, projectLocator, input.Project.LogicalRelativePath);
            var deploymentKey = CanonicalIdentity.CreateEntityKey(
                input.Solution,
                EntityKind.DeploymentUnit,
                input.Project.LogicalRelativePath);
            AddEntity(
                new LogicalEntity(
                    EntityKind.DeploymentUnit,
                    deploymentKey,
                    DisplayName(input.Project.LogicalRelativePath),
                    input.Project.LogicalRelativePath),
                projectLocator,
                deploymentEvidenceKind,
                deploymentEvidence.CanonicalKey);
        }

        if (isExecutable || hostBootstrap || hasController)
        {
            var componentEvidence = AddEvidence(
                hasController ? "component:controller-surface" : "component:deployable-application",
                projectLocator,
                input.Project.LogicalRelativePath);
            var componentKey = CanonicalIdentity.CreateEntityKey(
                input.Solution,
                EntityKind.Component,
                input.Project.LogicalRelativePath);
            AddEntity(
                new LogicalEntity(
                    EntityKind.Component,
                    componentKey,
                    DisplayName(input.Project.LogicalRelativePath),
                    input.Project.LogicalRelativePath),
                projectLocator,
                hasController ? "component:controller-surface" : "component:deployable-application",
                componentEvidence.CanonicalKey);
        }

        return new ArchitectureExtractionResult(
            entities.Values.OrderBy(entity => entity.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            occurrences
                .OrderBy(occurrence => occurrence.EntityCanonicalKey, StringComparer.Ordinal)
                .ThenBy(occurrence => occurrence.ShapeDigest, StringComparer.Ordinal)
                .ToImmutableArray(),
            evidence.OrderBy(record => record.CanonicalKey, StringComparer.Ordinal).ToImmutableArray());
    }

    internal static string? ReadOutputType(string? projectFileContents)
    {
        if (string.IsNullOrWhiteSpace(projectFileContents))
        {
            return null;
        }

        var document = XDocument.Parse(projectFileContents);
        return document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "OutputType")
            ?.Value
            ?.Trim();
    }

    internal static bool LooksLikeDeploymentUnitName(string candidate) =>
        candidate.Contains("Service", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("Api", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("Worker", StringComparison.OrdinalIgnoreCase);

    private static bool InheritsControllerBase(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.Name, ControllerBaseMetadata, StringComparison.Ordinal)
                || string.Equals(current.ToDisplayString(), "Microsoft.AspNetCore.Mvc.ControllerBase", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHttpBoundaryAttribute(IMethodSymbol method) =>
        method.GetAttributes().Any(attribute =>
        {
            var name = attribute.AttributeClass?.Name ?? string.Empty;
            return name.StartsWith("Http", StringComparison.Ordinal)
                || name is "RouteAttribute" or "Route";
        });

    private static string? ToLogicalDocumentPath(ProjectIdentity project, string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(absolutePath);
            if (!Path.IsPathRooted(absolutePath) && absolutePath.Contains('/', StringComparison.Ordinal))
            {
                return LogicalPath.RequireRelative(absolutePath.Replace('\\', '/'), nameof(absolutePath));
            }

            var fileName = Path.GetFileName(full);
            var projectDirectory = Path.GetDirectoryName(project.LogicalRelativePath)?.Replace('\\', '/') ?? string.Empty;
            var candidate = string.IsNullOrEmpty(projectDirectory) ? fileName : projectDirectory + "/" + fileName;
            return LogicalPath.RequireRelative(candidate, nameof(absolutePath));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string DocumentKey(SolutionIdentity solution, string relativePath)
    {
        try
        {
            return CanonicalIdentity.CreateDocumentKey(solution, relativePath);
        }
        catch (ArgumentException)
        {
            return CanonicalIdentity.CreateDocumentKey(solution, "unknown.cs");
        }
    }

    private static SourceSpan SpanOf(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return new SourceSpan(
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1,
            Math.Max(1, span.EndLinePosition.Character + 1));
    }

    private static string DisplayName(string logicalRelativePath) =>
        Path.GetFileNameWithoutExtension(logicalRelativePath.Replace('\\', '/'));

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
