namespace DocumentOcr.Common.Models;

/// <summary>
/// Provenance flag for a page's contribution to the consolidated document.
/// <c>Extracted</c> = page reported its own <c>fileTkNumber</c>;
/// <c>Inferred</c> = identifier was forward-filled from a previous page (FR-020).
/// </summary>
public enum IdentifierSource
{
    Extracted,
    Inferred
}
