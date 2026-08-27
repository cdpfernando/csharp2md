namespace Csharp2Md.Analysis.Inventory;

internal static class SharedFileRead
{
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

    internal static ImmutableArray<byte> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        const int maxAttempts = 8;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 4096,
                    FileOptions.SequentialScan);
                var length = stream.Length;
                if (length > int.MaxValue)
                {
                    throw new IOException($"File '{path}' is too large to read.");
                }

                var buffer = GC.AllocateUninitializedArray<byte>((int)length);
                stream.ReadExactly(buffer);
                return [.. buffer];
            }
            catch (Exception exception) when (
                attempt < maxAttempts
                && IsTransientShare(exception))
            {
                Thread.Sleep(Math.Min(1000, 25 * attempt * attempt));
            }
        }
    }

    private static bool IsTransientShare(Exception exception) =>
        exception is UnauthorizedAccessException
        || (exception is IOException io
            && io is not FileNotFoundException
            && io is not DirectoryNotFoundException
            && io.HResult is SharingViolation or LockViolation);
}
