using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Tests.Facts.Identity;

public sealed class FactIdentityTests
{
    private static readonly ProjectFactId Project = ProjectFactId.Create("src/Acme.Payments/Acme.Payments.csproj");

    [Fact]
    public void ProjectId_RelativePath_UsesNormativeGrammarAndUppercasePercentEscapes() =>
        Assert.Equal(
            "id1:project;path=src%2FAcme.Payments%2FAcme.Payments.csproj",
            Project.Value);

    [Theory]
    [InlineData("C:/repo/App.csproj")]
    [InlineData("/repo/App.csproj")]
    [InlineData("src\\App.csproj")]
    [InlineData("src/./App.csproj")]
    [InlineData("src/../App.csproj")]
    [InlineData("src//App.csproj")]
    public void ProjectId_NonRelativeOrNonNormalizedPath_IsRejected(string path) =>
        Assert.Throws<ArgumentException>(() => ProjectFactId.Create(path));

    [Fact]
    public void TargetAndDocumentIds_NestedIdsAndFixedKeys_AreEmittedInNormativeOrder()
    {
        var target = TargetFactId.Create(Project, "net10.0");
        var document = DocumentFactId.Create(Project, "Handlers/Payment.cs");

        Assert.Equal(
            "id1:target;project=id1%3Aproject%3Bpath%3Dsrc%252FAcme.Payments%252FAcme.Payments.csproj;tfm=net10.0",
            target.Value);
        Assert.Equal(
            "id1:document;project=id1%3Aproject%3Bpath%3Dsrc%252FAcme.Payments%252FAcme.Payments.csproj;path=Handlers%2FPayment.cs",
            document.Value);
    }

