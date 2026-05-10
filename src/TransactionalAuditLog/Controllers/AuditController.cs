using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TransactionalAuditLog.Common;
using TransactionalAuditLog.Models;
using TransactionalAuditLog.Services;

namespace TransactionalAuditLog.Controllers;

/// <summary>Handles retrieval of audit events.</summary>
[ApiController]
[Route("api/v1/audit")]
[EnableRateLimiting(RateLimitPolicies.Fixed)]
public sealed class AuditController(IAuditService auditService) : ControllerBase
{
    /// <summary>Searches audit events by actor or resource type.</summary>
    /// <remarks>Results are ordered most-recent-first.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(AuditEntryResponse[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery(Name = "actor_id")] string? actorId,
        [FromQuery(Name = "resource_type")] string? resourceType,
        CancellationToken cancellationToken)
    {
        var result = await auditService.SearchAsync(actorId, resourceType, cancellationToken);

        // SearchAsync only ever fails with Validation — no Conflict or NotFound cases exist on this path.
        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed.",
                Detail = result.Error
            });

        return Ok(result.Value);
    }
}
