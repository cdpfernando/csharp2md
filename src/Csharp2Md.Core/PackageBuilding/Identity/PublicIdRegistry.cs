using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.PackageBuilding.Identity;

internal sealed class PublicIdRegistry
{
    private readonly Dictionary<string, string> categories = new(StringComparer.Ordinal);

    internal string Register(string prefix, string canonicalCategory)
    {
        if (prefix.Length != 3 || prefix.Any(character => character is < 'a' or > 'z'))
        {
            throw new ArgumentException("A public ID prefix must contain three lowercase letters.", nameof(prefix));
        }

        canonicalCategory = CanonicalText.Require(canonicalCategory, nameof(canonicalCategory));
        var id = prefix + "_" + Base32Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalCategory)).AsSpan(0, 10));
        if (categories.TryGetValue(id, out var existing) && !StringComparer.Ordinal.Equals(existing, canonicalCategory))
        {
            throw new PublicIdCollisionException(id, existing, canonicalCategory);
        }

        categories[id] = canonicalCategory;
        return id;
    }

    internal SolutionId RegisterSolution(SolutionIdentity solution)
    {
        ArgumentNullException.ThrowIfNull(solution);
        return new SolutionId(Register("sol", solution.CanonicalKey));
    }

    private static string Base32Hex(ReadOnlySpan<byte> bytes)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuv";
        var result = new char[16];
        var buffer = 0;
        var bits = 0;
        var position = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                result[position++] = alphabet[(buffer >> (bits -= 5)) & 31];
            }
        }

        return new string(result);
    }
}

internal sealed class PublicIdCollisionException : InvalidOperationException
{
    internal PublicIdCollisionException(string id, string firstCategory, string secondCategory)
        : base($"Public ID collision '{id}' between canonical categories '{firstCategory}' and '{secondCategory}'.")
    {
        Id = id;
        FirstCategory = firstCategory;
        SecondCategory = secondCategory;
    }

    internal string Id { get; }
    internal string FirstCategory { get; }
    internal string SecondCategory { get; }
}
