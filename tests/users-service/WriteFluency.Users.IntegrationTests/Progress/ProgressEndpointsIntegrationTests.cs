using System.Net;
using System.Net.Http.Json;
using Shouldly;
using WriteFluency.Users.IntegrationTests.Infrastructure;

namespace WriteFluency.Users.IntegrationTests.Progress;

public class ProgressEndpointsIntegrationTests : IClassFixture<UsersApiIntegrationFixture>
{
    private readonly UsersApiIntegrationFixture _fixture;

    public ProgressEndpointsIntegrationTests(UsersApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProgressEndpoints_OnUsersApi_ShouldReturnNotFound_ForGetEndpoints()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var summary = await client.GetAsync("/users/progress/summary");
        summary.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var items = await client.GetAsync("/users/progress/items");
        items.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var attempts = await client.GetAsync("/users/progress/attempts");
        attempts.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProgressEndpoints_OnUsersApi_ShouldReturnNotFound_ForPostEndpoints()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        using var startRequest = new HttpRequestMessage(HttpMethod.Post, "/users/progress/start")
        {
            Content = JsonContent.Create(new
            {
                ExerciseId = 100,
                ExerciseTitle = "Exercise 100"
            })
        };
        startRequest.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");

        var start = await client.SendAsync(startRequest);
        start.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var completeRequest = new HttpRequestMessage(HttpMethod.Post, "/users/progress/complete")
        {
            Content = JsonContent.Create(new
            {
                ExerciseId = 100,
                AccuracyPercentage = 0.9
            })
        };
        completeRequest.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");

        var complete = await client.SendAsync(completeRequest);
        complete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private bool CanRunIntegration()
    {
        return _fixture.IsAvailable;
    }
}
