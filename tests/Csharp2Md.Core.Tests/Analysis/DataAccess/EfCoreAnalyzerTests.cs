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

    // DAD-03: a literal ToTable proves the table's name, so the claim is Exact and names both sides.
    [Fact]
    public void Analyze_EntityChainEndingInLiteralToTable_YieldsAnExactTableConfiguredClaim()
    {
        var claim = Assert.Single(EfCoreAnalysis.Claims(
            """
            class OrderConfiguration
            {
                public void Configure(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>().ToTable("tb_order");
                }
            }
            """));

        Assert.Equal(DatabaseClaimKind.TableConfigured, claim.Kind);
        Assert.Equal("Order", claim.EntityText);
        Assert.Equal("tb_order", claim.ObjectText);
        Assert.Equal(DatabaseObjectKind.Table, claim.ObjectKind);
        Assert.Equal(FactResolution.Exact, claim.ShapeConfidence);
    }

    // DAD-13: the configuration claim is owned by the configuring member and evidenced at the call.
    [Fact]
    public void Analyze_TableConfiguredClaim_IsOwnedByTheConfiguringMemberAndEvidencedAtTheCall()
    {
        const string Source = """
            class OrderConfiguration
            {
                public void Configure(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>().ToTable("tb_order");
                }
            }
            """;

        var claim = Assert.Single(EfCoreAnalysis.Claims(Source));

        Assert.Equal(EfCoreAnalysis.SymbolIdOfMethodDeclaration(Source, "Configure"), claim.OwnerId);
        Assert.Equal(5, claim.Evidence.StartLine);
        Assert.Equal(9, claim.Evidence.StartColumn);
        Assert.Equal(5, claim.Evidence.EndLine);
        Assert.Equal(57, claim.Evidence.EndColumn);
    }

    // Spec Edge Case: a non-literal ToTable argument configures nothing, leaving the convention path.
    [Fact]
    public void Analyze_ToTableWithANonLiteralArgument_YieldsNoTableConfiguredClaim()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderConfiguration
            {
                public void Configure(ModelBuilder modelBuilder, string tableName)
                {
                    modelBuilder.Entity<Order>().ToTable(tableName);
                }
            }
            """);

        Assert.Empty(claims);
    }

    // A chain split across statements hides the entity, so nothing is claimed rather than mis-attributed.
    [Fact]
    public void Analyze_ToTableReachedThroughAChainSplitAcrossStatements_YieldsNothing()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderConfiguration
            {
                public void Configure(ModelBuilder modelBuilder)
                {
                    var entity = modelBuilder.Entity<Order>();
                    entity.ToTable("tb_order");
                }
            }
            """);

        Assert.Empty(claims);
    }

    // DAD-05: a literal HasColumnName proves the column, attributed to the enclosing Entity<T>().
    [Fact]
    public void Analyze_PropertyChainEndingInLiteralHasColumnName_YieldsAnExactColumnConfiguredClaim()
    {
        var claim = Assert.Single(EfCoreAnalysis.Claims(
            """
            class OrderConfiguration
            {
                public void Configure(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>().Property(x => x.Status).HasColumnName("order_status");
                }
            }
            """));

        Assert.Equal(DatabaseClaimKind.ColumnConfigured, claim.Kind);
        Assert.Equal("Order", claim.EntityText);
        Assert.Equal("Status", claim.PropertyText);
        Assert.Equal("order_status", claim.ColumnText);
        Assert.Equal(FactResolution.Exact, claim.ShapeConfidence);
    }

    // Spec Edge Case: a non-literal HasColumnName argument configures nothing.
    [Fact]
    public void Analyze_HasColumnNameWithANonLiteralArgument_YieldsNoColumnConfiguredClaim()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderConfiguration
            {
                public void Configure(ModelBuilder modelBuilder, string columnName)
                {
                    modelBuilder.Entity<Order>().Property(x => x.Status).HasColumnName(columnName);
                }
            }
            """);

        Assert.Empty(claims);
    }

    // Without an enclosing Entity<T>() the owning entity is unknown, so nothing is claimed.
    [Fact]
    public void Analyze_PropertyChainWithNoEnclosingEntityCall_YieldsNothing()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderConfiguration
            {
                public void Configure(EntityTypeBuilder builder)
                {
                    builder.Property(x => x.Status).HasColumnName("order_status");
                }
            }
            """);

        Assert.Empty(claims);
    }

    // DAD-07 / DAD-08: the brief's query reads the set and projects three columns.
    [Fact]
    public void Analyze_EntitySetReadFlowingIntoAProjection_YieldsAReadAccessAndOneReadColumnPerProjectedProperty()
    {
        const string Source = """
            class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }

            class OrderService
            {
                private OrderDbContext _context;

                public Task<OrderDto> GetOrder(int orderId)
                {
                    return _context.Orders
                        .Where(x => x.Id == orderId)
                        .Select(x => new OrderDto { Id = x.Id, Status = x.Status, Amount = x.Amount })
                        .FirstOrDefaultAsync();
                }
            }
            """;

        var claims = EfCoreAnalysis.Claims(Source);

        var access = Assert.Single(claims.Where(claim => claim.Kind == DatabaseClaimKind.Access));
        Assert.Equal(DatabaseOperation.Read, access.Operation);
        Assert.Equal("Order", access.EntityText);
        Assert.Equal("Orders", access.PropertyText);
        Assert.Equal(EfCoreAnalysis.SymbolIdOfMethodDeclaration(Source, "GetOrder"), access.OwnerId);

        // DAD-08 scopes `read` to properties referenced outside a Where lambda.
        var columns = claims
            .Where(claim => claim.Kind == DatabaseClaimKind.ColumnAccess && claim.Usage == ColumnUsage.Read)
            .ToList();
        Assert.Equal(3, columns.Count);
        Assert.Equal("Id", columns[0].PropertyText);
        Assert.Equal("Status", columns[1].PropertyText);
        Assert.Equal("Amount", columns[2].PropertyText);
        Assert.All(columns, column => Assert.Equal(ColumnUsage.Read, column.Usage));
        Assert.All(columns, column => Assert.Equal("Order", column.EntityText));
        Assert.All(
            columns,
            column => Assert.Equal(
                EfCoreAnalysis.SymbolIdOfMethodDeclaration(Source, "GetOrder"), column.OwnerId));
    }

    // DAD-07: the access stands on its own - no LINQ chain means no columns, not no access.
    [Fact]
    public void Analyze_EntitySetReadWithNoLinqChain_YieldsTheAccessClaimAndNoColumnClaims()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }

            class OrderService
            {
                private OrderDbContext _context;

                public object All() => _context.Orders;
            }
            """);

        var access = Assert.Single(claims.Where(claim => claim.Kind == DatabaseClaimKind.Access));
        Assert.Equal(DatabaseOperation.Read, access.Operation);
        Assert.Empty(claims.Where(claim => claim.Kind == DatabaseClaimKind.ColumnAccess));
    }

    // DAD-09: a Where lambda's property references are filters, not projections.
    [Fact]
    public void Analyze_WhereLambdaReferencingAnEntityProperty_YieldsExactlyOneFilterColumnClaim()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }

            class OrderService
            {
                private OrderDbContext _context;

                public object GetOrder(int orderId) => _context.Orders.Where(x => x.Id == orderId);
            }
            """);

        var column = Assert.Single(claims.Where(claim => claim.Kind == DatabaseClaimKind.ColumnAccess));
        Assert.Equal("Id", column.PropertyText);
        Assert.Equal(ColumnUsage.Filter, column.Usage);
    }

    // DAD-08 + DAD-09: filtering by a column and projecting it are two distinct uses of it.
    [Fact]
    public void Analyze_PropertyReferencedInBothAWhereAndAProjection_YieldsAFilterClaimAndAReadClaim()
    {
        var claims = EfCoreAnalysis.Claims(
            """
            class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }

            class OrderService
            {
                private OrderDbContext _context;

                public object GetOrder(int orderId) => _context.Orders
                    .Where(x => x.Id == orderId)
                    .Select(x => new OrderDto { Id = x.Id });
            }
            """);

        var idColumns = claims
            .Where(claim => claim.Kind == DatabaseClaimKind.ColumnAccess && claim.PropertyText == "Id")
            .ToList();

        Assert.Equal(2, idColumns.Count);
        Assert.Contains(idColumns, column => column.Usage == ColumnUsage.Filter);
        Assert.Contains(idColumns, column => column.Usage == ColumnUsage.Read);
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

    public static FactId SymbolIdOfTypeDeclaration(string source, string typeName) =>
        SymbolIdOf(CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == typeName));

    public static FactId SymbolIdOfMethodDeclaration(string source, string methodName) =>
        SymbolIdOf(CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == methodName));

    private static FactId SymbolIdOf(MemberDeclarationSyntax declaration)
    {
        return SymbolFactId.CreateSyntactic(
            DataAccessTestFacts.ProjectId,
            "OrderRepository.cs",
            SyntaxFactExtractor.DeclarationKind(declaration),
            SyntaxFactExtractor.DeclarationSignature(declaration)).ToFactId();
    }
}
