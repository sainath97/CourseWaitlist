using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class PromotionTests
{
    [Fact]
    [Trait("Slice", "C7")]
    [Trait("Case", "T-30")]
    public void Drop_EnrolledLearnerWithEligibleWaitlist_PromotesEarliestWaiter()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrolledResult = service.Enroll("A");
        var firstWaitlistResult = service.Enroll("B");
        var secondWaitlistResult = service.Enroll("C");

        // Act
        var dropResult = service.Drop("A");

        // Assert
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().Equal("B");
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        firstWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        firstWaitlistResult.WaitlistPosition.Should().Be(1);
        secondWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        secondWaitlistResult.WaitlistPosition.Should().Be(2);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["B"], ["C"]));
    }

    [Fact]
    [Trait("Slice", "C7")]
    [Trait("Case", "T-31")]
    public void Drop_SuccessiveEnrolledLearners_PromotesWaitersInFifoOrder()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrollResults = new[]
        {
            service.Enroll("A"),
            service.Enroll("B"),
            service.Enroll("C"),
            service.Enroll("D")
        };

        // Act
        var firstDropResult = service.Drop("A");
        var secondDropResult = service.Drop("B");

        // Assert
        firstDropResult.Outcome.Should().Be(DropOutcome.Dropped);
        firstDropResult.PromotedLearnerIds.Should().Equal("B");
        secondDropResult.Outcome.Should().Be(DropOutcome.Dropped);
        secondDropResult.PromotedLearnerIds.Should().Equal("C");
        enrollResults[0].Outcome.Should().Be(EnrollOutcome.Enrolled);
        enrollResults[1].Outcome.Should().Be(EnrollOutcome.Waitlisted);
        enrollResults[2].Outcome.Should().Be(EnrollOutcome.Waitlisted);
        enrollResults[3].Outcome.Should().Be(EnrollOutcome.Waitlisted);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["C"], ["D"]));
    }

    [Fact]
    [Trait("Slice", "C7")]
    [Trait("Case", "T-32")]
    public void Drop_RemovedWaitlistLearner_IsNotPromotedLater()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrolledResult = service.Enroll("A");
        var firstWaitlistResult = service.Enroll("B");
        var secondWaitlistResult = service.Enroll("C");

        // Act
        var waitlistDropResult = service.Drop("B");
        var enrolledDropResult = service.Drop("A");

        // Assert
        waitlistDropResult.Outcome.Should().Be(DropOutcome.RemovedFromWaitlist);
        waitlistDropResult.PromotedLearnerIds.Should().BeEmpty();
        enrolledDropResult.Outcome.Should().Be(DropOutcome.Dropped);
        enrolledDropResult.PromotedLearnerIds.Should().Equal("C");
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        firstWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        secondWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["C"]));
    }

    [Fact]
    [Trait("Slice", "C7")]
    [Trait("Case", "T-61")]
    public void Drop_PromotionShiftsWaitlistPosition()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrollResults = new[]
        {
            service.Enroll("A"),
            service.Enroll("B"),
            service.Enroll("C")
        };

        // Act
        var dropResult = service.Drop("A");
        var repeatedEnrollmentResult = service.Enroll("C");

        // Assert
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().Equal("B");
        repeatedEnrollmentResult.Outcome.Should().Be(EnrollOutcome.AlreadyWaitlisted);
        repeatedEnrollmentResult.WaitlistPosition.Should().Be(1);
        enrollResults[0].Outcome.Should().Be(EnrollOutcome.Enrolled);
        enrollResults[1].Outcome.Should().Be(EnrollOutcome.Waitlisted);
        enrollResults[2].Outcome.Should().Be(EnrollOutcome.Waitlisted);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["B"], ["C"]));
    }
}
