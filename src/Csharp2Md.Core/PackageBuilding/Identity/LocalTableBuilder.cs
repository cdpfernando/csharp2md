using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.PackageBuilding.Identity;

internal readonly record struct LocalHandle
{
    internal LocalHandle(string value) => Value = CanonicalText.Require(value, nameof(value));
    internal string Value { get; }
}

internal sealed class LocalTable
{
    private readonly IReadOnlyDictionary<string, LocalHandle> handles;

    internal LocalTable(string solutionKey, IReadOnlyDictionary<string, LocalHandle> handles)
    {
        SolutionKey = CanonicalText.Require(solutionKey, nameof(solutionKey));
        this.handles = handles;
    }

    internal string SolutionKey { get; }
    internal IReadOnlyDictionary<string, LocalHandle> Handles => handles;
    internal LocalHandle Resolve(string canonicalKey) => handles.TryGetValue(canonicalKey, out var handle)
        ? handle : throw new KeyNotFoundException($"No local handle exists for '{canonicalKey}'.");
}

internal static class LocalTableBuilder
{
    internal static LocalTable Build(string solutionKey, IEnumerable<string> canonicalKeys)
    {
        ArgumentNullException.ThrowIfNull(canonicalKeys);
        var keys = canonicalKeys.Select(key => CanonicalText.Require(key, nameof(canonicalKeys)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var handles = new Dictionary<string, LocalHandle>(keys.Length, StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < keys.Length; ordinal++)
        {
            handles.Add(keys[ordinal], new LocalHandle(ToBase36(ordinal)));
        }

        return new LocalTable(solutionKey, handles);
    }

    private static string ToBase36(int ordinal)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        Span<char> result = stackalloc char[6];
        var position = result.Length;
        do
        {
            result[--position] = alphabet[ordinal % 36];
            ordinal /= 36;
        } while (ordinal > 0 && position > 0);

        if (ordinal > 0)
        {
            throw new LocalHandleOverflowException();
        }

        return new string(result[position..]);
    }
}

internal sealed class LocalHandleOverflowException : InvalidOperationException
{
    internal LocalHandleOverflowException() : base("Local handle overflow exceeds six base36 characters.") { }
}
