using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.Extraction;

internal sealed record CausalRelationExtractionInput(
    SolutionIdentity Solution,
    ProjectIdentity Project,
    AnalysisVariant Variant,
    Compilation Compilation,
    ImmutableArray<ProjectIdentity> ReferencedProjects,
    ImmutableArray<ReferencedProjectCompilation> ReferencedCompilations);

internal sealed record CausalRelationExtractionResult(
    ImmutableArray<LogicalEntity> Entities,
    ImmutableArray<VariantOccurrence> Occurrences,
    ImmutableArray<EvidenceRecord> Evidence,
    ImmutableArray<FactualRelation> Relations,
    ImmutableArray<KnowledgeGap> Gaps);

internal static class CausalRelationExtractor
{
    private const string ProjectReference = "project-reference";
    private const string InternalInvocation = "internal-invocation";
    private const string StructuralTypeUse = "structural-type-use";
    private const string Http = "http";
    private const string Grpc = "grpc";
    private const string Messaging = "messaging";
    private const string Contract = "contract";

    public static CausalRelationExtractionResult Extract(CausalRelationExtractionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Solution);
        ArgumentNullException.ThrowIfNull(input.Project);
        ArgumentNullException.ThrowIfNull(input.Variant);
        ArgumentNullException.ThrowIfNull(input.Compilation);

        var entities = new Dictionary<string, LogicalEntity>(StringComparer.Ordinal);
        var occurrences = new List<VariantOccurrence>();
        var evidence = new List<EvidenceRecord>();
        var relations = new List<FactualRelation>();
        var gaps = new List<KnowledgeGap>();

        void AddEntity(LogicalEntity entity, LogicalLocator locator, string shape, string evidenceKey, ProjectIdentity? owner = null)
        {
            if (!entities.TryAdd(entity.CanonicalKey, entity))
            {
                return;
            }

            occurrences.Add(new VariantOccurrence(entity.CanonicalKey, owner ?? input.Project, input.Variant, locator, shape, [evidenceKey]));
        }

        EvidenceRecord AddEvidence(string kind, LogicalLocator locator, string payload)
        {
            var digest = Digest(kind + ":" + payload);
            var record = new EvidenceRecord(
                "evidence:" + digest,
                CanonicalIdentity.CreateDocumentKey(input.Solution, locator.RelativePath),
                input.Variant,
                locator.Span,
                digest);
            evidence.Add(record);
            return record;
        }

        string AddSymbol(ISymbol symbol, LogicalLocator locator, EvidenceRecord proof, ProjectIdentity? owner = null)
        {
            var kind = symbol is IMethodSymbol ? EntityKind.Callable : EntityKind.Symbol;
            var qualified = symbol.ToDisplayString();
            var display = string.IsNullOrWhiteSpace(symbol.Name) ? qualified : symbol.Name;
            // Qualified by the symbol's real owner, not just its display string: Roslyn renders a
            // top-level-statements entry point identically in every project, which otherwise collides
            // every host's Program into one entity and floods Component-scope lifting through it.
            var key = CanonicalIdentity.CreateEntityKey(input.Solution, kind, owner ?? input.Project, qualified);
            AddEntity(new LogicalEntity(kind, key, display, qualified), locator, "semantic:" + kind.ToString().ToLowerInvariant(), proof.CanonicalKey, owner);
            return key;
        }

        // PKG-05 excludes "usos de tipo nao retidos": a symbol with no declaration in the analyzed source
        // (BCL, NuGet, any other referenced assembly) is not retainable as a causal target. A symbol
        // reached through a same-solution ProjectReference is still IsInSource - Roslyn resolves it via a
        // CompilationReference to the referenced project's own compilation, not raw metadata - so a
        // genuine cross-component call or type use is unaffected.
        //
        // DEP-01 requires proven membership: a shared symbol declared in a directly-referenced project
        // must be attributed to that project, not to every root that happens to call it - otherwise
        // Component/DeploymentUnit-scope lifting crosses the citing root against every project that
        // shares the symbol instead of against its true owner (the eShop/STATE.md fan-out defect). The
        // declaration keeps its own locator (its real file and span), matching how a ProjectReference
        // target already names itself rather than the citing root.
        string? AddSymbolIfDeclaredInSource(ISymbol symbol, LogicalLocator locator, EvidenceRecord proof)
        {
            var declaration = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
            if (declaration is null)
            {
                return null;
            }

            var owner = SymbolOwnership.Resolve(symbol, input.Project, input.Compilation, input.ReferencedCompilations) ?? input.Project;
            var declarationLocator = owner.CanonicalKey == input.Project.CanonicalKey
                ? locator
                : SymbolOwnership.DeclarationLocator(symbol, owner);
            return AddSymbol(symbol, declarationLocator, proof, owner);
        }

