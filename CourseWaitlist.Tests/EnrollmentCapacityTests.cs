using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class EnrollmentCapacityTests
{
    [Fact]
    [Trait("Slice", "C2")]
    [Trait("Case", "T-09")]
    public void Enroll_AvailableSeat_ReturnsEnrolledWithoutWaitlistPosition()
    {
        // Arrange
        var builder = new TestServiceBuilder();
        var service = builder.Build();

        // Act
        var result = service.Enroll("A");

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.Enrolled);
        result.WaitlistPosition.Should().BeNull();
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }

    [Fact]
    [Trait("Slice", "C2")]
    [Trait("Case", "T-10")]
    public void Enroll_WhitespaceAroundLearnerId_StoresTrimmedId()
    {
        // Arrange
        var builder = new TestServiceBuilder();
        var service = builder.Build();

        // Act
        var result = service.Enroll("  alice  ");

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.Enrolled);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["alice"]));
    }

    [Fact]
    [Trait("Slice", "C2")]
    [Trait("Case", "T-11")]
    public void Enroll_TwoLearnersWithCapacityTwo_EnrollsBothInOrder()
    {
        // Arrange
        var builder = new TestServiceBuilder().WithCapacity(2);
        var service = builder.Build();

        // Act
        var firstResult = service.Enroll("A");
        var secondResult = service.Enroll("B");

        // Assert
        firstResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        secondResult.Outcome.Should().Be(EnrollOutcome.Enrolled);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A", "B"]));
    }

    [Fact]
    [Trait("Slice", "C2")]
    [Trait("Case", "T-12")]
    public void Enroll_CapacityOne_EnrollsSingleLearner()
    {
        // Arrange
        var builder = new TestServiceBuilder().WithCapacity(1);
        var service = builder.Build();

        // Act
        var result = service.Enroll("A");

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.Enrolled);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }

    [Fact]
    [Trait("Slice", "C2")]
    [Trait("Case", "T-13")]
    public void Enroll_NoPrerequisiteAndNoCompletedCourses_ReturnsEnrolled()
    {
        // Arrange
        var builder = new TestServiceBuilder().WithPrerequisite(null);
        var service = builder.Build();

        // Act
        var result = service.Enroll("A");

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.Enrolled);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot(["A"]));
    }

    [Fact]
    [Trait("Slice", "C2")]
    [Trait("Case", "T-59")]
    public void Enroll_64CharacterLearnerId_ReturnsEnrolled()
    {
        // Arrange
        var builder = new TestServiceBuilder().WithCapacity(2);
        var service = builder.Build();
        var learnerId = new string('a', 64);

        // Act
        var result = service.Enroll(learnerId);

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.Enrolled);
        var snapshot = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(builder.ExpectedSnapshot([learnerId]));
    }
}
