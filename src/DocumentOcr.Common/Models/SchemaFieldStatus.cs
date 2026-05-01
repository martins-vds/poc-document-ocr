namespace DocumentOcr.Common.Models;

/// <summary>
/// State machine for a single <see cref="SchemaField"/>:
/// <c>Pending</c> → <c>Confirmed</c> (user accepted OCR) or
/// <c>Pending</c> → <c>Corrected</c> (user provided a different value).
/// Transitions back to <c>Pending</c> are not permitted.
/// See data-model.md § Field-status state machine (FR-016).
/// </summary>
public enum SchemaFieldStatus
{
    Pending,
    Confirmed,
    Corrected
}
