namespace Csharp2Md.Analysis.Storage;

public sealed record DiagnosticRecord
{
    public string Code { get; }

    public string Message { get; }

    public string? IdentityOrKey { get; }

    public DiagnosticRecord(string code, string message, string? identityOrKey)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        IdentityOrKey = ValidateIdentityOrKey(identityOrKey);
        Code = code;
        Message = message;
    }

    private static string? ValidateIdentityOrKey(string? identityOrKey)
    {
        if (identityOrKey is null)
        {
            return null;
        }

        if (IsAbsoluteFilesystemPath(identityOrKey))
        {
            throw new ArgumentException(
                "IdentityOrKey must be a relative path or fact id, not an absolute filesystem path.",
                nameof(identityOrKey));
        }

        return identityOrKey;
    }

    private static bool IsAbsoluteFilesystemPath(string value) =>
        Path.IsPathRooted(value)
        || (value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':');
}
