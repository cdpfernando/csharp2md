namespace Csharp2Md.Analysis.Tests.Pipeline;

internal static class PackageSnapshot
{
    public static IReadOnlyDictionary<string, byte[]> Capture(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(directory, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    public static void AssertEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var key in expected.Keys)
        {
            Assert.True(expected[key].AsSpan().SequenceEqual(actual[key]), $"Bytes at '{key}' changed.");
        }
    }
}
