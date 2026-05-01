# Implementation Plan: Consolidated Processed-Document Schema

**Branch**: `001-document-schema-aggregation` | **Date**: 2026-05-01 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/001-document-schema-aggregation/spec.md`

## Summary

Replace the per-page persistence model in Cosmos DB with a single consolidated `ProcessedDocument` record per logical document, keyed by `fileTkNumber` and forward-filled across pages where the identifier is missing. Each of the 13 schema fields (excluding `pageCount`) is persisted as a `SchemaField` carrying immutable OCR provenance (`ocrValue`, `ocrConfidence`) alongside mutable reviewer state (`reviewedValue`, `reviewedAt`, `reviewedBy`, `fieldStatus`). Reviewers take exclusive ownership of a record via explicit checkout/check-in (24-hour opportunistic auto-release); review status (`Pending`/`Reviewed`) is independent of checkout state and derived from per-field statuses. The Processor function and Blazor WebApp are both updated; the Cosmos container is wiped at deployment.

The work is delivered TDD per Constitution Principle II — every behavioral FR has at least one failing test before the corresponding production code lands.

## Technical Context

**Language/Version**: C# / .NET 8.0 (Functions host `dotnet-isolated`); Blazor Server in WebApp.
**Primary Dependencies**: `Azure.AI.FormRecognizer` (OCR), `Microsoft.Azure.Cosmos` (persistence + ETag concurrency), `Microsoft.Azure.Functions.Worker` (queue trigger), `Microsoft.AspNetCore.Components` (Blazor), `Newtonsoft.Json` (Cosmos serialization), `xunit` + `Moq` (tests).
**Storage**: Azure Cosmos DB SQL API. Container `ProcessedDocuments`, partition key `/identifier` (= `fileTkNumber`). Same container reused; schema changes; `ETag` used for optimistic concurrency on checkout/save/check-in operations (defense-in-depth alongside the explicit checkout lock).
**Testing**: `xunit` + `Moq` in `tests/DocumentOcr.Tests.csproj`. Pure unit tests; no live Azure. Constitution Principle II forbids integration tests against live Azure.
**Target Platform**: Linux (Functions consumption / Premium plan, App Service for WebApp). Same as today.
**Project Type**: Multi-project .NET solution with two deployable units (`DocumentOcr.Processor` Functions host + `DocumentOcr.WebApp` Blazor) sharing `DocumentOcr.Common`.
**Performance Goals**: Constitution: 60s p95 end-to-end for a typical 10-page PDF; Operations API ≤ 500 ms p95 for non-start endpoints. Checkout/check-in/save endpoints inherit the 500 ms budget.
**Constraints**: Keyless auth (Managed Identity) preserved; `local.settings.json` and `appsettings.Development.json` remain gitignored; nullable reference types stay enabled; no file > ~300 lines.
**Scale/Scope**: POC scale — single-region, low double-digit reviewer count, hundreds of documents per day. Cosmos provisioned throughput unchanged.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle                         | Pre-Phase 0 | Post-Phase 1 | Notes                                                                                                                                                                                                                                                                                                                                                           |
| --------------------------------- | ----------- | ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| I. Code Quality & Maintainability | ✅           | ✅            | New services (`DocumentSchemaMapperService`, `DocumentReviewService`, `DocumentLockService`) behind interfaces in `DocumentOcr.Common.Interfaces`; registered in both `Processor/Program.cs` and `WebApp/Program.cs` as needed. No file > 300 lines projected. Nullable enabled.                                                                                |
| II. Testing (NON-NEGOTIABLE)      | ✅           | ✅            | Every FR maps to at least one test (FR-012). Test plan in `quickstart.md` enumerates the failing-first ordering. Tests use mocks; no live Azure.                                                                                                                                                                                                                |
| III. User Experience Consistency  | ✅           | ✅            | `Review.razor` and `Documents.razor` rewrites stay within the existing Bootstrap component vocabulary. New states (`Confirmed` / `Corrected` / `CheckedOut`) are added to `docs/REVIEW-PAGE-UX.md` in the same PR. Operations API contract unchanged.                                                                                                           |
| IV. Performance & Reliability     | ✅           | ✅            | No new external service calls. Cosmos writes per document grow from 1 to 1 (same). Checkout/check-in are single point reads + single ETag-conditional replace. Pipeline failure modes (single-page OCR error, duplicate processing, missing identifier) all degrade gracefully per the spec.                                                                    |
| V. Security & Secure-by-Default   | ✅           | ✅            | No new external surfaces. Checkout/check-in/save authorization enforced server-side at both the controller layer (HTTP mapping) and the service layer (`InvalidOperationException` on non-holder per `IDocumentLockService` guarantee 6 / `IDocumentReviewService`). All user input validated at the WebApp boundary. Managed-identity Cosmos access unchanged. |

**Gates pass with one documented deviation** (see Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/001-document-schema-aggregation/
├── plan.md              # This file (/speckit.plan output)
├── research.md          # Phase 0 — design decisions and rejected alternatives
├── data-model.md        # Phase 1 — entities, persisted JSON shape, state machines
├── quickstart.md        # Phase 1 — TDD walkthrough and local validation steps
├── contracts/           # Phase 1 — interface contracts (services + REST)
│   ├── IDocumentSchemaMapperService.md
│   ├── IDocumentReviewService.md
│   ├── IDocumentLockService.md
│   └── ReviewController.http.md
├── checklists/
│   └── requirements.md  # Existing — passes
└── spec.md              # Existing — clarifications Q1–Q11 resolved
```

