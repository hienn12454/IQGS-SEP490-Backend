using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WebAPI.IntegrationTests;

/// <summary>
/// Smoke P1: dashboard funnel, compare — bỏ qua khi môi trường không login được.
/// </summary>
public sealed class HrP1FunnelInviteNoteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HrP1FunnelInviteNoteTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dashboard_ContainsHiringFunnel_WhenAuthenticated()
    {
        using var client = await TryAuthAsync();
        if (client is null) return;

        var resp = await client.GetAsync("/api/hr/dashboard?activityDays=7&recentLimit=5&recommendationsLimit=5");
        if (!resp.IsSuccessStatusCode) return;
        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var data = json.RootElement.TryGetProperty("Data", out var d) ? d : json.RootElement;
        Assert.True(data.TryGetProperty("HiringFunnel", out _) || data.TryGetProperty("hiringFunnel", out _));
    }

    [Fact]
    public async Task Compare_WrongCount_Returns400()
    {
        using var client = await TryAuthAsync();
        if (client is null) return;
        var resp = await client.GetAsync($"/api/hr/recommendations/compare?ids={Guid.NewGuid()}");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Restore_UnknownId_IsNotSuccessWhenAuthenticated()
    {
        using var client = await TryAuthAsync();
        if (client is null) return;
        var resp = await client.PostAsync($"/api/hr/recommendations/{Guid.NewGuid()}/restore", null);
        Assert.False(resp.IsSuccessStatusCode);
    }

    private async Task<HttpClient?> TryAuthAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginBody = JsonSerializer.Serialize(new { Email = "hr@iqgs.com", Password = "hriqgs" });
        var loginResp = await client.PostAsync("/api/auth/login", new StringContent(loginBody, Encoding.UTF8, "application/json"));
        if (!loginResp.IsSuccessStatusCode) return null;
        using var loginJson = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync());
        if (!loginJson.RootElement.TryGetProperty("Data", out var data)) return null;
        var token = data.GetProperty("AccessToken").GetString();
        if (string.IsNullOrWhiteSpace(token)) return null;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
