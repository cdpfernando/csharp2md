namespace Csharp2Md.Analysis.Inventory;

/// <summary>
/// The accepted and excluded document count and byte total for one <see cref="DocumentPolicyCategory"/>.
/// Public because it rides on the public <c>FactualSnapshot</c> (AD-017 slot pattern), consumed by
/// Storage across the assembly boundary.
/// </summary>
public readonly record struct DocumentPolicyCategoryTotal(
    DocumentPolicyCategory Category,
    int AcceptedCount,
    long AcceptedBytes,
    int ExcludedCount,
    long ExcludedBytes);

/// <summary>
/// Accepted and excluded document counts and byte totals per supported-document policy category
/// (GCPC-034), carried on the <c>FactualSnapshot</c> the same way AD-017 carries diagnostics.
/// </summary>
public sealed record DocumentPolicyReport(ImmutableArray<DocumentPolicyCategoryTotal> Categories)
{
    public static DocumentPolicyReport Empty { get; } = new(ImmutableArray<DocumentPolicyCategoryTotal>.Empty);

    public int AcceptedCount => Categories.Sum(static category => category.AcceptedCount);

    public long AcceptedBytes => Categories.Sum(static category => category.AcceptedBytes);

    public int ExcludedCount => Categories.Sum(static category => category.ExcludedCount);

    public long ExcludedBytes => Categories.Sum(static category => category.ExcludedBytes);

    public DocumentPolicyCategoryTotal For(DocumentPolicyCategory category)
    {
        foreach (var entry in Categories)
        {
            if (entry.Category == category)
            {
                return entry;
            }
        }

        return new DocumentPolicyCategoryTotal(category, 0, 0, 0, 0);
    }

    /// <summary>Builds a report from one document's worth of decisions: (category, accepted, byte size).</summary>
    internal static DocumentPolicyReport FromOutcomes(
        IEnumerable<(DocumentPolicyCategory Category, bool Accepted, long Bytes)> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var totals = new Dictionary<DocumentPolicyCategory, DocumentPolicyCategoryTotal>();
        foreach (var (category, accepted, bytes) in outcomes)
        {
            totals.TryGetValue(category, out var existing);
            totals[category] = accepted
                ? existing with
                {
                    Category = category,
                    AcceptedCount = existing.AcceptedCount + 1,
                    AcceptedBytes = existing.AcceptedBytes + bytes,
                }
                : existing with
                {
                    Category = category,
                    ExcludedCount = existing.ExcludedCount + 1,
                    ExcludedBytes = existing.ExcludedBytes + bytes,
                };
        }

        return new DocumentPolicyReport([.. totals.Values.OrderBy(static total => total.Category)]);
    }

    public DocumentPolicyReport Merge(DocumentPolicyReport other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var totals = new Dictionary<DocumentPolicyCategory, DocumentPolicyCategoryTotal>();
        foreach (var entry in Categories.Concat(other.Categories))
        {
            totals.TryGetValue(entry.Category, out var existing);
            totals[entry.Category] = new DocumentPolicyCategoryTotal(
                entry.Category,
                existing.AcceptedCount + entry.AcceptedCount,
                existing.AcceptedBytes + entry.AcceptedBytes,
                existing.ExcludedCount + entry.ExcludedCount,
                existing.ExcludedBytes + entry.ExcludedBytes);
        }

        return new DocumentPolicyReport([.. totals.Values.OrderBy(static total => total.Category)]);
    }
}