        string AddNamed(EntityKind kind, string name, LogicalLocator locator, EvidenceRecord proof, ProjectIdentity? owner = null)
        {
            var key = CanonicalIdentity.CreateEntityKey(input.Solution, kind, name);
            AddEntity(new LogicalEntity(kind, key, name, name), locator, "causal:" + kind.ToString().ToLowerInvariant(), proof.CanonicalKey, owner);
            return key;
        }

        void AddRelation(string category, string source, string target, EvidenceRecord proof, string discriminator)
        {
            var key = "relation:" + Digest(category + ":" + source + ":" + target + ":" + discriminator);
            relations.Add(new FactualRelation(key, source, target, category, [proof.CanonicalKey]));
        }

        void AddGap(GapKind kind, string cause, string source, EvidenceRecord proof, string discriminator)
        {
            var key = "gap:" + Digest(kind + ":" + cause + ":" + source + ":" + discriminator);
            gaps.Add(new KnowledgeGap(key, kind, cause, [source], [proof.CanonicalKey]));
        }

        foreach (var referencedProject in input.ReferencedProjects.OrderBy(project => project.CanonicalKey, StringComparer.Ordinal))
        {
            var locator = new LogicalLocator(input.Project.LogicalRelativePath, new SourceSpan(1, 1, 1, 1), input.Project);
            // The payload must name both ends: two different roots referencing the same target would
            // otherwise hash to the same evidence key and DistinctBy would silently keep only one root's
            // document, reattributing every other root's edge to it at Project/Component scope.
            var proof = AddEvidence(ProjectReference, locator, input.Project.LogicalRelativePath + "->" + referencedProject.LogicalRelativePath);
            var source = AddNamed(EntityKind.Project, input.Project.LogicalRelativePath, locator, proof);
            // The referenced project entity occurs in itself, not in the root citing it - otherwise every
            // project that references X would be folded into X's own membership, and scope-lifting would
            // cross X's occurrence set (every referencer) against the citing root instead of just X. Its
            // locator names X's own path too, so it cannot be mistaken for evidence living in the citing root.
            var targetLocator = new LogicalLocator(referencedProject.LogicalRelativePath, new SourceSpan(1, 1, 1, 1), referencedProject);
            var target = AddNamed(EntityKind.Project, referencedProject.LogicalRelativePath, targetLocator, proof, owner: referencedProject);
            AddRelation(ProjectReference, source, target, proof, referencedProject.CanonicalKey);
        }

