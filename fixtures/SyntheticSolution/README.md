# SyntheticSolution fixture

Three services, deliberately excluded from `csharp2md.slnx` (see T2) so their
intentional breakage never fails the repo build gate. Each directory has no
`.sln`, so each resolves as a `LooseProjects` service boundary (P1-03).

- **Acme.Shared.Contracts** — class library, explicit `<PackageId>`, referenced
  by both other services (internal direct-reference signal, P2-04). Also owns
  the shared `IEventBus` abstraction and message record types used below.
- **Acme.Orders** — calls `Acme.Payments` over HTTP via
  `IHttpClientFactory.CreateClient("PaymentService")`; publishes `OrderPlaced`
  via `IEventBus.PublishAsync`. Also calls two more logical HTTP targets to
  exercise config-based name resolution end to end:
  `"NotificationService"` (env-var indirection → dynamic, P2-08) and
  `"ShippingService"` (absent from config entirely → unresolved, P2-09).
- **Acme.Payments** — exposes a real gRPC service (`payments.proto`, codegen
  verified) with one unary method (`AuthorizePayment`) and one server-streaming
  method (`StreamPaymentStatus`); subscribes to `OrderPlaced` via
  `IEventBus.Subscribe`; publishes `PaymentProcessed`, which has **no**
  subscriber anywhere in this fixture — the unpaired-publish path (P2-15).

**Deliberately left unrestored:** `Acme.Payments`. It has real NuGet
(`Grpc.AspNetCore`) and protobuf-codegen dependencies, so without a prior
`dotnet restore` its generated `Payments.PaymentsBase` type and the `Grpc.*`
namespaces are genuinely unresolved — a real "possible missing restore"
scenario (P1-08), not a simulated one. Test setup that exercises the full
fixture must restore `Acme.Orders` and `Acme.Shared.Contracts` but must
**not** restore `Acme.Payments`.

**Messaging pattern:** no external broker. `IEventBus` (in
`Acme.Shared.Contracts`) is a minimal in-repo abstraction —
`PublishAsync<TEvent>` / `Subscribe<TEvent>` — chosen so the fixture builds
without pulling in a message-broker dependency. `MessagingDetector` (T17) is
expected to recognize this shape.

`appsettings.json` (under `Acme.Orders/`) holds the three name-resolution
variants; `ConfigIndexer`/`ServiceNameResolver` (T8/T9) index config across
**all** discovered service roots, so its location doesn't restrict which
service's calls can resolve against it.

## Extended for multi-solution composition (`Acme.Shipping`)

The original three services have no **positive** cross-solution pair: the only
`HandleAsync` for `OrderPlaced` lives in `Acme.Orders`, and `Acme.Payments`
exposes gRPC only. **`Acme.Shipping`** is a restorable fourth solution added so
a two-solution batch can prove messaging and HTTP correlation end to end:

- handles `OrderPlaced` through local `IIntegrationEventHandler.HandleAsync`
  (inbound messaging whose protocol operation key equals the one `Acme.Orders`
  publishes)
- exposes `POST shipments`, closing the `ShippingService` client call
  `Acme.Orders/OrderService.cs` already makes (`PostAsJsonAsync("shipments", ...)`)
- project-references `Acme.Shared.Contracts` like Orders does
- restores and builds without new NuGet dependencies (local `HttpPostAttribute`
  / `ControllerBase` stand-ins, same pattern as Orders' `HttpGet`)

`Acme.Shipping.slnx` includes Shipping and Shared.Contracts only — not Payments
or Broken. **`Acme.Payments` stays untouched** so its deliberate unrestored
state still serves P1-08.

**Closed in T26 (was a known gap vs. spec.md's P2 Independent Test):** that
narrative names a "simulated gRPC call," but T2's approved Done-when only
specified Payments *exposing* a gRPC service (server-side), so no gRPC client
invocation existed here. T26's own Done-when requires making the P2
Independent Test executable, so the missing client call was added:
`Acme.Orders/PaymentsGrpcClient.cs` declares `Grpc.Core.ClientBase` and a
generated-shaped `PaymentsClient`, and `OrderService.AuthorizePaymentAsync`
calls its unary `AuthorizePayment` RPC. The base type is declared in-fixture
rather than pulled from `Grpc.AspNetCore` for the same reason `IEventBus`
stands in for a broker — `Acme.Orders` must stay restorable without new
external dependencies. `GrpcClientDetector` matches on the namespace-qualified
base type, so this exercises the real rule, not a weakened one. The detected
target is `Payments` (the proto service behind the client), which no config
entry names — so the edge is `sincrono-bloqueante` / `unresolved`, per AD-005.

## Extended in T3 (SolutionLoader integration tests)

T2's original fixture had no `.sln`/`.slnx` anywhere (every service was a
`LooseProjects` boundary), so `SolutionLoader` had nothing to call
`OpenSolutionAsync` against — P1-05 through P1-09 need a real solution file.
Added:

- **`Acme.Orders/Acme.Orders.slnx`** — bundles `Acme.Orders` (restored),
  `Acme.Shared.Contracts` (restored), `Acme.Broken` (see below), and a
  reference to `../Acme.DoesNotExist/Acme.DoesNotExist.csproj`, which does
  not exist on disk at all. Exercises `Ok` (P1-07 baseline), `Degraded`
  (P1-07), and `SkipUnrecognizedProjects` (P1-06 — without it, opening this
  `.slnx` throws instead of skipping the missing reference).
- **`Acme.Broken/Acme.Broken.csproj`** — `Sdk="Acme.NonExistent.Sdk/99.99.99"`,
  a real unresolvable-SDK reference. Confirmed empirically (not just
  inferred) to produce a `WorkspaceDiagnostic` with `Kind == Failure` when
  opened via `MSBuildWorkspace.OpenSolutionAsync`.
- **`Acme.Payments/Acme.Payments.slnx`** — bundles `Acme.Payments`
  (deliberately unrestored, per T2) and `Acme.Shared.Contracts` (restored).
  Isolated from `Acme.Orders.slnx` so `PossibleMissingRestore` classification
  can be tested without the `Degraded`/missing-reference noise in the other
  solution.
- **`Directory.Build.props`** (this directory) — disables SourceLink for the
  whole fixture tree. `MSBuildWorkspace` surfaces SourceLink's own
  "repository has no remote" / "no commits" build-task warnings as
  `Kind == Failure` workspace diagnostics (confirmed empirically) — pure
  noise for projects that are never packed or published. Without this, every
  fixture project appeared falsely `Degraded` until the repo's first commit.

**Not fixture-based:** `UnsupportedForCompilation` (P1-09,
`GetCompilationAsync() == null` because `Project.SupportsCompilation` is
`false`) is tested via a hand-built `Project` on a plain `AdhocWorkspace`
with an unregistered language string, not through this fixture — no real,
low-effort way to make an MSBuild-loaded C#/.NET project trigger that branch
was found (F# would, but pulling in the F# SDK for one test case wasn't
judged worth it). See `SolutionLoaderClassificationTests.cs`.

## Knowledge package oracle

`knowledge-oracle.json` is hand-authored acceptance data. It names the expected
roots, boundary edges, variant occurrences, causal cycle, controlled unresolved
Shipping destination, and values that must not reach a published package. The
fixture adds `Acme.Shipping.Tests` to make include-tests observable and changes
`Acme.Shared.Contracts` to `net8.0;net10.0` so per-project target frameworks
are exercised without a solution-wide target framework.
