using System.Text.Json;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Reading;

public sealed class QuarantinedPackageReadTests
{
    [Fact]
    [Trait("Requirement", "STOR-38")]
    public void Read_PackageWithQuarantinedDerivedFact_OmitsItFromSnapshotAndExposesQuarantine()
    {
        using var output = TempOutputRoot.Create();
        var package = Path.Combine(output.DirectoryPath, "quarantined-package");
        var document = DomainMapper.ToWire(SolutionAndComponentSnapshot(), new ManifestContext("s-test", "Acme.sln"));
        var invalid = document.Components[0] with { Name = "Pay  ments.Api" };
        invalid = invalid with { ContentSha256 = CanonicalJson.PayloadContentSha256(invalid) };
        var mutated = document with { Components = [invalid] };
        var report = PackageValidator.Validate(mutated);
        Assert.Equal("failed", report.Document.RunCertification.Status);
        Assert.True(report.Document.Components.IsEmpty);
        PackageDirectoryWriter.Write(package, report.Document);

        var result = FactualPackageReader.Read(package);

        var remaining = Assert.Single(result.Snapshot.Facts);
        Assert.Equal("Solution", remaining.Reference.FactType);
        Assert.Equal(report.Document.Solutions[0].Identity.Id, remaining.Reference.Id.Value);
        Assert.DoesNotContain(result.Snapshot.Facts, fact => fact.Reference.Id.Value == invalid.Identity.Id);
        Assert.DoesNotContain(result.Snapshot.Facts, fact => fact.Reference.FactType == "Component");
        Assert.True(result.Snapshot.ConfirmedRelations.IsEmpty);

        var quarantined = Assert.Single(result.Quarantine);
        Assert.Equal("Component", quarantined.RecordKind);
        Assert.Equal("construction", quarantined.Gate);
        Assert.Contains(invalid.Identity.Id, quarantined.IdentityOrKey, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(quarantined.Detail));
        Assert.Equal(JsonValueKind.Object, quarantined.Payload.ValueKind);
        Assert.Equal("Pay  ments.Api", quarantined.Payload.GetProperty("name").GetString());
        Assert.Equal("failed", result.Certification.Status);
    }

    private static FactualSnapshot SolutionAndComponentSnapshot() =>
        new(
            [Solution.Create(AcmeSolution), Component.Create(AcmeSolution, "Payments.Api", [])],
            [],
            [],
            [],
            [],
            []);

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");
}
