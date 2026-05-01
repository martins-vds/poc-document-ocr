using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DocumentOcr.Common.Models;

/// <summary>
/// One entry per page in a consolidated document indicating whether the
/// page's identifier was extracted by OCR or forward-filled (FR-020).
/// </summary>
public class PageProvenanceEntry
{
    [JsonProperty("pageNumber")]
    public int PageNumber { get; set; }

    [JsonProperty("identifierSource")]
    [JsonConverter(typeof(StringEnumConverter))]
    public IdentifierSource IdentifierSource { get; set; }

    [JsonProperty("extractedIdentifier")]
    public string? ExtractedIdentifier { get; set; }

    public PageProvenanceEntry()
    {
    }

    public static PageProvenanceEntry Extracted(int pageNumber, string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException("Extracted entries require a non-null identifier.", nameof(identifier));
        }

        return new PageProvenanceEntry
        {
            PageNumber = pageNumber,
            IdentifierSource = IdentifierSource.Extracted,
            ExtractedIdentifier = identifier,
        };
    }

    public static PageProvenanceEntry Inferred(int pageNumber)
    {
        return new PageProvenanceEntry
        {
            PageNumber = pageNumber,
            IdentifierSource = IdentifierSource.Inferred,
            ExtractedIdentifier = null,
        };
    }

    /// <summary>
    /// data-model.md invariant: Inferred entries MUST have null
    /// <see cref="ExtractedIdentifier"/>; Extracted entries MUST have a
    /// non-null one.
    /// </summary>
    public void EnsureValid()
    {
        switch (IdentifierSource)
        {
            case IdentifierSource.Extracted when string.IsNullOrEmpty(ExtractedIdentifier):
                throw new InvalidOperationException("Extracted PageProvenanceEntry requires a non-null ExtractedIdentifier.");
            case IdentifierSource.Inferred when ExtractedIdentifier is not null:
                throw new InvalidOperationException("Inferred PageProvenanceEntry must have null ExtractedIdentifier.");
        }
    }
}
