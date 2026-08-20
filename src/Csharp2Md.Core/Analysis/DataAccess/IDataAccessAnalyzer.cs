namespace Csharp2Md.Core.Analysis.DataAccess;

/// <summary>
/// The extension seam for data access discovery: a strategy that inspects one document and appends raw
/// claims. A new ORM or data access library attaches by implementing this and registering with the
/// collector, without the collector itself changing.
/// </summary>
internal interface IDataAccessAnalyzer
{
    /// <summary>
    /// The analyzer's stable identity, which becomes the detector provenance of every fact its claims
    /// eventually produce.
    /// </summary>
    DataAccessAnalyzerId Id { get; }

    /// <summary>
    /// Appends every claim this analyzer observes in <paramref name="context"/>. Implementations append
    /// only; they never read, reorder or remove what another analyzer contributed.
    /// </summary>
    void Analyze(DataAccessContext context, ImmutableArray<RawDatabaseClaim>.Builder claims);
}
