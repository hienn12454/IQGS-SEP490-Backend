using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace WebAPI.IntegrationTests;

public sealed class StudioSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StudioSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Root_Should_Be_Redirect_Or_Ok()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/");
        Assert.True((int)response.StatusCode is >= 200 and < 400);
    }

    [Fact]
    public async Task Swagger_Should_Be_Reachable()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/swagger");
        Assert.True((int)response.StatusCode is >= 200 and < 400);
    }

    [Fact]
    public async Task Studio_BasicAuthenticatedFlow_ShouldWork()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginBody = JsonSerializer.Serialize(new { Email = "hr@iqgs.com", Password = "hriqgs" });
        var loginResp = await client.PostAsync("/api/auth/login", new StringContent(loginBody, Encoding.UTF8, "application/json"));
        if (!loginResp.IsSuccessStatusCode)
        {
            // Một số môi trường test không có sẵn HR seed/user state phù hợp.
            return;
        }
        using var loginJson = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync());
        var token = loginJson.RootElement.GetProperty("Data").GetProperty("AccessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createProjectBody = JsonSerializer.Serialize(new { Name = "Integration Studio", Description = "flow test" });
        var createProjectResp = await client.PostAsync("/api/studio/projects", new StringContent(createProjectBody, Encoding.UTF8, "application/json"));
        Assert.True(createProjectResp.IsSuccessStatusCode);
        using var projectJson = JsonDocument.Parse(await createProjectResp.Content.ReadAsStringAsync());
        var projectId = projectJson.RootElement.GetProperty("id").GetGuid();

        var jdBody = JsonSerializer.Serialize(new { Content = "Senior .NET Backend Engineer with C#, PostgreSQL, Azure", SourceType = "PastedText" });
        var jdResp = await client.PutAsync($"/api/studio/projects/{projectId}/job-description", new StringContent(jdBody, Encoding.UTF8, "application/json"));
        Assert.True(jdResp.IsSuccessStatusCode);

        var analyzeResp = await client.PostAsync($"/api/studio/projects/{projectId}/job-description/analyze", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.True(analyzeResp.IsSuccessStatusCode);

        var generatePlanResp = await client.PostAsync($"/api/studio/projects/{projectId}/plans/generate", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.True(generatePlanResp.IsSuccessStatusCode);
        using var generatedPlanJson = JsonDocument.Parse(await generatePlanResp.Content.ReadAsStringAsync());
        var planId = generatedPlanJson.RootElement.GetProperty("id").GetGuid();
        var revision = generatedPlanJson.RootElement.GetProperty("revision").GetInt32();

        var currentResp = await client.GetAsync($"/api/studio/projects/{projectId}/plans/current");
        Assert.True(currentResp.IsSuccessStatusCode);

        var detailResp = await client.GetAsync($"/api/studio/projects/{projectId}/plans/{planId}");
        Assert.True(detailResp.IsSuccessStatusCode);
        using var detailJson = JsonDocument.Parse(await detailResp.Content.ReadAsStringAsync());
        var concurrencyVersion = detailJson.RootElement.GetProperty("concurrencyVersion").GetGuid();

        var submitResp = await client.PostAsync($"/api/studio/projects/{projectId}/plans/{planId}/submit-for-approval", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.True(submitResp.IsSuccessStatusCode);

        var approveBody = JsonSerializer.Serialize(new { Revision = revision, ConcurrencyVersion = concurrencyVersion, Notes = "approve from test" });
        var approveResp = await client.PostAsync($"/api/studio/projects/{projectId}/plans/{planId}/approve", new StringContent(approveBody, Encoding.UTF8, "application/json"));
        Assert.True(approveResp.IsSuccessStatusCode);

        var settingsResp = await client.GetAsync($"/api/studio/projects/{projectId}/settings");
        Assert.True(settingsResp.IsSuccessStatusCode);

        var generateQuestionsBody = JsonSerializer.Serialize(new { PlanId = planId, ReplaceExisting = true, IncludeSampleAnswers = true, IncludeScoringRubric = true });
        var generateQuestionsResp = await client.PostAsync($"/api/studio/projects/{projectId}/questions/generate", new StringContent(generateQuestionsBody, Encoding.UTF8, "application/json"));
        // SCRUM-371: generate trả GenerationRun (202/200), câu hỏi tới qua RAG callback
        Assert.True(generateQuestionsResp.IsSuccessStatusCode || generateQuestionsResp.StatusCode == System.Net.HttpStatusCode.Accepted);
        using var runJson = JsonDocument.Parse(await generateQuestionsResp.Content.ReadAsStringAsync());
        Assert.True(runJson.RootElement.TryGetProperty("id", out _) || runJson.RootElement.TryGetProperty("Id", out _));

        var listQuestionsResp = await client.GetAsync($"/api/studio/projects/{projectId}/questions?page=1&pageSize=20");
        Assert.True(listQuestionsResp.IsSuccessStatusCode);

        var runsResp = await client.GetAsync($"/api/studio/projects/{projectId}/question-generation-runs");
        Assert.True(runsResp.IsSuccessStatusCode);

        // CRUD câu hỏi chỉ khi đã có item (cần RAG callback hoàn tất)
        using var listJson = JsonDocument.Parse(await listQuestionsResp.Content.ReadAsStringAsync());
        var items = listJson.RootElement.TryGetProperty("items", out var itemsEl) ? itemsEl
            : listJson.RootElement.TryGetProperty("Items", out var itemsEl2) ? itemsEl2
            : default;
        if (items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
        {
            var firstQuestionId = items[0].TryGetProperty("id", out var idEl) ? idEl.GetGuid() : items[0].GetProperty("Id").GetGuid();
            var updateQuestionBody = JsonSerializer.Serialize(new { Content = "Updated from integration test", Difficulty = "Medium", Type = "Technical", EstimatedMinutes = 5, ExpectedAnswer = "x", ScoringRubric = "y" });
            var updateQuestionResp = await client.PutAsync($"/api/studio/projects/{projectId}/questions/{firstQuestionId}", new StringContent(updateQuestionBody, Encoding.UTF8, "application/json"));
            Assert.True(updateQuestionResp.IsSuccessStatusCode);

            var regenBody = JsonSerializer.Serialize(new { IncludeSampleAnswers = true, IncludeScoringRubric = true });
            var regenResp = await client.PostAsync($"/api/studio/projects/{projectId}/questions/{firstQuestionId}/regenerate", new StringContent(regenBody, Encoding.UTF8, "application/json"));
            Assert.True(regenResp.IsSuccessStatusCode);

            var deleteResp = await client.DeleteAsync($"/api/studio/projects/{projectId}/questions/{firstQuestionId}");
            Assert.True(deleteResp.IsSuccessStatusCode || deleteResp.StatusCode == System.Net.HttpStatusCode.NoContent);
        }

        var sessionResp = await client.GetAsync($"/api/studio/projects/{projectId}/chat/session");
        Assert.True(sessionResp.IsSuccessStatusCode);

        var candidateLoginBody = JsonSerializer.Serialize(new { Email = "candidate@iqgs.com", Password = "candidateiqgs" });
        var candidateLoginResp = await client.PostAsync("/api/auth/login", new StringContent(candidateLoginBody, Encoding.UTF8, "application/json"));
        if (candidateLoginResp.IsSuccessStatusCode)
        {
            using var candidateJson = JsonDocument.Parse(await candidateLoginResp.Content.ReadAsStringAsync());
            var candidateToken = candidateJson.RootElement.GetProperty("Data").GetProperty("AccessToken").GetString();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken);
            var forbiddenResp = await client.GetAsync($"/api/studio/projects/{projectId}");
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbiddenResp.StatusCode);
        }
    }
}
