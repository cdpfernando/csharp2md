using System.Text.RegularExpressions;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Extraction;

internal static partial class SecretRedactor
{
    private const string Mask = "***";

    public static bool TryRedact(string candidate, out RedactedExcerpt excerpt)
    {
        excerpt = default;
        if (string.IsNullOrEmpty(candidate) || !LooksLikeSecret(candidate))
        {
            return false;
        }

        excerpt = RedactedExcerpt.Create(MaskSecret(candidate));
        return true;
    }

    private static bool LooksLikeSecret(string candidate) =>
        ContainsIgnoreCase(candidate, "Password=")
        || ContainsIgnoreCase(candidate, "Pwd=")
        || ContainsIgnoreCase(candidate, "User ID=")
        || ContainsIgnoreCase(candidate, "User Id=")
        || ContainsIgnoreCase(candidate, "Data Source=")
        || ContainsIgnoreCase(candidate, "Initial Catalog=")
        || ContainsIgnoreCase(candidate, "ConnectionString")
        || ContainsIgnoreCase(candidate, "Bearer ")
        || ContainsIgnoreCase(candidate, "Authorization:")
        || ContainsIgnoreCase(candidate, "token=")
        || ContainsIgnoreCase(candidate, "-----BEGIN CERTIFICATE-----")
        || ContainsIgnoreCase(candidate, "-----BEGIN ") && ContainsIgnoreCase(candidate, "PRIVATE KEY");

    private static string MaskSecret(string candidate)
    {
        var masked = AssignmentValuePattern().Replace(candidate, "${key}" + Mask);
        masked = BearerPattern().Replace(masked, "${prefix}" + Mask);
        masked = PemBodyPattern().Replace(masked, "${header}\n" + Mask + "\n${footer}");
        if (masked.Contains(Mask, StringComparison.Ordinal) || masked.Contains("[REDACTED]", StringComparison.Ordinal))
        {
            return masked;
        }

        return "[REDACTED]";
    }

    private static bool ContainsIgnoreCase(string text, string value) =>
        text.Contains(value, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"(?<key>(?i)Password=|Pwd=|User ID=|User Id=|Data Source=|Initial Catalog=|token=)[^;\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex AssignmentValuePattern();

    [GeneratedRegex(@"(?<prefix>(?i)(?:Authorization:\s*)?Bearer\s+)\S+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"(?<header>-----BEGIN [^-]+-----)\s*(?<body>[A-Za-z0-9+/=\s]+)\s*(?<footer>-----END [^-]+-----)", RegexOptions.CultureInvariant)]
    private static partial Regex PemBodyPattern();
}
