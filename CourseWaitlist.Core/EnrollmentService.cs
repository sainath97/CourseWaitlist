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
    private sealed record WaitlistEntry(string LearnerId, long Sequence);

    private readonly OfferingOptions _options;
    private readonly ILearnerRecords _records;
    private readonly List<string> _enrolled = new();
    private readonly List<WaitlistEntry> _waitlist = new();
    private long _nextSequence;

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
        var normalizedLearnerId = NormalizeLearnerId(learnerId);
        if (normalizedLearnerId is null)
        {
            return new EnrollResult(EnrollOutcome.InvalidRequest, null);
        }

        var enrolledIndex = _enrolled.FindIndex(
            enrolledLearnerId => StringComparer.OrdinalIgnoreCase.Equals(enrolledLearnerId, normalizedLearnerId));
        if (enrolledIndex >= 0)
        {
            return new EnrollResult(EnrollOutcome.AlreadyEnrolled, null);
        }

        var waitlistIndex = _waitlist.FindIndex(
            entry => StringComparer.OrdinalIgnoreCase.Equals(entry.LearnerId, normalizedLearnerId));
        if (waitlistIndex >= 0)
        {
            return new EnrollResult(EnrollOutcome.AlreadyWaitlisted, waitlistIndex + 1);
        }

        if (!IsEligible(normalizedLearnerId))
        {
            return new EnrollResult(EnrollOutcome.PrerequisiteNotMet, null);
        }

        if (_enrolled.Count >= _options.Capacity)
        {
            _waitlist.Add(new WaitlistEntry(normalizedLearnerId, _nextSequence++));
            return new EnrollResult(EnrollOutcome.Waitlisted, _waitlist.Count);
        }

        _enrolled.Add(normalizedLearnerId);
        return new EnrollResult(EnrollOutcome.Enrolled, null);
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
            _enrolled.ToArray(),
            _waitlist
                .OrderBy(entry => entry.Sequence)
                .Select(entry => entry.LearnerId)
                .ToArray());
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

    private bool IsEligible(string learnerId)
    {
        var prerequisiteCourseCode = _options.PrerequisiteCourseCode;
        return string.IsNullOrWhiteSpace(prerequisiteCourseCode)
            || _records.HasCompleted(learnerId, prerequisiteCourseCode);
    }
}
