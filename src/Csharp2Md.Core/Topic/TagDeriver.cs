using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Topic;

/// <summary>
/// The six additive <c>tags</c> rules (Frontmatter Schema, `tags` Derivation; WIKI-06). Every rule
/// but <c>api-endpoint</c> scans the whole document — unlike <see cref="FileTypeClassifier"/>, which
/// looks only at the title type — because a tag describes a pattern found anywhere in the source (a
/// bootstrapping call in <c>Program.cs</c>, a DI registration in a hosting extension file).
/// Syntax-only, like every other Topic derivation (WIKI-13). Emits a sorted, de-duplicated,
/// never-null list.
/// </summary>
internal static class TagDeriver
{
    private static readonly string[] DependencyInjectionMethods =
        ["AddScoped", "AddSingleton", "AddTransient", "AddHostedService"];

    private static readonly string[] MessagingMemberNames = ["Publish", "PublishAsync", "Subscribe", "SubscribeAsync"];

    public static IReadOnlyList<string> Derive(SyntaxTree tree, BaseTypeDeclarationSyntax? titleType)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var root = tree.GetRoot();
        var identifiers = root.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)
            .ToList();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

        var tags = new SortedSet<string>(StringComparer.Ordinal);

        if (IsBootstrapping(invocations, identifiers))
        {
            tags.Add("bootstrapping");
        }

        if (invocations.Any(invocation =>
            DependencyInjectionMethods.Contains(InvokedMethodName(invocation), StringComparer.Ordinal)))
        {
            tags.Add("dependency-injection");
        }

        if (identifiers.Any(name =>
                name.Contains("EventBus", StringComparison.Ordinal)
                || name.Contains("IIntegrationEventHandler", StringComparison.Ordinal))
            || identifiers.Any(name => MessagingMemberNames.Contains(name, StringComparer.Ordinal)))
        {
            tags.Add("event-driven");
        }

        if (identifiers.Any(name =>
            name is "DbContext" or "DbSet" or "IQueryable" || name.EndsWith("Repository", StringComparison.Ordinal)))
        {
            tags.Add("persistence");
        }

        if (HasAsyncPattern(root))
        {
            tags.Add("async-patterns");
        }

        if (IsApiEndpoint(titleType))
        {
            tags.Add("api-endpoint");
        }

        return tags.ToList();
    }

    // "WebHost.Create*", "IWebHostBuilder", "WebApplication.CreateBuilder".
    private static bool IsBootstrapping(List<InvocationExpressionSyntax> invocations, List<string> identifiers) =>
        identifiers.Contains("IWebHostBuilder", StringComparer.Ordinal)
        || invocations.Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
            && ((ReceiverName(access) == "WebHost" && access.Name.Identifier.ValueText.StartsWith("Create", StringComparison.Ordinal))
                || (ReceiverName(access) == "WebApplication" && access.Name.Identifier.ValueText == "CreateBuilder")));

    private static string ReceiverName(MemberAccessExpressionSyntax access) => SimpleName(access.Expression.ToString());

    private static string? InvokedMethodName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => null,
    };

    // Matches the string-based simple-name convention MessagingDetector.SimpleName already uses:
    // take the segment after the last namespace separator.
    private static string SimpleName(string qualifiedName)
    {
        var lastSeparator = qualifiedName.LastIndexOf('.');
        return lastSeparator >= 0 ? qualifiedName[(lastSeparator + 1)..] : qualifiedName;
    }

    private static string StripGenericArgs(string typeName)
    {
        var genericStart = typeName.IndexOf('<');
        return genericStart >= 0 ? typeName[..genericStart] : typeName;
    }

    // "async modifier, await expression, or a Task/Task<T> return type."
    private static bool HasAsyncPattern(SyntaxNode root) =>
        root.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method =>
            method.Modifiers.Any(SyntaxKind.AsyncKeyword) || IsTaskReturnType(method.ReturnType))
        || root.DescendantNodes().OfType<AwaitExpressionSyntax>().Any();

    private static bool IsTaskReturnType(TypeSyntax returnType) =>
        SimpleName(StripGenericArgs(returnType.ToString())) is "Task";

    // Same rule FileTypeClassifier's controller check uses, applied to the title type only — the one
    // tag rule that is about a specific type rather than the whole document.
    private static bool IsApiEndpoint(BaseTypeDeclarationSyntax? titleType) =>
        titleType is TypeDeclarationSyntax typeDeclaration
        && (typeDeclaration.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal)
            || (typeDeclaration.BaseList?.Types.OfType<SimpleBaseTypeSyntax>()
                .Any(baseType => SimpleName(StripGenericArgs(baseType.Type.ToString()))
                    .EndsWith("Controller", StringComparison.Ordinal)) ?? false));
}
