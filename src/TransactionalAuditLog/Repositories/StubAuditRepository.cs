using TransactionalAuditLog.Models;

namespace TransactionalAuditLog.Repositories;

public sealed class StubAuditRepository : IAuditRepository
{
    private readonly List<AuditEntry> _entries = [];
    private readonly object _lock = new();

    public Task<AuditEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_entries.FirstOrDefault(e => e.Id == id));
        }
    }

    public Task SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_lock)
        {
            if (_entries.Any(e => e.Id == entry.Id))
                return Task.CompletedTask;

            _entries.Add(entry);
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
            IReadOnlyList<AuditEntry> results = _entries
                .Where(predicate)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
            return Task.FromResult(results);
        }
    }
}
