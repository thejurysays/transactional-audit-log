using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TransactionalAuditLog.Common;
using TransactionalAuditLog.Models;
using TransactionalAuditLog.Services;

namespace TransactionalAuditLog.Controllers;

/// <summary>Handles ingestion of audit events.</summary>
[ApiController]
[Route("api/v1/audit/events")]
[EnableRateLimiting(RateLimitPolicies.Fixed)]
public sealed class AuditEventsController(IAuditService auditService) : ControllerBase
{
    /// <summary>Ingests a new audit event.</summary>
    /// <remarks>
    /// Idempotent: if an optional <c>EventId</c> is supplied and already exists, returns 409.
    /// Omitting <c>EventId</c> auto-generates one.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(AuditEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestAsync(
        [FromBody] IngestEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await auditService.IngestAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ResultErrorType.Conflict => Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Event already exists.",
                    Detail = result.Error
                }),
                ResultErrorType.Validation => BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed.",
                    Detail = result.Error
                }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
