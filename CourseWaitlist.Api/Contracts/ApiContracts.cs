namespace CourseWaitlist.Api.Contracts;

public sealed record EnrollRequest(string? LearnerId);

public sealed record MutationResponse(
    string Outcome,
    string LearnerId,
    int? WaitlistPosition,
    IReadOnlyList<string>? PromotedLearnerIds);