        foreach (var tree in input.Compilation.SyntaxTrees.OrderBy(tree => tree.FilePath, StringComparer.Ordinal))
        {
            var relativePath = ToLogicalPath(input.Project, tree.FilePath);
            if (relativePath is null)
            {
                continue;
            }

            var model = input.Compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var locator = new LogicalLocator(relativePath, SpanOf(invocation), input.Project);
                var proof = AddEvidence("invocation", locator, invocation.ToString());
                var sourceMethod = model.GetEnclosingSymbol(invocation.SpanStart) as IMethodSymbol;
                if (sourceMethod is null)
                {
                    continue;
                }

                var source = AddSymbol(sourceMethod, locator, proof);
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol targetMethod)
                {
                    if (IsHttpInvocation(invocation))
                    {
                        var destination = ExtractStringArgument(invocation);
                        if (destination is not null)
                        {
                            var external = AddNamed(EntityKind.ExternalSystem, "http:" + destination, locator, proof);
                            AddRelation(Http, source, external, proof, invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            continue;
                        }
                    }

                    AddGap(GapKind.Unknown, "unresolved-invocation", source, proof, invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    continue;
                }

                if (IsMessaging(targetMethod))
                {
                    var eventType = targetMethod.TypeArguments.FirstOrDefault()?.ToDisplayString();
                    if (string.IsNullOrWhiteSpace(eventType))
                    {
                        AddGap(GapKind.Unknown, "messaging-payload-unresolved", source, proof, invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        continue;
                    }

                    var contract = AddNamed(EntityKind.Contract, eventType, locator, proof);
                    AddRelation(Messaging, source, contract, proof, invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    AddRelation(Contract, source, contract, proof, "contract:" + invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    continue;
                }

                if (IsHttp(targetMethod))
                {
                    var destination = ExtractStringArgument(invocation);
                    if (destination is null)
                    {
                        AddGap(GapKind.Candidate, "http-destination-unresolved", source, proof, invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        continue;
                    }

                    var external = AddNamed(EntityKind.ExternalSystem, "http:" + destination, locator, proof);
                    AddRelation(Http, source, external, proof, invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    continue;
                }

                if (IsGrpc(targetMethod))
                {
                    var external = AddNamed(EntityKind.ExternalSystem, "grpc:" + targetMethod.ContainingType.ToDisplayString(), locator, proof);
                    AddRelation(Grpc, source, external, proof, invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    continue;
                }

                var target = AddSymbolIfDeclaredInSource(targetMethod, locator, proof);
                if (target is not null)
                {
                    AddRelation(InternalInvocation, source, target, proof, invocation.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            foreach (var typeSyntax in root.DescendantNodes().OfType<TypeSyntax>())
            {
                if (typeSyntax.Parent is BaseTypeSyntax)
                {
                    continue;
                }

                var sourceMethod = typeSyntax.Ancestors().OfType<BaseMethodDeclarationSyntax>()
                    .Select(method => model.GetDeclaredSymbol(method))
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault();
                var targetType = model.GetTypeInfo(typeSyntax).Type;
                if (sourceMethod is null || targetType is null || targetType.TypeKind == TypeKind.Error)
                {
                    continue;
                }

                var locator = new LogicalLocator(relativePath, SpanOf(typeSyntax), input.Project);
                var proof = AddEvidence("type-use", locator, typeSyntax.ToString());
                var source = AddSymbol(sourceMethod, locator, proof);
                var target = AddSymbolIfDeclaredInSource(targetType, locator, proof);
                if (target is not null)
                {
                    AddRelation(StructuralTypeUse, source, target, proof, typeSyntax.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        return new CausalRelationExtractionResult(
            entities.Values.OrderBy(entity => entity.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            occurrences.OrderBy(item => item.EntityCanonicalKey, StringComparer.Ordinal).ThenBy(item => item.Locator.RelativePath, StringComparer.Ordinal).ToImmutableArray(),
            evidence.DistinctBy(item => item.CanonicalKey).OrderBy(item => item.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            relations.OrderBy(item => item.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            gaps.OrderBy(item => item.CanonicalKey, StringComparer.Ordinal).ToImmutableArray());
    }

    private static bool IsMessaging(IMethodSymbol method) => method.Name is "Publish" or "PublishAsync" or "Subscribe" or "SubscribeAsync";

    private static bool IsHttp(IMethodSymbol method) => method.Name is "GetAsync" or "PostAsync" or "PostAsJsonAsync" or "PutAsync" or "DeleteAsync";

    private static bool IsHttpInvocation(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax member
        && member.Name.Identifier.ValueText is "GetAsync" or "PostAsync" or "PostAsJsonAsync" or "PutAsync" or "DeleteAsync";

    private static bool IsGrpc(IMethodSymbol method) =>
        method.ContainingType.BaseType?.ToDisplayString().Contains("Grpc.Core.ClientBase", StringComparison.Ordinal) == true;

    private static string? ExtractStringArgument(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Select(argument => argument.Expression).OfType<LiteralExpressionSyntax>()
            .FirstOrDefault(literal => literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))?.Token.ValueText;

    private static string? ToLogicalPath(ProjectIdentity project, string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(project.LogicalRelativePath)?.Replace('\\', '/') ?? string.Empty;
        var fileName = Path.GetFileName(absolutePath);
        return string.IsNullOrEmpty(directory) ? fileName : directory + "/" + fileName;
    }

    private static SourceSpan SpanOf(SyntaxNode node) => SpanOf(node.GetLocation());

    private static SourceSpan SpanOf(Location location)
    {
        var span = location.GetLineSpan();
        return new SourceSpan(
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }


    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
