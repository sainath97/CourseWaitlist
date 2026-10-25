using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace CourseWaitlist.Tests.Api;

public class SnapshotEndpointTests
{
    private const string OfferingPath = "/offerings/CS201-TEST";
    private const string EnrollmentsPath = $"{OfferingPath}/enrollments";

    [Fact]
    [Trait("Slice", "A3")]
    [Trait("Case", "T-57")]
    public async Task Get_OfferingSnapshot_ReflectsEnrolledAndWaitlistOrder()
    {
        // Arrange
        using var factory = new ApiFactory(new Dictionary<string, string?>
        {
            ["LearnerRecords:learner-005:0"] = "CS101"
        });
        using var client = factory.CreateClient();

        // Act
        using var initialResponse = await client.GetAsync(OfferingPath);

        // Assert
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertSnapshotAsync(
            initialResponse,
            "CS201-TEST",
            "CS201",
            2,
            Array.Empty<string>(),
            Array.Empty<string>());

        // Act
        await EnrollLearnerAsync(client, "learner-001", HttpStatusCode.Created);
        await EnrollLearnerAsync(client, "learner-002", HttpStatusCode.Created);
        await EnrollLearnerAsync(client, "learner-003", HttpStatusCode.Accepted);
        await EnrollLearnerAsync(client, "learner-005", HttpStatusCode.Accepted);
        using var populatedResponse = await client.GetAsync(OfferingPath);

        // Assert
        populatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertSnapshotAsync(
            populatedResponse,
            "CS201-TEST",
            "CS201",
            2,
            ["learner-001", "learner-002"],
            ["learner-003", "learner-005"]);

        // Act
        using var dropResponse = await client.DeleteAsync($"{EnrollmentsPath}/learner-001");

        // Assert
        dropResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        using var promotedResponse = await client.GetAsync(OfferingPath);

        // Assert
        promotedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertSnapshotAsync(
            promotedResponse,
            "CS201-TEST",
            "CS201",
            2,
            ["learner-002", "learner-003"],
            ["learner-005"]);
    }

    [Fact]
    [Trait("Slice", "A3")]
    [Trait("Case", "T-58")]
    public async Task Get_UnknownOffering_ReturnsNotFoundProblem()
    {
        // Arrange
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/offerings/UNKNOWN");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(404);
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

    private static async Task AssertSnapshotAsync(
        HttpResponseMessage response,
        string offeringId,
        string courseCode,
        int capacity,
        string[] enrolled,
        string[] waitlist)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("offeringId").GetString().Should().Be(offeringId);
        body.GetProperty("courseCode").GetString().Should().Be(courseCode);
        body.GetProperty("capacity").GetInt32().Should().Be(capacity);
        body.GetProperty("enrolled").EnumerateArray()
            .Select(learner => learner.GetString())
            .Should().Equal(enrolled);
        body.GetProperty("waitlist").EnumerateArray()
            .Select(learner => learner.GetString())
            .Should().Equal(waitlist);
    }
}
