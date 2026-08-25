namespace Csharp2Md.Domain.Proof;

public readonly record struct ClassifierIdentity
{
    private readonly string? _id;

    public string Id => _id ?? throw new InvalidOperationException("An uninitialized classifier identity has no id.");

    public int Version { get; }

    private ClassifierIdentity(string id, int version)
    {
        _id = id;
        Version = version;
    }

    public static ClassifierIdentity Create(string id, int version)
    {
        if (string.IsNullOrWhiteSpace(id) || !IsLowercaseReverseDns(id))
        {
            throw new ArgumentException("A classifier identifier must be a lowercase, dotted reverse-DNS name.", nameof(id));
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "A classifier version must be a positive integer.");
        }

        return new ClassifierIdentity(id, version);
    }

    public override string ToString() => Id;

    private static bool IsLowercaseReverseDns(string id) =>
        id.Contains('.', StringComparison.Ordinal)
        && id.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '-')
        && id.Split('.').All(static segment => segment.Length > 0);
}
