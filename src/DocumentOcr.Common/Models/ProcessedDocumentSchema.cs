namespace DocumentOcr.Common.Models;

/// <summary>
/// Single source of truth for the 13 reviewable schema field names from
/// spec FR-002 (excludes <c>pageCount</c>, which is a record-level integer).
/// Order matches data-model.md § Field Catalog.
/// </summary>
public static class ProcessedDocumentSchema
{
    /// <summary>Camel-case field names in catalog order.</summary>
    public static IReadOnlyList<string> FieldNames { get; } = new[]
    {
        "fileTkNumber",
        "criminalCodeForm",
        "policeFileNumber",
        "agency",
        "accusedSex",
        "accusedName",
        "accusedDatefBirth",
        "mainCharge",
        "signedOn",
        "judgeSignature",
        "endorsementSignature",
        "endorsementSignedOn",
        "additionalCharges",
    };

    /// <summary>
    /// Mapping from field name → expected <c>OcrValue</c> CLR type. Strings
    /// for free-form fields, <see cref="bool"/> for the two signature flags
    /// (FR-006).
    /// </summary>
    public static IReadOnlyDictionary<string, Type> FieldTypes { get; } = new Dictionary<string, Type>
    {
        ["fileTkNumber"] = typeof(string),
        ["criminalCodeForm"] = typeof(string),
        ["policeFileNumber"] = typeof(string),
        ["agency"] = typeof(string),
        ["accusedSex"] = typeof(string),
        ["accusedName"] = typeof(string),
        ["accusedDatefBirth"] = typeof(string),
        ["mainCharge"] = typeof(string),
        ["signedOn"] = typeof(string),
        ["judgeSignature"] = typeof(bool),
        ["endorsementSignature"] = typeof(bool),
        ["endorsementSignedOn"] = typeof(string),
        ["additionalCharges"] = typeof(string),
    };

    /// <summary>
    /// Multi-value fields whose contributions across pages are concatenated
    /// (FR-005). All other fields use highest-confidence-wins (FR-004).
    /// </summary>
    public static IReadOnlySet<string> MultiValueFields { get; } = new HashSet<string>
    {
        "mainCharge",
        "additionalCharges",
    };
}
