using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.DataAccess.EfCore;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class EfCoreAnalyzerTests
{
    // DAD-01: one claim per DbSet<TEntity> property the DbContext subclass declares.
    [Fact]
    public void Analyze_DbContextWithTwoEntitySets_YieldsExactlyTwoClaimsNamingBothEntities()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
                public DbSet<Customer> Customers { get; set; }
            }
            """);

        Assert.Equal(2, claims.Length);
        Assert.All(claims, claim => Assert.Equal(DatabaseClaimKind.EntitySetExposed, claim.Kind));
        Assert.Equal("Order", claims[0].EntityText);
        Assert.Equal("Orders", claims[0].PropertyText);
        Assert.Equal("Customer", claims[1].EntityText);
        Assert.Equal("Customers", claims[1].PropertyText);
    }

    // DAD-01: the claim is sourced at the declaring type's symbol id, not at the property's.
    [Fact]
    public void Analyze_EntitySetClaim_IsSourcedAtTheDeclaringContextTypeSymbolId()
    {
        const string Source = """
            class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }
            """;

        var claim = Assert.Single(EfCoreAnalysis.Claims(Source));

        Assert.Equal(EfCoreAnalysis.SymbolIdOfTypeDeclaration(Source, "OrderDbContext"), claim.OwnerId);
    }

    // DAD-13: evidence spans the DbSet property declaration that proves the exposure.
    [Fact]
    public void Analyze_EntitySetClaim_CarriesEvidenceSpanningTheDbSetPropertyDeclaration()
    {
        var claim = Assert.Single(EfCoreAnalysis.Claims(
            """
            class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }
            """));

        Assert.Equal(DataAccessTestFacts.Context().DocumentId, claim.Evidence.DocumentId);
        Assert.Equal("src/App/OrderRepository.cs", claim.Evidence.RelativePath);
        Assert.Equal(3, claim.Evidence.StartLine);
        Assert.Equal(5, claim.Evidence.StartColumn);
        Assert.Equal(3, claim.Evidence.EndLine);
        Assert.Equal(45, claim.Evidence.EndColumn);
    }

    // DAD-01: the exposure is proven by syntax alone, and every claim names the analyzer that made it.
    [Fact]
    public void Analyze_EntitySetClaim_IsSyntacticAndAttributedToTheEfCoreAnalyzer()
    {
        var claim = Assert.Single(EfCoreAnalysis.Claims(
            """
            class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }
            """));

        Assert.Equal(FactResolution.Syntactic, claim.ShapeConfidence);
        Assert.Equal(DataAccessAnalyzerId.Create("csharp2md.dataaccess.efcore"), claim.AnalyzerId);
    }

    // Spec Edge Case: a DbSet property on a type that does not derive from DbContext proves nothing.
    [Fact]
    public void Analyze_DbSetPropertyOnATypeThatDoesNotDeriveFromDbContext_YieldsNothing()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderCache
            {
                public DbSet<Order> Orders { get; set; }
            }
            """);

        Assert.Empty(claims);
    }

    // DAD-17: a name is never evidence of database access.
    [Fact]
    public void Analyze_RepositoryNamedClassWithNoPersistenceApiUsage_YieldsNothing()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderRepository
            {
                public Order Load(int id) => new Order { Id = id };
            }
            """);

        Assert.Empty(claims);
    }

    // The shared framework-type filter keeps a DbSet over a BCL shape out of the entity catalogue.
    [Fact]
    public void Analyze_EntitySetOverAFrameworkType_IsFilteredOutOfTheCatalogue()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderDbContext : DbContext
            {
                public DbSet<DateTime> Stamps { get; set; }
                public DbSet<Order> Orders { get; set; }
            }
            """);

        var claim = Assert.Single(claims);
        Assert.Equal("Order", claim.EntityText);
    }

    // DAD-01 is only reachable on a real run if the analyzer is one the collector actually runs.
    [Fact]
    public void RegisteredAnalyzers_IncludeTheEfCoreAnalyzer()
    {
        var registered = Assert.Single(
            DataAccessCollector.RegisteredAnalyzers.OfType<EfCoreAnalyzer>());

        Assert.Equal(DataAccessAnalyzerId.Create("csharp2md.dataaccess.efcore"), registered.Id);
    }
}

internal static class EfCoreAnalysis
{
    public static ImmutableArray<RawDatabaseClaim> Claims(string source)
    {
        var claims = ImmutableArray.CreateBuilder<RawDatabaseClaim>();
        new EfCoreAnalyzer().Analyze(DataAccessTestFacts.Context(source), claims);
        return claims.ToImmutable();
    }

    public static FactId SymbolIdOfTypeDeclaration(string source, string typeName)
    {
        var declaration = CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == typeName);

        return SymbolFactId.CreateSyntactic(
            DataAccessTestFacts.ProjectId,
            "OrderRepository.cs",
            SyntaxFactExtractor.DeclarationKind(declaration),
            SyntaxFactExtractor.DeclarationSignature(declaration)).ToFactId();
    }
}
