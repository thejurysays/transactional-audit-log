namespace TransactionalAuditLog.Models;

public sealed record DeadLetterEntry
{
    public required DateTimeOffset FailedAt { get; init; }
    public required string Reason { get; init; }
    public required IngestEventRequest Event { get; init; }
}
