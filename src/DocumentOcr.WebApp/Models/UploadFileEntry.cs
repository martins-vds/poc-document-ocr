using DocumentOcr.Common.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace DocumentOcr.WebApp.Models;

/// <summary>
/// Per-file UI aggregate driving the Upload page (feature 002).
/// Replaces the bare <see cref="IBrowserFile"/> list so each file
/// carries its own page-range picker state independently.
/// Per data-model.md §6.
/// </summary>
public sealed class UploadFileEntry
{
    public IBrowserFile File { get; init; } = default!;

    /// <summary>Resolved by pdf.js once the preview loads. Null until then.</summary>
    public int? TotalPages { get; set; }

    /// <summary>The raw text typed by the user. Empty = "all pages".</summary>
    public string RangeExpression { get; set; } = string.Empty;

    /// <summary>Parser error from <see cref="PageSelection.TryParse"/>; null when valid.</summary>
    public string? RangeError { get; set; }

    /// <summary>The most recently parsed selection. Null when <see cref="RangeError"/> is set.</summary>
    public PageSelection? Selection { get; set; }
}
