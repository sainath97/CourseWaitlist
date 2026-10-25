using CourseWaitlist.Core;
using FluentAssertions;

namespace CourseWaitlist.Tests.Support;

public static class SnapshotAssertions
{
    public static void ShouldMatch(this OfferingSnapshot actual, OfferingSnapshot expected)
    {
        actual.OfferingId.Should().Be(expected.OfferingId);
        actual.CourseCode.Should().Be(expected.CourseCode);
        actual.Capacity.Should().Be(expected.Capacity);
        actual.Enrolled.Should().Equal(expected.Enrolled);
        actual.Waitlist.Should().Equal(expected.Waitlist);
    }
}
