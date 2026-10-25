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
        if (options.Capacity < 1)
        {
            throw new ArgumentException("Offering capacity must be at least 1.", nameof(options));
        }

        _options = options;
        _records = records;
    }

    public EnrollResult Enroll(string? learnerId)
    {
        if (NormalizeLearnerId(learnerId) is null)
        {
            return new EnrollResult(EnrollOutcome.InvalidRequest, null);
        }

        throw new NotImplementedException();
    }

    public DropResult Drop(string? learnerId)
    {
        if (NormalizeLearnerId(learnerId) is null)
        {
            return new DropResult(DropOutcome.InvalidRequest, Array.Empty<string>());
        }

        throw new NotImplementedException();
    }

    public OfferingSnapshot GetSnapshot()
    {
        return new OfferingSnapshot(
            _options.Id,
            _options.CourseCode,
            _options.Capacity,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static string? NormalizeLearnerId(string? learnerId)
    {
        if (learnerId is null)
        {
            return null;
        }

        var normalized = learnerId.Trim();
        return normalized.Length is > 0 and <= 64 ? normalized : null;
    }
}
