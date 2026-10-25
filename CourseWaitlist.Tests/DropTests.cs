using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class DropTests
{
    [Fact]
    [Trait("Slice", "C6")]
    [Trait("Case", "T-25")]
    public void Drop_EnrolledLearnerWithoutWaitlist_FreesSeatForNextLearner()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrollmentResult = service.Enroll("A");

        // Act
        var dropResult = service.Drop("A");
        var afterDrop = service.GetSnapshot();
        var nextEnrollmentResult = service.Enroll("B");

        // Assert
        enrollmentResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().BeEmpty();
        InvariantAssertions.AssertInvariants(afterDrop);
        afterDrop.ShouldMatch(builder.ExpectedSnapshot(Array.Empty<string>()));
        nextEnrollmentResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        var finalSnapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(finalSnapshot);
        finalSnapshot.ShouldMatch(builder.ExpectedSnapshot(["B"]));
    }

    [Fact]
    [Trait("Slice", "C6")]
    [Trait("Case", "T-26")]
    public void Drop_UnknownLearner_ReturnsNotEnrolledWithoutChangingState()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(2)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrollmentResult = service.Enroll("A");

        // Act
        var dropResult = service.Drop("Z");

        // Assert
        enrollmentResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        dropResult.Outcome.Should().Be(DropOutcome.NotEnrolled);
        dropResult.PromotedLearnerIds.Should().BeEmpty();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }

    [Fact]
    [Trait("Slice", "C6")]
    [Trait("Case", "T-27")]
    public void Drop_AlreadyDroppedLearner_ReturnsNotEnrolledWithoutChangingState()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(2)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrollmentResult = service.Enroll("A");

        // Act
        var firstDropResult = service.Drop("A");
        var beforeRepeatDrop = service.GetSnapshot();
        var repeatedDropResult = service.Drop("A");
        var afterRepeatDrop = service.GetSnapshot();

        // Assert
        enrollmentResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        firstDropResult.Outcome.Should().Be(DropOutcome.Dropped);
        repeatedDropResult.Outcome.Should().Be(DropOutcome.NotEnrolled);
        repeatedDropResult.PromotedLearnerIds.Should().BeEmpty();
        InvariantAssertions.AssertInvariants(beforeRepeatDrop);
        InvariantAssertions.AssertInvariants(afterRepeatDrop);
        afterRepeatDrop.ShouldMatch(beforeRepeatDrop);
    }

    [Fact]
    [Trait("Slice", "C6")]
    [Trait("Case", "T-28")]
    public void Drop_WaitlistedLearner_RemovesEntryAndShiftsPositionsWithoutPromotion()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();
        var firstEnrollmentResult = service.Enroll("A");
        var firstWaitlistResult = service.Enroll("B");
        var secondWaitlistResult = service.Enroll("C");

        // Act
        var dropResult = service.Drop("B");

        // Assert
        firstEnrollmentResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        firstWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        firstWaitlistResult.WaitlistPosition.Should().Be(1);
        secondWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        secondWaitlistResult.WaitlistPosition.Should().Be(2);
        dropResult.Outcome.Should().Be(DropOutcome.RemovedFromWaitlist);
        dropResult.PromotedLearnerIds.Should().BeEmpty();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"], ["C"]));

        var repeatedEnrollmentResult = service.Enroll("C");
        repeatedEnrollmentResult.Outcome.Should().Be(EnrollOutcome.AlreadyWaitlisted);
        repeatedEnrollmentResult.WaitlistPosition.Should().Be(1);
    }

    [Fact]
    [Trait("Slice", "C6")]
    [Trait("Case", "T-29")]
    public void Drop_EnrolledLearnerIdVariant_DropsCaseInsensitively()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(2)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrollmentResult = service.Enroll("Alice");

        // Act
        var dropResult = service.Drop("  ALICE ");

        // Assert
        enrollmentResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().BeEmpty();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(Array.Empty<string>()));
    }

    [Fact]
    [Trait("Slice", "C6")]
    [Trait("Case", "T-29")]
    public void Drop_WaitlistedLearnerIdVariant_RemovesCaseInsensitively()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();
        var enrollmentResult = service.Enroll("A");
        var waitlistResult = service.Enroll("Bob");

        // Act
        var dropResult = service.Drop(" bob ");

        // Assert
        enrollmentResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        waitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        waitlistResult.WaitlistPosition.Should().Be(1);
        dropResult.Outcome.Should().Be(DropOutcome.RemovedFromWaitlist);
        dropResult.PromotedLearnerIds.Should().BeEmpty();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }
}
