using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class SecretRedactorTests
{
    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public void TryRedact_PasswordEqualsSecret_IsFlaggedWithAMaskAndWithoutTheSecret()
    {
        const string candidate = "Password=secret";

        var flagged = SecretRedactor.TryRedact(candidate, out var excerpt);

        Assert.True(flagged);
        Assert.True(
            excerpt.Value.Contains("***", StringComparison.Ordinal)
            || excerpt.Value.Contains("[REDACTED]", StringComparison.Ordinal),
            "A flagged excerpt must carry a visible redaction marker.");
        Assert.DoesNotContain("secret", excerpt.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public void TryRedact_PasswordEqualsSecret_DoesNotHashTheSecretValue()
    {
        const string secret = "secret";
        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

        Assert.True(SecretRedactor.TryRedact("Password=" + secret, out var excerpt));

        Assert.DoesNotContain(secretHash, excerpt.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, excerpt.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public void TryRedact_NonSecretLiteral_IsNotFlagged()
    {
        var flagged = SecretRedactor.TryRedact("OrderStatus.Placed", out _);

        Assert.False(flagged);
    }

    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public void TryRedact_ConnectionString_IsFlaggedWithoutTheSecret()
    {
        const string candidate = "Server=localhost;Database=orders;User ID=sa;Password=hunter2;";

        Assert.True(SecretRedactor.TryRedact(candidate, out var excerpt));
        Assert.True(
            excerpt.Value.Contains("***", StringComparison.Ordinal)
            || excerpt.Value.Contains("[REDACTED]", StringComparison.Ordinal),
            "A flagged excerpt must carry a visible redaction marker.");
        Assert.DoesNotContain("hunter2", excerpt.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public void TryRedact_BearerToken_IsFlaggedWithoutTheToken()
    {
        const string token = "eyJhbGciOiJIUzI1NiJ9.payload.sig";

        Assert.True(SecretRedactor.TryRedact("Authorization: Bearer " + token, out var excerpt));
        Assert.True(
            excerpt.Value.Contains("***", StringComparison.Ordinal)
            || excerpt.Value.Contains("[REDACTED]", StringComparison.Ordinal),
            "A flagged excerpt must carry a visible redaction marker.");
        Assert.DoesNotContain(token, excerpt.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-55")]
    public void TryRedact_CertificatePem_IsFlaggedWithoutThePemBody()
    {
        const string body = "MIICUTCCAfugAwIBAgIBADANBgkqhkiG9w0BAQQFADBXMQswCQYDVQQGEwJDTjEL";
        var candidate = "-----BEGIN CERTIFICATE-----" + Environment.NewLine + body + Environment.NewLine + "-----END CERTIFICATE-----";

        Assert.True(SecretRedactor.TryRedact(candidate, out var excerpt));
        Assert.True(
            excerpt.Value.Contains("***", StringComparison.Ordinal)
            || excerpt.Value.Contains("[REDACTED]", StringComparison.Ordinal),
            "A flagged excerpt must carry a visible redaction marker.");
        Assert.DoesNotContain(body, excerpt.Value, StringComparison.Ordinal);
    }
}
