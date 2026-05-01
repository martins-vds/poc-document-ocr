using DocumentOcr.Common.Models;

namespace DocumentOcr.Common.Services;

/// <summary>
/// T045a — pure filter helper used by Documents.razor. Extracted for unit
/// testing per US4 review.
/// </summary>
public static class DocumentListFilter
{
    public enum CheckoutFilter
    {
        All,
        Free,
        CheckedOut,
    }

    public static IEnumerable<DocumentOcrEntity> Filter(
        IEnumerable<DocumentOcrEntity> source,
        ReviewStatus? reviewStatus,
        CheckoutFilter checkoutFilter)
    {
        ArgumentNullException.ThrowIfNull(source);

        var query = source;
        if (reviewStatus is not null)
        {
            query = query.Where(d => d.ReviewStatus == reviewStatus.Value);
        }

        query = checkoutFilter switch
        {
            CheckoutFilter.Free => query.Where(d => string.IsNullOrEmpty(d.CheckedOutBy)),
            CheckoutFilter.CheckedOut => query.Where(d => !string.IsNullOrEmpty(d.CheckedOutBy)),
            _ => query,
        };

        return query;
    }

    /// <summary>
    /// Number of fields in <paramref name="entity"/>'s schema with a status
    /// other than <see cref="SchemaFieldStatus.Pending"/>.
    /// </summary>
    public static int CountFieldsReviewed(DocumentOcrEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.Schema.Values.Count(f => f.FieldStatus != SchemaFieldStatus.Pending);
    }
}
