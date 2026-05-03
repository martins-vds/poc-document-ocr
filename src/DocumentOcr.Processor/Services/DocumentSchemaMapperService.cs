using DocumentOcr.Common.Models;
using DocumentOcr.Common.Services;
using DocumentOcr.Processor.Models;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Processor.Services;

/// <summary>
/// T022 + T030 — Maps an <see cref="AggregatedDocument"/> to the consolidated
/// <see cref="DocumentOcrEntity"/> per data-model.md and
/// contracts/IDocumentSchemaMapperService.md.
///
/// Merge rules:
/// - Single-value fields: highest-confidence page wins (FR-004).
/// - <c>mainCharge</c> / <c>additionalCharges</c>: page-ordered concatenation
///   joined with <c>"\n"</c>, aggregated confidence = min (FR-005).
/// - Signature fields: <c>"signed"</c>/<c>"present"</c> → <c>true</c>; else <c>false</c> (FR-006).
/// - Always emits all 13 schema keys (absent fields kept as Pending null).
/// </summary>
public class DocumentSchemaMapperService : IDocumentSchemaMapperService
{
    private static readonly HashSet<string> SignatureTrueValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "signed",
        "present",
    };

    private readonly ILogger<DocumentSchemaMapperService> _logger;
    private readonly Func<DateTime> _clock;
    private readonly Func<Guid> _idGenerator;

    public DocumentSchemaMapperService(ILogger<DocumentSchemaMapperService> logger)
        : this(logger, () => DateTime.UtcNow, Guid.NewGuid)
    {
    }

    /// <summary>Test-friendly constructor injecting a clock and id-generator.</summary>
    public DocumentSchemaMapperService(
        ILogger<DocumentSchemaMapperService> logger,
        Func<DateTime> clock,
        Func<Guid> idGenerator)
    {
        _logger = logger;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    public DocumentOcrEntity Map(
        AggregatedDocument aggregatedDocument,
        int documentNumber,
        string originalFileName,
        string pdfBlobUrl,
        string outputBlobName)
    {
        if (aggregatedDocument is null)
        {
            throw new ArgumentNullException(nameof(aggregatedDocument));
        }
        if (aggregatedDocument.Pages.Count == 0)
        {
            throw new ArgumentException("AggregatedDocument must contain at least one page.", nameof(aggregatedDocument));
        }

        var pages = aggregatedDocument.Pages.OrderBy(p => p.PageNumber).ToList();
        var firstPage = pages[0].PageNumber;

        var identifier = aggregatedDocument.Identifier;
        if (string.IsNullOrWhiteSpace(identifier))
        {
            identifier = $"unknown-{originalFileName}-{firstPage}";
        }

        var schema = BuildSchema(pages);

        return new DocumentOcrEntity
        {
            Id = _idGenerator().ToString(),
            Identifier = identifier,
            OriginalFileName = originalFileName,
            BlobName = outputBlobName,
            ContainerName = "processed-documents",
            PdfBlobUrl = pdfBlobUrl,
            DocumentNumber = documentNumber,
            PageCount = pages.Count,
            PageNumbers = pages.Select(p => p.PageNumber).ToList(),
            ProcessedAt = _clock(),
            Schema = schema,
            PageProvenance = aggregatedDocument.PageProvenance.ToList(),
            ReviewStatus = ReviewStatus.Pending,
            ReviewedBy = null,
            ReviewedAt = null,
            LastCheckedInBy = null,
            LastCheckedInAt = null,
            CheckedOutBy = null,
            CheckedOutAt = null,
        };
    }

    private Dictionary<string, SchemaField> BuildSchema(List<PageOcrResult> pages)
    {
        var schema = new Dictionary<string, SchemaField>(StringComparer.Ordinal);

        foreach (var fieldName in ProcessedDocumentSchema.FieldNames)
        {
            var contributions = CollectContributions(pages, fieldName);

            if (contributions.Count == 0)
            {
                schema[fieldName] = SchemaField.CreateInitial(null, null);
                continue;
            }

            var expectedType = ProcessedDocumentSchema.FieldTypes[fieldName];
            if (expectedType == typeof(bool))
            {
                schema[fieldName] = MergeSignatureField(contributions);
            }
            else if (expectedType == typeof(DateOnly))
            {
                schema[fieldName] = MergeDateField(contributions);
            }
            else if (ProcessedDocumentSchema.MultiValueFields.Contains(fieldName))
            {
                schema[fieldName] = MergeConcatenatedField(contributions);
            }
            else
            {
                schema[fieldName] = MergeHighestConfidenceField(contributions);
            }
        }

        return schema;
    }

    private static List<FieldContribution> CollectContributions(List<PageOcrResult> pages, string fieldName)
    {
        var list = new List<FieldContribution>();
        foreach (var page in pages)
        {
            if (!page.ExtractedData.TryGetValue("Fields", out var fieldsObj) || fieldsObj is not Dictionary<string, object> fields)
            {
                continue;
            }
            if (!fields.TryGetValue(fieldName, out var fieldObj) || fieldObj is not Dictionary<string, object> fieldDict)
            {
                continue;
            }

            var raw = ExtractRawValue(fieldDict);
            var confidence = ExtractConfidence(fieldDict);
            if (raw is null && confidence is null)
            {
                continue;
            }

            list.Add(new FieldContribution(page.PageNumber, raw, confidence));
        }
        return list;
    }

    private static object? ExtractRawValue(Dictionary<string, object> fieldDict)
    {
        // Order matches DocumentIntelligenceService output keys.
        if (fieldDict.TryGetValue("valueString", out var s)) return s;
        if (fieldDict.TryGetValue("content", out var c)) return c;
        if (fieldDict.TryGetValue("valueDate", out var d)) return d?.ToString();
        if (fieldDict.TryGetValue("valuePhoneNumber", out var p)) return p;
        if (fieldDict.TryGetValue("valueSignature", out var sig)) return sig;
        return null;
    }

    private static double? ExtractConfidence(Dictionary<string, object> fieldDict)
    {
        if (!fieldDict.TryGetValue("confidence", out var c) || c is null)
        {
            return null;
        }
        return Convert.ToDouble(c);
    }

    private SchemaField MergeHighestConfidenceField(List<FieldContribution> contributions)
    {
        var best = contributions
            .OrderByDescending(c => c.Confidence ?? double.MinValue)
            .First();
        return SchemaField.CreateInitial(best.RawValue?.ToString(), best.Confidence);
    }

    /// <summary>
    /// Date field merge (FR-002a): pick the highest-confidence page (same
    /// rule as <see cref="MergeHighestConfidenceField"/>), then attempt to
    /// parse the raw OCR text via <see cref="DateFieldParser"/>. On success
    /// <c>OcrValue</c> is the ISO <c>yyyy-MM-dd</c> string; on failure it
    /// is <c>null</c>. The original raw text is always preserved in
    /// <c>OcrRawText</c> so the reviewer can see what was OCR'd.
    /// </summary>
    private SchemaField MergeDateField(List<FieldContribution> contributions)
    {
        var best = contributions
            .OrderByDescending(c => c.Confidence ?? double.MinValue)
            .First();
        var raw = best.RawValue?.ToString();

        if (DateFieldParser.TryParse(raw, out var parsed))
        {
            return SchemaField.CreateInitial(parsed.ToString("yyyy-MM-dd"), best.Confidence, raw);
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning(
                "Date field on page {Page} could not be parsed (raw='{Raw}'); persisting null OcrValue with raw text fallback.",
                best.PageNumber, raw);
        }

        return SchemaField.CreateInitial(null, best.Confidence, raw);
    }

    private SchemaField MergeConcatenatedField(List<FieldContribution> contributions)
    {
        var ordered = contributions.OrderBy(c => c.PageNumber).ToList();
        var parts = ordered
            .Select(c => c.RawValue?.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (parts.Count == 0)
        {
            return SchemaField.CreateInitial(null, null);
        }

        var concatenated = string.Join("\n", parts);
        var minConfidence = ordered
            .Where(c => !string.IsNullOrWhiteSpace(c.RawValue?.ToString()))
            .Select(c => c.Confidence)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .DefaultIfEmpty(0.0)
            .Min();
        return SchemaField.CreateInitial(concatenated, minConfidence);
    }

    private SchemaField MergeSignatureField(List<FieldContribution> contributions)
    {
        // For booleans a single page's value is the OCR value; aggregated
        // confidence is min across contributors that returned a value.
        var present = contributions
            .Select(c => c.RawValue?.ToString())
            .Any(v => v is not null && SignatureTrueValues.Contains(v));

        var confidences = contributions
            .Where(c => c.Confidence.HasValue)
            .Select(c => c.Confidence!.Value)
            .ToList();

        var aggregated = confidences.Count > 0 ? confidences.Min() : (double?)null;

        // Log if any contributor returned an unrecognized signature value (FR-006 warn path).
        foreach (var contribution in contributions)
        {
            var raw = contribution.RawValue?.ToString();
            if (raw is not null && !SignatureTrueValues.Contains(raw) && !string.Equals(raw, "unsigned", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unrecognized signature value '{Raw}' on page {Page}; mapping to false.",
                    raw, contribution.PageNumber);
            }
        }

        return SchemaField.CreateInitial(present, aggregated);
    }

    private sealed record FieldContribution(int PageNumber, object? RawValue, double? Confidence);
}
