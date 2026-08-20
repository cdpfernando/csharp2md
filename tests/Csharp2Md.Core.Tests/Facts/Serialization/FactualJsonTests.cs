using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Facts.Serialization;

public sealed class FactualJsonTests
{
    [Fact]
    public void SerializeAndDeserialize_EveryFactualFamily_RoundTripsThroughSourceGeneratedContract()
    {
        var document = EveryFamilyDocument();

        var bytes = FactualJsonSerializer.Serialize(document);
        var restored = FactualJsonSerializer.Deserialize(bytes);

        Assert.Equal(bytes, FactualJsonSerializer.Serialize(restored));
        Assert.Single(restored.Solutions);
        Assert.Single(restored.Projects);
        Assert.Single(restored.Targets);
        Assert.Single(restored.Documents);
        Assert.Single(restored.SourceSections);
        Assert.Single(restored.Symbols);
        Assert.Single(restored.Components);
        Assert.Single(restored.Relations);
        Assert.Single(restored.Diagnostics);
        Assert.Single(restored.Coverage);
    }

    [Fact]
    public void Serialize_UnorderedFamilies_UsesCanonicalFactIdOrder()
    {
        var later = Project("id1:project;path=B.csproj", "B.csproj");
        var earlier = Project("id1:project;path=A.csproj", "A.csproj");
        var document = EmptyDocument() with { Projects = [later, earlier] };

        using var json = JsonDocument.Parse(FactualJsonSerializer.Serialize(document));
        var ids = json.RootElement.GetProperty("projects")
            .EnumerateArray()
            .Select(static project => project.GetProperty("header").GetProperty("id").GetString()!)
            .ToArray();

        Assert.Equal(["id1:project;path=A.csproj", "id1:project;path=B.csproj"], ids);
    }

