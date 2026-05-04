# Customizing the schema

This is a **Proof of Concept**. Different customers will configure their
Document Intelligence (DI) model to extract a different set of fields,
so the reviewable schema *must* be easy to swap. This guide is the
end-to-end checklist to do so safely.

> **TL;DR** — the reviewable field catalog lives in **one** file:
> [`src/DocumentOcr.Common/Models/ProcessedDocumentSchema.cs`](../src/DocumentOcr.Common/Models/ProcessedDocumentSchema.cs).
> Everything else (mapper, web review form, tests) reads from it.
> Edit that file, edit the matching tests, and you are done.

---

## 1. Where field names come from

The pipeline has **two** independent name spaces:

| Layer                 | What it is                                                                                                             | Where it's defined                                                                                                                                                          |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DI model fields**   | The exact field keys returned by `DocumentAnalysisClient.AnalyzeDocumentAsync(modelId, ...)`. Driven by your DI model. | The DI Studio model designer (or `prebuilt-document` if you stick with KV-pair extraction). The model id is configured per Function App via `DocumentIntelligence:ModelId`. |
| **Reviewable schema** | The 13 (or N) fields the WebApp shows on the Review page and persists into Cosmos DB `Schema`.                         | [`ProcessedDocumentSchema.FieldNames`](../src/DocumentOcr.Common/Models/ProcessedDocumentSchema.cs).                                                                        |

**Names must match.** [`DocumentSchemaMapperService`](../src/DocumentOcr.Processor/Services/DocumentSchemaMapperService.cs) iterates `ProcessedDocumentSchema.FieldNames` and pulls each by exact name out of the per-page `Fields` dictionary that `DocumentIntelligenceService` produces. If your DI field is `customer_name` but the catalog says `customerName`, the mapper records `null` with status `Pending`.

There is also one **identifier** field used for page aggregation (FR-020 forward-fill): the field name is read from app setting `DocumentProcessing:IdentifierFieldName` (default `identifier`). Today this defaults to `fileTkNumber` for the included sample. The identifier field can — and usually does — also appear in `FieldNames` so it shows up on the Review form.

---

## 2. The single source of truth

[`ProcessedDocumentSchema.cs`](../src/DocumentOcr.Common/Models/ProcessedDocumentSchema.cs) declares four collections that drive **all** downstream behavior:

```csharp
public static IReadOnlyList<string> FieldNames           // catalog order, drives form rendering
public static IReadOnlyDictionary<string, Type> FieldTypes // string | DateOnly | bool
public static IReadOnlySet<string> MultiValueFields      // concatenate across pages instead of "best wins"
public static IReadOnlySet<string> DateFields            // parsed via DateFieldParser → ISO yyyy-MM-dd
```

Per-field merge behavior follows from those tables (see [`DocumentSchemaMapperService.BuildSchema`](../src/DocumentOcr.Processor/Services/DocumentSchemaMapperService.cs)):

| `FieldTypes[name]`                        | Merge rule                                                                                                                                                        |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `bool`                                    | OR-fold across pages; raw text values `"signed"` / `"present"` map to `true` (FR-006). Use this for signature presence flags.                                     |
| `DateOnly`                                | Highest-confidence page wins, then `DateFieldParser.TryParse` → `OcrValue` is the ISO `yyyy-MM-dd` string; raw OCR text is preserved on `SchemaField.OcrRawText`. |
| `string` and listed in `MultiValueFields` | Page-ordered concatenation joined with `\n`; aggregated confidence = min across contributing pages (FR-005).                                                      |
| `string` (default)                        | Highest-confidence page wins (FR-004).                                                                                                                            |

---

## 3. Step-by-step: replacing the schema

Assume your customer's DI model emits these fields:

```
customerName, invoiceNumber, invoiceDate, dueDate, totalAmount,
isPaid, lineItems
```

### 3.1 Train / configure the DI model

