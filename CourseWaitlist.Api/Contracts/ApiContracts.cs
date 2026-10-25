namespace CourseWaitlist.Api.Contracts;

public sealed record EnrollRequest(string? LearnerId);

public sealed record MutationResponse(
    string Outcome,
    string LearnerId,
    int? WaitlistPosition,
    IReadOnlyList<string>? PromotedLearnerIds);

public sealed record SnapshotResponse(
    string OfferingId,
    string CourseCode,
    int Capacity,
    IReadOnlyList<string> Enrolled,
    IReadOnlyList<string> Waitlist);
