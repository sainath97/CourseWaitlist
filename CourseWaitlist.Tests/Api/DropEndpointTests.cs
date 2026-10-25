using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace CourseWaitlist.Tests.Api;

public class DropEndpointTests
{
    private const string EnrollmentsPath = "/offerings/CS201-TEST/enrollments";

    [Fact]
    [Trait("Slice", "A2")]
    [Trait("Case", "T-53")]
    public async Task Delete_EnrolledLearner_PromotesWaitlistedLearner()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await EnrollLearnerAsync(client, "learner-001", HttpStatusCode.Created);
        await EnrollLearnerAsync(client, "learner-002", HttpStatusCode.Created);
        await EnrollLearnerAsync(client, "learner-003", HttpStatusCode.Accepted);

        // Act
        using var response = await client.DeleteAsync($"{EnrollmentsPath}/learner-001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("outcome").GetString().Should().Be("Dropped");
        body.GetProperty("learnerId").GetString().Should().Be("learner-001");
        body.GetProperty("promotedLearnerIds").EnumerateArray()
            .Select(learner => learner.GetString())
            .Should().Equal("learner-003");
        body.TryGetProperty("waitlistPosition", out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Slice", "A2")]
    [Trait("Case", "T-54")]
    public async Task Delete_AlreadyDroppedLearner_ReturnsIdempotentNoOp()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await EnrollLearnerAsync(client, "learner-001", HttpStatusCode.Created);

        // Act
        using var firstResponse = await client.DeleteAsync($"{EnrollmentsPath}/learner-001");
        using var response = await client.DeleteAsync($"{EnrollmentsPath}/learner-001");

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        firstBody.GetProperty("outcome").GetString().Should().Be("Dropped");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("outcome").GetString().Should().Be("NotEnrolled");
        body.GetProperty("promotedLearnerIds").GetArrayLength().Should().Be(0);
        body.TryGetProperty("waitlistPosition", out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Slice", "A2")]
    [Trait("Case", "T-55")]
    public async Task Delete_WaitlistedLearner_RemovesWithoutPromotion()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await EnrollLearnerAsync(client, "learner-001", HttpStatusCode.Created);
        await EnrollLearnerAsync(client, "learner-002", HttpStatusCode.Created);
        await EnrollLearnerAsync(client, "learner-003", HttpStatusCode.Accepted);

        // Act
        using var waitlistedResponse = await client.DeleteAsync($"{EnrollmentsPath}/learner-003");
        using var enrolledResponse = await client.DeleteAsync($"{EnrollmentsPath}/learner-001");

        // Assert
        waitlistedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var waitlistedBody = await waitlistedResponse.Content.ReadFromJsonAsync<JsonElement>();
        waitlistedBody.GetProperty("outcome").GetString().Should().Be("RemovedFromWaitlist");
        waitlistedBody.GetProperty("promotedLearnerIds").GetArrayLength().Should().Be(0);

        enrolledResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var enrolledBody = await enrolledResponse.Content.ReadFromJsonAsync<JsonElement>();
        enrolledBody.GetProperty("outcome").GetString().Should().Be("Dropped");
        enrolledBody.GetProperty("promotedLearnerIds").GetArrayLength().Should().Be(0);
    }

    [Fact]
    [Trait("Slice", "A2")]
    [Trait("Case", "T-56")]
    public async Task Delete_UnknownOffering_ReturnsNotFoundProblem()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.DeleteAsync(
            "/offerings/UNKNOWN/enrollments/learner-001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(404);
    }

    [Fact]
    [Trait("Slice", "A2")]
    [Trait("Case", "T-62")]
    public async Task Delete_WhitespaceLearnerId_ReturnsBadRequestWithoutChangingState()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await EnrollLearnerAsync(client, "learner-001", HttpStatusCode.Created);

        // Act
        using var response = await client.DeleteAsync($"{EnrollmentsPath}/%20");
        using var repeatEnrollment = await client.PostAsJsonAsync(
            EnrollmentsPath,
            new { learnerId = "learner-001" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("outcome").GetString().Should().Be("InvalidRequest");

        var repeatBody = await repeatEnrollment.Content.ReadFromJsonAsync<JsonElement>();
        repeatBody.GetProperty("outcome").GetString().Should().Be("AlreadyEnrolled");
    }

    private static async Task EnrollLearnerAsync(
        HttpClient client,
        string learnerId,
        HttpStatusCode expectedStatusCode)
    {
        using var response = await client.PostAsJsonAsync(
            EnrollmentsPath,
            new { learnerId });
        response.StatusCode.Should().Be(expectedStatusCode);
    }
}
