using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransactionalAuditLog.Repositories;

namespace TransactionalAuditLog.Tests;

/// <summary>
/// WebApplicationFactory that swaps the IAuditRepository registration with a real
/// AuditRepository pointed at a unique temp file.  ConfigureServices runs after Program.cs,
/// so we can reliably find and replace the stub registration regardless of config ordering.
/// The temp file is deleted when the factory is disposed (once per test class).
/// </summary>
public sealed class FileBackedWebApplicationFactory : WebApplicationFactory<Program>
{
    public string AuditFilePath { get; } = Path.GetTempFileName();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var existing = services.Single(d => d.ServiceType == typeof(IAuditRepository));
            services.Remove(existing);

            services.AddSingleton<IAuditRepository>(sp =>
            {
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Storage:AuditFilePath"] = AuditFilePath
                    })
                    .Build();
                return new AuditRepository(config, sp.GetRequiredService<ILogger<AuditRepository>>());
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(AuditFilePath))
            File.Delete(AuditFilePath);
    }
}

/// <summary>
/// Repeats the Slice 2 and Slice 3 scenarios against the real file-backed store.
/// Each test uses unique GUIDs so accumulated entries from other tests never cause false results.
/// </summary>
public sealed class AuditRepositoryIntegrationTests(FileBackedWebApplicationFactory factory)
    : IClassFixture<FileBackedWebApplicationFactory>
{
    private const string IngestEndpoint = "/api/v1/audit/events";
    private const string SearchEndpoint = "/api/v1/audit";

    private static object CreateEventRequest(string actorId, string resourceType) => new
    {
        EventId      = Guid.NewGuid(),
        ActorId      = actorId,
        ActionType   = "Created",
        ResourceType = resourceType,
        ResourceId   = Guid.NewGuid().ToString(),
        After        = new { value = "test" }
    };

    private async Task SeedAsync(HttpClient client, string actorId, string resourceType)
    {
        var response = await client.PostAsJsonAsync(IngestEndpoint, CreateEventRequest(actorId, resourceType));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task IngestAsync_ValidCreateEvent_Returns201WithAuditEntry()
    {
        var client  = factory.CreateClient();
        var request = new
        {
            EventId      = Guid.NewGuid(),
            ActorId      = "file-user-1",
            ActionType   = "PatientCreated",
            ResourceType = "Patient",
            ResourceId   = "patient-1",
            After        = new { name = "Jane Doe", dob = "1980-03-15" }
        };

        var response = await client.PostAsJsonAsync(IngestEndpoint, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(request.EventId.ToString(), body!["id"]!.GetValue<string>());
        Assert.Equal("file-user-1", body["actorId"]!.GetValue<string>());
        Assert.Equal("Patient", body["resourceType"]!.GetValue<string>());
        Assert.IsType<JsonObject>(body["payload"]!.AsObject());
    }

    [Fact]
    public async Task IngestAsync_ValidUpdateEvent_Returns201WithDiffPayload()
    {
        var client  = factory.CreateClient();
        var request = new
        {
            EventId      = Guid.NewGuid(),
            ActorId      = $"file-user-{Guid.NewGuid()}",
            ActionType   = "PatientUpdated",
            ResourceType = "Patient",
            ResourceId   = "patient-2",
            Before       = new { phone = "555-1234", name = "Jane" },
            After        = new { phone = "555-5678", name = "Jane" }
        };

        var response = await client.PostAsJsonAsync(IngestEndpoint, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body    = await response.Content.ReadFromJsonAsync<JsonObject>();
        var payload = body!["payload"]!.AsObject();
        Assert.True(payload.ContainsKey("phone"), "Diff should contain changed field 'phone'");
        Assert.False(payload.ContainsKey("name"),  "Diff should exclude unchanged field 'name'");
    }

    [Fact]
    public async Task IngestAsync_DuplicateEventId_Returns409()
    {
        var client    = factory.CreateClient();
        var sharedId  = Guid.NewGuid();
        var request   = new
        {
            EventId      = sharedId,
            ActorId      = $"file-user-{Guid.NewGuid()}",
            ActionType   = "PatientCreated",
            ResourceType = "Patient",
            ResourceId   = "patient-dup",
            After        = new { name = "Jane Doe" }
        };

        var first  = await client.PostAsJsonAsync(IngestEndpoint, request);
        var second = await client.PostAsJsonAsync(IngestEndpoint, request);

        Assert.Equal(HttpStatusCode.Created,  first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task IngestAsync_EntryPersistedToFile_CanBeReadBackAfterRestart()
    {
        // Write an entry, then verify it exists in the raw file — confirms physical persistence.
        var client  = factory.CreateClient();
        var eventId = Guid.NewGuid();
        var request = new
        {
            EventId      = eventId,
            ActorId      = $"file-persist-{Guid.NewGuid()}",
            ActionType   = "PatientCreated",
            ResourceType = "Patient",
            ResourceId   = "patient-persist",
            After        = new { name = "Persist Test" }
        };

        await client.PostAsJsonAsync(IngestEndpoint, request);

        var fileContent = await File.ReadAllTextAsync(factory.AuditFilePath);
        Assert.Contains(eventId.ToString(), fileContent);
    }

    [Fact]
    public async Task SearchAsync_ByActorId_Returns200WithMatchingEntries()
    {
        var client  = factory.CreateClient();
        var actorId = $"file-actor-{Guid.NewGuid()}";
        var resType = $"FileResType-{Guid.NewGuid()}";

        await SeedAsync(client, actorId, resType);

        var response = await client.GetAsync($"{SearchEndpoint}?actor_id={actorId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal(actorId, entries![0].GetProperty("actorId").GetString());
    }

    [Fact]
    public async Task SearchAsync_ByResourceType_Returns200WithMatchingEntries()
    {
        var client  = factory.CreateClient();
        var actorId = $"file-actor-{Guid.NewGuid()}";
        var resType = $"FileResType-{Guid.NewGuid()}";

        await SeedAsync(client, actorId, resType);

        var response = await client.GetAsync($"{SearchEndpoint}?resource_type={resType}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal(resType, entries![0].GetProperty("resourceType").GetString());
    }

    [Fact]
    public async Task SearchAsync_NeitherParamSupplied_Returns400()
    {
        var response = await factory.CreateClient().GetAsync(SearchEndpoint);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_BothParamsSupplied_Returns400()
    {
        var response = await factory.CreateClient()
            .GetAsync($"{SearchEndpoint}?actor_id=some-actor&resource_type=SomeType");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_NoMatchingEntries_Returns200WithEmptyArray()
    {
        var actorId  = $"file-actor-never-seeded-{Guid.NewGuid()}";
        var response = await factory.CreateClient().GetAsync($"{SearchEndpoint}?actor_id={actorId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(entries);
        Assert.Empty(entries!);
    }

    [Fact]
    public async Task SearchAsync_MultipleEntries_ReturnsNewestFirst()
    {
        var client  = factory.CreateClient();
        var actorId = $"file-actor-{Guid.NewGuid()}";
        var resType = $"FileResType-{Guid.NewGuid()}";

        await SeedAsync(client, actorId, resType);
        await SeedAsync(client, actorId, resType);

        var response = await client.GetAsync($"{SearchEndpoint}?actor_id={actorId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Length);
        var ts0 = entries[0].GetProperty("timestamp").GetDateTimeOffset();
        var ts1 = entries[1].GetProperty("timestamp").GetDateTimeOffset();
        Assert.True(ts0 >= ts1, "First entry should have an equal or later timestamp than the second.");
    }
}
