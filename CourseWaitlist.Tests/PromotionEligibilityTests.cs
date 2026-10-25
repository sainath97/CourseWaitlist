using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class PromotionEligibilityTests
{
    [Fact]
    [Trait("Slice", "C8")]
    [Trait("Case", "T-33")]
    public void Drop_IneligibleHeadWaiter_SkipsAndRetainsThem()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A", "B", "C");
        var service = builder.Build();

        // Act
        var enrolledResult = service.Enroll("A");
        var firstWaitlistResult = service.Enroll("B");
        var secondWaitlistResult = service.Enroll("C");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        firstWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        firstWaitlistResult.WaitlistPosition.Should().Be(1);
        secondWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        secondWaitlistResult.WaitlistPosition.Should().Be(2);
        var beforeEligibilityChange = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(beforeEligibilityChange);
        beforeEligibilityChange.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B", "C"]));

        // Act
        builder.Records.Remove("B", "CS101");
        var dropResult = service.Drop("A");

        // Assert
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().Equal("C");
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["C"], ["B"]));
    }

    [Fact]
    [Trait("Slice", "C8")]
    [Trait("Case", "T-34")]
    public void Drop_SkippedWaiterRegainsEligibility_PromotesThemOnNextDrop()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A", "B", "C", "D");
        var service = builder.Build();

        // Act
        var enrolledResult = service.Enroll("A");
        var firstWaitlistResult = service.Enroll("B");
        var secondWaitlistResult = service.Enroll("C");
        var thirdWaitlistResult = service.Enroll("D");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        firstWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        firstWaitlistResult.WaitlistPosition.Should().Be(1);
        secondWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        secondWaitlistResult.WaitlistPosition.Should().Be(2);
        thirdWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        thirdWaitlistResult.WaitlistPosition.Should().Be(3);
        var beforeEligibilityChange = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(beforeEligibilityChange);
        beforeEligibilityChange.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B", "C", "D"]));

        // Act
        builder.Records.Remove("B", "CS101");
        var firstDropResult = service.Drop("A");

        // Assert
        firstDropResult.Outcome.Should().Be(DropOutcome.Dropped);
        firstDropResult.PromotedLearnerIds.Should().Equal("C");
        var afterFirstDrop = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(afterFirstDrop);
        afterFirstDrop.ShouldMatch(builder.ExpectedSnapshot(["C"], ["B", "D"]));

        // Act
        builder.Records.Add("B", "CS101");
        var secondDropResult = service.Drop("C");

        // Assert
        secondDropResult.Outcome.Should().Be(DropOutcome.Dropped);
        secondDropResult.PromotedLearnerIds.Should().Equal("B");
        var afterSecondDrop = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(afterSecondDrop);
        afterSecondDrop.ShouldMatch(builder.ExpectedSnapshot(["B"], ["D"]));
    }

    [Fact]
    [Trait("Slice", "C8")]
    [Trait("Case", "T-35")]
    public void Drop_OnlyWaiterIsIneligible_LeavesSeatFreeAndRetainsWaiter()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A", "B");
        var service = builder.Build();

        // Act
        var enrolledResult = service.Enroll("A");
        var waitlistResult = service.Enroll("B");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        waitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        waitlistResult.WaitlistPosition.Should().Be(1);
        var beforeEligibilityChange = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(beforeEligibilityChange);
        beforeEligibilityChange.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B"]));

        // Act
        builder.Records.Remove("B", "CS101");
        var dropResult = service.Drop("A");

        // Assert
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().Equal(Array.Empty<string>());
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(Array.Empty<string>(), ["B"]));
    }

    [Fact]
    [Trait("Slice", "C8")]
    [Trait("Case", "T-36")]
    public void Enroll_NewLearnerWhenSkippedWaiterRegainsEligibility_PromotesWaiterFirst()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A", "B", "C");
        var service = builder.Build();

        // Act
        var enrolledResult = service.Enroll("A");
        var waitlistResult = service.Enroll("B");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        waitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        waitlistResult.WaitlistPosition.Should().Be(1);
        var beforeEligibilityChange = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(beforeEligibilityChange);
        beforeEligibilityChange.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B"]));

        // Act
        builder.Records.Remove("B", "CS101");
        var dropResult = service.Drop("A");

        // Assert
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().Equal(Array.Empty<string>());
        var afterDrop = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(afterDrop);
        afterDrop.ShouldMatch(builder.ExpectedSnapshot(Array.Empty<string>(), ["B"]));

        // Act
        builder.Records.Add("B", "CS101");
        var newLearnerResult = service.Enroll("C");

        // Assert
        newLearnerResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        newLearnerResult.WaitlistPosition.Should().Be(1);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["B"], ["C"]));
    }

    [Fact]
    [Trait("Slice", "C8")]
    [Trait("Case", "T-37")]
    public void Enroll_PromotedWaiterRepeatsEnrollment_ReconcilesSeatBeforeDuplicateCheck()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A", "B");
        var service = builder.Build();

        // Act
        var enrolledResult = service.Enroll("A");
        var waitlistResult = service.Enroll("B");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        waitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        waitlistResult.WaitlistPosition.Should().Be(1);
        var beforeEligibilityChange = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(beforeEligibilityChange);
        beforeEligibilityChange.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B"]));

        // Act
        builder.Records.Remove("B", "CS101");
        var dropResult = service.Drop("A");

        // Assert
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().Equal(Array.Empty<string>());
        var afterDrop = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(afterDrop);
        afterDrop.ShouldMatch(builder.ExpectedSnapshot(Array.Empty<string>(), ["B"]));

        // Act
        builder.Records.Add("B", "CS101");
        var repeatedEnrollmentResult = service.Enroll("B");

        // Assert
        repeatedEnrollmentResult.Outcome.Should().Be(EnrollOutcome.AlreadyEnrolled);
        repeatedEnrollmentResult.WaitlistPosition.Should().BeNull();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["B"]));
    }

    [Fact]
    [Trait("Slice", "C8")]
    [Trait("Case", "T-38")]
    public void Enroll_IneligibleNewcomerWithNewlyEligibleSkippedWaiter_PromotesWaiter()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A", "B");
        var service = builder.Build();

        // Act
        var enrolledResult = service.Enroll("A");
        var waitlistResult = service.Enroll("B");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        waitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        waitlistResult.WaitlistPosition.Should().Be(1);
        var beforeEligibilityChange = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(beforeEligibilityChange);
        beforeEligibilityChange.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B"]));

        // Act
        builder.Records.Remove("B", "CS101");
        var dropResult = service.Drop("A");

        // Assert
        dropResult.Outcome.Should().Be(DropOutcome.Dropped);
        dropResult.PromotedLearnerIds.Should().Equal(Array.Empty<string>());
        var afterDrop = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(afterDrop);
        afterDrop.ShouldMatch(builder.ExpectedSnapshot(Array.Empty<string>(), ["B"]));

        // Act
        builder.Records.Add("B", "CS101");
        var ineligibleNewcomerResult = service.Enroll("C");

        // Assert
        ineligibleNewcomerResult.Outcome.Should().Be(EnrollOutcome.PrerequisiteNotMet);
        ineligibleNewcomerResult.WaitlistPosition.Should().BeNull();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["B"]));
    }
}
