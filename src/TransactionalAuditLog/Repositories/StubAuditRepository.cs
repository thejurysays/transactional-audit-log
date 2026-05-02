using TransactionalAuditLog.Models;

namespace TransactionalAuditLog.Repositories;

public sealed class StubAuditRepository : IAuditRepository
{
    // Dictionary keyed by entry ID gives O(1) lookup for FindByIdAsync and TryAdd, versus O(n) linear scan on a List.
    private readonly Dictionary<Guid, AuditEntry> _entries = [];
    private readonly object _lock = new();

    public Task<AuditEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.TryGetValue(id, out var entry);
            return Task.FromResult(entry);
        }
    }

    public Task SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_lock)
        {
            _entries.TryAdd(entry.Id, entry);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> SearchByActorAsync(string actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        return SearchAsync(e => e.ActorId == actorId);
    }

    public Task<IReadOnlyList<AuditEntry>> SearchByResourceTypeAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        return SearchAsync(e => e.ResourceType == resourceType);
    }

    private Task<IReadOnlyList<AuditEntry>> SearchAsync(Func<AuditEntry, bool> predicate)
    {
        lock (_lock)
        {
            IReadOnlyList<AuditEntry> results = _entries.Values
                .Where(predicate)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
            return Task.FromResult(results);
        }
    }
}
