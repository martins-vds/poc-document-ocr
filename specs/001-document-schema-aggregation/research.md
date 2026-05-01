# Phase 0 — Research

**Feature**: Consolidated Processed-Document Schema
**Status**: Complete (no NEEDS CLARIFICATION remaining; spec Q1–Q11 are all resolved)

This document records the technical-decision research dispatched for Phase 0. Every clarification listed in `spec.md` was answered by the user during the spec / clarify phase, so the work here is limited to confirming **how** the chosen behaviors will be realized in the existing codebase.

---

## D1: Cosmos DB optimistic concurrency for checkout / check-in

**Decision**: Use Cosmos DB's native ETag (`_etag`) on `ReplaceItemAsync` with `ItemRequestOptions.IfMatchEtag` for every write that mutates checkout state or per-field state. The explicit checkout flag (`checkedOutBy`) is the *user-facing* lock; the ETag is the *infrastructural* safety net guaranteeing no torn writes if a stale request slips through.

**Rationale**:
- Already supported by `Microsoft.Azure.Cosmos` SDK; no new dependencies.
- Single round-trip; preserves the constitution's 500 ms p95 budget for non-start API calls.
- Defense-in-depth: even if a UI bug skips the checkout check, the ETag prevents silent overwrites.

**Alternatives considered**:
- **Cosmos Stored Procedure for atomic checkout**: rejected — adds a deployment artifact and per-account configuration; ETag-conditional replace covers the same ground with less surface area.
- **Optimistic merge without ETag (read-modify-write with full payload)**: rejected — race-window between read and write would silently overwrite a concurrent reviewer's edits, defeating the entire point of the checkout model.

---

## D2: Forward-fill of `fileTkNumber` (Q8 → FR-020)

**Decision**: Implement forward-fill inside `DocumentAggregatorService` (existing class), iterating the page-ordered `PageOcrResult` list once, carrying the most recently seen `fileTkNumber`. Each page produces one `PageProvenanceEntry` recording the page number and whether the identifier was `Extracted` (this page reported it) or `Inferred` (carried from a previous page). Leading orphans (no preceding extracted identifier) form a synthetic identifier `unknown-<blob>-<firstPage>`.

**Rationale**:
- Single linear pass; deterministic; testable with a list of `PageOcrResult` fixtures and no Azure mocks.
- Provenance is computed once and persisted with the consolidated record so the Review page can flag inferred boundaries (FR-020).

