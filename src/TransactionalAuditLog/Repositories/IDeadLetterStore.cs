using TransactionalAuditLog.Models;

namespace TransactionalAuditLog.Repositories;

public interface IDeadLetterStore
{
    Task AppendAsync(DeadLetterEntry entry, CancellationToken cancellationToken = default);
}
