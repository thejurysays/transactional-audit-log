using TransactionalAuditLog.Common;
using TransactionalAuditLog.Models;

namespace TransactionalAuditLog.Services;

public interface IAuditService
{
    Task<Result<AuditEntryResponse>> IngestAsync(IngestEventRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AuditEntryResponse>>> SearchAsync(
        string? actorId,
        string? resourceType,
        CancellationToken cancellationToken = default);
}
