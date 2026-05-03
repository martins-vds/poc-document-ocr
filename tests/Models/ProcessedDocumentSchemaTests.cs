using DocumentOcr.Common.Models;

namespace DocumentOcr.Tests.Models;

/// <summary>
/// T005 — assert <see cref="ProcessedDocumentSchema.FieldNames"/> matches
/// the data-model.md § Field Catalog exactly (13 names, in catalog order,
/// excludes <c>pageCount</c>).
/// </summary>
public class ProcessedDocumentSchemaTests
{
    [Fact]
    public void FieldNames_ContainsExactly13Names()
    {
        Assert.Equal(13, ProcessedDocumentSchema.FieldNames.Count);
    }

    [Fact]
    public void FieldNames_DoesNotContainPageCount()
    {
        Assert.DoesNotContain("pageCount", ProcessedDocumentSchema.FieldNames);
    }

    [Fact]
    public void FieldNames_AreInCatalogOrder()
    {
        var expected = new[]
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

        Assert.Equal(expected, ProcessedDocumentSchema.FieldNames.ToArray());
    }

    [Fact]
    public void FieldTypes_HasEntryForEveryFieldName()
    {
        foreach (var name in ProcessedDocumentSchema.FieldNames)
        {
            Assert.True(ProcessedDocumentSchema.FieldTypes.ContainsKey(name),
                $"FieldTypes missing entry for '{name}'.");
        }
    }

    [Fact]
    public void FieldTypes_SignatureFieldsAreBool()
    {
        Assert.Equal(typeof(bool), ProcessedDocumentSchema.FieldTypes["judgeSignature"]);
        Assert.Equal(typeof(bool), ProcessedDocumentSchema.FieldTypes["endorsementSignature"]);
    }

    [Fact]
    public void FieldTypes_DateFieldsAreDateOnly()
    {
        Assert.Equal(typeof(DateOnly), ProcessedDocumentSchema.FieldTypes["accusedDateOfBirth"]);
        Assert.Equal(typeof(DateOnly), ProcessedDocumentSchema.FieldTypes["signedOn"]);
        Assert.Equal(typeof(DateOnly), ProcessedDocumentSchema.FieldTypes["endorsementSignedOn"]);
    }

    [Fact]
    public void DateFields_ContainExactlyTheThreeDateColumns()
    {
        Assert.Equal(
            new[] { "accusedDateOfBirth", "endorsementSignedOn", "signedOn" },
            ProcessedDocumentSchema.DateFields.OrderBy(s => s).ToArray());
        Assert.True(ProcessedDocumentSchema.IsDateField("signedOn"));
        Assert.False(ProcessedDocumentSchema.IsDateField("accusedName"));
    }

    [Fact]
    public void MultiValueFields_AreMainChargeAndAdditionalCharges()
    {
        Assert.Contains("mainCharge", ProcessedDocumentSchema.MultiValueFields);
        Assert.Contains("additionalCharges", ProcessedDocumentSchema.MultiValueFields);
        Assert.Equal(2, ProcessedDocumentSchema.MultiValueFields.Count);
    }
}