    [Fact]
    public void Serialize_MachineBytes_AreUtf8LfWithoutBomTimestampOrAbsoluteRoot()
    {
        var bytes = FactualJsonSerializer.Serialize(EveryFamilyDocument());
        var json = Encoding.UTF8.GetString(bytes);

        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.DoesNotContain('\r', json);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain("timestamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("D:\\workspace", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeAndDeserialize_SymbolIdentityFields_RoundTripEveryNewProperty()
    {
        var document = EveryFamilyDocument();

        var restored = FactualJsonSerializer.Deserialize(FactualJsonSerializer.Serialize(document));

        var symbol = Assert.Single(restored.Symbols);
        Assert.Equal("Feature", symbol.Name);
        Assert.Equal("global::App.Feature", symbol.FullyQualifiedName);
        Assert.Equal("App", symbol.Namespace);
        Assert.Equal("global::App.Outer", symbol.ContainingType);
        Assert.Equal(
            "id1:syntactic-symbol;project=x;document=Feature.cs;kind=class;signature=Outer",
            symbol.ContainingSymbolId);
        Assert.Equal("class Feature", symbol.Signature);
        Assert.Equal(1, symbol.Arity);
        Assert.Equal(["global::System.String"], symbol.ParameterTypes.ToArray());
    }

    [Fact]
    public void Serialize_SymbolIdentityFields_UseTheSnakeCaseWirePropertyNames()
    {
        using var json = JsonDocument.Parse(FactualJsonSerializer.Serialize(EveryFamilyDocument()));

        var symbol = json.RootElement.GetProperty("symbols").EnumerateArray().Single();
        Assert.Equal("Feature", symbol.GetProperty("name").GetString());
        Assert.Equal("global::App.Feature", symbol.GetProperty("fully_qualified_name").GetString());
        Assert.Equal("App", symbol.GetProperty("namespace").GetString());
        Assert.Equal("global::App.Outer", symbol.GetProperty("containing_type").GetString());
        Assert.Equal(
            "id1:syntactic-symbol;project=x;document=Feature.cs;kind=class;signature=Outer",
            symbol.GetProperty("containing_symbol_id").GetString());
        Assert.Equal("class Feature", symbol.GetProperty("signature").GetString());
        Assert.Equal(1, symbol.GetProperty("arity").GetInt32());
        Assert.Equal(
            ["global::System.String"],
            symbol.GetProperty("parameter_types").EnumerateArray().Select(static item => item.GetString()!).ToArray());
        Assert.Equal(3, json.RootElement.GetProperty("schema_version").GetInt32());
    }

    [Fact]
    public void Map_SymbolFactWithEveryIdentityFieldPopulated_CarriesThemOntoTheWireContract()
    {
        var owner = SymbolFactId.CreateSyntactic(MapperProject, "Feature.cs", "class", "Outer");
        var symbol = MapperSymbol() with
        {
            Namespace = "App",
            ContainingType = "global::App.Outer",
            ContainingSymbolId = owner,
            Arity = 2,
            ParameterTypes = ["global::System.String", "global::System.Int32"],
        };

        var mapped = Assert.Single(FactualJsonMapper.Map(new ValidatedFactFragment([symbol], [])).Symbols);

        Assert.Equal("Feature", mapped.Name);
        Assert.Equal("global::App.Feature", mapped.FullyQualifiedName);
        Assert.Equal("App", mapped.Namespace);
        Assert.Equal("global::App.Outer", mapped.ContainingType);
        Assert.Equal(owner.Value, mapped.ContainingSymbolId);
        Assert.Equal("class Feature", mapped.Signature);
        Assert.Equal(2, mapped.Arity);
        Assert.Equal(["global::System.String", "global::System.Int32"], mapped.ParameterTypes.ToArray());
    }

    [Fact]
    public void Map_SymbolFactWithoutNamespaceContainingTypeOrOwner_CarriesNullForAllThree()
    {
        var mapped = Assert.Single(FactualJsonMapper.Map(new ValidatedFactFragment([MapperSymbol()], [])).Symbols);

        Assert.Null(mapped.Namespace);
        Assert.Null(mapped.ContainingType);
        Assert.Null(mapped.ContainingSymbolId);
        Assert.Equal("Feature", mapped.Name);
        Assert.Equal(0, mapped.Arity);
        Assert.Empty(mapped.ParameterTypes);
    }

    private static readonly ProjectFactId MapperProject = ProjectFactId.Create("src/App/App.csproj");

    private static SymbolFact MapperSymbol()
    {
        var id = SymbolFactId.CreateSyntactic(MapperProject, "Feature.cs", "class", "Feature");
        return new SymbolFact(
            FactHeader.Create(id.ToFactId(), FactKind.Symbol, FactResolution.Syntactic),
            id,
            DocumentFactId.Create(MapperProject, "Feature.cs"),
            "class",
            ContainsErrorSymbol: false,
            [],
            [],
            [],
            Semantics: null,
            Name: "Feature",
            FullyQualifiedName: "global::App.Feature",
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: "class Feature",
            Arity: 0,
            ParameterTypes: []);
    }

    [Fact]
    public void Serialize_SchemaVersionOtherThanThree_IsRejected() =>
        Assert.Throws<ArgumentException>(() => FactualJsonSerializer.Serialize(EmptyDocument() with { SchemaVersion = 2 }));

    [Fact]
    public void SourceGeneratedContext_ContainsRootAndEveryTransitiveFamilyContract()
    {
        var typeInfo = FactualJsonContext.Default.FactualJsonDocument;
        var propertyTypes = typeInfo.Properties.Select(static property => property.PropertyType).ToArray();

        Assert.Equal(typeof(FactualJsonDocument), typeInfo.Type);
        Assert.Contains(typeof(ImmutableArray<SolutionFactJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<ProjectFactJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<TargetFactJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<DocumentFactJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<SourceSectionFactJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<SymbolFactJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<ComponentFactJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<RelationFactJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<AnalysisDiagnosticJson>), propertyTypes);
        Assert.Contains(typeof(ImmutableArray<CoverageFactJson>), propertyTypes);
    }

    [Fact]
    public Task Serialize_RepresentativeFactualDocument_MatchesApprovedSpecSnapshot()
    {
        var json = Encoding.UTF8.GetString(FactualJsonSerializer.Serialize(EmptyDocument() with
        {
            Projects = [Project("id1:project;path=src%2FApp.csproj", "src/App.csproj")],
        }));

        return Verifier.Verify(json, "json").UseDirectory("snapshots");
    }

    private static FactualJsonDocument EveryFamilyDocument()
    {
        const string projectId = "id1:project;path=src%2FApp.csproj";
        const string targetId = "id1:target;project=id1%3Aproject%3Bpath%3Dsrc%252FApp.csproj;tfm=net10.0";
        const string documentId = "id1:document;project=id1%3Aproject%3Bpath%3Dsrc%252FApp.csproj;path=Feature.cs";
        const string symbolId = "id1:syntactic-symbol;project=x;document=Feature.cs;kind=class;signature=Feature";
        const string containingSymbolId = "id1:syntactic-symbol;project=x;document=Feature.cs;kind=class;signature=Outer";
        const string sectionId = "id1:section;document=x;kind=type;ordinal=1";
        const string componentId = "id1:component;kind=library;owners=x";
        const string relationId = "id1:relation;owner=x;kind=http;claim=get;ordinal=1";
        const string diagnosticId = "id1:diagnostic;stage=document;scope=x;code=C2M1;fingerprint=f";
        var evidence = new EvidenceJson(documentId, "Feature.cs", 1, 1, 1, 8);
        var provenance = new FactProvenanceJson("csharp2md", "3.0.0", null, null);

        return new FactualJsonDocument(
            FactualJsonSerializer.SchemaVersion,
            [new(Header("id1:solution;path=App.slnx", "solution"), "App", [projectId])],
            [new(Header(projectId, "project"), projectId, "App", "src/App.csproj", [targetId], [documentId])],
            [new(Header(targetId, "target"), targetId, projectId, "net10.0")],
            [new(Header(documentId, "document"), documentId, projectId, "Feature.cs", [sectionId], [symbolId])],
            [new(new FactHeaderJson(sectionId, "source-section", "syntactic", [provenance], [evidence], []), documentId, "type", 1, 0, 7, "class C")],
            [new(new FactHeaderJson(symbolId, "symbol", "syntactic", [provenance], [evidence], []), symbolId, documentId, "class", false, [], [], [],
                null, "Feature", "global::App.Feature", "App", "global::App.Outer", containingSymbolId, "class Feature", 1, ["global::System.String"])],
            [new(Header(componentId, "component"), componentId, "library", [projectId])],
            [new(new FactHeaderJson(relationId, "relation", "unresolved", [new("csharp2md", "3.0.0", "id1:detector;name=io.csharp2md.http", "1.0.0")], [evidence], [diagnosticId]), relationId, documentId, null, "http", "http-request", "No target proved.")],
            [new(diagnosticId, "C2M1", "warning", "document", documentId, "Binding degraded", [new("reason", "missing target")], [evidence], null)],
            [new(documentId, "document", null, "applicable", "attempted", "syntactic", [diagnosticId])]);
    }

    private static FactualJsonDocument EmptyDocument() => new(
        FactualJsonSerializer.SchemaVersion,
        [], [], [], [], [], [], [], [], [], []);

    private static ProjectFactJson Project(string id, string relativePath) => new(
        Header(id, "project"),
        id,
        Path.GetFileNameWithoutExtension(relativePath),
        relativePath,
        [],
        []);

    private static FactHeaderJson Header(string id, string kind) => new(
        id,
        kind,
        "syntactic",
        [new("csharp2md", "3.0.0", null, null)],
        [],
        []);
}
