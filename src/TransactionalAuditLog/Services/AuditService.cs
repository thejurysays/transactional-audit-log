using Polly;
using Polly.Registry;
using TransactionalAuditLog.Common;
using TransactionalAuditLog.Models;
using TransactionalAuditLog.Repositories;

namespace TransactionalAuditLog.Services;

public sealed class AuditService : IAuditService
{
    private readonly IAuditRepository _repository;
    private readonly DiffEngine _diffEngine;
    private readonly LogPseudonymizer _pseudonymizer;
    private readonly IDeadLetterStore _deadLetterStore;
    private readonly ILogger<AuditService> _logger;
    private readonly ResiliencePipeline _savePipeline;

    public AuditService(
        IAuditRepository repository,
        DiffEngine diffEngine,
        LogPseudonymizer pseudonymizer,
        IDeadLetterStore deadLetterStore,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<AuditService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(diffEngine);
        ArgumentNullException.ThrowIfNull(pseudonymizer);
        ArgumentNullException.ThrowIfNull(deadLetterStore);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _diffEngine = diffEngine;
        _pseudonymizer = pseudonymizer;
        _deadLetterStore = deadLetterStore;
        _logger = logger;
        _savePipeline = pipelineProvider.GetPipeline(ResiliencePipelines.AuditSave);
    }

    public async Task<Result<AuditEntryResponse>> IngestAsync(
        IngestEventRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = request.EventId ?? Guid.NewGuid();

        var existing = await _repository.FindByIdAsync(id, cancellationToken);
        if (existing is not null)
        {
            _logger.LogWarning("Duplicate audit event rejected {EventId}", id);
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
            payload = _diffEngine.Compute(request.Before, request.After);
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

        try
        {
            await _savePipeline.ExecuteAsync(
                async ct => await _repository.SaveAsync(entry, ct), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Caller cancelled — not a store failure; preserve cancellation semantics.
            throw;
        }
        catch (Exception ex)
        {
            // Dead-letter capture is the durability backstop — must complete even if the
            // caller's token is cancelled between the store failure and this write.
            await _deadLetterStore.AppendAsync(
                new DeadLetterEntry
                {
                    FailedAt = DateTimeOffset.UtcNow,
                    Reason = $"{ex.GetType().Name}: {ex.Message}",
                    Event = request
                },
                CancellationToken.None);

            _logger.LogError(ex,
                "Audit event {EventId} routed to dead letter after retries exhausted", id);

            return Result<AuditEntryResponse>.Failure(
                "The audit store is temporarily unavailable; the event was captured for retry.",
                ResultErrorType.ServiceUnavailable);
        }

        _logger.LogInformation(
            "Audit event ingested {EventId} by actor {ActorIdHash} for {ResourceType}/{ResourceIdHash}",
            id,
            _pseudonymizer.Pseudonymize(request.ActorId),
            request.ResourceType,
            _pseudonymizer.Pseudonymize(request.ResourceId));

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
            ? await _repository.SearchByActorAsync(filterValue, cancellationToken)
            : await _repository.SearchByResourceTypeAsync(filterValue, cancellationToken);

        var logValue = hasActor ? _pseudonymizer.Pseudonymize(filterValue) : filterValue;
        _logger.LogInformation(
            "Audit search completed. Filter={Filter} Value={Value} Count={Count}",
            filterName, logValue, entries.Count);

        return Result<IReadOnlyList<AuditEntryResponse>>.Success(
            entries.Select(AuditEntryResponse.From).ToList());
    }
}
