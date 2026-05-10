using TransactionalAuditLog.Common;
using TransactionalAuditLog.Models;
using TransactionalAuditLog.Repositories;

namespace TransactionalAuditLog.Services;

public sealed class AuditService(
    IAuditRepository repository,
    DiffEngine diffEngine,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task<Result<AuditEntryResponse>> IngestAsync(
        IngestEventRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = request.EventId ?? Guid.NewGuid();

        var existing = await repository.FindByIdAsync(id, cancellationToken);
        if (existing is not null)
        {
            logger.LogWarning("Duplicate audit event rejected {EventId}", id);
            return Result<AuditEntryResponse>.Failure(
                $"An event with ID '{id}' already exists.",
                ResultErrorType.Conflict);
        }

        string payload;
        if (request.Before is null && request.After is not null)
            payload = request.After.ToJsonString();
        else if (request.Before is not null && request.After is null)
            payload = request.Before.ToJsonString();
        else if (request.Before is not null && request.After is not null)
            payload = diffEngine.Compute(request.Before, request.After);
        else
            return Result<AuditEntryResponse>.Failure(
                "At least one of 'Before' or 'After' must be provided.",
                ResultErrorType.Validation);

        var entry = new AuditEntry
        {
            Id = id,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = request.ActorId,
            ActionType = request.ActionType,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Payload = payload
        };

        await repository.SaveAsync(entry, cancellationToken);

        logger.LogInformation(
            "Audit event ingested {EventId} by actor {ActorId} for {ResourceType}/{ResourceId}",
            id, request.ActorId, request.ResourceType, request.ResourceId);

        return Result<AuditEntryResponse>.Success(AuditEntryResponse.From(entry));
    }

    public async Task<Result<IReadOnlyList<AuditEntryResponse>>> SearchAsync(
        string? actorId,
        string? resourceType,
        CancellationToken cancellationToken = default)
    {
        var hasActor    = !string.IsNullOrWhiteSpace(actorId);
        var hasResource = !string.IsNullOrWhiteSpace(resourceType);

        if (!hasActor && !hasResource)
            return Result<IReadOnlyList<AuditEntryResponse>>.Failure(
                "Exactly one of 'actor_id' or 'resource_type' must be provided.",
                ResultErrorType.Validation);

        if (hasActor && hasResource)
            return Result<IReadOnlyList<AuditEntryResponse>>.Failure(
                "Provide either 'actor_id' or 'resource_type', not both.",
                ResultErrorType.Validation);

        var (filterName, filterValue) = hasActor
            ? ("actor_id",      actorId!)
            : ("resource_type", resourceType!);

        IReadOnlyList<AuditEntry> entries = hasActor
            ? await repository.SearchByActorAsync(filterValue, cancellationToken)
            : await repository.SearchByResourceTypeAsync(filterValue, cancellationToken);

        logger.LogInformation(
            "Audit search completed. Filter={Filter} Value={Value} Count={Count}",
            filterName, filterValue, entries.Count);

        return Result<IReadOnlyList<AuditEntryResponse>>.Success(
            entries.Select(AuditEntryResponse.From).ToList());
    }
}