### Source Code (repository root)

The existing layout (`src/DocumentOcr.Common`, `src/DocumentOcr.Processor`, `src/DocumentOcr.WebApp`, `tests/`) is preserved per Constitution Engineering Constraints. Only the files below are added or modified.

```text
src/DocumentOcr.Common/
├── Interfaces/
│   ├── IBlobStorageService.cs                  # unchanged
│   ├── ICosmosDbService.cs                     # extended: ReplaceWithETagAsync, GetByIdentifierAsync
│   ├── IDocumentSchemaMapperService.cs         # NEW: page OCR → ProcessedDocument
│   ├── IDocumentReviewService.cs               # NEW: per-field save/transition logic (used by WebApp)
│   ├── IDocumentLockService.cs                 # NEW: checkout/check-in/cancel/auto-release (used by WebApp)
│   └── ICurrentUserService.cs                  # NEW: authenticated principal abstraction (used by WebApp)
├── Models/
│   ├── DocumentOcrEntity.cs                    # REWRITTEN: new persisted shape (see data-model.md)
│   ├── SchemaField.cs                          # NEW
│   ├── SchemaFieldStatus.cs                    # NEW: enum Pending/Confirmed/Corrected
│   ├── ReviewStatus.cs                         # NEW: enum Pending/Reviewed
│   ├── IdentifierSource.cs                     # NEW: enum Extracted/Inferred
│   ├── PageProvenanceEntry.cs                  # NEW
│   └── ProcessedDocumentSchema.cs              # NEW: static field-name catalog + types
└── Services/
    ├── BlobStorageService.cs                   # unchanged
    ├── CosmosDbService.cs                      # extended: ETag-conditional replace + GetByIdentifierAsync
    ├── DocumentSchemaMapperService.cs          # NEW
    ├── DocumentReviewService.cs                # NEW
    ├── DocumentLockService.cs                  # NEW
    └── DocumentListFilter.cs                   # NEW (analysis-fix C4: pure filter helper for Documents.razor)

src/DocumentOcr.Processor/
├── Functions/
│   └── PdfProcessorFunction.cs                 # MODIFIED: replace per-page persistence with mapper + duplicate-skip
├── Services/
│   ├── DocumentIntelligenceService.cs          # MODIFIED: fix valueSignature ("present" → "signed") + surface boundingRegions
│   └── DocumentAggregatorService.cs            # MODIFIED: forward-fill identifier; emit page provenance
├── Models/
│   ├── PageOcrResult.cs                        # extended: optional confidence-per-field accessor
│   └── AggregatedDocument.cs                   # extended: PageProvenance entries
└── Program.cs                                  # MODIFIED: register new services

src/DocumentOcr.WebApp/
├── Components/Pages/
│   ├── Documents.razor                         # MODIFIED: progress column + checkout column + filter
│   └── Review.razor                            # REWRITTEN: single-form schema + checkout banner + per-field state badges
├── Controllers/
│   └── ReviewController.cs                     # NEW or extended: checkout/check-in/cancel/save endpoints
├── Services/
│   └── CurrentUserService.cs                   # NEW (thin): exposes authenticated principal as the reviewerId
└── Program.cs                                  # MODIFIED: register IDocumentReviewService, IDocumentLockService, ICurrentUserService

tests/
├── DocumentOcr.Tests.csproj                    # MODIFIED: add ProjectReference to DocumentOcr.Common
├── Models/
│   ├── SchemaFieldTests.cs                     # NEW
│   ├── ProcessedDocumentSchemaTests.cs         # NEW
│   └── (existing tests preserved)
└── Services/
    ├── DocumentSchemaMapperServiceTests.cs     # NEW (covers FR-001..FR-006, FR-009, FR-014)
    ├── DocumentAggregatorServiceTests.cs       # NEW (covers FR-001, FR-020 incl. inferred-pages warning log)
    ├── DocumentReviewServiceTests.cs           # NEW (covers FR-015..FR-018)
    ├── DocumentLockServiceTests.cs             # NEW (covers FR-021..FR-024 incl. stale-release warning log)
    ├── DocumentIntelligenceServiceTests.cs     # NEW (analysis-fix F2: covers MapSignatureValue helper)
    ├── DocumentListFilterTests.cs              # NEW (analysis-fix C4: covers list-page filter)
    ├── PdfProcessorFunctionTests.cs            # NEW (analysis-fix C1+C3: covers FR-019 duplicate-skip and FR-013 logging)
    └── CosmosDbServiceTests.cs                 # NEW (covers ETag conditional replace + GetByIdentifier)
```

