using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AccessRequestHub.DTOs;
using AccessRequestHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AccessRequestHub.Tests;

public class IntegrationTests : IClassFixture<AccessRequestHubFactory>
{
    private readonly AccessRequestHubFactory _factory;
    
    public IntegrationTests(AccessRequestHubFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(string email)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-User", email);
        return client;
    }

    private async Task<Guid> GetAppId(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessRequestHub.Data.AppDbContext>();
        var app = await db.Applications.FirstOrDefaultAsync(a => a.Name == name);
        return app!.Id;
    }

    [Fact]
    public async Task Test1_StandardFlow()
    {
        var aliceClient = CreateClient("alice@example.com");
        var crmId = await GetAppId("CRM");

        var reqDto = new CreateRequestDto(Guid.NewGuid(), crmId, EnvironmentType.NonProduction, AccessLevel.Read, "Test1");
        var createResponse = await aliceClient.PostAsJsonAsync("/api/requests", reqDto);
        createResponse.EnsureSuccessStatusCode();
        var requestData = await createResponse.Content.ReadFromJsonAsync<AccessRequestResponseDto>();
        Assert.NotNull(requestData);
        Assert.Equal("PendingManager", requestData.Status);

        var bobClient = CreateClient("bob@example.com");
        var approveDto = new ApproveRequestDto(requestData.Version);
        var approveResp = await bobClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/approve", approveDto);
        approveResp.EnsureSuccessStatusCode();
        var updated = await approveResp.Content.ReadFromJsonAsync<AccessRequestDetailDto>();
        Assert.NotNull(updated);
        Assert.Equal("Approved", updated.Status);
    }

    [Fact]
    public async Task Test2_HighRiskFlow()
    {
        var aliceClient = CreateClient("alice@example.com");
        var crmId = await GetAppId("CRM");

        var reqDto = new CreateRequestDto(Guid.NewGuid(), crmId, EnvironmentType.Production, AccessLevel.Read, "Test2");
        var createResp = await aliceClient.PostAsJsonAsync("/api/requests", reqDto);
        var requestData = await createResp.Content.ReadFromJsonAsync<AccessRequestResponseDto>();

        var bobClient = CreateClient("bob@example.com");
        var bobApproveResp = await bobClient.PostAsJsonAsync($"/api/requests/{requestData!.Id}/approve", new ApproveRequestDto(requestData.Version));
        bobApproveResp.EnsureSuccessStatusCode();
        var bobUpdated = await bobApproveResp.Content.ReadFromJsonAsync<AccessRequestDetailDto>();
        Assert.Equal("PendingSystemOwner", bobUpdated!.Status);

        var carolClient = CreateClient("carol@example.com");
        var carolApproveResp = await carolClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/approve", new ApproveRequestDto(bobUpdated.Version));
        carolApproveResp.EnsureSuccessStatusCode();
        var carolUpdated = await carolApproveResp.Content.ReadFromJsonAsync<AccessRequestDetailDto>();
        Assert.Equal("Approved", carolUpdated!.Status);
    }

    [Fact]
    public async Task Test3_AuthorizationFailure()
    {
        var aliceClient = CreateClient("alice@example.com");
        var crmId = await GetAppId("CRM");
        var reqDto = new CreateRequestDto(Guid.NewGuid(), crmId, EnvironmentType.NonProduction, AccessLevel.Read, "Test3");
        var createResp = await aliceClient.PostAsJsonAsync("/api/requests", reqDto);
        var requestData = await createResp.Content.ReadFromJsonAsync<AccessRequestResponseDto>();

        var danaClient = CreateClient("dana@example.com"); 
        var approveResp1 = await danaClient.PostAsJsonAsync($"/api/requests/{requestData!.Id}/approve", new ApproveRequestDto(requestData.Version));
        Assert.Equal(HttpStatusCode.Forbidden, approveResp1.StatusCode);

        var approveResp3 = await aliceClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/approve", new ApproveRequestDto(requestData.Version));
        Assert.Equal(HttpStatusCode.Forbidden, approveResp3.StatusCode);

        var erinClient = CreateClient("erin@example.com");
        var approveResp4 = await erinClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/approve", new ApproveRequestDto(requestData.Version));
        Assert.Equal(HttpStatusCode.Forbidden, approveResp4.StatusCode);
    }

