using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Validation;

public sealed class PackageValidatorTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    private const string StructuralArtifactKey = "facts/structural.json";

    [Fact]
    [Trait("Requirement", "STOR-25")]
    public void ReadPayloadOrThrow_UnknownProperty_AbortsSchemaNamingArtifactKey()
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Write(ValidSolutionDto()).AsSpan());
        var mutated = json.Replace("\"identity\"", "\"unknown_field\":true,\"identity\"", StringComparison.Ordinal);

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.ReadPayloadOrThrow<SolutionDto>(Encoding.UTF8.GetBytes(mutated), StructuralArtifactKey));

        Assert.Equal("schema", exception.Gate);
        Assert.Contains(StructuralArtifactKey, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-25")]
    public void ReadPayloadOrThrow_TypeMismatch_AbortsSchemaNamingArtifactKey()
    {
        var dto = ValidSolutionDto();
        var json = Encoding.UTF8.GetString(CanonicalJson.Write(dto).AsSpan());
        var mutated = json.Replace($"\"{dto.ContentSha256}\"", "true", StringComparison.Ordinal);

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.ReadPayloadOrThrow<SolutionDto>(Encoding.UTF8.GetBytes(mutated), StructuralArtifactKey));

        Assert.Equal("schema", exception.Gate);
        Assert.Contains(StructuralArtifactKey, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-26")]
    public void Validate_UnregisteredFactType_AbortsNamingTheValue()
    {
        const string unregistered = "NotARegisteredFact";
        var document = DomainMapper.ToWire(SolutionSnapshot(), Context);
        var mutated = document with
        {
            Solutions = [document.Solutions[0] with { Identity = document.Solutions[0].Identity with { FactType = unregistered } }],
        };

        var exception = Assert.Throws<PublicationRejectedException>(() => PackageValidator.Validate(mutated));

        Assert.Equal("unregistered-kind", exception.Gate);
        Assert.Contains(unregistered, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-26")]
    public void Validate_UnregisteredObservationKind_AbortsNamingTheValue()
    {
        const string unregistered = "not-a-registered-observation";
        var document = DomainMapper.ToWire(ObservationSnapshot(), Context);
        var wireName = document.Observations.Keys.Single();
        var original = document.Observations[wireName][0];
        var mutated = document with
        {
            Observations = document.Observations.SetItem(
                wireName,
                [original with { Identity = original.Identity with { Kind = unregistered } }]),
        };

        var exception = Assert.Throws<PublicationRejectedException>(() => PackageValidator.Validate(mutated));

        Assert.Equal("unregistered-kind", exception.Gate);
        Assert.Contains(unregistered, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-26")]
    public void Validate_UnregisteredRelationKind_AbortsNamingTheValue()
    {
        const string unregistered = "not-a-registered-relation";
        var document = DomainMapper.ToWire(ConfirmedRelationSnapshot(), Context);
        var wireName = document.ConfirmedRelations.Keys.Single();
        var original = document.ConfirmedRelations[wireName][0];
        var mutated = document with
        {
            ConfirmedRelations = document.ConfirmedRelations.SetItem(
                wireName,
                [original with { Kind = unregistered }]),
        };

        var exception = Assert.Throws<PublicationRejectedException>(() => PackageValidator.Validate(mutated));

        Assert.Equal("unregistered-kind", exception.Gate);
        Assert.Contains(unregistered, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-27")]
    public void Validate_TwoFactsSharingOneIdentity_AbortsNamingTheIdentity()
    {
        var document = DomainMapper.ToWire(SolutionSnapshot(), Context);
        var fact = document.Solutions[0];
        var identity = fact.Identity.Id;
        var mutated = document with { Solutions = [fact, fact] };

        var exception = Assert.Throws<PublicationRejectedException>(() => PackageValidator.Validate(mutated));

        Assert.Equal("identity-collision", exception.Gate);
        Assert.Contains(identity, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-28")]
    public void Validate_ContentHashMismatch_AbortsNamingTheIdentity()
    {
        var document = DomainMapper.ToWire(SolutionSnapshot(), Context);
        var fact = document.Solutions[0] with { ContentSha256 = new string('0', 64) };
        var mutated = document with { Solutions = [fact] };

        Assert.NotEqual(CanonicalJson.PayloadContentSha256(fact), fact.ContentSha256);

        var exception = Assert.Throws<PublicationRejectedException>(() => PackageValidator.Validate(mutated));

        Assert.Equal("content-hash", exception.Gate);
        Assert.Contains(fact.Identity.Id, exception.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Requirement", "STOR-29")]
    [InlineData("/opt/app")]
    [InlineData("\\server\\share")]
    [InlineData("C:/windows/system32")]
    public void Validate_AbsoluteFilesystemPath_AbortsNamingTheField(string absolutePath)
    {
        var document = DomainMapper.ToWire(ComponentSnapshot(), Context);
        var renamed = document.Components[0] with { Name = absolutePath };
        renamed = renamed with { ContentSha256 = CanonicalJson.PayloadContentSha256(renamed) };
        var mutated = document with { Components = [renamed] };

        var exception = Assert.Throws<PublicationRejectedException>(() => PackageValidator.Validate(mutated));

        Assert.Equal("absolute-path", exception.Gate);
        Assert.Contains("name", exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-29")]
    public void Validate_RelativePathInDomainPathField_DoesNotAbort()
    {
        const string relativePath = "src/Acme.Payments/Invoice.cs";
        var document = DomainMapper.ToWire(DocumentSnapshot(), Context);

        Assert.Equal(relativePath, document.Documents[0].RelativePath);

        var report = PackageValidator.Validate(document);

        Assert.Equal(relativePath, report.Document.Documents[0].RelativePath);
        Assert.True(report.Quarantine.IsEmpty);
    }

    private static SolutionDto ValidSolutionDto() =>
        DomainMapper.ToWire(SolutionSnapshot(), Context).Solutions[0];

    private static FactualSnapshot SolutionSnapshot() =>
        new([Solution.Create(AcmeSolution)], [], [], [], [], []);

    private static FactualSnapshot DocumentSnapshot() =>
        new([Document.Create(AcmeProject, "src/Acme.Payments/Invoice.cs")], [], [], [], [], []);

    private static FactualSnapshot ComponentSnapshot() =>
        new([Component.Create(AcmeSolution, "Payments.Api", [])], [], [], [], [], []);

    private static FactualSnapshot ObservationSnapshot() =>
        new(
            [],
            [Observation.Create(
                Solution.Create(AcmeSolution).Reference,
                ObservationKind.Invocation,
                NormalizedPayload.Create([]),
                1,
                new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Payments/Invoice.cs", new SourceSpan(1, 1, 1, 8)),
                EvidenceMethod.Semantic,
                new BindingDiagnostic("BIND001", "Bound successfully."),
                DocumentHash.Create(new string('a', 64)),
                new ExtractorVersion(1))],
            [],
            [],
            [],
            []);

    private static FactualSnapshot ConfirmedRelationSnapshot()
    {
        var relation = ConfirmedRelation.Create(
            RelationKind.Contains,
            Solution.Create(AcmeSolution).Reference,
            Project.Create(AcmeProject).Reference,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([new ObservationIdentity(
                Solution.Create(AcmeSolution).Reference,
                ObservationKind.Invocation,
                NormalizedPayload.Create([]),
                1)]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
        return new FactualSnapshot([], [], [relation], [], [], []);
    }

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");
}
