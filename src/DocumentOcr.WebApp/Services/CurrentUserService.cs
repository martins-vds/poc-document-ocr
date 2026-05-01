using System.Security.Claims;
using DocumentOcr.Common.Interfaces;

namespace DocumentOcr.WebApp.Services;

/// <summary>
/// T038 — Resolves the authenticated reviewer's UPN from the current
/// HTTP context. Throws when no principal is present (callers should be
/// behind <c>[Authorize]</c>).
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentUserUpn()
    {
        var user = _httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No HttpContext available.");

        if (user.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("Reviewer is not authenticated.");
        }

        var upn = user.FindFirst("preferred_username")?.Value
                  ?? user.FindFirst(ClaimTypes.Upn)?.Value
                  ?? user.FindFirst(ClaimTypes.Email)?.Value
                  ?? user.FindFirst(ClaimTypes.Name)?.Value;

        return upn ?? throw new InvalidOperationException("Authenticated principal is missing a UPN claim.");
    }
}
