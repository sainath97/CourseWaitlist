using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class EnrollmentDuplicateTests
{
    [Fact]
    [Trait("Slice", "C5")]
    [Trait("Case", "T-20")]
    public void Enroll_AlreadyEnrolledLearner_ReturnsAlreadyEnrolled()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(2)
            .WithPrerequisite(null);
        var service = builder.Build();

        // Act
        var firstResult = service.Enroll("A");
        var duplicateResult = service.Enroll("A");

        // Assert
        firstResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        duplicateResult.Outcome.Should().Be(EnrollOutcome.AlreadyEnrolled);
        duplicateResult.WaitlistPosition.Should().BeNull();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }

    [Fact]
    [Trait("Slice", "C5")]
    [Trait("Case", "T-21")]
    public void Enroll_AlreadyWaitlistedLearners_ReturnsExistingPositions()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();

        // Act
        var enrolledResult = service.Enroll("A");
        var firstWaitlistResult = service.Enroll("B");
        var secondWaitlistResult = service.Enroll("C");
        var repeatedFirstWaitlistResult = service.Enroll("B");
        var repeatedSecondWaitlistResult = service.Enroll("C");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        firstWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        firstWaitlistResult.WaitlistPosition.Should().Be(1);
        secondWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        secondWaitlistResult.WaitlistPosition.Should().Be(2);
        repeatedFirstWaitlistResult.Outcome.Should().Be(EnrollOutcome.AlreadyWaitlisted);
        repeatedFirstWaitlistResult.WaitlistPosition.Should().Be(1);
        repeatedSecondWaitlistResult.Outcome.Should().Be(EnrollOutcome.AlreadyWaitlisted);
        repeatedSecondWaitlistResult.WaitlistPosition.Should().Be(2);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B", "C"]));
    }

    [Fact]
    [Trait("Slice", "C5")]
    [Trait("Case", "T-22")]
    public void Enroll_EnrolledLearnerIdVariants_AreCaseInsensitiveAndTrimmed()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(2)
            .WithPrerequisite(null);
        var service = builder.Build();

        // Act
        var firstResult = service.Enroll(" Alice ");
        var lowercaseResult = service.Enroll("alice");
        var uppercaseResult = service.Enroll("ALICE");

        // Assert
        firstResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        lowercaseResult.Outcome.Should().Be(EnrollOutcome.AlreadyEnrolled);
        uppercaseResult.Outcome.Should().Be(EnrollOutcome.AlreadyEnrolled);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["Alice"]));
    }

    [Fact]
    [Trait("Slice", "C5")]
    [Trait("Case", "T-22")]
    public void Enroll_WaitlistedLearnerIdVariants_AreCaseInsensitiveAndTrimmed()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();

        // Act
        var enrolledResult = service.Enroll("A");
        var firstWaitlistResult = service.Enroll(" Bob ");
        var lowercaseResult = service.Enroll("bob");
        var uppercaseResult = service.Enroll("BOB");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        firstWaitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        firstWaitlistResult.WaitlistPosition.Should().Be(1);
        lowercaseResult.Outcome.Should().Be(EnrollOutcome.AlreadyWaitlisted);
        lowercaseResult.WaitlistPosition.Should().Be(1);
        uppercaseResult.Outcome.Should().Be(EnrollOutcome.AlreadyWaitlisted);
        uppercaseResult.WaitlistPosition.Should().Be(1);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"], ["Bob"]));
    }

    [Fact]
    [Trait("Slice", "C5")]
    [Trait("Case", "T-23")]
    public void Enroll_AlreadyEnrolledLearnerWhosePrerequisiteWasRemoved_RemainsAlreadyEnrolled()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite("CS101")
            .WithCompleted("CS101", "A");
        var service = builder.Build();

        // Act
        var firstResult = service.Enroll("A");
        builder.Records.Remove("A", "CS101");
        var repeatedResult = service.Enroll("A");

        // Assert
        firstResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        repeatedResult.Outcome.Should().Be(EnrollOutcome.AlreadyEnrolled);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }

    [Fact]
    [Trait("Slice", "C5")]
    [Trait("Case", "T-24")]
    public void Enroll_EnrolledDuplicate_LeavesSnapshotUnchanged()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(2)
            .WithPrerequisite(null);
        var service = builder.Build();
        var firstResult = service.Enroll("A");

        // Act
        var before = service.GetSnapshot();
        var duplicateResult = service.Enroll("A");
        var after = service.GetSnapshot();

        // Assert
        firstResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        duplicateResult.Outcome.Should().Be(EnrollOutcome.AlreadyEnrolled);
        InvariantAssertions.AssertInvariants(before);
        InvariantAssertions.AssertInvariants(after);
        after.ShouldMatch(before);
    }

    [Fact]
    [Trait("Slice", "C5")]
    [Trait("Case", "T-24")]
    public void Enroll_WaitlistedDuplicate_LeavesSnapshotUnchanged()
    {
        // Arrange
        var builder = new TestServiceBuilder()
            .WithCapacity(1)
            .WithPrerequisite(null);
        var service = builder.Build();
        var firstResult = service.Enroll("A");
        var waitlistResult = service.Enroll("B");

        // Act
        var before = service.GetSnapshot();
        var duplicateResult = service.Enroll("B");
        var after = service.GetSnapshot();

        // Assert
        firstResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        waitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        duplicateResult.Outcome.Should().Be(EnrollOutcome.AlreadyWaitlisted);
        InvariantAssertions.AssertInvariants(before);
        InvariantAssertions.AssertInvariants(after);
        after.ShouldMatch(before);
    }

    [Fact]
    [Trait("Slice", "C5")]
    [Trait("Case", "T-60")]
    public void Enroll_AlreadyWaitlistedLearnerWhosePrerequisiteWasRemoved_RemainsAlreadyWaitlisted()
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
        builder.Records.Remove("B", "CS101");
        var repeatedResult = service.Enroll("B");

        // Assert
        enrolledResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        waitlistResult.Outcome.Should().Be(EnrollOutcome.Waitlisted);
        waitlistResult.WaitlistPosition.Should().Be(1);
        repeatedResult.Outcome.Should().Be(EnrollOutcome.AlreadyWaitlisted);
        repeatedResult.WaitlistPosition.Should().Be(1);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"], ["B"]));
    }
}
