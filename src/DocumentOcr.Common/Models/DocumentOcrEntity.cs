using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DocumentOcr.Common.Models;

/// <summary>
/// Persisted Cosmos shape for a consolidated document. Class name preserved
/// for source-control continuity but the serialized shape is the rewritten
/// <c>ProcessedDocument</c> defined in data-model.md.
/// </summary>
public class DocumentOcrEntity
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonProperty("originalFileName")]
    public string OriginalFileName { get; set; } = string.Empty;

    [JsonProperty("blobName")]
    public string BlobName { get; set; } = string.Empty;

    [JsonProperty("containerName")]
    public string ContainerName { get; set; } = string.Empty;

    [JsonProperty("pdfBlobUrl")]
    public string PdfBlobUrl { get; set; } = string.Empty;

    [JsonProperty("documentNumber")]
    public int DocumentNumber { get; set; }

    [JsonProperty("pageCount")]
    public int PageCount { get; set; }

    [JsonProperty("pageNumbers")]
    public List<int> PageNumbers { get; set; } = new();

    [JsonProperty("processedAt")]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// All 13 reviewable schema fields keyed by camelCase name. Always
    /// contains every key in <see cref="ProcessedDocumentSchema.FieldNames"/>.
    /// </summary>
    [JsonProperty("schema")]
    public Dictionary<string, SchemaField> Schema { get; set; } = new();

    [JsonProperty("pageProvenance")]
    public List<PageProvenanceEntry> PageProvenance { get; set; } = new();

    [JsonProperty("reviewStatus")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;

    /// <summary>
    /// UPN of the reviewer who first transitioned the record to Reviewed.
    /// Immutable thereafter (FR-018).
    /// </summary>
    [JsonProperty("reviewedBy")]
    public string? ReviewedBy { get; set; }

    [JsonProperty("reviewedAt")]
    public DateTime? ReviewedAt { get; set; }

    [JsonProperty("lastCheckedInBy")]
    public string? LastCheckedInBy { get; set; }

    [JsonProperty("lastCheckedInAt")]
    public DateTime? LastCheckedInAt { get; set; }

    [JsonProperty("checkedOutBy")]
    public string? CheckedOutBy { get; set; }

    [JsonProperty("checkedOutAt")]
    public DateTime? CheckedOutAt { get; set; }

    /// <summary>Cosmos-managed ETag for optimistic concurrency.</summary>
    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}
