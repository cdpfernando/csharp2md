namespace Csharp2Md.Storage;

internal readonly record struct FilesystemRetryPolicy(int MaxAttempts, Func<int, TimeSpan> Delay)
{
    internal static FilesystemRetryPolicy Default { get; } = new(
        12,
        static attempt => TimeSpan.FromMilliseconds(Math.Min(1000, 50 << Math.Clamp(attempt - 1, 0, 5))));

    internal static FilesystemRetryPolicy FastFail { get; } = new(1, static _ => TimeSpan.Zero);
}

internal static class FilesystemIo
{
    internal static void MoveDirectory(string source, string destination, FilesystemRetryPolicy retry) =>
        Execute(
            () => Directory.Move(source, destination),
            retry,
            $"Could not move '{source}' to '{destination}'.");

    internal static void DeleteDirectory(string path, FilesystemRetryPolicy retry)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Execute(
            () => Directory.Delete(path, recursive: true),
            retry,
            $"Could not delete '{path}'.");
    }

    private static void Execute(Action action, FilesystemRetryPolicy retry, string prefix)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfLessThan(retry.MaxAttempts, 1);
        ArgumentNullException.ThrowIfNull(retry.Delay);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception exception) when (
                attempt < retry.MaxAttempts
                && exception is IOException or UnauthorizedAccessException)
            {
                var delay = retry.Delay(attempt);
                if (delay > TimeSpan.Zero)
                {
                    Thread.Sleep(delay);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"{prefix} {exception.Message}", exception);
            }
        }
    }
}
