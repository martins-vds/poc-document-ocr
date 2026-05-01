# Contract: `IDocumentSchemaMapperService`

**Project**: `DocumentOcr.Common.Interfaces`
**Consumed by**: `DocumentOcr.Processor.Functions.PdfProcessorFunction`

Maps the per-page OCR results for a single logical document into one consolidated `DocumentOcrEntity` (the rewritten Cosmos shape — see [`../data-model.md`](../data-model.md)).

```csharp
public interface IDocumentSchemaMapperService
{
    /// <summary>
    /// Build a consolidated document from page OCR results that already share
    /// (or have been forward-filled to share) the same identifier.
    /// </summary>
    /// <param name="aggregatedDocument">
    /// The aggregator output. Pages are in source-PDF page order. The
    /// <c>Identifier</c> property holds either the OCR-extracted
    /// <c>fileTkNumber</c> or the synthetic <c>unknown-{blob}-{firstPage}</c>
    /// fallback.
    /// </param>
    /// <param name="documentNumber">1-based sequence within the source PDF.</param>
    /// <param name="originalFileName">Source PDF blob name.</param>
    /// <param name="pdfBlobUrl">Absolute URL of the per-document output PDF.</param>
    /// <param name="outputBlobName">Per-document output blob name.</param>
    /// <returns>A new <see cref="DocumentOcrEntity"/> ready to persist.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="aggregatedDocument"/> has zero pages.
    /// </exception>
    DocumentOcrEntity Map(
        AggregatedDocument aggregatedDocument,
        int documentNumber,
        string originalFileName,
        string pdfBlobUrl,
        string outputBlobName);
}
```

## Contract guarantees

1. **All 13 schema fields are always present** in the returned `Schema` dictionary, even when OCR did not return a value for a given field. Absent fields are stored as `SchemaField { OcrValue=null, OcrConfidence=null, FieldStatus=Pending }`.
2. **Concatenation rules** for `mainCharge` and `additionalCharges`: page-ordered concatenation joined with `"\n"`; whitespace-only contributions are skipped; aggregated confidence = `Min(contributingConfidences)`; if no page contributed, the field is absent (rule 1 applies).
3. **Signature mapping**: a DI field of `type=signature` with `valueSignature in {"signed", "present"}` maps to `OcrValue=true`; any other value maps to `OcrValue=false`. Confidence flows through.
4. **Identifier fallback**: if `aggregatedDocument.Identifier` is null/empty, the entity's `Identifier` is set to `"unknown-" + originalFileName + "-" + firstPageNumber`; the `fileTkNumber` schema field is still populated as `Pending` with `OcrValue=null`.
5. **Page provenance**: `PageProvenance` is copied from the aggregator output verbatim and contains one entry per page.
6. **Initial review state**: `ReviewStatus=Pending`, `ReviewedBy=null`, `ReviewedAt=null`, `LastCheckedInBy=null`, `LastCheckedInAt=null`, `CheckedOutBy=null`, `CheckedOutAt=null`.
7. **Pure function**: no Azure calls; no I/O; deterministic given the inputs (except `Id = Guid.NewGuid()` and `ProcessedAt = DateTime.UtcNow` which the test plan injects via thin abstractions where needed for assertion).

## Test cases (failing-first, drive the implementation)

| Test                                                                    | Asserts     |
| ----------------------------------------------------------------------- | ----------- |
| `Map_AlwaysPopulatesAll13SchemaKeys`                                    | guarantee 1 |
| `Map_ConcatenatesMainChargeAcrossPagesInOrder`                          | guarantee 2 |
| `Map_AggregatedConfidenceIsMinimumOfContributors`                       | guarantee 2 |
| `Map_SignaturePresent_MapsToTrue` / `Map_SignatureUnsigned_MapsToFalse` | guarantee 3 |
| `Map_NullIdentifier_UsesSyntheticFallback`                              | guarantee 4 |
| `Map_PreservesPageProvenance`                                           | guarantee 5 |
| `Map_InitialState_AllFieldsPendingRecordPending`                        | guarantee 6 |