    [Fact]
    public async Task Test4_Idempotency()
    {
        var aliceClient = CreateClient("alice@example.com");
        var crmId = await GetAppId("CRM");
        var clientRequestId = Guid.NewGuid();
        var reqDto = new CreateRequestDto(clientRequestId, crmId, EnvironmentType.NonProduction, AccessLevel.Read, "Idempotency");
        
        var resp1Task = aliceClient.PostAsJsonAsync("/api/requests", reqDto);
        var resp2Task = aliceClient.PostAsJsonAsync("/api/requests", reqDto);

        var results = await Task.WhenAll(resp1Task, resp2Task);
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessRequestHub.Data.AppDbContext>();
        var alice = await db.Users.FirstOrDefaultAsync(u => u.Email == "alice@example.com");
        var count = await db.AccessRequests.CountAsync(r => r.RequesterId == alice!.Id && r.ClientRequestId == clientRequestId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Test5_Concurrency()
    {
        var aliceClient = CreateClient("alice@example.com");
        var crmId = await GetAppId("CRM");
        var reqDto = new CreateRequestDto(Guid.NewGuid(), crmId, EnvironmentType.NonProduction, AccessLevel.Read, "Concurrency");
        var createResp = await aliceClient.PostAsJsonAsync("/api/requests", reqDto);
        var requestData = await createResp.Content.ReadFromJsonAsync<AccessRequestResponseDto>();

        var bobClient = CreateClient("bob@example.com");
        var approveDto = new ApproveRequestDto(requestData!.Version);
        var rejectDto = new RejectRequestDto(requestData.Version, "reject it");

        var approveTask = bobClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/approve", approveDto);
        var rejectTask = bobClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/reject", rejectDto);

        var results = await Task.WhenAll(approveTask, rejectTask);
        
        var statuses = results.Select(r => r.StatusCode).ToList();
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);
    }

    [Fact]
    public async Task Test6_RejectReason()
    {
        var aliceClient = CreateClient("alice@example.com");
        var crmId = await GetAppId("CRM");
        var reqDto = new CreateRequestDto(Guid.NewGuid(), crmId, EnvironmentType.NonProduction, AccessLevel.Read, "Reject");
        var createResp = await aliceClient.PostAsJsonAsync("/api/requests", reqDto);
        var requestData = await createResp.Content.ReadFromJsonAsync<AccessRequestResponseDto>();

        var bobClient = CreateClient("bob@example.com");
        var rejectDtoNoReason = new RejectRequestDto(requestData!.Version, "   ");
        var respBad = await bobClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/reject", rejectDtoNoReason);
        Assert.Equal(HttpStatusCode.BadRequest, respBad.StatusCode);

        var rejectDtoReason = new RejectRequestDto(requestData.Version, "Valid reason");
        var respOk = await bobClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/reject", rejectDtoReason);
        respOk.EnsureSuccessStatusCode();

        var updated = await respOk.Content.ReadFromJsonAsync<AccessRequestDetailDto>();
        Assert.Equal("Rejected", updated!.Status);
        
        var audit = updated.AuditTimeline.Last();
        Assert.Equal("Valid reason", audit.Reason);
        Assert.Equal("Rejected", audit.ToStatus);
    }

    [Fact]
    public async Task Test7_TerminalState()
    {
        var aliceClient = CreateClient("alice@example.com");
        var crmId = await GetAppId("CRM");
        var reqDto = new CreateRequestDto(Guid.NewGuid(), crmId, EnvironmentType.NonProduction, AccessLevel.Read, "Terminal");
        var createResp = await aliceClient.PostAsJsonAsync("/api/requests", reqDto);
        var requestData = await createResp.Content.ReadFromJsonAsync<AccessRequestResponseDto>();

        var bobClient = CreateClient("bob@example.com");
        await bobClient.PostAsJsonAsync($"/api/requests/{requestData!.Id}/approve", new ApproveRequestDto(requestData.Version));

        var respFail = await bobClient.PostAsJsonAsync($"/api/requests/{requestData.Id}/approve", new ApproveRequestDto(requestData.Version + 1));
        Assert.Equal(HttpStatusCode.Conflict, respFail.StatusCode);
    }

    [Fact]
    public async Task Test8_RequesterTanpaManager()
    {
        var danaClient = CreateClient("dana@example.com");
        var crmId = await GetAppId("CRM");
        var reqDto = new CreateRequestDto(Guid.NewGuid(), crmId, EnvironmentType.NonProduction, AccessLevel.Read, "Test");
        var createResp = await danaClient.PostAsJsonAsync("/api/requests", reqDto);
        
        Assert.Equal(HttpStatusCode.BadRequest, createResp.StatusCode);
    }

    [Fact]
    public async Task Test9_AuditAppendOnly()
    {
        var aliceClient = CreateClient("alice@example.com");
        var crmId = await GetAppId("CRM");
        var reqDto = new CreateRequestDto(Guid.NewGuid(), crmId, EnvironmentType.NonProduction, AccessLevel.Read, "Audit");
        await aliceClient.PostAsJsonAsync("/api/requests", reqDto);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessRequestHub.Data.AppDbContext>();
        
        var firstAudit = await db.AuditEvents.FirstAsync();
        
        firstAudit.Reason = "Modified hacked";
        
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Test10_ConstraintDB()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessRequestHub.Data.AppDbContext>();
        
        var crm = await db.Applications.FirstAsync(a => a.Name == "CRM");
        var alice = await db.Users.FirstAsync(u => u.Email == "alice@example.com");
        var policy = await db.PolicyVersions.FirstAsync();

        var request = new AccessRequest
        {
            Id = Guid.NewGuid(),
            ClientRequestId = Guid.NewGuid(),
            RequesterId = alice.Id,
            ApplicationId = crm.Id,
            Environment = EnvironmentType.Production,
            AccessLevel = AccessLevel.Read,
            Justification = "Direct insert",
            IsHighRisk = false, 
            Status = RequestStatus.PendingManager,
            PolicyVersionCode = policy.Code,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.AccessRequests.Add(request);
        
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
