# Testing Guide

The unit-test project is a single xUnit assembly:
[`tests/DocumentOcr.Tests.csproj`](../tests/DocumentOcr.Tests.csproj). It
covers the Common library, the Processor, and the WebApp.

## Quickest path

```bash
./scripts/run-tests.sh           # bash
./scripts/run-tests.ps1          # PowerShell
```

Both wrap `dotnet test` and accept `--filter`, `--coverage`, and
`--no-build`. See [`scripts/README.md`](../scripts/README.md).

## Layout

```
tests/
├── DocumentOcr.Tests.csproj     # xUnit + Moq + bUnit
├── Models/                      # POCO + value-object tests
│   ├── DocumentResultTests.cs
│   ├── OperationTests.cs
│   ├── PageProvenanceEntryTests.cs
│   ├── PageSelectionTests.cs
│   ├── ProcessedDocumentSchemaTests.cs   # schema catalog contract
│   ├── ProcessingResultTests.cs
│   ├── QueueMessageTests.cs
│   └── SchemaFieldTests.cs
├── Services/                    # Service-layer behavior tests
│   ├── BlobStorageServiceTests.cs
│   ├── BlobStorageServiceMockedTests.cs
│   ├── CosmosDbServiceTests.cs
│   ├── DateFieldParserTests.cs
│   ├── DocumentAggregatorServiceTests.cs
│   ├── DocumentIntelligenceServiceTests.cs
│   ├── DocumentIntelligenceServiceAnalyzeTests.cs
│   ├── DocumentListFilterTests.cs
│   ├── DocumentLockServiceTests.cs
│   ├── DocumentReviewServiceTests.cs
│   ├── DocumentSchemaMapperServiceTests.cs
│   ├── OperationServiceTests.cs
│   ├── OperationsApiGetTests.cs
│   ├── OperationsApiStartTests.cs
│   ├── PdfProcessorFunctionTests.cs
│   ├── PdfProcessorPageRangeTests.cs
│   ├── QueueServiceTests.cs
│   └── ReviewUiHelpersTests.cs
└── WebApp/                      # bUnit Razor component tests
    ├── PdfRangePickerTests.cs
    └── UploadPageGatingTests.cs
```

## Frameworks

| Package                  | Version | Purpose                 |
| ------------------------ | ------- | ----------------------- |
| `xunit`                  | 2.9.x   | Test framework          |
| `Microsoft.NET.Test.Sdk` | 18.0.x  | VSTest host             |
| `Moq`                    | 4.20.x  | Mocking                 |
| `bunit`                  | 1.40.x  | Razor component testing |
| `coverlet.collector`     | 6.0.x   | Cross-platform coverage |

Target framework is `net10.0` — must match the rest of the solution.

## Running subsets

```bash
# Run a single class
./scripts/run-tests.sh --filter FullyQualifiedName~DocumentSchemaMapperServiceTests

# Run a namespace
./scripts/run-tests.sh --filter FullyQualifiedName~DocumentOcr.Tests.Models

# Run a single test
./scripts/run-tests.sh --filter "FullyQualifiedName=DocumentOcr.Tests.Services.PdfProcessorPageRangeTests.Run_RestrictsLoopToSelectedPages"

# Coverage
./scripts/run-tests.sh --coverage
# → emits TestResults/<guid>/coverage.cobertura.xml
```

## Writing new tests

Convention: `MethodName_Scenario_ExpectedResult`, AAA structure.

```csharp
[Fact]
public void Map_FieldNotInOcr_RecordsPendingNull()
{
    // Arrange
    var aggregated = AggregatedDocumentBuilder.Empty();
    var sut = new DocumentSchemaMapperService(NullLogger<DocumentSchemaMapperService>.Instance);

    // Act
    var entity = sut.Map(aggregated, 1, "f.pdf", "https://x", "f_doc_1.pdf");

    // Assert
    Assert.All(ProcessedDocumentSchema.FieldNames,
        n => Assert.Equal(SchemaFieldStatus.Pending, entity.Schema[n].FieldStatus));
}
```

`Moq` for service interfaces, `bUnit`'s `TestContext` for Razor components, and `Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance` to satisfy logger constructors.

## Schema-related tests

[`ProcessedDocumentSchemaTests`](../tests/Models/ProcessedDocumentSchemaTests.cs)
hard-codes the expected catalog (13 names, in order). **It must be updated
in lock-step with `ProcessedDocumentSchema.cs`** — see
[CUSTOMIZING-SCHEMA.md § 3.3](CUSTOMIZING-SCHEMA.md#33-update-the-schema-unit-tests).

`DocumentSchemaMapperServiceTests`, `DocumentReviewServiceTests`, and `ReviewUiHelpersTests` reference some field names by literal string (e.g. `"fileTkNumber"`, `"signedOn"`). Grep before changing the catalog:

```bash
grep -RIn 'fileTkNumber\|signedOn\|judgeSignature' tests/
```

## What is NOT tested

- Live calls to Azure Document Intelligence, Cosmos DB, or Blob Storage. The relevant service classes are tested with `Moq` doubles or in-memory streams.
- End-to-end queue-trigger ↔ HTTP flow.
- PDF rendering correctness (PdfSharpCore output is not pixel-diffed).

For an end-to-end smoke test, follow [docs/QUICKSTART.md § 6](QUICKSTART.md#6-smoke-test-the-pipeline).

## Troubleshooting

| Symptom                                     | Fix                                                                                                                                                            |
| ------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `dotnet test` reports "no tests found"      | Run `dotnet build tests/DocumentOcr.Tests.csproj` first, or drop `--no-build`.                                                                                 |
| `bUnit` test fails with "no renderer"       | Make sure the test class derives from `bunit.TestContext`.                                                                                                     |
| Schema test fails after editing field names | Update [`tests/Models/ProcessedDocumentSchemaTests.cs`](../tests/Models/ProcessedDocumentSchemaTests.cs) — see [CUSTOMIZING-SCHEMA.md](CUSTOMIZING-SCHEMA.md). |
| Coverage report missing                     | Use `--coverage` (script wraps `--collect "XPlat Code Coverage"`). The Cobertura XML lands under `tests/TestResults/<guid>/`.                                  |
