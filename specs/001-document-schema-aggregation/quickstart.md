# Quickstart — Implementing the Consolidated Schema Feature

**Audience**: An engineer (or a `/speckit.implement` run) executing this feature TDD-style.
**Prerequisites**: Working local dev loop per `docs/QUICKSTART.md` (Azurite, Document Intelligence creds, Cosmos creds in `local.settings.json` and `appsettings.Development.json`).

This walkthrough enumerates the failing-first ordering and validation steps. Each numbered block is a single TDD slice: write the test, watch it fail, implement the production code, watch it pass, then move on.

---

## 0. One-time setup

```bash
# From repo root
cd tests
# Add reference to Common (currently only references Processor)
dotnet add DocumentOcr.Tests.csproj reference ../src/DocumentOcr.Common/DocumentOcr.Common.csproj
cd ..
dotnet build
```

If `tests/DocumentOcr.Tests.csproj` fails to build because of the `net10.0` vs `net8.0` mismatch noted in `research.md` D6, change its `<TargetFramework>` to `net8.0`.

---

## 1. Models (no behavior; pure shape)

Add the new files in `src/DocumentOcr.Common/Models/`:
- `SchemaFieldStatus.cs`, `ReviewStatus.cs`, `IdentifierSource.cs` (enums)
- `SchemaField.cs`, `PageProvenanceEntry.cs`
- `ProcessedDocumentSchema.cs` (static field-name catalog)

Tests in `tests/Models/`:
- `SchemaFieldTests` — invariant: cannot construct `Confirmed` with `ReviewedValue != OcrValue`; cannot construct `Pending` with non-null `ReviewedAt`.
- `ProcessedDocumentSchemaTests` — `FieldNames` contains all 13 expected names in catalog order.

Then rewrite `Models/DocumentOcrEntity.cs` to the shape in `data-model.md`. Existing tests (`DocumentResultTests`, etc.) compile-break — fix them to match the new property names; add `SkipReason` to any that no longer apply.

**Validate**: `dotnet build && dotnet test` — model tests green, no regressions.

---

## 2. `DocumentSchemaMapperService` (pure; no Azure)

Tests in `tests/Services/DocumentSchemaMapperServiceTests.cs` covering the 7 cases listed in the contract. Implementation in `src/DocumentOcr.Common/Services/`. Add the interface to `src/DocumentOcr.Common/Interfaces/IDocumentSchemaMapperService.cs`.

**Validate**: All mapper tests green.

---

## 3. Forward-fill in `DocumentAggregatorService` + signature fix in `DocumentIntelligenceService`

- In `tests/Services/DocumentAggregatorServiceTests.cs`: write tests for (a) forward-fill of identifier across pages, (b) leading orphans get the synthetic identifier, (c) `PageProvenance` entries record `Extracted` vs `Inferred` correctly.
- Modify `DocumentAggregatorService.AggregatePagesByIdentifier` to perform the linear forward-fill pass and emit `PageProvenance`. Extend `AggregatedDocument` with `List<PageProvenanceEntry> PageProvenance`.
- In `DocumentIntelligenceService.AnalyzeDocumentAsync`, replace the hard-coded `"present"` with the SDK-returned value (cast to lower-case string; default `"present"` if SDK returns null for back-compat). Add a unit test using a small fake or use a lightweight wrapper — if the SDK is hard to mock, document the change in the PR description and rely on the schema-mapper tests for coverage of the `signed`-vs-`unsigned` mapping.

**Validate**: Aggregator tests green. Build clean.

---

## 4. `CosmosDbService` extensions (ETag conditional replace + identifier lookup)

Tests in `tests/Services/CosmosDbServiceTests.cs` using a `Mock<Container>`:
- `Update_PassesIfMatchEtag` — `ReplaceWithETagAsync` passes `IfMatchEtag` from the entity (T021).
- `Replace_OnETagMismatch_ThrowsCosmosException412` (T021).
- `GetByIdentifier_WhenRecordExists_ReturnsIt` / `GetByIdentifier_WhenAbsent_ReturnsNull` — single-partition `SELECT TOP 1` keyed on `identifier` (T021 / T018a). This is the lookup that backs the FR-019 duplicate-skip behavior in section 7.

Extend `ICosmosDbService` with `ReplaceWithETagAsync` (T018) and `GetByIdentifierAsync` (T018a). The lock/review services consume these via the entity-carried ETag.

**Validate**: Cosmos service tests green.

---

## 5. `DocumentReviewService` (per-field save + record transition)

Interface, implementation, and tests per `contracts/IDocumentReviewService.md`. 8 tests, ordered as listed.

Note: signature OCR values are normalized via the `MapSignatureValue` helper extracted from `DocumentIntelligenceService` (T033, analysis-fix F2). Cover that helper in `tests/Services/DocumentIntelligenceServiceTests.cs`.

**Validate**: All review-service tests green.

---

