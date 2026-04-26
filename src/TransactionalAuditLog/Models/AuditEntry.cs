namespace TransactionalAuditLog.Models;

public sealed record AuditEntry
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string ActorId { get; init; }
    public required string ActionType { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public required string Payload { get; init; }
}
