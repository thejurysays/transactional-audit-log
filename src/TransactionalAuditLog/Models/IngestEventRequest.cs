using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace TransactionalAuditLog.Models;

public sealed class IngestEventRequest
{
    public Guid? EventId { get; init; }

    [Required]
    public string ActorId { get; init; } = string.Empty;

    [Required]
    public string ActionType { get; init; } = string.Empty;

    [Required]
    public string ResourceType { get; init; } = string.Empty;

    [Required]
    public string ResourceId { get; init; } = string.Empty;

    public JsonObject? Before { get; init; }
    public JsonObject? After { get; init; }
}
