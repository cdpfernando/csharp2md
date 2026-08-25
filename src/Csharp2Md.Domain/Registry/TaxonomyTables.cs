namespace Csharp2Md.Domain.Registry;

public sealed partial record TaxonomyTables
{
    public ImmutableArray<FactTypeDescriptor> FactTypes { get; init; } = [];
}