1. In **Document Intelligence Studio**, build a custom model that extracts those fields with **the exact names you'll use in C#**. Camel-case is the convention used in the rest of the catalog.
2. Note the **model id** (e.g. `invoice-extractor-v1`).
3. Set it on the Function App:

   ```
   DocumentIntelligence:ModelId = invoice-extractor-v1
   ```

   For local dev, edit `src/DocumentOcr.Processor/local.settings.json`. For Azure, set the app setting via portal / `az` CLI / `azd env set`.

4. Decide which field is the **document identifier** used to group pages into a logical document. Set the app setting:

   ```
   DocumentProcessing:IdentifierFieldName = invoiceNumber
   ```

### 3.2 Edit `ProcessedDocumentSchema.cs`

Replace **all four** collections atomically:

```csharp
public static IReadOnlyList<string> FieldNames { get; } = new[]
{
    "invoiceNumber",   // identifier — keep first by convention
    "customerName",
    "invoiceDate",
    "dueDate",
    "totalAmount",
    "isPaid",
    "lineItems",
};

public static IReadOnlyDictionary<string, Type> FieldTypes { get; } = new Dictionary<string, Type>
{
    ["invoiceNumber"] = typeof(string),
    ["customerName"]  = typeof(string),
    ["invoiceDate"]   = typeof(DateOnly),
    ["dueDate"]       = typeof(DateOnly),
    ["totalAmount"]   = typeof(string),  // store as string; the WebApp does no arithmetic
    ["isPaid"]        = typeof(bool),
    ["lineItems"]     = typeof(string),
};

public static IReadOnlySet<string> MultiValueFields { get; } = new HashSet<string>
{
    "lineItems", // each page contributes one line; we want them concatenated
};

public static IReadOnlySet<string> DateFields { get; } = new HashSet<string>(StringComparer.Ordinal)
{
    "invoiceDate",
    "dueDate",
};
```

### 3.3 Update the schema unit tests

`tests/Models/ProcessedDocumentSchemaTests.cs` pins the catalog to a hard-coded array. **It will fail until you update it** — that is by design (it is the safety net that catches accidental drift). Match the new catalog exactly:

```csharp
[Fact] public void FieldNames_ContainsExactly7Names()           // was 13
[Fact] public void FieldNames_AreInCatalogOrder()               // expected[] = your new names
[Fact] public void FieldTypes_SignatureFieldsAreBool()          // rename to your bool fields, or delete
[Fact] public void FieldTypes_DateFieldsAreDateOnly()           // your date fields
[Fact] public void DateFields_ContainExactlyTheTwoDateColumns() // your DateFields
[Fact] public void MultiValueFields_AreLineItems()              // your MultiValueFields
```

A handful of other tests reference the old names (e.g.
`DocumentSchemaMapperServiceTests`, `DocumentReviewServiceTests`,
`ReviewUiHelpersTests`) — they iterate `ProcessedDocumentSchema.FieldNames`
and so are mostly resilient, but any line that mentions `"fileTkNumber"`,
`"signedOn"`, etc. by literal string must be retargeted.

```bash
grep -RIn 'fileTkNumber\|criminalCodeForm\|policeFileNumber\|agency\|accusedSex\|accusedName\|accusedDateOfBirth\|mainCharge\|signedOn\|judgeSignature\|endorsementSignature\|endorsementSignedOn\|additionalCharges' tests/
```

### 3.4 Build and run the tests

```bash
./scripts/run-tests.sh
```

Iterate until green. The test project will refuse to compile or run if you broke the contract — that is the whole point.

### 3.5 Drop existing Cosmos data (optional, recommended)

The reviewable schema is part of every persisted document. Existing records still carry the **old** `Schema` keys, so the WebApp will show them as `Pending null`. For a POC the simplest fix is to recreate the container:

```bash
az cosmosdb sql container delete \
  --account-name <cosmos-acct> --resource-group <rg> \
  --database-name DocumentOcrDb --name ProcessedDocuments
```

