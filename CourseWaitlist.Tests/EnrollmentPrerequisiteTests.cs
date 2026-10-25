using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class EnrollmentPrerequisiteTests
{
    [Fact]
    [Trait("Slice", "C3")]
    [Trait("Case", "T-14")]
    public void Enroll_PrerequisiteNotCompleted_ReturnsPrerequisiteNotMetWithoutChangingState()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101");
        var service = builder.Build();

        // Act
        var result = service.Enroll("A");

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.PrerequisiteNotMet);
        result.WaitlistPosition.Should().BeNull();
        var afterIneligibleAttempt = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(afterIneligibleAttempt);
        afterIneligibleAttempt.ShouldMatch(builder.ExpectedSnapshot(Array.Empty<string>()));

        builder.Records.Add("B", "CS101");
        var eligibleResult = service.Enroll("B");

        eligibleResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        var afterEligibleAttempt = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(afterEligibleAttempt);
        afterEligibleAttempt.ShouldMatch(builder.ExpectedSnapshot(["B"]));
    }

    [Fact]
    [Trait("Slice", "C3")]
    [Trait("Case", "T-15")]
    public void Enroll_PrerequisiteCompleted_ReturnsEnrolled()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(2)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A");
        var service = builder.Build();

        // Act
        var result = service.Enroll("A");

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.Enrolled);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }
}
