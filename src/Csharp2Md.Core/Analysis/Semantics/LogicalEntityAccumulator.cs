namespace Csharp2Md.Core.Analysis.Semantics;

internal sealed class OccurrenceCollisionException : InvalidOperationException
{
    public string Code { get; } = "occurrence-collision";

    public OccurrenceCollisionException(string entityCanonicalKey, string variantKey)
        : base($"Incompatible shapes for '{entityCanonicalKey}' within variant '{variantKey}'.")
    {
        EntityCanonicalKey = entityCanonicalKey;
        VariantKey = variantKey;
    }

    public string EntityCanonicalKey { get; }

    public string VariantKey { get; }
}

internal sealed class LogicalEntityAccumulator
{
    private readonly Dictionary<string, LogicalEntity> _entities = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Entity, string Variant), VariantOccurrence> _occurrences = new();
    private readonly string _solutionCanonicalKey;

    public LogicalEntityAccumulator(SolutionIdentity solution)
    {
        ArgumentNullException.ThrowIfNull(solution);
        _solutionCanonicalKey = solution.CanonicalKey;
    }

    public void Add(LogicalEntity entity, VariantOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(occurrence);

        if (!string.Equals(entity.CanonicalKey, occurrence.EntityCanonicalKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Occurrence entity key must match the logical entity.", nameof(occurrence));
        }

        if (!entity.CanonicalKey.Contains(_solutionCanonicalKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Entity identity must stay within the accumulator solution.");
        }

        if (_entities.TryGetValue(entity.CanonicalKey, out var existingEntity))
        {
            if (existingEntity.Kind != entity.Kind
                || !string.Equals(existingEntity.DisplayName, entity.DisplayName, StringComparison.Ordinal)
                || !string.Equals(existingEntity.QualifiedName, entity.QualifiedName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Logical entity '{entity.CanonicalKey}' changed shape across variants.");
            }
        }
        else
        {
            _entities[entity.CanonicalKey] = entity;
        }

        var variantKey = CanonicalIdentity.VariantKey(occurrence.Variant);
        var key = (occurrence.EntityCanonicalKey, variantKey);
        if (_occurrences.TryGetValue(key, out var existingOccurrence))
        {
            if (!string.Equals(existingOccurrence.ShapeDigest, occurrence.ShapeDigest, StringComparison.Ordinal))
            {
                throw new OccurrenceCollisionException(occurrence.EntityCanonicalKey, variantKey);
            }

            return;
        }

        _occurrences[key] = occurrence;
    }

    public ImmutableArray<LogicalEntity> Entities() =>
        _entities.Values.OrderBy(entity => entity.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();

    public ImmutableArray<VariantOccurrence> Occurrences() =>
        _occurrences.Values
            .OrderBy(occurrence => occurrence.EntityCanonicalKey, StringComparer.Ordinal)
            .ThenBy(occurrence => CanonicalIdentity.VariantKey(occurrence.Variant), StringComparer.Ordinal)
            .ToImmutableArray();
}
