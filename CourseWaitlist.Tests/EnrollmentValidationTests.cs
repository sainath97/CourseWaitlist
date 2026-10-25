using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class EnrollmentValidationTests
{
    [Fact]
    [Trait("Slice", "C1")]
    [Trait("Case","T-01")]
    public void Constructor_ZeroCapacity_ThrowsArgumentException()
    {
        // Arrange
        var builder = new TestServiceBuilder().WithCapacity(0);

        // Act
        Action act = () => builder.Build();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Slice", "C1")]
    [Trait("Case","T-02")]
    public void Constructor_NegativeCapacity_ThrowsArgumentException()
    {
        // Arrange
        var builder = new TestServiceBuilder().WithCapacity(-1);

        // Act
        Action act = () => builder.Build();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Slice", "C1")]
    [Trait("Case","T-03, T-04, T-05")]
    public void Enroll_InvalidLearnerId_ReturnsInvalidRequestAndPreservesState(string? learnerId)
    {
        // Arrange
        var service = new TestServiceBuilder().Build();

        // Act
        var result = service.Enroll(learnerId);

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.InvalidRequest);
        var after = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(after);
        after.ShouldMatch(EmptySnapshot());
    }

    [Fact]
    [Trait("Slice", "C1")]
    [Trait("Case","T-06")]
    public void Enroll_65CharacterLearnerId_ReturnsInvalidRequestAndPreservesState()
    {
        // Arrange
        var service = new TestServiceBuilder().Build();
        var learnerId = new string('a', 65);

        // Act
        var result = service.Enroll(learnerId);

        // Assert
        result.Outcome.Should().Be(EnrollOutcome.InvalidRequest);
        var after = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(after);
        after.ShouldMatch(EmptySnapshot());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Slice", "C1")]
    [Trait("Case","T-07")]
    public void Drop_InvalidLearnerId_ReturnsInvalidRequestAndPreservesState(string? learnerId)
    {
        // Arrange
        var service = new TestServiceBuilder().Build();

        // Act
        var result = service.Drop(learnerId);

        // Assert
        result.Outcome.Should().Be(DropOutcome.InvalidRequest);
        var after = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(after);
        after.ShouldMatch(EmptySnapshot());
    }

    [Fact]
    [Trait("Slice", "C1")]
    [Trait("Case","T-07")]
    public void Drop_65CharacterLearnerId_ReturnsInvalidRequestAndPreservesState()
    {
        // Arrange
        var service = new TestServiceBuilder().Build();
        var learnerId = new string('a', 65);

        // Act
        var result = service.Drop(learnerId);

        // Assert
        result.Outcome.Should().Be(DropOutcome.InvalidRequest);
        var after = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(after);
        after.ShouldMatch(EmptySnapshot());
    }

    [Fact]
    [Trait("Slice", "C1")]
    [Trait("Case","T-08")]
    public void InvalidEnrollAndDrop_LeaveSnapshotUnchanged()
    {
        // Arrange
        var service = new TestServiceBuilder().Build();

        // Act
        var enrollResult = service.Enroll("");
        var dropResult = service.Drop("");

        // Assert
        enrollResult.Outcome.Should().Be(EnrollOutcome.InvalidRequest);
        dropResult.Outcome.Should().Be(DropOutcome.InvalidRequest);
        var after = service.GetSnapshot();
        InvariantAssertions.AssertInvariants(after);
        after.ShouldMatch(EmptySnapshot());
    }

    [Fact]
    [Trait("Slice", "C1")]
    [Trait("Case","T-63")]
    public void GetSnapshot_EmptyOffering_ReturnsEmptySnapshot()
    {
        // Arrange
        var service = new TestServiceBuilder().Build();

        // Act
        var snapshot = service.GetSnapshot();

        // Assert
        InvariantAssertions.AssertInvariants(snapshot);
        snapshot.ShouldMatch(EmptySnapshot());
    }

    private static OfferingSnapshot EmptySnapshot() =>
        new("offering-1", "COURSE-101", 2, Array.Empty<string>(), Array.Empty<string>());
}
