using DocumentOcr.WebApp.Models;

namespace DocumentOcr.WebApp.Services;

/// <summary>
/// Pure submit-gating logic for the Upload page (feature 002, US2).
/// Extracted so it can be unit-tested without spinning up the full Blazor
/// renderer (which would require an authenticated AuthorizationStateProvider
/// and a real <c>InputFile</c> harness).
/// </summary>
public static class UploadGating
{
    /// <summary>
    /// Returns <c>true</c> when at least one file is selected AND every entry
    /// has a non-null <see cref="UploadFileEntry.TotalPages"/> (preview probe
    /// completed) AND a null <see cref="UploadFileEntry.RangeError"/>.
    /// </summary>
    public static bool CanSubmit(IReadOnlyList<UploadFileEntry> entries)
    {
        if (entries.Count == 0) return false;
        foreach (var e in entries)
        {
            if (e.RangeError is not null) return false;
            if (e.TotalPages is null) return false;
        }
        return true;
    }

    /// <summary>Names of files whose range expression is invalid; drives the inline alert.</summary>
    public static IEnumerable<string> BlockedFileNames(IReadOnlyList<UploadFileEntry> entries)
    {
        foreach (var e in entries)
        {
            if (e.RangeError is not null) yield return e.File.Name;
        }
    }
}