**Alternatives considered**:
- **Backward-fill** (use the next page's identifier): rejected — semantically reads "this page belongs to the next document," which is wrong for sequential multi-document PDFs.
- **Window of N preceding pages instead of nearest**: rejected — adds tunables for no measurable benefit; nearest-preceding is the user's intent for sequential docs.

---

## D3: Document Intelligence signature value handling (Q3 → FR-006)

**Decision**: Modify `DocumentIntelligenceService.AnalyzeDocumentAsync` so that for `DocumentFieldType.Signature` the emitted `valueSignature` reflects the actual SDK value (`"signed"` when DI detects a signature, otherwise `"unsigned"` / absent) instead of the current hard-coded `"present"`. The `confidence` field already flows through. The `DocumentSchemaMapperService` then maps `(type=signature, valueSignature=signed)` → `ocrValue=true` per FR-006.

**Rationale**:
- Spec sample shows DI returns `valueSignature: "signed"`; the current `"present"` literal is a known-incorrect simplification.
- Centralizing the boolean derivation in the schema mapper (not the OCR service) keeps the OCR service responsible for "translate DI shape → dictionary" and the mapper responsible for "translate dictionary → consolidated schema field".

**SDK note**: `Azure.AI.FormRecognizer` v4 surfaces signature presence via `DocumentFieldType.Signature` and `DocumentField.Value.AsString()` returning `"signed"` / `"unsigned"`. If the installed version returns the value differently, the mapper falls back to `"present"`-vs-absent and the warning log fires per FR-006 (this matches the existing pessimistic behavior).

**Alternatives considered**:
- **Keep `"present"` literal and treat any presence as `true`**: rejected — loses the "unsigned but detected" distinction and silently inflates `judgeSignature = true` rates.

---

## D4: Concatenated-field confidence aggregation (Q4 → FR-005)

**Decision**: In `DocumentSchemaMapperService`, for `mainCharge` and `additionalCharges`, compute aggregated confidence as `Math.Min` over the per-page confidence values that contributed text. If no contributing page reports a confidence, persist `null`.

**Rationale**:
- Constant-time, easy to explain to reviewers ("weakest link"), aligns with how the UX color bands already work — the per-field badge will pick the band corresponding to the worst contributing page.

**Alternatives considered**:
- **Multiplication** (joint probability): rejected by user (Q4 = B). Was discussed; user chose minimum.
- **Length-weighted average**: rejected by user.

---

## D5: Stale-checkout auto-release (Q9a → FR-022)

**Decision**: Opportunistic, triggered only by the next checkout attempt against a held record older than 24 hours. No background timer / TimerTrigger. Implementation lives in `DocumentLockService.TryCheckoutAsync`: after reading the record, if `checkedOutAt` is more than `StaleCheckoutThreshold` ago, the service clears `checkedOutBy`/`checkedOutAt` (under ETag) and proceeds to set them for the requesting reviewer. The previous holder's saved per-field edits are preserved unchanged.

**Rationale**:
- POC simplicity (constitution: avoid over-engineering).
- No Functions cold-start cost for a TimerTrigger that fires hourly.
- Works at any traffic level — if no one tries to check out the stale record, no one cares.

**Alternatives considered**:
- **TimerTrigger background sweep**: rejected for now; can be added later without changing persisted shape.
- **Client-side timer that auto-checks-in on idle**: rejected — fragile (browser tab close, sleep) and bypasses server-side authority.

---

## D6: Test framework choice for new tests

**Decision**: Continue with `xunit` + `Moq` (already in `tests/DocumentOcr.Tests.csproj`). Add a `ProjectReference` to `DocumentOcr.Common` so new tests can target services in that project directly.

**Rationale**: Constitution mandates determinism and abstraction over external services; both already idiomatic in the existing test project. Adding the `Common` reference is a single-line `.csproj` change.

**Note**: `tests/DocumentOcr.Tests.csproj` currently targets `net10.0` while `Common` and `Processor` target `net8.0` (per constitution). This is a pre-existing inconsistency; not in scope for this feature. If `dotnet test` exhibits an issue, the test project will be downgraded to `net8.0` as a one-line fix and noted in the PR — but this is not a planning-time blocker.

---

## D7: Reviewer identity (`reviewedBy` / `checkedOutBy` / `lastCheckedInBy`)

**Decision**: Use the authenticated user's User Principal Name (UPN) from the existing Microsoft Entra ID integration in the WebApp. Exposed via a thin `ICurrentUserService` injected into `ReviewController` and into `DocumentReviewService` / `DocumentLockService` indirectly (the services accept the reviewer identifier as a parameter; the controller resolves it from `HttpContext.User`).

**Rationale**:
- UPN is human-readable in the UI ("Checked out by alice@contoso.com").
- Stays string-typed in Cosmos; no schema lock-in to a specific identity provider.
- Constitution Principle V (server-side authorization): the controller, not the Razor page, resolves identity.

**Alternatives considered**:
- **Object ID (oid claim)**: rejected — opaque to humans; would require a separate display-name lookup for the UI. UPN is sufficient for the POC.

---

## D8: Cosmos container wipe on deployment (Q1 → FR-010)

**Decision**: Add a one-shot operator-acknowledged step to `infra/hooks/postprovision.sh` (and `.ps1`) that empties the `ProcessedDocuments` container by deleting and recreating it. The script requires an environment variable `CONFIRM_WIPE_DOCUMENTS=yes` to proceed; otherwise it logs a warning and exits non-zero so `azd up` fails loudly.

**Rationale**:
- Deployment-time, explicit, and impossible to trigger from application code at runtime.
- Aligns with constitution's Operational Safety: destructive, requires acknowledgment.

**Alternatives considered**:
- **Auto-wipe in `Program.cs` startup**: rejected — runs every deploy without operator awareness; dangerous.
- **Manual portal step in deployment runbook only**: rejected — easy to forget; lets per-page legacy records linger and crash `Review.razor`.

---

## Summary

All design decisions trace to a Resolved Clarification in `spec.md` or to an existing constitution rule. No new external dependencies, no new Azure services, no new deployment artifacts beyond the postprovision hook update. Implementation can proceed to Phase 1 (Data Model + Contracts).
