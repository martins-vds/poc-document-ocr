using Newtonsoft.Json;

namespace DocumentOcr.Common.Models;

public class DocumentOcrEntity
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("documentNumber")]
    public int DocumentNumber { get; set; }

    [JsonProperty("originalFileName")]
    public string OriginalFileName { get; set; } = string.Empty;

    [JsonProperty("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonProperty("pageCount")]
    public int PageCount { get; set; }

    [JsonProperty("pageNumbers")]
    public List<int> PageNumbers { get; set; } = new();

    [JsonProperty("pdfBlobUrl")]
    public string PdfBlobUrl { get; set; } = string.Empty;

    [JsonProperty("extractedData")]
    public Dictionary<string, object> ExtractedData { get; set; } = new();

    [JsonProperty("processedAt")]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("containerName")]
    public string ContainerName { get; set; } = string.Empty;

    [JsonProperty("blobName")]
    public string BlobName { get; set; } = string.Empty;

    [JsonProperty("reviewStatus")]
    public string ReviewStatus { get; set; } = "Pending";

    [JsonProperty("assignedTo")]
    public string? AssignedTo { get; set; }

    [JsonProperty("reviewedBy")]
    public string? ReviewedBy { get; set; }

    [JsonProperty("reviewedAt")]
    public DateTime? ReviewedAt { get; set; }
}
