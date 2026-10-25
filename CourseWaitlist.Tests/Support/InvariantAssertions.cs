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

    public static void AssertNoFreeSeatBesideEligibleWaiter(
        OfferingSnapshot snapshot,
        Func<string, bool> isEligible)
    {
        if (snapshot.Enrolled.Count < snapshot.Capacity)
        {
            snapshot.Waitlist.Where(isEligible).Should().BeEmpty();
        }
    }
}
