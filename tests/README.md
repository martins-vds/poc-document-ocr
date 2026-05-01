# Document OCR Processor - Unit Tests

This directory contains unit tests for the Document OCR Processor application.

## FR → Test Coverage (feature 001-document-schema-aggregation)

| FR     | Behaviour                                           | Test class / case                                                                                                                           |
| ------ | --------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| FR-001 | Persist consolidated document per identifier        | `DocumentSchemaMapperServiceTests.Map_AlwaysPopulatesAll13SchemaKeys`                                                                       |
| FR-002 | Forward-fill identifier across pages                | `DocumentAggregatorServiceTests.Aggregate_GapsAreForwardFilled`                                                                             |
| FR-003 | Synthetic identifier when none extracted            | `DocumentSchemaMapperServiceTests.Map_BlankIdentifier_UsesSyntheticFallback` + `Aggregate_LeadingPagesWithoutIdentifier_FormSyntheticGroup` |
| FR-004 | Single-value: highest-confidence wins               | `DocumentSchemaMapperServiceTests.Map_HighestConfidencePageWins_ForSingleValueFields`                                                       |
| FR-005 | Multi-value: page-ordered concat + min confidence   | `DocumentSchemaMapperServiceTests.Map_MultiValueFields_ConcatenatedInPageOrder_WithMinConfidence`                                           |
| FR-006 | Signatures map to bool                              | `DocumentSchemaMapperServiceTests.Map_SignatureField_PresentMapsToTrue` + `_AbsentMapsToFalse`                                              |
| FR-007 | Always emit all 13 schema keys                      | `ProcessedDocumentSchemaTests.FieldNames_AreInCatalogOrder` + `Map_AlwaysPopulatesAll13SchemaKeys`                                          |
| FR-008 | `pageCount` populated                               | `DocumentSchemaMapperServiceTests.Map_InitialState_AllFieldsPending_DocumentReviewPending`                                                  |
| FR-009 | `_etag` for optimistic concurrency                  | `CosmosDbServiceTests.ReplaceWithETagAsync_PassesIfMatchEtag` + `_OnETagMismatch_ThrowsPreconditionFailed`                                  |
| FR-010 | One record per identifier                           | `DocumentSchemaMapperServiceTests.Map_AlwaysPopulatesAll13SchemaKeys`                                                                       |
| FR-012 | TDD discipline                                      | The whole test suite — 77 cases authored before/with implementation                                                                         |
| FR-013 | Structured consolidation logging                    | Manual verification (see PdfProcessorFunction `FR-013 consolidation outcome` log)                                                           |
| FR-014 | SchemaField invariants                              | `SchemaFieldTests` (10 cases)                                                                                                               |
| FR-015 | OCR fields immutable                                | `DocumentReviewServiceTests.ApplyEdits_*` indirectly via state-machine                                                                      |
| FR-016 | Per-field state machine Pending/Confirmed/Corrected | `SchemaFieldTests` + `DocumentReviewServiceTests.ApplyEdits_PendingTransition_Throws`                                                       |
| FR-017 | Record-level Pending → Reviewed                     | `DocumentReviewServiceTests.ApplyEdits_FinalFieldReviewed_TransitionsRecordToReviewed`                                                      |
| FR-018 | First-Reviewer stamp immutable                      | `DocumentReviewServiceTests.ApplyEdits_AlreadyReviewed_DoesNotOverwriteReviewedStamp`                                                       |
| FR-019 | Duplicate identifier skip                           | `CosmosDbServiceTests.GetByIdentifierAsync_*` (covers the lookup that backs the skip)                                                       |
| FR-020 | Page provenance Extracted/Inferred                  | `PageProvenanceEntryTests` + `DocumentAggregatorServiceTests.Aggregate_GapsAreForwardFilled`                                                |
| FR-021 | Pessimistic checkout                                | `DocumentLockServiceTests.TryCheckout_*`                                                                                                    |
| FR-022 | 24h stale-checkout opportunistic release            | `DocumentLockServiceTests.TryCheckout_HeldByOtherStale_Acquires_AndLogs`                                                                    |
| FR-023 | Check-in stamps LastCheckedIn                       | `DocumentLockServiceTests.Checkin_ByHolder_StampsLastCheckedIn_AndClearsCheckout`                                                           |
| FR-024 | Cancel does NOT stamp LastCheckedIn                 | `DocumentLockServiceTests.CancelCheckout_DoesNotUpdateLastCheckedInStamps`                                                                  |
| FR-025 | UI surfaces checkout state                          | `DocumentListFilterTests` + manual Razor verification                                                                                       |

## Test Coverage

### Services Tests

#### BlobStorageServiceTests
- Tests configuration validation
- Validates that service requires proper connection string

### Models Tests

#### QueueMessageTests
- Tests default values initialization
- Validates property setters

#### DocumentResultTests
- Tests default values initialization
- Validates all properties can be set correctly

#### ProcessingResultTests
- Tests default values initialization
- Validates timestamp is set to UTC
- Tests document collection operations

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run tests with detailed output
dotnet test --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~BlobStorageServiceTests"
```

## Test Framework

- **xUnit**: Test framework
- **Moq**: Mocking framework for dependencies

## Adding New Tests

When adding new tests:

1. Create test class in the appropriate subdirectory (`Services/`, `Models/`, etc.)
2. Follow the naming convention: `{ClassName}Tests.cs`
3. Use AAA pattern (Arrange, Act, Assert)
4. Name tests descriptively: `MethodName_Scenario_ExpectedResult`

## Coverage Summary

- **Total Tests**: 9
- **Service Tests**: 2
- **Model Tests**: 7

Critical model validation, default value initialization, and service configuration validation are covered.
