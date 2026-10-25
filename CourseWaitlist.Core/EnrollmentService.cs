namespace CourseWaitlist.Core;

/// <summary>
/// Maintains one offering while preserving these invariants:
/// <list type="bullet">
/// <item>Enrolled count never exceeds capacity.</item>
/// <item>A learner is never both enrolled and waitlisted.</item>
/// <item>Neither list contains duplicate learner IDs, compared case-insensitively.</item>
/// <item>Waitlist entries are in strictly ascending sequence order.</item>
/// <item>After every valid Enroll and every Drop that removes a learner, a free seat and a currently eligible waitlisted learner do not coexist.</item>
/// </list>
/// </summary>
public class EnrollmentService
{
    private readonly OfferingOptions _options;
    private readonly ILearnerRecords _records;

    public EnrollmentService(OfferingOptions options, ILearnerRecords records)
    {
        _options = options;
        _records = records;
    }

    public EnrollResult Enroll(string? learnerId)
    {
        throw new NotImplementedException();
    }

    public DropResult Drop(string? learnerId)
    {
        throw new NotImplementedException();
    }

    public OfferingSnapshot GetSnapshot()
    {
        throw new NotImplementedException();
    }
}
