using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransactionalAuditLog.Models;
using TransactionalAuditLog.Repositories;

namespace TransactionalAuditLog.Tests;

/// <summary>
/// Swaps <see cref="IAuditRepository"/> for an always-throwing fake and points the
/// <see cref="IDeadLetterStore"/> at a unique temp file so we can verify the dead-letter
/// write end-to-end. ConfigureServices runs after Program.cs (ADR-018).
/// </summary>
public sealed class FailingStoreWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DeadLetterFilePath { get; } = Path.GetTempFileName();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var repo = services.Single(d => d.ServiceType == typeof(IAuditRepository));
            services.Remove(repo);
            services.AddSingleton<IAuditRepository, AlwaysFailingAuditRepository>();

            var dls = services.Single(d => d.ServiceType == typeof(IDeadLetterStore));
            services.Remove(dls);
            services.AddSingleton<IDeadLetterStore>(sp =>
            {
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Storage:DeadLetterFilePath"] = DeadLetterFilePath
                    })
                    .Build();
                return new DeadLetterStore(
                    config, sp.GetRequiredService<ILogger<DeadLetterStore>>());
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(DeadLetterFilePath))
            File.Delete(DeadLetterFilePath);
    }

    private sealed class AlwaysFailingAuditRepository : IAuditRepository
    {
        public Task<AuditEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuditEntry?>(null);

        public Task SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated store failure");

        public Task<IReadOnlyList<AuditEntry>> SearchByActorAsync(string actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>([]);

        public Task<IReadOnlyList<AuditEntry>> SearchByResourceTypeAsync(string resourceType, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>([]);
    }
}

public sealed class AuditResiliencyIntegrationTests(FailingStoreWebApplicationFactory factory)
    : IClassFixture<FailingStoreWebApplicationFactory>
{
    private const string IngestEndpoint = "/api/v1/audit/events";

    [Fact]
    public async Task IngestAsync_WhenStoreFails_Returns503AndWritesDeadLetterFile()
    {
        var client  = factory.CreateClient();
        var eventId = Guid.NewGuid();
        var actorId = $"failing-store-actor-{Guid.NewGuid()}";
        var request = new
        {
            EventId      = eventId,
            ActorId      = actorId,
            ActionType   = "PatientCreated",
            ResourceType = "Patient",
            ResourceId   = "patient-fail-1",
            After        = new { name = "Jane Doe" }
        };

        var response = await client.PostAsJsonAsync(IngestEndpoint, request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var fileContent = await File.ReadAllTextAsync(factory.DeadLetterFilePath);
        Assert.Contains(actorId, fileContent);
        Assert.Contains(eventId.ToString(), fileContent);
    }
}
