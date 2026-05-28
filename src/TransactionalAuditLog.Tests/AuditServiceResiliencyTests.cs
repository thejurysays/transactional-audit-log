using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Registry;
using Polly.Retry;
using TransactionalAuditLog.Common;
using TransactionalAuditLog.Models;
using TransactionalAuditLog.Repositories;
using TransactionalAuditLog.Services;

namespace TransactionalAuditLog.Tests;

public sealed class AuditServiceResiliencyTests
{
    private static ResiliencePipelineProvider<string> BuildPipelineProvider()
    {
        var services = new ServiceCollection();
        services.AddResiliencePipeline(ResiliencePipelines.AuditSave, b =>
            b.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                BackoffType = DelayBackoffType.Constant
            }));
        return services.BuildServiceProvider().GetRequiredService<ResiliencePipelineProvider<string>>();
    }

    private static LogPseudonymizer BuildPseudonymizer() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:PseudonymKey"] = "test-key" })
            .Build());

    private static IngestEventRequest ValidRequest() => new()
    {
        EventId      = Guid.NewGuid(),
        ActorId      = "actor-1",
        ActionType   = "PatientCreated",
        ResourceType = "Patient",
        ResourceId   = "patient-1",
        After        = new System.Text.Json.Nodes.JsonObject { ["name"] = "Jane" }
    };

    private static AuditService BuildService(IAuditRepository repo, IDeadLetterStore dls) =>
        new(repo,
            new DiffEngine(),
            BuildPseudonymizer(),
            dls,
            BuildPipelineProvider(),
            NullLogger<AuditService>.Instance);

    [Fact]
    public async Task IngestAsync_StoreFailsThenSucceeds_RetrySucceeds_NoDeadLetter()
    {
        var repo = new ThrowingAuditRepository(throwTimes: 1);
        var dls  = new SpyDeadLetterStore();
        var service = BuildService(repo, dls);

        var result = await service.IngestAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, repo.SaveAttempts);
        Assert.Empty(dls.Entries);
    }

    [Fact]
    public async Task IngestAsync_StoreAlwaysFails_RoutesToDeadLetter_ReturnsServiceUnavailable()
    {
        var repo = new ThrowingAuditRepository(throwTimes: int.MaxValue);
        var dls  = new SpyDeadLetterStore();
        var service = BuildService(repo, dls);

        var request = ValidRequest();
        var result  = await service.IngestAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultErrorType.ServiceUnavailable, result.ErrorType);
        Assert.Single(dls.Entries);
        Assert.Equal(request.EventId, dls.Entries[0].Event.EventId);
        Assert.Equal("InvalidOperationException: simulated store failure", dls.Entries[0].Reason);
    }

    private sealed class ThrowingAuditRepository : IAuditRepository
    {
        private readonly int _throwTimes;
        public int SaveAttempts { get; private set; }

        public ThrowingAuditRepository(int throwTimes) => _throwTimes = throwTimes;

        public Task<AuditEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuditEntry?>(null);

        public Task SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (SaveAttempts <= _throwTimes)
                throw new InvalidOperationException("simulated store failure");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEntry>> SearchByActorAsync(string actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>([]);

        public Task<IReadOnlyList<AuditEntry>> SearchByResourceTypeAsync(string resourceType, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>([]);
    }

    private sealed class SpyDeadLetterStore : IDeadLetterStore
    {
        public List<DeadLetterEntry> Entries { get; } = [];

        public Task AppendAsync(DeadLetterEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
