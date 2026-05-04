# Tests

The Document OCR test suite is split into two projects:

| Project                                                       | Scope                                                                                                                                                                                                                                                                                                 |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [DocumentOcr.UnitTests](DocumentOcr.UnitTests/)               | Pure in-process unit tests. Models, services, helpers, and fine-grained Blazor component tests via bUnit. Uses Moq + bUnit fakes — no external dependencies. Runs in CI on every push.                                                                                                                |
| [DocumentOcr.IntegrationTests](DocumentOcr.IntegrationTests/) | Full-stack integration tests. Hosts the WebApp in-process via `WebApplicationFactory<Program>`, exercises Blazor pages end-to-end via bUnit, and (optionally) round-trips storage / persistence through Azurite + the Cosmos DB Emulator. Emulator-bound tests are skipped when emulators are absent. |

## Run

```bash
# All tests
./scripts/run-tests.sh

# Unit tests only (fast)
./scripts/run-tests.sh --unit

# Integration tests only
./scripts/run-tests.sh --integration

# Filter
./scripts/run-tests.sh --filter 'FullyQualifiedName~PdfControllerTests'
```

The same options are available in `scripts/run-tests.ps1` via `-Scope Unit | Integration | All`.

## Folder layout

```
tests/
├── DocumentOcr.UnitTests/              # fast, isolated tests (no external deps)
│   ├── Models/                         # POCO + invariants
│   ├── Services/                       # service unit tests with Moq
│   └── WebApp/                         # bUnit + helper unit tests
└── DocumentOcr.IntegrationTests/       # end-to-end / boundary tests
    ├── Fixtures/
    │   ├── AzuriteFixture.cs           # detects Azurite on 10000/10001
    │   ├── CosmosEmulatorFixture.cs    # detects Cosmos emulator on 8081
    │   ├── DocumentIntelligenceStub.cs # in-process IDocumentIntelligenceService double
    │   └── WebAppFactory.cs            # WebApplicationFactory<Program> + test auth
    ├── Processor/                      # storage/persistence round-trip tests
    ├── WebApp/Api/                     # WAF tests for PdfController + ReviewController
    ├── WebApp/Auth/                    # auth-pipeline boundary tests
    └── WebApp/Pages/                   # full-render bUnit tests for Blazor pages
```

## Conventions

- `[Fact]` / `[Theory]` for in-process tests that always run.
- `[SkippableFact]` (`Xunit.SkippableFact`) + `Skip.IfNot(...)` for tests that
  require Azurite or the Cosmos emulator. The fixture's `IsAvailable` flag
  drives the skip.
- WebApp tests use the per-scenario factory pattern:
  `_factory.ForScenario(authenticatedUpn: ..., configureServices: ...)` —
  never mutate state on the shared `IClassFixture` instance because
  `WebApplicationFactory` caches the host on first `CreateClient`.
- Integration tests should `using var factory = _factory.ForScenario(...)`
  to dispose the per-test host.

## Local emulator setup (optional)

```bash
# Azurite (for storage round-trip tests)
azurite --silent --location /tmp/azurite &

# Cosmos DB emulator — run via Docker if not on Windows
# https://learn.microsoft.com/azure/cosmos-db/how-to-develop-emulator
```

When neither is running, integration tests still pass — emulator-bound
cases simply report as **skipped**.
