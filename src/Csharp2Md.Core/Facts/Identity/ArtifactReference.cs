using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Csharp2Md.Core.Facts.Identity;

public readonly record struct ArtifactReference
{
    private static readonly Regex PathPattern = new(
        "^facts/(?<type>[a-z][a-z0-9-]*)/(?<prefix>[0-9a-f]{2})/(?<hash>[0-9a-f]{64})\\.json$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized artifact reference has no value.");

    public static ArtifactReference Create(FactId factId)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(factId.Value)));
        return new ArtifactReference($"facts/{factId.Type}/{hash[..2]}/{hash}.json");
    }

    public static ArtifactReference Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var match = PathPattern.Match(value);
        if (!match.Success || !string.Equals(match.Groups["prefix"].Value, match.Groups["hash"].Value[..2], StringComparison.Ordinal))
        {
            throw new ArgumentException("The artifact reference is not canonical.", nameof(value));
        }

        return new ArtifactReference(value);
    }

    public static bool IsCollision(
        FactId firstId,
        ArtifactReference firstReference,
        FactId secondId,
        ArtifactReference secondReference) =>
        firstId != secondId && firstReference == secondReference;

    private ArtifactReference(string value) => _value = value;

    public override string ToString() => Value;
}
