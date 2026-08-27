using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Facts;

public interface IFact
{
    FactReference Reference { get; }

    FactFamily Family { get; }
}

internal static class FactGuards
{
    public static void RequireInitialized<T>(T value, string parameterName)
        where T : struct, IEquatable<T>
    {
        if (value.Equals(default(T)))
        {
            throw new ArgumentException($"A fact requires an initialized {parameterName}.", parameterName);
        }
    }

    public static void RequireDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentException($"'{value}' is not a defined value required for {parameterName}.", parameterName);
        }
    }
}

public sealed record Solution : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Structural;

    public SolutionId Id { get; }

    private Solution(FactReference reference, SolutionId id)
    {
        Reference = reference;
        Id = id;
    }

    public static Solution Create(SolutionId id)
    {
        FactGuards.RequireInitialized(id, nameof(id));

        var reference = new FactReference(new FactId("solution", id.Value), nameof(Solution));
        return new Solution(reference, id);
    }
}

public sealed record Project : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Structural;

    public ProjectId Id { get; }

    private Project(FactReference reference, ProjectId id)
    {
        Reference = reference;
        Id = id;
    }

    public static Project Create(ProjectId id)
    {
        FactGuards.RequireInitialized(id, nameof(id));

        var reference = new FactReference(new FactId("project", id.Value), nameof(Project));
        return new Project(reference, id);
    }
}

public sealed record Document : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Structural;

    public ProjectId OwningProject { get; }

    public string RelativePath { get; }

    public DocumentHash? SourceHash { get; }

    private Document(FactReference reference, ProjectId owningProject, string relativePath, DocumentHash? sourceHash)
    {
        Reference = reference;
        OwningProject = owningProject;
        RelativePath = relativePath;
        SourceHash = sourceHash;
    }

    public static Document Create(ProjectId owningProject, string relativePath, DocumentHash? sourceHash = null)
    {
        FactGuards.RequireInitialized(owningProject, nameof(owningProject));
        var path = FactIdGrammar.ValidateRelativePath(relativePath, nameof(relativePath));
        if (sourceHash is { } hash && hash.Equals(default(DocumentHash)))
        {
            throw new ArgumentException("A source hash must be initialized when supplied.", nameof(sourceHash));
        }

        var id = FactIdGrammar.Create("document", ("project", owningProject.Value), ("path", path));
        var reference = new FactReference(id, nameof(Document));
        return new Document(reference, owningProject, path, sourceHash);
    }
}

public sealed record Symbol : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Structural;

    public ProjectId OwningProject { get; }

    public CanonicalSymbolSignature Signature { get; }

    public SymbolFacetSet Facets { get; }

    public DeclarationLocator? DeclarationLocator { get; }

    private Symbol(
        FactReference reference,
        ProjectId owningProject,
        CanonicalSymbolSignature signature,
        SymbolFacetSet facets,
        DeclarationLocator? declarationLocator)
    {
        Reference = reference;
        OwningProject = owningProject;
        Signature = signature;
        Facets = facets;
        DeclarationLocator = declarationLocator;
    }

    public static Symbol Create(
        CanonicalSymbolSignature signature,
        ProjectId owningProject,
        SymbolFacetSet facets,
        DeclarationLocator? declarationLocator = null)
    {
        FactGuards.RequireInitialized(signature, nameof(signature));
        FactGuards.RequireInitialized(owningProject, nameof(owningProject));
        if (facets.Facets.IsDefault)
        {
            throw new ArgumentException("A symbol requires an initialized facet set.", nameof(facets));
        }

        if (declarationLocator is { } locator)
        {
            if (locator.Equals(default(DeclarationLocator)) || locator.Document.Equals(default(DocumentId)))
            {
                throw new ArgumentException("A declaration locator requires an initialized document.", nameof(declarationLocator));
            }

            if (locator.Hash.Equals(default(DocumentHash)))
            {
                throw new ArgumentException("A declaration locator requires an initialized hash.", nameof(declarationLocator));
            }
        }

        var id = FactIdGrammar.Create("symbol", ("project", owningProject.Value), ("signature", signature.Value));
        var reference = new FactReference(id, nameof(Symbol));
        return new Symbol(reference, owningProject, signature, facets, declarationLocator);
    }
}