## 6. `DocumentLockService` (checkout / check-in / cancel + stale auto-release)

Interface, implementation, and tests per `contracts/IDocumentLockService.md`. 8 tests, ordered as listed. Inject an `Func<DateTime>` clock so the 24-hour test is deterministic.

**Validate**: All lock-service tests green.

---

## 7. Wire into `Processor` pipeline

- In `Functions/PdfProcessorFunction.cs`, replace the `combinedExtractedData` block with the FR-019 duplicate-skip pre-check followed by the mapper + create:
  ```csharp
  var existing = await _cosmosDbService.GetByIdentifierAsync(aggregatedDoc.Identifier);
  if (existing is not null)
  {
      _logger.LogWarning(
          "Skipping duplicate identifier {Identifier} (existing record {ExistingId}) for operation {OperationId}",
          aggregatedDoc.Identifier, existing.Id, operationId);
      processingResult.AppendSkip(aggregatedDoc.Identifier, existing.Id);
      continue;
  }
  var entity = _schemaMapper.Map(aggregatedDoc, documentNumber, message.BlobName, blobUrl, outputBlobName);
  await _cosmosDbService.CreateDocumentAsync(entity);
  ```
- Register `IDocumentSchemaMapperService` in `Program.cs` (Scoped).
- Add the `Process_DuplicateIdentifier_*` tests in `tests/Services/PdfProcessorFunctionTests.cs` (T021a) and the `Process_LogsConsolidationOutcomeAtInformation` test (T051).
- The existing operation-progress and error-handling code is untouched. The owning operation transitions to `Succeeded` even when every document is skipped.

**Validate**: `dotnet build` clean. Run end-to-end against Azurite + a Cosmos DB Emulator (or live POC Cosmos) using a sample multi-document PDF; verify a single Cosmos document per `fileTkNumber` with all 13 schema keys.

---

## 8. Wire into `WebApp`

- Register `ICurrentUserService`, `IDocumentReviewService`, `IDocumentLockService` in `WebApp/Program.cs` (Scoped).
- Add `Controllers/ReviewController.cs` per `contracts/ReviewController.http.md`.
- Rewrite `Components/Pages/Review.razor`:
  - Remove the per-page tab UI; render a single form with all 13 fields.
  - On load, call `POST /api/review/{id}/checkout`. Render the checkout banner; if 409, render read-only with the holder info (FR-025).
  - Per-field controls: show OCR value (read-only), confidence bar, `[Confirm]` / `[Edit]` buttons, status badge.
  - Footer: `[Save]` (PUT), `[Save & Check In]` (POST checkin with edits), `[Cancel]` (POST cancel-checkout).
  - On the page-boundary banner: render the `PageProvenance` summary highlighting `Inferred` boundaries.
- Update `Components/Pages/Documents.razor`: add columns for review progress (`X/13 reviewed`), checked-out-by indicator, and filter-by-checked-out (delegated to the `DocumentListFilter` helper covered by `tests/Services/DocumentListFilterTests.cs`, T045a).
- Update `docs/REVIEW-PAGE-UX.md` with the new states and badges.

**Validate**: Run the WebApp locally; full reviewer flow (load → checkout → edit → save → check-in → reload list shows `Reviewed`) works.

---

## 9. Postprovision wipe hook

Edit `infra/hooks/postprovision.sh` (and `.ps1`) to:
- Check `CONFIRM_WIPE_DOCUMENTS=yes`.
- If set, delete and recreate the `ProcessedDocuments` container (preserve provisioned throughput).
- Log loudly; exit non-zero if not confirmed AND legacy schema records are detected (cheap query: `SELECT TOP 1 * FROM c WHERE NOT IS_DEFINED(c.schema)`).

**Validate**: Run `azd provision` against a sandbox; confirm wipe behavior under both confirmation and refusal paths.

---

## 10. Documentation pass

Update:
- `docs/REVIEW-PAGE-UX.md` — new field-status badges, checkout banner, page-boundary highlight.
- `docs/ARCHITECTURE.md` — new diagram showing the schema mapper between aggregator and Cosmos.
- `README.md` — note the new persisted shape in the "Output" section.

---

## Definition of done

- [ ] All tests in `tests/` pass; new tests cover every FR in `spec.md` per Constitution Principle II.
- [ ] `dotnet build` clean across all 4 projects with zero warnings.
- [ ] End-to-end: a 5-page multi-document PDF produces N consolidated Cosmos records (one per `fileTkNumber`), each with all 13 schema keys.
- [ ] `Review.razor` flow: checkout → confirm/correct fields → save → check-in transitions `reviewStatus` to `Reviewed` once the last field leaves `Pending`.
- [ ] Stale-checkout test: simulate a 25-hour-old checkout in Cosmos, confirm next reviewer can acquire.
- [ ] Documentation updated.
- [ ] Constitution Check re-validated post-implementation; no new entries in `plan.md` Complexity Tracking.
