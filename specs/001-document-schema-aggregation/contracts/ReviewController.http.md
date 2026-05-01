# Contract: WebApp Review HTTP API

**Project**: `DocumentOcr.WebApp.Controllers.ReviewController`
**Base route**: `/api/review`
**Auth**: All endpoints require an authenticated Entra ID user (existing `[Authorize]` policy). The reviewer UPN is taken from `HttpContext.User.Identity.Name` via `ICurrentUserService`.

These endpoints back the `Review.razor` page. They are not part of the long-running Operations API; they are short reviewer-session calls and inherit the ≤500 ms p95 budget.

---

## `POST /api/review/{id}/checkout?identifier={partitionKey}`

Attempt to acquire an exclusive checkout.

**200 OK** — checkout acquired (or the caller already holds it):
```json
{
  "document": { /* full DocumentOcrEntity */ },
  "checkedOutBy": "alice@contoso.com",
  "checkedOutAt": "2026-05-01T14:00:00Z"
}
```

**409 Conflict** — held by another reviewer (FR-025):
```json
{
  "error": "AlreadyCheckedOut",
  "checkedOutBy": "bob@contoso.com",
  "checkedOutAt": "2026-05-01T13:42:11Z"
}
```

**404 Not Found** — no document with that id/partition.

---

## `PUT /api/review/{id}?identifier={partitionKey}`

Save per-field edits (no check-in). Caller MUST hold the checkout.

**Request body**:
```json
{
  "edits": {
    "criminalCodeForm": { "newStatus": "Confirmed", "newReviewedValue": null },
    "mainCharge":       { "newStatus": "Corrected", "newReviewedValue": "Theft under $5000 contrary to s.334(b)" }
  }
}
```

**200 OK** — returns the updated entity (with refreshed ETag).
**400 Bad Request** — invariant violation (e.g., attempted to mutate `ocrValue`, invalid state transition).
**403 Forbidden** — caller is not the current holder.
**404 Not Found** — record gone.
**409 Conflict** — concurrent modification (ETag mismatch); UI prompts reload.

---

## `POST /api/review/{id}/checkin?identifier={partitionKey}`

End the calling reviewer's checkout. Stamps `lastCheckedInBy` / `lastCheckedInAt` (FR-023). Does NOT itself save any edits — those go via `PUT` first; if the body is non-empty the controller applies edits then checks in (single atomic perceived action from the UI).

**Request body** (optional, same shape as `PUT`):
```json
{ "edits": { } }
```

**200 OK** — returns the updated entity. If all fields are now non-`Pending`, `reviewStatus` will be `Reviewed` and `reviewedBy` / `reviewedAt` will be set on the first such transition (FR-018).
**400 / 403 / 404 / 409** — same semantics as `PUT`.

---

## `POST /api/review/{id}/cancel-checkout?identifier={partitionKey}`

Discard the calling reviewer's checkout without stamping check-in (FR-024). Per-field edits previously committed via `PUT` are NOT rolled back.

**200 OK** — empty body.
**403 Forbidden** — caller is not the current holder.
**404 Not Found** — record gone.

---

## Error envelope

All non-2xx responses (except 401, which the auth middleware handles) use:
```json
{ "error": "<machineCode>", "message": "<human readable>", "details": { ... optional ... } }
```

## Test cases

Driven from `DocumentLockServiceTests` and `DocumentReviewServiceTests`; controller-level tests are limited to the auth-mapping and HTTP-status-mapping layer (one test per non-200 path) and may be added in a later phase if the controller logic grows beyond mechanical mapping.
