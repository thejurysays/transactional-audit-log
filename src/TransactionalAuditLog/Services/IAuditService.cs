using TransactionalAuditLog.Common;
using TransactionalAuditLog.Models;

namespace TransactionalAuditLog.Services;

public interface IAuditService
{
    Task<Result<AuditEntryResponse>> IngestAsync(IngestEventRequest request, CancellationToken cancellationToken = default);
}
