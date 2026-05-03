using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DocumentOcr.Common.Models;

/// <summary>
/// Per-field OCR + reviewer state. <c>OcrValue</c> and <c>OcrConfidence</c>
/// are immutable after creation by the mapper (FR-014). Reviewer mutations
/// only touch <see cref="ReviewedValue"/>, <see cref="ReviewedAt"/>,
/// <see cref="ReviewedBy"/>, and <see cref="FieldStatus"/>.
/// See data-model.md § Entity: SchemaField.
/// </summary>
public class SchemaField
{
    [JsonProperty("ocrValue")]
    public object? OcrValue { get; set; }

    /// <summary>
    /// Original OCR text for fields where <see cref="OcrValue"/> is a
    /// parsed/derived value (currently the three date fields per
    /// <c>ProcessedDocumentSchema.DateFields</c>). Always <c>null</c> for
    /// string and bool fields. Immutable after creation by the mapper.
    /// </summary>
    [JsonProperty("ocrRawText", NullValueHandling = NullValueHandling.Ignore)]
    public string? OcrRawText { get; set; }

    [JsonProperty("ocrConfidence")]
    public double? OcrConfidence { get; set; }

    [JsonProperty("reviewedValue")]
    public object? ReviewedValue { get; set; }

    [JsonProperty("reviewedAt")]
    public DateTime? ReviewedAt { get; set; }

    [JsonProperty("reviewedBy")]
    public string? ReviewedBy { get; set; }

    [JsonProperty("fieldStatus")]
    [JsonConverter(typeof(StringEnumConverter))]
    public SchemaFieldStatus FieldStatus { get; set; } = SchemaFieldStatus.Pending;

    /// <summary>Required by Newtonsoft for deserialization.</summary>
    public SchemaField()
    {
    }

    /// <summary>
    /// Construct an initial Pending field as emitted by the mapper.
    /// </summary>
    public static SchemaField CreateInitial(object? ocrValue, double? ocrConfidence, string? ocrRawText = null)
    {
        return new SchemaField
        {
            OcrValue = ocrValue,
            OcrRawText = ocrRawText,
            OcrConfidence = ocrConfidence,
            ReviewedValue = null,
            ReviewedAt = null,
            ReviewedBy = null,
            FieldStatus = SchemaFieldStatus.Pending,
        };
    }

    /// <summary>
    /// Validate the invariants from data-model.md § SchemaField:
    /// - Pending: ReviewedValue/At/By all null.
    /// - Confirmed: ReviewedValue is null OR equals OcrValue.
    /// - Corrected: ReviewedValue is non-null AND not equal to OcrValue.
    /// </summary>
    /// <exception cref="InvalidOperationException">If invariants are violated.</exception>
    public void EnsureValid()
    {
        switch (FieldStatus)
        {
            case SchemaFieldStatus.Pending:
                if (ReviewedValue is not null || ReviewedAt is not null || ReviewedBy is not null)
                {
                    throw new InvalidOperationException(
                        "Pending field MUST have null ReviewedValue, ReviewedAt, and ReviewedBy.");
                }
                break;

            case SchemaFieldStatus.Confirmed:
                if (ReviewedValue is not null && !ValuesEqual(ReviewedValue, OcrValue))
                {
                    throw new InvalidOperationException(
                        "Confirmed field's ReviewedValue must be null or equal to OcrValue.");
                }
                break;

            case SchemaFieldStatus.Corrected:
                if (ReviewedValue is null)
                {
                    throw new InvalidOperationException(
                        "Corrected field requires a non-null ReviewedValue.");
                }
                if (ValuesEqual(ReviewedValue, OcrValue))
                {
                    throw new InvalidOperationException(
                        "Corrected field's ReviewedValue must differ from OcrValue.");
                }
                break;
        }
    }

    internal static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }
}
