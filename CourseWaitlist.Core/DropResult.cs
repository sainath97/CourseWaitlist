namespace CourseWaitlist.Core;

public record DropResult(DropOutcome Outcome, IReadOnlyList<string> PromotedLearnerIds);
