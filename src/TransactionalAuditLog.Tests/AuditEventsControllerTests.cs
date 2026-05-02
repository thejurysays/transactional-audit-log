using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TransactionalAuditLog.Tests;

public sealed class AuditEventsControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Endpoint = "/api/v1/audit/events";

    [Fact]
    public async Task IngestAsync_ValidCreateEvent_Returns201WithAuditEntry()
    {
        var client = factory.CreateClient();
        var request = new
        {
            EventId = Guid.NewGuid(),
            ActorId = "user-1",
            ActionType = "PatientCreated",
            ResourceType = "Patient",
            ResourceId = "patient-1",
            After = new { name = "Jane Doe", dob = "1980-03-15" }
        };

        var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(request.EventId.ToString(), body!["id"]!.GetValue<string>());
        Assert.Equal("user-1", body["actorId"]!.GetValue<string>());
        Assert.Equal("Patient", body["resourceType"]!.GetValue<string>());
        Assert.IsType<JsonObject>(body["payload"]!.AsObject());
    }

    [Fact]
    public async Task IngestAsync_ValidUpdateEvent_Returns201WithDiffPayload()
    {
        var client = factory.CreateClient();
        var request = new
        {
            EventId = Guid.NewGuid(),
            ActorId = "user-2",
            ActionType = "PatientUpdated",
            ResourceType = "Patient",
            ResourceId = "patient-2",
            Before = new { phone = "555-1234", name = "Jane" },
            After  = new { phone = "555-5678", name = "Jane" }
        };

        var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var payload = body!["payload"]!.AsObject();
        Assert.True(payload.ContainsKey("phone"), "Diff should contain changed field 'phone'");
        Assert.False(payload.ContainsKey("name"), "Diff should exclude unchanged field 'name'");
    }

    [Fact]
    public async Task IngestAsync_ValidDeleteEvent_Returns201WithBeforePayload()
    {
        var client = factory.CreateClient();
        var request = new
        {
            EventId = Guid.NewGuid(),
            ActorId = "user-3",
            ActionType = "PatientDeleted",
            ResourceType = "Patient",
            ResourceId = "patient-3",
            Before = new { name = "Jane Doe", phone = "555-1234" }
        };

        var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var payload = body!["payload"]!.AsObject();
        Assert.Equal("Jane Doe", payload["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task IngestAsync_MissingRequiredField_Returns400()
    {
        var client = factory.CreateClient();
        var request = new
        {
            EventId = Guid.NewGuid(),
            // ActorId intentionally omitted
            ActionType = "PatientCreated",
            ResourceType = "Patient",
            ResourceId = "patient-x",
            After = new { name = "Test" }
        };

        var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestAsync_BothBeforeAndAfterNull_Returns400()
    {
        var client = factory.CreateClient();
        var request = new
        {
            EventId = Guid.NewGuid(),
            ActorId = "user-4",
            ActionType = "PatientCreated",
            ResourceType = "Patient",
            ResourceId = "patient-4"
            // Before and After both omitted
        };

        var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestAsync_DuplicateEventId_Returns409()
    {
        var client = factory.CreateClient();
        var sharedId = Guid.NewGuid();
        var request = new
        {
            EventId = sharedId,
            ActorId = "user-5",
            ActionType = "PatientCreated",
            ResourceType = "Patient",
            ResourceId = "patient-5",
            After = new { name = "Jane Doe" }
        };

        var first  = await client.PostAsJsonAsync(Endpoint, request);
        var second = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.Created,  first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task IngestAsync_NoEventIdSupplied_AutoAssignsId()
    {
        var client = factory.CreateClient();
        var request = new
        {
            ActorId = "user-6",
            ActionType = "PatientCreated",
            ResourceType = "Patient",
            ResourceId = "patient-6",
            After = new { name = "Test" }
        };

        var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var id = body!["id"]!.GetValue<string>();
        Assert.True(Guid.TryParse(id, out _), "Auto-assigned id should be a valid GUID");
    }
}
