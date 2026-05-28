using System.Text.Json;
using TransactionalAuditLog.Models;

namespace TransactionalAuditLog.Repositories;

public sealed class DeadLetterStore : IDeadLetterStore
{
    private const string DeadLetterFilePathKey = "Storage:DeadLetterFilePath";

    private readonly string _filePath;
    private readonly ILogger<DeadLetterStore> _logger;

    // Exclusive lock so concurrent failures cannot interleave appends and corrupt earlier entries.
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public DeadLetterStore(IConfiguration configuration, ILogger<DeadLetterStore> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _filePath = configuration.GetValue<string>(DeadLetterFilePathKey) ?? "dead_letter_events.json";
        _logger = logger;
    }

    public async Task AppendAsync(DeadLetterEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_filePath, line, cancellationToken);
            _logger.LogWarning(
                "Dead-letter entry written to {FilePath}. Reason={Reason}", _filePath, entry.Reason);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
