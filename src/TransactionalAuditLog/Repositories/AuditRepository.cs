using System.Text.Json;
using TransactionalAuditLog.Models;

namespace TransactionalAuditLog.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private const string AuditFilePathKey = "Storage:AuditFilePath";

    private readonly string _filePath;
    private readonly ILogger<AuditRepository> _logger;

    // Exclusive lock so concurrent requests cannot interleave appends or corrupt a read mid-write.
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // PropertyNameCaseInsensitive required for round-tripping: JSON keys are camelCase, model properties are PascalCase.
        PropertyNameCaseInsensitive = true
    };

    public AuditRepository(IConfiguration configuration, ILogger<AuditRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _filePath = configuration.GetValue<string>(AuditFilePathKey) ?? "audit_store.json";
        _logger = logger;
    }

    public async Task<AuditEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entries = await LoadAllAsync(cancellationToken);
        return entries.FirstOrDefault(e => e.Id == id);
    }

    public async Task SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_filePath, line, cancellationToken);
            _logger.LogInformation("Audit entry {EntryId} written to {FilePath}", entry.Id, _filePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> SearchByActorAsync(string actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        return await SearchAsync(e => e.ActorId == actorId, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> SearchByResourceTypeAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        return await SearchAsync(e => e.ResourceType == resourceType, cancellationToken);
    }

    private async Task<IReadOnlyList<AuditEntry>> SearchAsync(Func<AuditEntry, bool> predicate, CancellationToken cancellationToken)
    {
        var entries = await LoadAllAsync(cancellationToken);
        return entries
            .Where(predicate)
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    private async Task<List<AuditEntry>> LoadAllAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
                return [];
            var lines = await File.ReadAllLinesAsync(_filePath, cancellationToken);
            var entries = new List<AuditEntry>(lines.Length);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var entry = JsonSerializer.Deserialize<AuditEntry>(line, JsonOptions);
                if (entry is not null)
                    entries.Add(entry);
                else
                    _logger.LogWarning("Skipping malformed audit entry in {FilePath}", _filePath);
            }
            return entries;
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
