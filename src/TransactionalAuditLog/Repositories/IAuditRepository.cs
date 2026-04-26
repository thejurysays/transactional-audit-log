using TransactionalAuditLog.Models;

namespace TransactionalAuditLog.Repositories;

public interface IAuditRepository
{
    Task<AuditEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> SearchByActorAsync(string actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> SearchByResourceTypeAsync(string resourceType, CancellationToken cancellationToken = default);
}
