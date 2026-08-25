namespace Csharp2Md.Domain.Identity;

public sealed class IdentityLedger
{
    private readonly Dictionary<string, FactReference> _references = new(StringComparer.Ordinal);

    public int Count => _references.Count;

    public void Register(FactReference reference)
    {
        if (_references.TryGetValue(reference.Id.Value, out var existing))
        {
            if (!string.Equals(existing.FactType, reference.FactType, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Identity '{reference.Id.Value}' is already held by fact type '{existing.FactType}' and cannot also be held by fact type '{reference.FactType}'.",
                    nameof(reference));
            }

            return;
        }

        _references[reference.Id.Value] = reference;
    }
}
