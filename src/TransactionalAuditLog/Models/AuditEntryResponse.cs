using System.Text.Json;

namespace TransactionalAuditLog.Models;

public sealed record AuditEntryResponse
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string ActorId { get; init; }
    public required string ActionType { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public required JsonElement Payload { get; init; }

    public static AuditEntryResponse From(AuditEntry entry) => new()
    {
        Id = entry.Id,
        Timestamp = entry.Timestamp,
        ActorId = entry.ActorId,
        ActionType = entry.ActionType,
        ResourceType = entry.ResourceType,
        ResourceId = entry.ResourceId,
        Payload = ParsePayload(entry.Payload)
    };

    private static JsonElement ParsePayload(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.Clone();
    }
}
