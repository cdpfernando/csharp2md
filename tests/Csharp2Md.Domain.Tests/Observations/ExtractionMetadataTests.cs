using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Domain.Tests.Observations;

public sealed class ExtractionMetadataTests
{
    [Theory]
    [Trait("Requirement", "TAX-38")]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExtractorVersion_NonPositiveValue_IsRejected(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractorVersion(value));
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void ExtractorVersion_PositiveValue_IsAccepted()
    {
        var version = new ExtractorVersion(3);

        Assert.Equal(3, version.Value);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void ExtractorVersion_IsMonotonic_HigherValueComparesGreater()
    {
        var older = new ExtractorVersion(1);
        var newer = new ExtractorVersion(2);

        Assert.True(newer.CompareTo(older) > 0);
        Assert.True(older.CompareTo(newer) < 0);
        Assert.Equal(0, older.CompareTo(new ExtractorVersion(1)));
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void BindingDiagnostic_CarriesCodeAndMessage()
    {
        var diagnostic = new BindingDiagnostic("BIND001", "Could not bind the call target.");

        Assert.Equal("BIND001", diagnostic.Code);
        Assert.Equal("Could not bind the call target.", diagnostic.Message);
    }

    [Theory]
    [Trait("Requirement", "TAX-38")]
    [InlineData("")]
    [InlineData("   ")]
    public void BindingDiagnostic_NonCanonicalCode_IsRejected(string code)
    {
        Assert.Throws<ArgumentException>(() => new BindingDiagnostic(code, "message"));
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void BindingDiagnostic_NamesNoRoslynType()
    {
        var propertyTypes = typeof(BindingDiagnostic).GetProperties().Select(p => p.PropertyType);

        Assert.DoesNotContain(propertyTypes, t => t.Namespace?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true);
    }
}
