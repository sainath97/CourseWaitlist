namespace CourseWaitlist.Core;

public record OfferingSnapshot(
    string OfferingId,
    string CourseCode,
    int Capacity,
    IReadOnlyList<string> Enrolled,
    IReadOnlyList<string> Waitlist);
