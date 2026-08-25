namespace Csharp2Md.Domain.Facets;

public enum DataStoreTechnology
{
    Relational,
    Document,
    KeyValue,
    Cache,
    Unknown,
}

public enum DataObjectForm
{
    Table,
    View,
    Collection,
    KeySpace,
    CacheRegion,
    Unknown,
}

public enum DataOperationKind
{
    Read,
    Insert,
    Update,
    Delete,
    Execute,
    Unknown,
}

public enum MappingStateKind
{
    ExplicitConfirmation,
    ConventionalCandidate,
    Unresolved,
}
