using System.Collections.Frozen;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Indexes;

public sealed class SymbolIndexTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly ProjectFactId OtherProjectId = ProjectFactId.Create("src/Other/Other.csproj");

    [Fact]
    public void GetById_PresentIdentity_ReturnsThatExactSymbol()
    {
        var symbol = Symbol("PaymentsService", "global::Acme.Payments.PaymentsService");
        var index = Build(symbol, Symbol("OrderService", "global::Acme.Orders.OrderService"));

        var found = index.GetById(symbol.SymbolId);

        Assert.NotNull(found);
        Assert.Equal(symbol.SymbolId, found!.SymbolId);
        Assert.Equal("PaymentsService", found.Name);
    }

    [Fact]
    public void GetById_AbsentIdentity_ReturnsNull()
    {
        var index = Build(Symbol("PaymentsService", "global::Acme.Payments.PaymentsService"));

        Assert.Null(index.GetById(SymbolFactId.CreateSyntactic(ProjectId, "Nope.cs", "class", "Nope")));
    }

    [Fact]
    public void GetById_IsBackedByADirectKeyLookupNotALinearScan()
    {
        var field = typeof(SymbolIndex)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(FrozenDictionary<SymbolFactId, SymbolFact>));

        Assert.Equal(typeof(FrozenDictionary<SymbolFactId, SymbolFact>), field.FieldType);
    }

    [Fact]
    public void FindByName_SharedSimpleNameAcrossProjects_ReturnsEveryMatchOrderedByIdOrdinal()
    {
        var first = Symbol("PaymentService", "global::Company.Legacy.PaymentService", projectId: ProjectId);
        var second = Symbol("PaymentService", "global::Company.Payments.PaymentService", projectId: OtherProjectId);
        var index = Build(second, first);

        var found = index.FindByName("PaymentService");

        Assert.Equal(2, found.Length);
        Assert.Equal(
            new[] { first, second }.Select(static symbol => symbol.SymbolId.Value).Order(StringComparer.Ordinal),
            found.Select(static symbol => symbol.SymbolId.Value));
    }

    [Fact]
    public void FindByName_NoMatch_ReturnsEmptyRatherThanNullOrThrow()
    {
        var index = Build(Symbol("PaymentsService", "global::Acme.Payments.PaymentsService"));

        Assert.Empty(index.FindByName("NothingNamedThis"));
    }

    [Fact]
    public void FindByQualifiedName_KeywordQualifiedAndGlobalQualifiedSpellings_AllResolveTogether()
    {
        var keyword = Symbol("String", "string", projectId: ProjectId);
        var qualified = Symbol("String", "System.String", projectId: OtherProjectId);
        var globalQualified = Symbol("String", "global::System.String", projectId: ProjectFactId.Create("src/Third/Third.csproj"));
        var index = Build(keyword, qualified, globalQualified);

        var found = index.FindByQualifiedName("System.String");

        Assert.Equal(3, found.Length);
        Assert.Equal(found.Select(static symbol => symbol.SymbolId.Value), index.FindByQualifiedName("string").Select(static symbol => symbol.SymbolId.Value));
        Assert.Equal(found.Select(static symbol => symbol.SymbolId.Value), index.FindByQualifiedName("global::System.String").Select(static symbol => symbol.SymbolId.Value));
    }

    [Fact]
    public void FindByQualifiedName_NoMatch_ReturnsEmptyRatherThanNullOrThrow()
    {
        var index = Build(Symbol("PaymentsService", "global::Acme.Payments.PaymentsService"));

        Assert.Empty(index.FindByQualifiedName("global::Nothing.Declared.Here"));
    }

    [Fact]
    public void FindMembers_ReturnsEveryMemberRegardlessOfItsOwnResolution()
    {
        var exact = Member("Run", "global::Acme.Api", FactResolution.Exact, "one");
        var syntactic = Member("Run", "global::Acme.Api", FactResolution.Syntactic, "two");
        var unresolved = Member("Run", "global::Acme.Api", FactResolution.Unresolved, "three");
        var index = Build(unresolved, exact, syntactic);

        var found = index.FindMembers("global::Acme.Api", "Run");

        Assert.Equal(3, found.Length);
        Assert.Equal(
            [FactResolution.Exact, FactResolution.Syntactic, FactResolution.Unresolved],
            found.Select(static symbol => symbol.Header.Resolution).Order().ToArray());
    }

    [Fact]
    public void FindMembers_ContainingTypeGivenAsEitherSimpleOrQualifiedName_FindsTheSameMember()
    {
        var index = BuildFromSource("""
            namespace Acme.Payments;
            class PaymentsService
            {
                string AuthorizePayment(string orderId, decimal amount) => orderId;
            }
            """);

        var bySimpleName = index.FindMembers("PaymentsService", "AuthorizePayment");
        var byQualifiedName = index.FindMembers("Acme.Payments.PaymentsService", "AuthorizePayment");

        var method = Assert.Single(bySimpleName);
        Assert.Equal("AuthorizePayment", method.Name);
        Assert.Equal("global::Acme.Payments.PaymentsService", method.ContainingType);
        Assert.Equal(bySimpleName.Select(static symbol => symbol.SymbolId), byQualifiedName.Select(static symbol => symbol.SymbolId));
    }

    [Fact]
    public void FindMembers_NoMatch_ReturnsEmptyRatherThanNullOrThrow()
    {
        var index = Build(Member("Run", "global::Acme.Api", FactResolution.Syntactic, "one"));

        Assert.Empty(index.FindMembers("global::Acme.Api", "Absent"));
        Assert.Empty(index.FindMembers("global::Acme.Absent", "Run"));
    }

    [Fact]
    public void Build_SameSimpleNameInDifferentNamespaces_KeepsThemDistinctNeverMerged()
    {
        var legacy = Symbol("PaymentService", "global::Company.Legacy.PaymentService", projectId: ProjectId);
        var current = Symbol("PaymentService", "global::Company.Payments.PaymentService", projectId: OtherProjectId);
        var index = Build(legacy, current);

        Assert.Equal(2, index.FindByName("PaymentService").Length);
        Assert.Equal(legacy.SymbolId, Assert.Single(index.FindByQualifiedName("Company.Legacy.PaymentService")).SymbolId);
        Assert.Equal(current.SymbolId, Assert.Single(index.FindByQualifiedName("Company.Payments.PaymentService")).SymbolId);
        Assert.NotEqual(legacy.SymbolId, current.SymbolId);
    }

    [Fact]
    public void Build_TwoProjectsDeclaringTheIdenticalQualifiedName_KeepsBothAsDistinctCandidates()
    {
        var first = Symbol("PaymentService", "global::Company.Payments.PaymentService", projectId: ProjectId);
        var second = Symbol("PaymentService", "global::Company.Payments.PaymentService", projectId: OtherProjectId);
        var index = Build(first, second);

        var found = index.FindByQualifiedName("Company.Payments.PaymentService");

        Assert.Equal(2, found.Length);
        Assert.Equal(2, found.Select(static symbol => symbol.SymbolId).Distinct().Count());
    }

    [Fact]
    public void Build_PartialDeclarationsAcrossDocuments_AreIndexedUnderTheirOwnIdentitiesNotMerged()
    {
        var firstHalf = Symbol("Split", "global::Acme.Split", documentPath: "First.cs");
        var secondHalf = Symbol("Split", "global::Acme.Split", documentPath: "Second.cs");
        var index = Build(firstHalf, secondHalf);

        Assert.NotEqual(firstHalf.SymbolId, secondHalf.SymbolId);
        Assert.Equal(2, index.FindByQualifiedName("Acme.Split").Length);
        Assert.NotNull(index.GetById(firstHalf.SymbolId));
        Assert.NotNull(index.GetById(secondHalf.SymbolId));
    }

    [Fact]
    public void Build_SymbolContainingAnErrorSymbol_IsStillIndexedAndKeepsItsOwnResolution()
    {
        var broken = Symbol("Broken", "global::Acme.Broken") with
        {
            ContainsErrorSymbol = true,
            Header = FactHeader.Create(
                SymbolFactId.CreateSyntactic(ProjectId, "Feature.cs", "class", "Broken").ToFactId(),
                FactKind.Symbol,
                FactResolution.Unresolved),
        };
        var index = Build(broken);

        var found = Assert.Single(index.FindByName("Broken"));
        Assert.True(found.ContainsErrorSymbol);
        Assert.Equal(FactResolution.Unresolved, found.Header.Resolution);
        Assert.NotEqual(FactResolution.Exact, found.Header.Resolution);
    }

    [Fact]
    public void Build_IndexedSymbolRetainsItsOriginalNonNormalizedSignatureSpelling()
    {
        var index = BuildFromSource("class C { void Run(string name) { } }");

        var method = Assert.Single(index.FindByName("Run"));
        Assert.Contains("string", method.Signature, StringComparison.Ordinal);
        Assert.DoesNotContain("System.String", method.Signature, StringComparison.Ordinal);
        Assert.Equal(["global::System.String"], method.ParameterTypes.ToArray());
    }

    [Fact]
    public void Build_NoFacts_ReturnsAnEmptyQueryableIndexRatherThanThrowing()
    {
        var index = SymbolIndexBuilder.Build([], [], [], []);

        Assert.Empty(index.Symbols);
        Assert.Null(index.GetById(SymbolFactId.CreateSyntactic(ProjectId, "Feature.cs", "class", "Anything")));
        Assert.Empty(index.FindByName("Anything"));
        Assert.Empty(index.FindByQualifiedName("Anything"));
        Assert.Empty(index.FindMembers("Anything", "Member"));
    }

    [Fact]
    public void Build_ShuffledInputOrder_ProducesIdenticalQueryResults()
    {
        var facts = new[]
        {
            Symbol("Alpha", "global::Acme.Alpha", projectId: ProjectId),
            Symbol("Beta", "global::Acme.Beta", projectId: OtherProjectId),
            Symbol("Alpha", "global::Other.Alpha", projectId: OtherProjectId),
            Member("Run", "global::Acme.Alpha", FactResolution.Syntactic, "one"),
            Member("Run", "global::Acme.Alpha", FactResolution.Exact, "two"),
        };

        var forward = SymbolIndexBuilder.Build(facts, [], [], []);
        var reversed = SymbolIndexBuilder.Build(facts.Reverse().ToArray(), [], [], []);

        Assert.Equal(Ids(forward.Symbols), Ids(reversed.Symbols));
        Assert.Equal(Ids(forward.FindByName("Alpha")), Ids(reversed.FindByName("Alpha")));
        Assert.Equal(Ids(forward.FindByQualifiedName("Acme.Alpha")), Ids(reversed.FindByQualifiedName("Acme.Alpha")));
        Assert.Equal(Ids(forward.FindMembers("Alpha", "Run")), Ids(reversed.FindMembers("Alpha", "Run")));
        Assert.Equal(2, forward.FindMembers("Alpha", "Run").Length);
    }

    [Fact]
    public void Build_DuplicateIdentities_CollapseToOneEntryWithoutThrowing()
    {
        var symbol = Symbol("Duplicated", "global::Acme.Duplicated");
        var index = SymbolIndexBuilder.Build([symbol, symbol], [], [], []);

        Assert.Single(index.Symbols);
        Assert.Equal(symbol.SymbolId, Assert.Single(index.FindByName("Duplicated")).SymbolId);
    }

    [Fact]
    public void FindMethods_ArgumentCountSet_ReturnsOnlyMethodsWhoseParameterCountEqualsItExactly()
    {
        var none = Method("Authorize", "global::Acme.Api", [], "none");
        var one = Method("Authorize", "global::Acme.Api", ["global::System.String"], "one");
        var two = Method("Authorize", "global::Acme.Api", ["global::System.String", "global::System.Decimal"], "two");
        var index = Build(two, none, one);

        var found = index.FindMethods(new MethodLookup
        {
            Name = "Authorize",
            ReceiverType = "global::Acme.Api",
            ArgumentCount = 1,
        });

        Assert.Equal(one.SymbolId, Assert.Single(found).SymbolId);
    }

    [Fact]
    public void FindMethods_ZeroArgumentCount_MatchesTheZeroParameterOverloadAndIsNotAnUnsetFilterNoOp()
    {
        var none = Method("Authorize", "global::Acme.Api", [], "none");
        var one = Method("Authorize", "global::Acme.Api", ["global::System.String"], "one");
        var two = Method("Authorize", "global::Acme.Api", ["global::System.String", "global::System.Decimal"], "two");
        var index = Build(two, none, one);

        var zeroArguments = index.FindMethods(new MethodLookup
        {
            Name = "Authorize",
            ReceiverType = "global::Acme.Api",
            ArgumentCount = 0,
        });
        var unfiltered = index.FindMethods(new MethodLookup { Name = "Authorize", ReceiverType = "global::Acme.Api" });

        Assert.Equal(none.SymbolId, Assert.Single(zeroArguments).SymbolId);
        Assert.Equal(3, unfiltered.Length);
    }

    [Fact]
    public void FindMethods_ArgumentTypesSet_RanksTheExactTypeMatchFirstWithoutDroppingTheMismatchedCandidate()
    {
        var matching = Method("Authorize", "global::Acme.Api", ["global::System.String"], "zeta");
        var mismatched = Method("Authorize", "global::Acme.Api", ["global::System.Int32"], "alpha");
        var index = Build(mismatched, matching);

        var found = index.FindMethods(new MethodLookup
        {
            Name = "Authorize",
            ReceiverType = "global::Acme.Api",
            ArgumentCount = 1,
            ArgumentTypes = ["string"],
        });

        // The exact-type match deliberately sorts *after* the mismatched candidate by Id ordinal, so
        // only argument-type ranking - never the ordinal tie-break - can put it first.
        Assert.True(string.CompareOrdinal(matching.SymbolId.Value, mismatched.SymbolId.Value) > 0);
        Assert.Equal(2, found.Length);
        Assert.Equal(matching.SymbolId, found[0].SymbolId);
        Assert.Equal(mismatched.SymbolId, found[1].SymbolId);
    }

    [Fact]
    public void FindMethods_APropertyMatchingTheNameAndArgumentCount_IsNotReturnedBecauseOnlyMethodsQualify()
    {
        var index = BuildFromSource("""
            namespace Acme.Api;
            class Gateway
            {
                public string Authorize { get; set; }
            }
            """);

        Assert.Equal("property", Assert.Single(index.FindMembers("Gateway", "Authorize")).SymbolKind);
        Assert.Empty(index.FindMethods(new MethodLookup
        {
            Name = "Authorize",
            ReceiverType = "Gateway",
            ArgumentCount = 0,
        }));
    }

    [Fact]
    public void FindMethods_RealAuthorizePaymentFixtureMethod_MatchesItsDeclaredArgumentCountAndNotAMismatchedOne()
    {
        var relativePath = "Acme.Payments/PaymentsService.cs";
        var source = File.ReadAllText(TestPaths.SyntheticSolution(Path.Combine("Acme.Payments", "PaymentsService.cs")));
        var index = SymbolIndexBuilder.Build(
            SyntaxFactExtractor.Extract(ProjectId, relativePath, source).Symbols, [], [], []);

        var found = index.FindMethods(new MethodLookup
        {
            Name = "AuthorizePayment",
            ReceiverType = "PaymentsService",
            ArgumentCount = 2,
        });

        var method = Assert.Single(found);
        Assert.Equal("AuthorizePayment", method.Name);
        Assert.Equal("global::Acme.Payments.PaymentsService", method.ContainingType);
        Assert.Equal(2, method.ParameterTypes.Length);

        Assert.Empty(index.FindMethods(new MethodLookup
        {
            Name = "AuthorizePayment",
            ReceiverType = "PaymentsService",
            ArgumentCount = 1,
        }));
    }

    [Fact]
    public void FindCandidates_PaymentServiceDeclaredInTwoNamespaces_ReportsAmbiguousWithExactlyThoseTwoCandidates()
    {
        var legacy = Symbol("PaymentService", "global::Company.Legacy.PaymentService", projectId: ProjectId);
        var current = Symbol("PaymentService", "global::Company.Payments.PaymentService", projectId: OtherProjectId);
        var index = Build(legacy, current);

        var result = index.FindCandidates(new SymbolLookup { Name = "PaymentService" });

        Assert.Equal(SymbolLookupStatus.Ambiguous, result.Status);
        Assert.Equal(
            new[] { legacy, current }.Select(static symbol => symbol.SymbolId.Value).Order(StringComparer.Ordinal),
            result.Candidates.Select(static symbol => symbol.SymbolId.Value));

        // FindByName likewise surfaces both rather than picking one.
        Assert.Equal(2, index.FindByName("PaymentService").Length);

        // The qualified-name lookup bypasses the simple-name ambiguity entirely.
        var qualified = Assert.Single(index.FindByQualifiedName("Company.Payments.PaymentService"));
        Assert.Equal(current.SymbolId, qualified.SymbolId);
    }

    [Fact]
    public void FindCandidates_ExactlyOneCandidateMatches_ReportsUniqueNeverAmbiguous()
    {
        var index = Build(
            Symbol("PaymentsService", "global::Acme.Payments.PaymentsService"),
            Symbol("OrderService", "global::Acme.Orders.OrderService"));

        var result = index.FindCandidates(new SymbolLookup { Name = "OrderService" });

        Assert.Equal(SymbolLookupStatus.Unique, result.Status);
        Assert.Equal("OrderService", Assert.Single(result.Candidates).Name);
    }

    [Fact]
    public void FindCandidates_TieWithinOnePriorityTier_ReportsAmbiguousAndReturnsBothNeverOneArbitraryWinner()
    {
        var first = Symbol("Handler", "global::Acme.Shared.Handler", documentPath: "First.cs");
        var second = Symbol("Handler", "global::Acme.Shared.Handler", documentPath: "Second.cs");
        var index = Build(first, second);

        var result = index.FindCandidates(new SymbolLookup
        {
            Name = "Handler",
            Namespace = "Acme.Shared",
        });

        Assert.Equal(SymbolLookupStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Candidates.Length);
        Assert.Equal(
            new[] { first, second }.Select(static symbol => symbol.SymbolId.Value).Order(StringComparer.Ordinal),
            result.Candidates.Select(static symbol => symbol.SymbolId.Value));
    }

    [Fact]
    public void FindCandidates_NoCandidateAtAll_ReportsNotFoundWhichStaysDistinguishableFromAmbiguous()
    {
        var index = Build(Symbol("PaymentsService", "global::Acme.Payments.PaymentsService"));

        var result = index.FindCandidates(new SymbolLookup { Name = "NothingNamedThis" });

        Assert.Equal(SymbolLookupStatus.NotFound, result.Status);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void FindCandidates_ContextualHints_OrderCandidatesByThePrioritySequence()
    {
        var inContainingType = Member("Handle", "global::Acme.Api.Gateway", FactResolution.Syntactic, "containing")
            with
        { Namespace = "Acme.Api" };
        var inNamespace = Member("Handle", "global::Acme.Api.Other", FactResolution.Syntactic, "namespace")
            with
        { Namespace = "Acme.Api" };
        var inImportedNamespace = Member("Handle", "global::Acme.Imported.Bus", FactResolution.Syntactic, "import")
            with
        { Namespace = "Acme.Imported" };
        var inSameProject = Member("Handle", "global::Zeta.Elsewhere.Box", FactResolution.Syntactic, "project")
            with
        { Namespace = "Zeta.Elsewhere" };
        var elsewhere = Member("Handle", "global::Other.Far.Box", FactResolution.Syntactic, "global") with
        {
            Namespace = "Other.Far",
            DocumentId = DocumentFactId.Create(OtherProjectId, "Feature.cs"),
        };

        var documents = new[]
        {
            Document(ProjectId, "Feature.cs"),
            Document(OtherProjectId, "Feature.cs"),
        };
        var index = SymbolIndexBuilder.Build(
            [elsewhere, inSameProject, inImportedNamespace, inNamespace, inContainingType],
            [],
            documents,
            []);

        var result = index.FindCandidates(new SymbolLookup
        {
            Name = "Handle",
            ContainingType = "Acme.Api.Gateway",
            Namespace = "Acme.Api",
            Imports = ["Acme.Imported"],
            ProjectId = ProjectId.Value,
        });

        Assert.Equal(SymbolLookupStatus.Unique, result.Status);
        Assert.Equal(
            new[] { inContainingType, inNamespace, inImportedNamespace, inSameProject, elsewhere }
                .Select(static symbol => symbol.SymbolId.Value),
            result.Candidates.Select(static symbol => symbol.SymbolId.Value));
    }

    [Fact]
    public void FindCandidates_ExactIdThenQualifiedName_OutrankTheGlobalSimpleNameTier()
    {
        var target = Symbol("Handler", "global::Acme.Api.Handler", projectId: ProjectId);
        var qualifiedNameDecoy = Symbol("Acme.Api.Handler", "global::Decoy.Qualified", projectId: OtherProjectId);
        var identityDecoy = Symbol(target.SymbolId.Value, "global::Decoy.Identity", projectId: OtherProjectId);
        var index = Build(target, qualifiedNameDecoy, identityDecoy);

        var byQualifiedName = index.FindCandidates(new SymbolLookup { Name = "Acme.Api.Handler" });
        var byIdentity = index.FindCandidates(new SymbolLookup { Name = target.SymbolId.Value });

        Assert.Equal(2, byQualifiedName.Candidates.Length);
        Assert.Equal(target.SymbolId, byQualifiedName.Candidates[0].SymbolId);
        Assert.Equal(SymbolLookupStatus.Unique, byQualifiedName.Status);

        Assert.Equal(2, byIdentity.Candidates.Length);
        Assert.Equal(target.SymbolId, byIdentity.Candidates[0].SymbolId);
        Assert.Equal(SymbolLookupStatus.Unique, byIdentity.Status);
    }

    [Fact]
    public void Build_TwoFactsSharingOneIdentity_RecordsDuplicatedSymbolIdAndKeepsExactlyOneEntry()
    {
        var first = Symbol("Duplicated", "global::Acme.Duplicated");
        var second = first with { Signature = "class Duplicated /* second declaration */" };
        Assert.Equal(first.SymbolId, second.SymbolId);

        var index = SymbolIndexBuilder.Build([first, second], [], [], []);

        var diagnostic = Assert.Single(index.Diagnostics);
        Assert.Equal("C2M-SYMIDX-001", diagnostic.Code);
        Assert.Contains(diagnostic.Data, data => data is { Key: "rule", Value: "duplicated-symbol-id" });
        Assert.Contains(diagnostic.Data, data => data is { Key: "symbol_id" } && data.Value == first.SymbolId.Value);

        var kept = Assert.Single(index.Symbols);
        Assert.Equal(first.SymbolId, kept.SymbolId);
        Assert.Contains(kept, new[] { first, second });
    }

    [Fact]
    public void Build_ContainingSymbolIdReferencingAnAbsentId_RecordsInvalidContainingSymbolAndStillIndexesTheSymbol()
    {
        var absent = SymbolFactId.CreateSyntactic(ProjectId, "Missing.cs", "class", "global::Acme.Absent");
        var orphan = Member("Run", "global::Acme.Api", FactResolution.Syntactic, "orphan") with
        {
            ContainingSymbolId = absent,
        };

        var index = SymbolIndexBuilder.Build([orphan], [], [], []);

        var diagnostic = Assert.Single(index.Diagnostics);
        Assert.Equal("C2M-SYMIDX-002", diagnostic.Code);
        Assert.Contains(diagnostic.Data, data => data is { Key: "rule", Value: "invalid-containing-symbol" });
        Assert.Contains(diagnostic.Data, data => data is { Key: "containing_symbol_id" } && data.Value == absent.Value);

        Assert.NotNull(index.GetById(orphan.SymbolId));
        Assert.Equal(orphan.SymbolId, Assert.Single(index.FindByName("Run")).SymbolId);
    }

    [Fact]
    public void Build_SimpleNameWithCandidatesInTwoNamespaces_RecordsAmbiguousSymbolLookupWithoutAnyQuery()
    {
        var legacy = Symbol("PaymentService", "global::Company.Legacy.PaymentService", projectId: ProjectId)
            with
        { Namespace = "Company.Legacy" };
        var current = Symbol("PaymentService", "global::Company.Payments.PaymentService", projectId: OtherProjectId)
            with
        { Namespace = "Company.Payments" };

        // Read straight off the freshly built index - no Find* call precedes this.
        var index = SymbolIndexBuilder.Build([legacy, current], [], [], []);
        var diagnostic = Assert.Single(index.Diagnostics);

        Assert.Equal("C2M-SYMIDX-003", diagnostic.Code);
        Assert.Contains(diagnostic.Data, data => data is { Key: "rule", Value: "ambiguous-symbol-lookup" });
        Assert.Contains(diagnostic.Data, data => data is { Key: "name", Value: "PaymentService" });
    }

    [Fact]
    public void Build_ConsistentFacts_RecordsNoDiagnosticsAtAll()
    {
        var index = Build(
            Symbol("PaymentsService", "global::Acme.Payments.PaymentsService") with { Namespace = "Acme.Payments" },
            Symbol("OrderService", "global::Acme.Orders.OrderService") with { Namespace = "Acme.Orders" });

        Assert.Empty(index.Diagnostics);
    }

    /// <summary>
    /// Every string <c>SyntaxFactExtractor.DeclarationKind</c> can produce, paired with the one
    /// <see cref="IndexedSymbolKind"/> it must map to. The kinds spec.md section 5 does not name
    /// individually map to <c>Other</c> - bucketed, never dropped.
    /// </summary>
    public static TheoryData<string, IndexedSymbolKind> DeclarationKinds => new()
    {
        { "namespace", IndexedSymbolKind.Namespace },
        { "class", IndexedSymbolKind.Class },
        { "struct", IndexedSymbolKind.Struct },
        { "record", IndexedSymbolKind.RecordClass },
        { "record-struct", IndexedSymbolKind.RecordStruct },
        { "interface", IndexedSymbolKind.Interface },
        { "enum", IndexedSymbolKind.Enum },
        { "delegate", IndexedSymbolKind.Delegate },
        { "constructor", IndexedSymbolKind.Constructor },
        { "method", IndexedSymbolKind.Method },
        { "property", IndexedSymbolKind.Property },
        { "field", IndexedSymbolKind.Field },
        { "event", IndexedSymbolKind.Event },
        { "destructor", IndexedSymbolKind.Other },
        { "indexer", IndexedSymbolKind.Other },
        { "operator", IndexedSymbolKind.Other },
        { "conversion-operator", IndexedSymbolKind.Other },
        { "enum-member", IndexedSymbolKind.Other },
        { "member", IndexedSymbolKind.Other },
    };

    [Theory]
    [MemberData(nameof(DeclarationKinds))]
    public void IndexedSymbolKindMap_EveryDeclarationKind_MapsToExactlyOneIndexedSymbolKind(
        string declarationKind,
        IndexedSymbolKind expected) =>
        Assert.Equal(expected, IndexedSymbolKindMap.From(declarationKind));

    [Fact]
    public void IndexedSymbolKindMap_CoversEveryKindTheExtractorActuallyProduces()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/AllForms.cs", """
            namespace Sample;

            public delegate void Callback();

            public enum Level { Low }

            public interface IThing { }

            public readonly record struct Point(int X);

            public record Person(string Name);

            public struct Box { public int Value; }

            public class Everything : IThing
            {
                public int Field;
                public event Callback? Changed;
                public Everything() { }
                ~Everything() { }
                public int Property { get; set; }
                public int this[int index] => index;
                public void Method() { }
                public static Everything operator +(Everything left, Everything right) => left;
                public static explicit operator int(Everything value) => 0;
            }
            """);

        var produced = extraction.Symbols
            .Select(static symbol => symbol.SymbolKind)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var mapped = DeclarationKinds.Select(static row => (string)row[0]).ToArray();

        // The fixture really does exercise a broad spread of declaration forms...
        Assert.True(produced.Length >= 18, $"fixture only produced {produced.Length} kinds: {string.Join(", ", produced)}");

        // ...and not one of them falls outside the mapping table above.
        Assert.All(produced, kind => Assert.Contains(kind, mapped));
    }

    [Fact]
    public void Metrics_TotalSymbols_EqualsTheIndexedCountAfterDuplicatesCollapse()
    {
        var kept = Symbol("Duplicated", "global::Acme.Duplicated");
        var collision = kept with { Signature = "class Duplicated /* second */" };
        var other = Symbol("Solo", "global::Acme.Solo");

        var index = SymbolIndexBuilder.Build([kept, collision, other], [], [], []);

        Assert.Equal(2, index.Symbols.Length);
        Assert.Equal(index.Symbols.Length, index.Metrics.TotalSymbols);
    }

    [Fact]
    public void Metrics_ByResolutionAndByKind_EachSumToTotalSymbols()
    {
        var index = BuildFromSource("""
            namespace Acme.Payments;
            class PaymentsService
            {
                int _retries;
                string AuthorizePayment(string orderId) => orderId;
            }
            """);

        Assert.Equal(index.Metrics.TotalSymbols, index.Metrics.ByResolution.Values.Sum());
        Assert.Equal(index.Metrics.TotalSymbols, index.Metrics.ByKind.Values.Sum());
        Assert.Equal(1, index.Metrics.ByKind[IndexedSymbolKind.Class]);
        Assert.Equal(1, index.Metrics.ByKind[IndexedSymbolKind.Method]);
        Assert.Equal(1, index.Metrics.ByKind[IndexedSymbolKind.Field]);
    }

    [Fact]
    public void Metrics_DuplicateAndAmbiguousCounts_MatchTheRecordedDiagnosticCountsExactly()
    {
        var kept = Symbol("PaymentService", "global::Company.Legacy.PaymentService", projectId: ProjectId)
            with
        { Namespace = "Company.Legacy" };
        var collision = kept with { Signature = "class PaymentService /* second */" };
        var current = Symbol("PaymentService", "global::Company.Payments.PaymentService", projectId: OtherProjectId)
            with
        { Namespace = "Company.Payments" };

        var index = SymbolIndexBuilder.Build([kept, collision, current], [], [], []);

        Assert.Equal(
            index.Diagnostics.Count(diagnostic => diagnostic.Code == "C2M-SYMIDX-001"),
            index.Metrics.DuplicateIdCount);
        Assert.Equal(
            index.Diagnostics.Count(diagnostic => diagnostic.Code == "C2M-SYMIDX-003"),
            index.Metrics.AmbiguousSimpleNameCount);
        Assert.Equal(1, index.Metrics.DuplicateIdCount);
        Assert.Equal(1, index.Metrics.AmbiguousSimpleNameCount);
    }

    private static DocumentFact Document(ProjectFactId projectId, string relativePath)
    {
        var documentId = DocumentFactId.Create(projectId, relativePath);
        return new DocumentFact(
            FactHeader.Create(documentId.ToFactId(), FactKind.Document, FactResolution.Syntactic),
            documentId,
            projectId,
            relativePath,
            [],
            []);
    }

    private static string[] Ids(ImmutableArray<SymbolFact> symbols) =>
        symbols.Select(static symbol => symbol.SymbolId.Value).ToArray();

    private static SymbolIndex Build(params SymbolFact[] symbols) =>
        SymbolIndexBuilder.Build(symbols, [], [], []);

    private static SymbolIndex BuildFromSource(string source) =>
        SymbolIndexBuilder.Build(
            SyntaxFactExtractor.Extract(ProjectId, "src/App/Feature.cs", source).Symbols,
            [],
            [],
            []);

    private static SymbolFact Symbol(
        string name,
        string fullyQualifiedName,
        ProjectFactId? projectId = null,
        string documentPath = "Feature.cs")
    {
        var owner = projectId ?? ProjectId;
        var id = SymbolFactId.CreateSyntactic(owner, documentPath, "class", fullyQualifiedName);
        return new SymbolFact(
            FactHeader.Create(id.ToFactId(), FactKind.Symbol, FactResolution.Syntactic),
            id,
            DocumentFactId.Create(owner, documentPath),
            "class",
            ContainsErrorSymbol: false,
            [],
            [],
            [],
            Semantics: null,
            Name: name,
            FullyQualifiedName: fullyQualifiedName,
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: $"class {name}",
            Arity: 0,
            ParameterTypes: []);
    }

    private static SymbolFact Method(
        string name,
        string containingType,
        ImmutableArray<string> parameterTypes,
        string discriminator) =>
        Member(name, containingType, FactResolution.Syntactic, discriminator) with
        {
            ParameterTypes = parameterTypes,
        };

    private static SymbolFact Member(
        string name,
        string containingType,
        FactResolution resolution,
        string discriminator)
    {
        var id = SymbolFactId.CreateSyntactic(ProjectId, "Feature.cs", "method", $"{containingType}/{name}/{discriminator}");
        return new SymbolFact(
            FactHeader.Create(id.ToFactId(), FactKind.Symbol, resolution),
            id,
            DocumentFactId.Create(ProjectId, "Feature.cs"),
            "method",
            ContainsErrorSymbol: false,
            [],
            [],
            [],
            Semantics: null,
            Name: name,
            FullyQualifiedName: $"{containingType}.{name}",
            Namespace: null,
            ContainingType: containingType,
            ContainingSymbolId: null,
            Signature: $"void {name}()",
            Arity: 0,
            ParameterTypes: []);
    }
}