    [Fact]
    public void ProjectId_DifferentAbsoluteRoots_ProduceSameIdentity()
    {
        var underFirstRoot = ProjectFactId.Create("src/Acme.Payments/Acme.Payments.csproj");
        var underSecondRoot = ProjectFactId.Create("src/Acme.Payments/Acme.Payments.csproj");

        Assert.Equal(underFirstRoot, underSecondRoot);
        Assert.DoesNotContain("first-root", underFirstRoot.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("second-root", underSecondRoot.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectId_CaseAndUnicode_ArePreservedWithoutNormalization()
    {
        var upper = ProjectFactId.Create("Src/Café.csproj");
        var lowerDecomposed = ProjectFactId.Create("src/Cafe\u0301.csproj");

        Assert.NotEqual(upper, lowerDecomposed);
        Assert.Contains("Src", upper.Value, StringComparison.Ordinal);
        Assert.Contains("%C3%A9", upper.Value, StringComparison.Ordinal);
        Assert.Contains("%CC%81", lowerDecomposed.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvedSymbolId_DocumentationCommentId_UsesNormativeGrammar()
    {
        var target = TargetFactId.Create(Project, "net10.0");

        var id = SymbolFactId.CreateResolved(target, "M:Acme.Payment.Run(System.String)");

        Assert.EndsWith(";doc=M%3AAcme.Payment.Run%28System.String%29", id.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("span", id.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FallbackSymbolId_UnrelatedPrecedingEdit_DoesNotChangeIdentity()
    {
        var target = TargetFactId.Create(Project, "net10.0");
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment",
            "Run`1",
            1,
            "global::System.Threading.Tasks.Task",
            [new("global::System.String", SymbolParameterModifier.In)],
            ["global::T"]);

        var beforeEdit = SymbolFactId.CreateFallback(target, signature);
        var afterEdit = SymbolFactId.CreateFallback(target, signature);

        Assert.Equal(beforeEdit, afterEdit);
        Assert.DoesNotContain("line", beforeEdit.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("span", beforeEdit.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanonicalFallbackSignature_ContainsKindsContainmentMetadataArityTypesAndModifiersButNoParameterNames()
    {
        var signature = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payment",
            "TryRun`1",
            1,
            "global::System.Boolean",
            [new("global::System.String", SymbolParameterModifier.Ref), new("global::System.Int32", SymbolParameterModifier.Out)],
            ["global::T"]);

        Assert.Equal(
            "sig1;kind=method;container=global%3A%3AAcme.Payment;metadata=TryRun%601;arity=1;type=global%3A%3ASystem.Boolean;parameters=ref%20global%3A%3ASystem.String%2Cout%20global%3A%3ASystem.Int32;type-arguments=global%3A%3AT",
            signature.Value);
        Assert.DoesNotContain("parameterName", signature.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void SyntacticSymbolId_NormalizedSignature_IsStableWithoutLocation()
    {
        var beforeEdit = SymbolFactId.CreateSyntactic(Project, "Payment.cs", "method", "Run<T>(string value)");
        var afterEdit = SymbolFactId.CreateSyntactic(Project, "Payment.cs", "method", "Run<T>(string value)");

        Assert.Equal(beforeEdit, afterEdit);
        Assert.EndsWith(";kind=method;signature=Run%3CT%3E%28string%20value%29", beforeEdit.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" Run()")]
    [InlineData("Run() ")]
    [InlineData("Run(  )")]
    [InlineData("Run()\n")]
    public void SyntacticSymbolId_NonCanonicalSignature_IsRejected(string signature) =>
        Assert.Throws<ArgumentException>(() => SymbolFactId.CreateSyntactic(Project, "Payment.cs", "method", signature));

    [Fact]
    public void ComponentId_OwnersAreSortedOrdinallyAndDeduplicated()
    {
        var first = ProjectFactId.Create("A/A.csproj").ToFactId();
        var second = ProjectFactId.Create("B/B.csproj").ToFactId();

        var forward = ComponentFactId.Create("library", [first, second, first]);
        var reverse = ComponentFactId.Create("library", [second, first]);

        Assert.Equal(forward, reverse);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RelationId_NonOneBasedOrdinal_IsRejected(int ordinal) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RelationFactId.Create(Project.ToFactId(), "http", "claim", ordinal));

    [Fact]
    public void RelationId_EquivalentClaimsUseOnlyTheOneBasedOccurrenceOrdinal()
    {
        var first = RelationFactId.Create(Project.ToFactId(), "http", "GET /payments", 1);
        var second = RelationFactId.Create(Project.ToFactId(), "http", "GET /payments", 2);

        Assert.NotEqual(first, second);
        Assert.EndsWith(";claim=GET%20%2Fpayments;ordinal=1", first.Value, StringComparison.Ordinal);
        Assert.EndsWith(";claim=GET%20%2Fpayments;ordinal=2", second.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Com.Acme.Detector")]
    [InlineData("com_acme_detector")]
    [InlineData("detector")]
    [InlineData("com..detector")]
    public void DetectorId_NonLowerAsciiReverseDnsName_IsRejected(string name) =>
        Assert.Throws<ArgumentException>(() => DetectorId.Create(name));

    [Fact]
    public void DiagnosticAndDetectorIds_UseFixedNormativeKeys()
    {
        var detector = DetectorId.Create("io.csharp2md.aspnet-core");
        var diagnostic = DiagnosticId.Create("validation", Project.ToFactId(), "FACT012", "duplicate id");

        Assert.Equal("id1:detector;name=io.csharp2md.aspnet-core", detector.Value);
        Assert.StartsWith("id1:diagnostic;stage=validation;scope=", diagnostic.Value, StringComparison.Ordinal);
        Assert.EndsWith(";code=FACT012;fingerprint=duplicate%20id", diagnostic.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ArtifactReference_LongId_UsesLowercaseSha256AndPortablePath()
    {
        var longProject = ProjectFactId.Create($"src/{new string('a', 4000)}.csproj");

        var reference = ArtifactReference.Create(longProject.ToFactId());
        var segments = reference.Value.Split('/');

        Assert.Equal("facts/project", string.Join('/', segments[..2]));
        Assert.Equal(2, segments[2].Length);
        Assert.Matches("^[0-9a-f]{64}\\.json$", segments[3]);
        Assert.StartsWith(segments[2], segments[3], StringComparison.Ordinal);
    }

    [Fact]
    public void ArtifactReference_DifferentIdsDoNotCollideAndSameIdIsDeterministic()
    {
        var first = ArtifactReference.Create(Project.ToFactId());
        var repeated = ArtifactReference.Create(Project.ToFactId());
        var otherId = DocumentFactId.Create(Project, "Payment.cs").ToFactId();
        var other = ArtifactReference.Create(otherId);

        Assert.Equal(first, repeated);
        Assert.NotEqual(first, other);
        Assert.False(ArtifactReference.IsCollision(Project.ToFactId(), first, otherId, other));
    }

    [Fact]
    public void ArtifactReference_DistinctIdsWithSameReference_AreReportedAsCollision()
    {
        var forcedReference = ArtifactReference.Parse(
            "facts/project/aa/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.json");
        var otherId = DocumentFactId.Create(Project, "Payment.cs").ToFactId();

        Assert.True(ArtifactReference.IsCollision(Project.ToFactId(), forcedReference, otherId, forcedReference));
    }
}