Then re-run `azd provision` (or your manual creation script) to recreate it, and re-process your PDFs.

If you must keep history, write a one-shot migration script that rewrites each document's `schema` map.

---

## 4. What you do **not** need to change

Provided you only swap the four collections in `ProcessedDocumentSchema`:

- ✅ [`DocumentSchemaMapperService`](../src/DocumentOcr.Processor/Services/DocumentSchemaMapperService.cs) — iterates `FieldNames`, branches on `FieldTypes`. No changes.
- ✅ [`DocumentIntelligenceService`](../src/DocumentOcr.Processor/Services/DocumentIntelligenceService.cs) — emits a generic `Fields` dictionary keyed by whatever the DI model returns. No changes.
- ✅ [`Review.razor`](../src/DocumentOcr.WebApp/Components/Pages/Review.razor) — renders rows in `FieldNames` order, switches input control on `IsDateField`. No changes.
- ✅ Cosmos schema (`SchemaField` class, JSON shape) — fields are stored in a `Dictionary<string, SchemaField>` so the catalog is open-ended.
- ✅ `Operation`, `QueueMessage`, `DocumentResult` models — they do not reference field names.

You **do** need to change:

- ⚠️ `ProcessedDocumentSchema.cs` (the catalog).
- ⚠️ `tests/Models/ProcessedDocumentSchemaTests.cs` (pinned expectations).
- ⚠️ `DocumentProcessing:IdentifierFieldName` app setting (if your identifier field changed).
- ⚠️ `DocumentIntelligence:ModelId` app setting (custom model id).
- ⚠️ Any test that uses an old field name as a literal string (`grep` above).

---

## 5. Common customizations

### 5.1 Add a single optional field

1. Add the name to `FieldNames` (in the position you want on the form).
2. Add it to `FieldTypes` with the correct logical type.
3. If it's a date or signature, add to `DateFields` or set the type to `bool`.
4. Update the `FieldNames_ContainsExactly13Names` count assertion.

That's it — no mapper or UI changes.

### 5.2 Make a field multi-value (concatenate across pages)

Add the name to `MultiValueFields`. The mapper will switch from "highest confidence wins" to "concatenate page-ordered with min-confidence." Useful for line-items, addenda, secondary charges, etc.

### 5.3 Change merge logic for one field only

The current merge strategies are intentionally limited to the four cases above. If a customer needs e.g. **sum** for numeric fields, **earliest date wins** for dates, or **majority vote** across pages, you must add a branch to `DocumentSchemaMapperService.BuildSchema`. Keep the dispatch keyed off the catalog (don't sprinkle field names through the mapper) — e.g. introduce `ProcessedDocumentSchema.SumFields`.

### 5.4 Switch off automatic page aggregation

If each page is its own document (no identifier), set:

```
DocumentProcessing:IdentifierFieldName = __none__
```

…and remove the identifier field from your DI model. Pages without an identifier are aggregated as individual single-page documents (see `DocumentAggregatorService`).

---

## 6. Verification checklist

Before declaring the migration done:

1. `./scripts/run-tests.sh` — all green.
2. `./scripts/run-functions.sh` and drop a sample PDF on the queue.
3. Open the WebApp Review page and confirm:
   - All new fields render in your declared order.
   - Date fields show a date picker; signature fields show a yes/no.
   - Confidence badges appear on rows where the DI model returned a confidence.
   - The Page-boundary banner only shows pages where the identifier was inferred.
4. Inspect the Cosmos document in Data Explorer — the `schema` map keys should be **exactly** the new `FieldNames` (no leftover keys, no missing keys).
5. Re-deploy: `azd up` (or `func azure functionapp publish`) — and remember to set both `DocumentIntelligence:ModelId` and `DocumentProcessing:IdentifierFieldName` in the deployed Function App's settings.
