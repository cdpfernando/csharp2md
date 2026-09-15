using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Inventory;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class SupportedDocumentPolicyTests
{
    [Fact]
    [Trait("Requirement", "GCPC-026")]
    public void Decide_CSharpSource_IsAcceptedAsCSharpSource()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide("App/Program.cs");

        Assert.True(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.CSharpSource, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-026")]
    public void Decide_ProjectFile_IsAcceptedAsProjectFile()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide("App/App.csproj");

        Assert.True(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.ProjectFile, decision.Category);
    }

    [Theory]
    [Trait("Requirement", "GCPC-026")]
    [InlineData("App/appsettings.json")]
    [InlineData("App/appsettings.Development.json")]
    [InlineData("App/AppSettings.Production.json")]
    public void Decide_AppsettingsVariants_AreAcceptedAsConfiguration(string relativePath)
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide(relativePath);

        Assert.True(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.Configuration, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-029")]
    public void Decide_ConditionalExtension_IsExcludedWhenNoActiveClassifierDeclaresIt()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide("App/Protos/payments.proto");

        Assert.False(decision.Accepted);
    }

    [Fact]
    [Trait("Requirement", "GCPC-029")]
    public void Decide_ConditionalExtension_IsAcceptedAsConditionalWhenAnActiveClassifierDeclaresIt()
    {
        var policy = new SupportedDocumentPolicy(RegistryDeclaring(".proto"));

        var decision = policy.Decide("App/Protos/payments.proto");

        Assert.True(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.Conditional, decision.Category);
    }

    [Theory]
    [Trait("Requirement", "GCPC-026")]
    [InlineData("Web/app.ts")]
    [InlineData("Web/app.js")]
    [InlineData("Web/app.js.map")]
    public void Decide_FrontendScriptExtensions_AreExcludedAsFrontendScript(string relativePath)
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide(relativePath);

        Assert.False(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.FrontendScript, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-026")]
    public void Decide_Archive_IsExcludedAsArchive()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide("Web/assets.zip");

        Assert.False(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.Archive, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-026")]
    public void Decide_Image_IsExcludedAsStaticAsset()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide("Web/logo.png");

        Assert.False(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.StaticAsset, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-026")]
    public void Decide_PackageLock_IsExcludedAsPackageManagementArtifact()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide("Web/package-lock.json");

        Assert.False(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.PackageManagementArtifact, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-026")]
    public void Decide_Certificate_IsExcludedAsBinaryOrCertificate()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide("Web/cert.pfx");

        Assert.False(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.BinaryOrCertificate, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-026")]
    public void Decide_UnknownExtensionWithNoRegisteredConsumer_IsExcludedAsUnregistered()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry());

        var decision = policy.Decide("Web/notes.unknownext");

        Assert.False(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.Unregistered, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-030")]
    public void Decide_AllowlistedPath_IsAcceptedAsAllowlistedRegardlessOfExtension()
    {
        var policy = new SupportedDocumentPolicy(EmptyRegistry(), ["Web/app.ts"]);

        var decision = policy.Decide("Web/app.ts");

        Assert.True(decision.Accepted);
        Assert.Equal(DocumentPolicyCategory.Allowlisted, decision.Category);
    }

    [Fact]
    [Trait("Requirement", "GCPC-058")]
    public void Version_IsANonEmptyStringCarriedIntoProvenance()
    {
        Assert.False(string.IsNullOrWhiteSpace(SupportedDocumentPolicy.Version));
    }

    private static ClassifierCapabilityRegistry EmptyRegistry() => new([]);

    private static ClassifierCapabilityRegistry RegistryDeclaring(string extension) =>
        new([new ExtensionDeclaringPass(extension)]);

    private sealed class ExtensionDeclaringPass(string extension) : IClassifierPass, IDocumentConsumingClassifierPass
    {
        public string Name => "extension-declaring";

        public ImmutableArray<string> ConsumedDocumentExtensions => [extension];

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken) =>
            new(0, 0, 0, 0);
    }
}
