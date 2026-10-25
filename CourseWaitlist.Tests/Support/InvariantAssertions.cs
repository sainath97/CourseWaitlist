using CourseWaitlist.Core;
using FluentAssertions;

namespace CourseWaitlist.Tests.Support;

public static class InvariantAssertions
{
    public static void AssertInvariants(OfferingSnapshot snapshot)
    {
        snapshot.Enrolled.Count.Should().BeLessOrEqualTo(snapshot.Capacity);
        snapshot.Enrolled
            .Intersect(snapshot.Waitlist, StringComparer.OrdinalIgnoreCase)
            .Should()
            .BeEmpty();
        snapshot.Enrolled
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count()
            .Should()
            .Be(snapshot.Enrolled.Count);
        snapshot.Waitlist
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count()
            .Should()
            .Be(snapshot.Waitlist.Count);
    }
}
