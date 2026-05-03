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
        "accusedDateOfBirth",
        "mainCharge",
        "signedOn",
        "judgeSignature",
        "endorsementSignature",
        "endorsementSignedOn",
        "additionalCharges",
    };

    /// <summary>
    /// Mapping from field name → expected logical type of <c>OcrValue</c>.
    /// Strings for free-form fields, <see cref="bool"/> for the two
    /// signature flags (FR-006), <see cref="DateOnly"/> for the three date
    /// fields (FR-002a). For date fields the persisted <c>OcrValue</c> is
    /// the ISO <c>yyyy-MM-dd</c> string when parsing succeeds, otherwise
    /// <c>null</c>; the original OCR text is preserved in
    /// <c>OcrRawText</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, Type> FieldTypes { get; } = new Dictionary<string, Type>
    {
        ["fileTkNumber"] = typeof(string),
        ["criminalCodeForm"] = typeof(string),
        ["policeFileNumber"] = typeof(string),
        ["agency"] = typeof(string),
        ["accusedSex"] = typeof(string),
        ["accusedName"] = typeof(string),
        ["accusedDateOfBirth"] = typeof(DateOnly),
        ["mainCharge"] = typeof(string),
        ["signedOn"] = typeof(DateOnly),
        ["judgeSignature"] = typeof(bool),
        ["endorsementSignature"] = typeof(bool),
        ["endorsementSignedOn"] = typeof(DateOnly),
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

    /// <summary>
    /// Fields whose OCR text is parsed into a <see cref="DateOnly"/> value
    /// per FR-002a. Convenience accessor; equivalent to filtering
    /// <see cref="FieldTypes"/> by <c>typeof(DateOnly)</c>.
    /// </summary>
    public static IReadOnlySet<string> DateFields { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "accusedDateOfBirth",
        "signedOn",
        "endorsementSignedOn",
    };

    /// <summary>True when the field is one of the three date fields.</summary>
    public static bool IsDateField(string fieldName) => DateFields.Contains(fieldName);
}