**Structure Decision**: Preserve the three existing projects per the constitution. New cross-cutting services (`SchemaMapper`, `Review`, `Lock`) live in `DocumentOcr.Common` so both the Processor and the WebApp consume them; the Processor uses only `SchemaMapper` (one-way write at extraction), while the WebApp uses `Review` and `Lock` (read-modify-write at review time). The Operations API surface in `Processor.Functions.OperationsApi` is unchanged. Reviewer-facing checkout/save/check-in HTTP endpoints live in the WebApp (`ReviewController`), not in the Operations API, because they are session-scoped UI concerns rather than long-running pipeline operations.

## Complexity Tracking

> Constitution Check passed cleanly except for one documented deviation surfaced by `/speckit.analyze` (F3).

| Violation                                                                                                                     | Why Needed                                                                                                                                                                                                                                                                                        | Simpler Alternative Rejected Because                                                                                                                                                                                                                                                                                                                     |
| ----------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Single-shot ETag-conflict re-read in `DocumentLockService` (technically a bespoke retry loop under Constitution Principle IV) | Optimistic concurrency on Cosmos DB requires re-reading the entity once after a 412 response to attach the new ETag before re-attempting; this is the SDK-recommended pattern for read-modify-write and not a custom backoff loop. Bounded to exactly one retry; no exponential delay; no jitter. | (a) Removing the retry would force every transient ETag conflict to surface to the WebApp as a 409 / forced page reload, which is hostile UX for the common case where two browser tabs of the same reviewer race. (b) Wrapping the SDK's built-in retry policy does not help here because that policy targets transport faults, not 412 ETag conflicts. |
