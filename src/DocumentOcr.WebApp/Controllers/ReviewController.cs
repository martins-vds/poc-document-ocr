using DocumentOcr.Common.Interfaces;
using DocumentOcr.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

namespace DocumentOcr.WebApp.Controllers;

/// <summary>
/// T039 — REST endpoints used by the Review.razor page for checkout,
/// per-field saves, check-in, and cancel-checkout.
/// </summary>
[ApiController]
[Authorize]
[Route("api/review")]
public class ReviewController : ControllerBase
{
    private readonly IDocumentLockService _locks;
    private readonly IDocumentReviewService _review;
    private readonly ICurrentUserService _user;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(
        IDocumentLockService locks,
        IDocumentReviewService review,
        ICurrentUserService user,
        ILogger<ReviewController> logger)
    {
        _locks = locks;
        _review = review;
        _user = user;
        _logger = logger;
    }

    public sealed record CheckoutRequest(string DocumentId, string PartitionKey);
    public sealed record CheckinRequest(string DocumentId, string PartitionKey);
    public sealed record CancelRequest(string DocumentId, string PartitionKey);
    public sealed record SaveFieldsRequest(string DocumentId, string PartitionKey, Dictionary<string, FieldEdit> Edits);

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest req, CancellationToken ct)
    {
        try
        {
            var upn = _user.GetCurrentUserUpn();
            var result = await _locks.TryCheckoutAsync(req.DocumentId, req.PartitionKey, upn, ct);
            if (!result.Acquired)
            {
                return Conflict(new { message = "Document is checked out.", heldBy = result.HeldBy, heldAt = result.HeldAt });
            }
            return Ok(result.Document);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("save-fields")]
    public async Task<IActionResult> SaveFields([FromBody] SaveFieldsRequest req, CancellationToken ct)
    {
        try
        {
            var upn = _user.GetCurrentUserUpn();
            var entity = await _review.ApplyEditsAsync(req.DocumentId, req.PartitionKey, req.Edits, upn, ct);
            return Ok(entity);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning("ETag conflict saving fields for {DocumentId}", req.DocumentId);
            return Conflict(new { message = "Document changed by another writer; reload and retry." });
        }
    }

    [HttpPost("checkin")]
    public async Task<IActionResult> Checkin([FromBody] CheckinRequest req, CancellationToken ct)
    {
        try
        {
            var upn = _user.GetCurrentUserUpn();
            var entity = await _locks.CheckinAsync(req.DocumentId, req.PartitionKey, upn, ct);
            return Ok(entity);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelRequest req, CancellationToken ct)
    {
        try
        {
            var upn = _user.GetCurrentUserUpn();
            var entity = await _locks.CancelCheckoutAsync(req.DocumentId, req.PartitionKey, upn, ct);
            return Ok(entity);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
