using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TransactionalAuditLog.Tests;

public sealed class AuditControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string IngestEndpoint = "/api/v1/audit/events";
    private const string SearchEndpoint = "/api/v1/audit";

    private static object BuildRequest(string actorId, string resourceType) => new
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
        var response = await client.PostAsJsonAsync(IngestEndpoint, BuildRequest(actorId, resourceType));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_ByActorId_Returns200WithMatchingEntries()
    {
        var client  = factory.CreateClient();
        var actorId = $"actor-{Guid.NewGuid()}";
        var resType = $"ResType-{Guid.NewGuid()}";

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
        var actorId = $"actor-{Guid.NewGuid()}";
        var resType = $"ResType-{Guid.NewGuid()}";

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
        var client = factory.CreateClient();

        var response = await client.GetAsync(SearchEndpoint);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_BothParamsSupplied_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"{SearchEndpoint}?actor_id=some-actor&resource_type=SomeType");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_NoMatchingEntries_Returns200WithEmptyArray()
    {
        var client  = factory.CreateClient();
        var actorId = $"actor-never-seeded-{Guid.NewGuid()}";

        var response = await client.GetAsync($"{SearchEndpoint}?actor_id={actorId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(entries);
        Assert.Empty(entries!);
    }

    [Fact]
    public async Task SearchAsync_MultipleEntries_ReturnsNewestFirst()
    {
        var client  = factory.CreateClient();
        var actorId = $"actor-{Guid.NewGuid()}";
        var resType = $"ResType-{Guid.NewGuid()}";

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
