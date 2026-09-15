using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.Extraction;

internal sealed record ConfigurationPersistenceExtractionInput(SolutionIdentity Solution, ProjectIdentity Project, AnalysisVariant Variant, Compilation Compilation);
internal sealed record ConfigurationPersistenceExtractionResult(ImmutableArray<LogicalEntity> Entities, ImmutableArray<VariantOccurrence> Occurrences, ImmutableArray<EvidenceRecord> Evidence, ImmutableArray<FactualRelation> Relations);

internal static class ConfigurationPersistenceExtractor
{
    public static ConfigurationPersistenceExtractionResult Extract(ConfigurationPersistenceExtractionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var entities = new Dictionary<string, LogicalEntity>(StringComparer.Ordinal);
        var occurrences = new List<VariantOccurrence>(); var evidence = new List<EvidenceRecord>(); var relations = new List<FactualRelation>();
        void Add(EntityKind kind, string name, LogicalLocator locator, string category, string payload, string? source = null)
        {
            var digest = Digest(category + ":" + payload); var proof = "evidence:" + digest;
            evidence.Add(new EvidenceRecord(proof, CanonicalIdentity.CreateDocumentKey(input.Solution, locator.RelativePath), input.Variant, locator.Span, digest));
            var key = CanonicalIdentity.CreateEntityKey(input.Solution, kind, name);
            if (entities.TryAdd(key, new LogicalEntity(kind, key, name, name))) occurrences.Add(new VariantOccurrence(key, input.Project, input.Variant, locator, category, [proof]));
            if (source is not null) relations.Add(new FactualRelation("relation:" + Digest(category + ":" + source + ":" + key + ":" + locator.Span.StartLine), source, key, category, [proof]));
        }
        foreach (var tree in input.Compilation.SyntaxTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal))
        {
            var path = Path.GetFileName(tree.FilePath); if (string.IsNullOrWhiteSpace(path)) continue;
            var model = input.Compilation.GetSemanticModel(tree); var root = tree.GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) continue;
                var locator = new LogicalLocator(path, SpanOf(invocation), input.Project); var owner = model.GetEnclosingSymbol(invocation.SpanStart) as IMethodSymbol;
                var ownerKey = owner is null ? null : CanonicalIdentity.CreateEntityKey(input.Solution, EntityKind.Callable, owner.ToDisplayString());
                var literal = invocation.ArgumentList.Arguments.Select(a => a.Expression).OfType<LiteralExpressionSyntax>().FirstOrDefault(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))?.Token.ValueText;
                if (method.Name is "GetSection" or "GetValue" or "GetConnectionString" or "Configure")
                {
                    if (!string.IsNullOrWhiteSpace(literal)) Add(EntityKind.Configuration, method.Name == "GetSection" ? "section:" + literal : "key:" + literal, locator, "configuration", method.Name + ":" + literal, ownerKey);
                    continue;
                }
                var receiver = method.ReceiverType ?? method.ContainingType;
                if (IsDbContext(receiver) || IsDbSet(receiver))
                {
                    var store = receiver?.ToDisplayString() ?? "persistence";
                    Add(EntityKind.DataStore, store, locator, "persistence-store", store, ownerKey);
                    Add(EntityKind.DataOperation, method.Name, locator, "persistence-operation", method.Name, ownerKey);
                    if (IsDbSet(receiver) && receiver is INamedTypeSymbol { TypeArguments: [var entity] }) Add(EntityKind.DataObject, entity.ToDisplayString(), locator, "persistence-object", entity.ToDisplayString(), ownerKey);
                }
            }
        }
        return new(entities.Values.OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(), occurrences.OrderBy(x => x.EntityCanonicalKey, StringComparer.Ordinal).ToImmutableArray(), evidence.DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(), relations.OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray());
    }
    private static bool IsDbContext(ITypeSymbol? type) => type?.ToDisplayString().Contains("DbContext", StringComparison.Ordinal) == true;
    private static bool IsDbSet(ITypeSymbol? type) => type?.ToDisplayString().Contains("DbSet<", StringComparison.Ordinal) == true;
    private static SourceSpan SpanOf(SyntaxNode node) { var s=node.GetLocation().GetLineSpan(); return new(s.StartLinePosition.Line+1,s.StartLinePosition.Character+1,s.EndLinePosition.Line+1,s.EndLinePosition.Character+1); }
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
