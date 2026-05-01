namespace DocumentOcr.Common.Models;

/// <summary>
/// Record-level review status. Derived: <c>Reviewed</c> iff every
/// <see cref="SchemaField.FieldStatus"/> is not <see cref="SchemaFieldStatus.Pending"/>.
/// See data-model.md § Record-level state machine (FR-017).
/// </summary>
public enum ReviewStatus
{
    Pending,
    Reviewed
}
