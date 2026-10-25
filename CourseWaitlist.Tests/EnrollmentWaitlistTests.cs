using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class EnrollmentWaitlistTests
{
    [Fact]
    [Trait("Slice", "C4")]
    [Trait("Case", "T-16")]
    public void Enroll_FullOffering_AddsLearnerAtWaitlistPositionOne()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();

        // Act
        var firstResult = service.Enroll("A");
        var secondResult = service.Enroll("B");

        // Assert
        firstResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        secondResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        secondResult.WaitlistPosition.Should().Be(1);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B"]));
    }

    [Fact]
    [Trait("Slice", "C4")]
    [Trait("Case", "T-17")]
    public void Enroll_FullOffering_AssignsIncreasingWaitlistPositions()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();

        // Act
        var results = new[]
        {
            service.Enroll("A"),
            service.Enroll("B"),
            service.Enroll("C"),
            service.Enroll("D")
        };

        // Assert
        results[0].Outcome.Should().Be(EnrollOutcome.Enrolled);
        results[0].WaitlistPosition.Should().BeNull();
        results[1].Outcome.Should().Be(EnrollOutcome.Waitlisted);
        results[1].WaitlistPosition.Should().Be(1);
        results[2].Outcome.Should().Be(EnrollOutcome.Waitlisted);
        results[2].WaitlistPosition.Should().Be(2);
        results[3].Outcome.Should().Be(EnrollOutcome.Waitlisted);
        results[3].WaitlistPosition.Should().Be(3);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B", "C", "D"]));
    }

    [Fact]
    [Trait("Slice", "C4")]
    [Trait("Case", "T-18")]
    public void Enroll_FullOfferingAndIneligibleLearner_DoesNotAddLearnerToWaitlist()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A");
        var service = builder.Build();

        // Act
        var eligibleResult = service.Enroll("A");
        var afterEligibleEnroll = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(afterEligibleEnroll);
        var ineligibleResult = service.Enroll("B");

        // Assert
        eligibleResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        ineligibleResult.Outcome.Should().Be(EnrollOutcome.PrerequisiteNotMet);
        ineligibleResult.WaitlistPosition.Should().BeNull();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }

    [Fact]
    [Trait("Slice", "C4")]
    [Trait("Case", "T-19")]
    public void Enroll_FiveLearnersWithCapacityTwo_KeepsEnrolledCountWithinCapacity()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(2)
            .WithPrerequisite(null);
        var service = builder.Build();
        var learnerIds = new[] { "A", "B", "C", "D", "E" };
        var results = new List<EnrollResult>();

        // Act
        foreach (var learnerId in learnerIds)
        {
            results.Add(service.Enroll(learnerId));
            var snapshot = service.GetSnapshot();
            InvariantAssertions.AssertInvariants(snapshot);
            snapshot.Enrolled.Count.Should().BeLessOrEqualTo(2);
        }

        // Assert
        results[0].Outcome.Should().Be(EnrollOutcome.Enrolled);
        results[0].WaitlistPosition.Should().BeNull();
        results[1].Outcome.Should().Be(EnrollOutcome.Enrolled);
        results[1].WaitlistPosition.Should().BeNull();
        for (var index = 2; index < results.Count; index++)
        {
            results[index].Outcome.Should().Be(EnrollOutcome.Waitlisted);
            results[index].WaitlistPosition.Should().Be(index - 1);
        }

        var finalSnapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(finalSnapshot);
        finalSnapshot.Enrolled.Should().HaveCount(2);
        finalSnapshot.Waitlist.Should().HaveCount(3);
        finalSnapshot.ShouldMatch(builder.ExpectedSnapshot(["A", "B"], ["C", "D", "E"]));
    }
}
