namespace Csharp2Md.Domain.Identity;

public readonly record struct FactReference
{
    public FactId Id { get; }

    public string FactType { get; }

    public FactReference(FactId id, string factType)
    {
        Id = id;
        FactType = FactIdGrammar.RequireCanonicalText(factType, nameof(factType));
    }
}
