using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Facts;

public sealed class StructuralFactsTests
{
    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");

    private static CanonicalSymbolSignature RunSignature => CanonicalSymbolSignature.Create(
        "method", "global::Acme.Payment", "Run", 0, "global::System.Void");

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Solution_Create_ExposesReferenceAndFamily()
    {
        var solution = Solution.Create(AcmeSolution);

        Assert.Equal("Solution", solution.Reference.FactType);
        Assert.Equal(FactFamily.Structural, solution.Family);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Solution_Create_DefaultId_IsRejectedNamingId()
    {
        var exception = Assert.Throws<ArgumentException>(() => Solution.Create(default));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Project_Create_ExposesReferenceAndFamily()
    {
        var project = Project.Create(AcmeProject);

        Assert.Equal("Project", project.Reference.FactType);
        Assert.Equal(FactFamily.Structural, project.Family);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Project_Create_DefaultId_IsRejectedNamingId()
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(default));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Document_Create_ExposesReferenceAndFamily()
    {
        var document = Document.Create(AcmeProject, "src/Acme.Payments/Invoice.cs");

        Assert.Equal("Document", document.Reference.FactType);
        Assert.Equal(FactFamily.Structural, document.Family);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Document_Create_DefaultOwningProject_IsRejectedNamingOwningProject()
    {
        var exception = Assert.Throws<ArgumentException>(() => Document.Create(default, "src/Invoice.cs"));

        Assert.Equal("owningProject", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-08")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/absolute/path.cs")]
    public void Document_Create_InvalidRelativePath_IsRejectedNamingRelativePath(string path)
    {
        var exception = Assert.Throws<ArgumentException>(() => Document.Create(AcmeProject, path));

        Assert.Equal("relativePath", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Symbol_Create_ExposesReferenceAndFamily()
    {
        var symbol = Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([]));

        Assert.Equal("Symbol", symbol.Reference.FactType);
        Assert.Equal(FactFamily.Structural, symbol.Family);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Symbol_Create_DefaultSignature_IsRejectedNamingSignature()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Symbol.Create(default, AcmeProject, SymbolFacetSet.Create([])));

        Assert.Equal("signature", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Symbol_Create_DefaultOwningProject_IsRejectedNamingOwningProject()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Symbol.Create(RunSignature, default, SymbolFacetSet.Create([])));

        Assert.Equal("owningProject", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void Symbol_Create_DefaultFacetSet_IsRejectedNamingFacets()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Symbol.Create(RunSignature, AcmeProject, default));

        Assert.Equal("facets", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-14")]
    public void Symbol_CarriesSymbolFacetSet_AndCallableIsAFacetValueNotAType()
    {
        var facets = SymbolFacetSet.Create([SymbolFacet.Callable]);
        var symbol = Symbol.Create(RunSignature, AcmeProject, facets);

        Assert.Equal(facets, symbol.Facets);
        Assert.Null(typeof(IFact).Assembly.GetTypes().FirstOrDefault(type => type.Name == "Callable"));
    }

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void Symbol_Create_OmittingLocator_LeavesDeclarationLocatorAbsent()
    {
        var symbol = Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([]));

        Assert.Null(symbol.DeclarationLocator);
    }

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void Symbol_Create_WithAndWithoutLocator_ProducesTheSameFactId()
    {
        var locator = new DeclarationLocator(
            DocumentId.Create("src/Acme.Payments/Invoice.cs"),
            "src/Acme.Payments/Invoice.cs",
            new SourceSpan(10, 1, 24, 2),
            DocumentHash.Create(new string('a', 64)));

        var withoutLocator = Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([]));
        var withLocator = Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([]), locator);

        Assert.Equal(locator, withLocator.DeclarationLocator);
        Assert.Equal(withoutLocator.Reference.Id.Value, withLocator.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void Symbol_Create_SuppliedUninitializedLocator_IsRejectedNamingDeclarationLocator()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([]), default(DeclarationLocator)));

        Assert.Equal("declarationLocator", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void StructuralFamily_HasExactlyFourTypesEachExposingReferenceAndFamily()
    {
        var expectedNames = new HashSet<string> { "Solution", "Project", "Document", "Symbol" };
        var actualNames = typeof(IFact).Assembly.GetTypes()
            .Where(type => typeof(IFact).IsAssignableFrom(type) && !type.IsInterface)
            .Select(type => type.Name)
            .Where(expectedNames.Contains)
            .ToHashSet();

        Assert.Equal(expectedNames, actualNames);

        var instances = new IFact[]
        {
            Solution.Create(AcmeSolution),
            Project.Create(AcmeProject),
            Document.Create(AcmeProject, "src/Invoice.cs"),
            Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([])),
        };
        Assert.All(instances, fact => Assert.Equal(FactFamily.Structural, fact.Family));
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void StructuralFamily_EachTypesReferenceFactType_MatchesItsRegistryDescriptorNameExactly()
    {
        var registeredNames = FactTypeTable.All
            .Where(descriptor => descriptor.Family == FactFamily.Structural)
            .Select(descriptor => descriptor.Name)
            .ToHashSet();

        var instances = new IFact[]
        {
            Solution.Create(AcmeSolution),
            Project.Create(AcmeProject),
            Document.Create(AcmeProject, "src/Invoice.cs"),
            Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([])),
        };

        Assert.All(instances, fact => Assert.Contains(fact.Reference.FactType, registeredNames));
        Assert.Equal(registeredNames.Count, instances.Select(fact => fact.Reference.FactType).Distinct().Count());
    }
}
