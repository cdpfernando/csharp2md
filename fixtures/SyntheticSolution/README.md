# SyntheticSolution fixture

Small, hand-authored C# projects with a hand-written oracle. The fixture is deliberately
outside `csharp2md.slnx`, so its intentional breakage never fails the repository build.
Framework types (ASP.NET controllers and attributes, EF Core `DbContext`/`DbSet`, gRPC
`ClientBase`, DI, Swagger filters) are declared as local stand-ins, so every project except
`Acme.Payments` restores without new NuGet dependencies.

## Projects

- **Acme.Shared.Contracts**: class library with an explicit `<PackageId>`, multi-targeted
  `net8.0;net10.0` to exercise per-project target frameworks without a solution-wide one.
  Owns the `IEventBus` abstraction (`PublishAsync<TEvent>` / `Subscribe<TEvent>`, no external
  broker), the `OrderPlaced` and `PaymentProcessed` records, `PaymentClient`, the causal cycle
  `CycleProbe.Start -> Continue -> Start`, and a `Diagnostics.AuditRecorder` whose simple name
  ties with the one in `Acme.Orders`.
- **Acme.Orders**: executable. `OrderService` makes three named HTTP calls through
  `IHttpClientFactory.CreateClient`: `PaymentService` (resolved in `appsettings.json`),
  `NotificationService` (an environment-variable indirection) and `ShippingService` (absent
  from configuration, so unresolved). It also calls the unary `AuthorizePayment` gRPC method
  through an in-fixture `PaymentsClient`, publishes `OrderPlaced` through `IEventBus`, and
  calls `PaymentClient` across the project boundary. `Data/` holds a stand-in `DbContext`,
  entity configuration, a repository and hand-written SQL. `appsettings.json` and
  `Data/OrderSqlQueries.cs` carry the safety inputs a published package must never contain.
- **Acme.Orders.Worker**: a second executable that only subscribes to `OrderPlaced`. It makes
  `Acme.Shared.Contracts` a library shared by two applications in `Acme.Orders.slnx`, while the
  same library has a single application consumer in `Acme.Payments.slnx`. It adds no HTTP
  client, `DbContext` or route.
- **Acme.Payments**: executable exposing a real gRPC service (`Protos/payments.proto`, one
  unary and one server-streaming method). It subscribes to `OrderPlaced` and publishes
  `PaymentProcessed`, which has no subscriber anywhere in the fixture. It is the only project
  with a real NuGet and codegen dependency (`Grpc.AspNetCore`), and it is left unrestored on
  purpose: without a restore, the generated `Payments.PaymentsBase` and the `Grpc.*` namespaces
  are genuinely unresolved. `AmbiguousReferenceProbe` references the tied `AuditRecorder` name.
- **Acme.Shipping**: executable that closes two of `Acme.Orders`' edges. It handles
  `OrderPlaced` through a local `IIntegrationEventHandler<TEvent>` and exposes
  `POST shipments`.
- **Acme.Shipping.Tests**: test project that references `Acme.Shipping` directly (and
  `Acme.Shared.Contracts` only transitively). It makes `--include-tests` observable and must
  not appear in a default package.
- **Acme.Broken**: `Sdk="Acme.NonExistent.Sdk/99.99.99"`, a real unresolvable SDK.

`Directory.Build.props` disables SourceLink for the whole tree: `MSBuildWorkspace` reports its
"no git remote" / "no commits" warnings as `Failure` workspace diagnostics, which is noise for
projects that are never packed.

## Solutions

| Solution | Projects | Restorable with `dotnet restore` |
| --- | --- | --- |
| `Acme.Journey.slnx` | `Acme.Orders` (and `Acme.Shared.Contracts` through its reference) | yes |
| `Acme.Orders/Acme.Orders.slnx` | `Acme.Orders`, `Acme.Shared.Contracts`, `Acme.Broken`, `Acme.Orders.Worker`, and a reference to `Acme.DoesNotExist`, which does not exist on disk | no (MSB3202 for the missing project) |
| `Acme.Payments/Acme.Payments.slnx` | `Acme.Payments`, `Acme.Shared.Contracts` | not intended |
| `Acme.Shipping/Acme.Shipping.slnx` | `Acme.Shipping`, `Acme.Shipping.Tests`, `Acme.Shared.Contracts` | yes |

`Acme.Journey.slnx` is the end-to-end solution: analyze it to get a committed, certified
package. `Acme.Orders.slnx` combines the broken SDK and the missing project reference; variant
planning for it fails with a cause that names `Acme.Broken`.

## Oracle

`knowledge-oracle.json` is hand-authored acceptance data: the expected roots (`Acme.Orders`,
`Acme.Payments`, `Acme.Shipping`), boundary edges (HTTP to `PaymentService` and
`ShippingService`, gRPC to `Payments`, publish and subscribe of `OrderPlaced`), variant
occurrences (`Acme.Shared.Contracts` on `net8.0` and `net10.0`, `Acme.Shipping` on `net10.0`),
the causal cycle, the controlled gap (`ShippingService is unresolved`), and the values that must
not reach a published package (the two fixture secrets, the absolute fixture path and
`Acme.Shipping.Tests`).

## Test consumers

- `Csharp2Md.Core.Tests`: `ProjectVariantPlannerTests` (Payments and Orders solutions),
  `ProjectVariantWorkspaceTests` (per-variant workspaces; direct vs. transitive references
  through `Acme.Shipping.Tests`), `ArchitectureFactExtractorTests` (`Acme.Orders` and
  `Acme.Shared.Contracts` project files).
- `Csharp2Md.Cli.Tests`: `KnowledgePackageJourneyTests` (analyzes `Acme.Journey.slnx`),
  `KnowledgePackageFailureTests` (analyzes a copy of the fixture through `Acme.Journey.slnx`),
  `KnowledgeAnalyzeCommandTests` (Orders and Payments solution paths),
  `SyntheticSolutionFixtureTests` (pins the oracle and the source shapes it names) and
  `Isolation/FixtureRetentionTests` (the fixture exists).
