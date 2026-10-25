using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace CourseWaitlist.Tests.Api;

public class EnrollEndpointTests
{
    private const string EnrollmentsPath = "/offerings/CS201-TEST/enrollments";

    [Fact]
    [Trait("Slice", "A1")]
    [Trait("Case", "T-46")]
    public async Task Post_EligibleLearner_EnrolledWithCreatedResponse()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await PostLearnerAsync(client, EnrollmentsPath, "learner-001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("outcome").GetString().Should().Be("Enrolled");
        body.GetProperty("learnerId").GetString().Should().Be("learner-001");
        body.TryGetProperty("waitlistPosition", out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Slice", "A1")]
    [Trait("Case", "T-47")]
    public async Task Post_OfferingFull_WaitlistsLearnerWithAcceptedResponse()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var firstResponse = await PostLearnerAsync(client, EnrollmentsPath, "learner-001");
        using var secondResponse = await PostLearnerAsync(client, EnrollmentsPath, "learner-002");
        using var response = await PostLearnerAsync(client, EnrollmentsPath, "learner-003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("outcome").GetString().Should().Be("Waitlisted");
        body.GetProperty("waitlistPosition").GetInt32().Should().Be(1);
        body.GetProperty("learnerId").GetString().Should().Be("learner-003");
    }

    [Fact]
    [Trait("Slice", "A1")]
    [Trait("Case", "T-48")]
    public async Task Post_AlreadyEnrolledLearner_ReturnsIdempotentSuccess()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var firstResponse = await PostLearnerAsync(client, EnrollmentsPath, "learner-001");
        using var response = await PostLearnerAsync(client, EnrollmentsPath, "learner-001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("outcome").GetString().Should().Be("AlreadyEnrolled");
    }

    [Fact]
    [Trait("Slice", "A1")]
    [Trait("Case", "T-49")]
    public async Task Post_AlreadyWaitlistedLearner_ReturnsSamePosition()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var firstResponse = await PostLearnerAsync(client, EnrollmentsPath, "learner-001");
        using var secondResponse = await PostLearnerAsync(client, EnrollmentsPath, "learner-002");
        using var thirdResponse = await PostLearnerAsync(client, EnrollmentsPath, "learner-003");
        using var response = await PostLearnerAsync(client, EnrollmentsPath, "learner-003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("outcome").GetString().Should().Be("AlreadyWaitlisted");
        body.GetProperty("waitlistPosition").GetInt32().Should().Be(1);
    }

    [Fact]
    [Trait("Slice", "A1")]
    [Trait("Case", "T-50")]
    public async Task Post_LearnerMissingPrerequisite_ReturnsUnprocessableProblem()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await PostLearnerAsync(client, EnrollmentsPath, "learner-004");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(422);
        body.GetProperty("outcome").GetString().Should().Be("PrerequisiteNotMet");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"learnerId\":null}")]
    [InlineData("{\"learnerId\":\"\"}")]
    [InlineData("{\"learnerId\":\"   \"}")]
    [Trait("Slice", "A1")]
    [Trait("Case", "T-51")]
    public async Task Post_InvalidBody_ReturnsBadRequestProblem(string requestBody)
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(EnrollmentsPath, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("outcome").GetString().Should().Be("InvalidRequest");
    }

    [Fact]
    [Trait("Slice", "A1")]
    [Trait("Case", "T-52")]
    public async Task Post_UnknownOffering_ReturnsNotFoundProblem()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await PostLearnerAsync(
            client,
            "/offerings/UNKNOWN/enrollments",
            "learner-001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(404);
    }

    private static Task<HttpResponseMessage> PostLearnerAsync(
        HttpClient client,
        string path,
        string learnerId) =>
        client.PostAsJsonAsync(path, new { learnerId });
}
