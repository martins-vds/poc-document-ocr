using DocumentOcr.Common.Models;

namespace DocumentOcr.UnitTests.Models;

/// <summary>
/// T004 — invariants for <see cref="SchemaField"/> from data-model.md
/// § Validation rules (FR-014, FR-016).
/// </summary>
public class SchemaFieldTests
{
    [Fact]
    public void Pending_WithReviewedValue_FailsValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Pending,
            ReviewedValue = "def",
        };

        Assert.Throws<InvalidOperationException>(() => field.EnsureValid());
    }

    [Fact]
    public void Pending_WithReviewedAt_FailsValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Pending,
            ReviewedAt = DateTime.UtcNow,
        };

        Assert.Throws<InvalidOperationException>(() => field.EnsureValid());
    }

    [Fact]
    public void Pending_WithReviewedBy_FailsValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Pending,
            ReviewedBy = "alice@contoso.com",
        };

        Assert.Throws<InvalidOperationException>(() => field.EnsureValid());
    }

    [Fact]
    public void Confirmed_WithReviewedValueDifferentFromOcrValue_FailsValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Confirmed,
            ReviewedValue = "def",
        };

        Assert.Throws<InvalidOperationException>(() => field.EnsureValid());
    }

    [Fact]
    public void Confirmed_WithNullReviewedValue_PassesValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Confirmed,
            ReviewedValue = null,
        };

        var ex = Record.Exception(() => field.EnsureValid());
        Assert.Null(ex);
    }

    [Fact]
    public void Confirmed_WithReviewedValueEqualToOcrValue_PassesValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Confirmed,
            ReviewedValue = "abc",
        };

        var ex = Record.Exception(() => field.EnsureValid());
        Assert.Null(ex);
    }

    [Fact]
    public void Corrected_WithNullReviewedValue_FailsValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Corrected,
            ReviewedValue = null,
        };

        Assert.Throws<InvalidOperationException>(() => field.EnsureValid());
    }

    [Fact]
    public void Corrected_WithReviewedValueEqualToOcrValue_FailsValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Corrected,
            ReviewedValue = "abc",
        };

        Assert.Throws<InvalidOperationException>(() => field.EnsureValid());
    }

    [Fact]
    public void Corrected_WithDifferentReviewedValue_PassesValidation()
    {
        var field = new SchemaField
        {
            OcrValue = "abc",
            FieldStatus = SchemaFieldStatus.Corrected,
            ReviewedValue = "def",
        };

        var ex = Record.Exception(() => field.EnsureValid());
        Assert.Null(ex);
    }

    [Fact]
    public void CreateInitial_ProducesPendingFieldWithOnlyOcrFieldsSet()
    {
        var field = SchemaField.CreateInitial("abc", 0.95);

        Assert.Equal("abc", field.OcrValue);
        Assert.Equal(0.95, field.OcrConfidence);
        Assert.Null(field.ReviewedValue);
        Assert.Null(field.ReviewedAt);
        Assert.Null(field.ReviewedBy);
        Assert.Equal(SchemaFieldStatus.Pending, field.FieldStatus);
    }
}
