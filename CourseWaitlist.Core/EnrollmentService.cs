namespace CourseWaitlist.Core;

/// <summary>
/// Maintains one offering while preserving these invariants:
/// <list type="bullet">
/// <item>Enrolled count never exceeds capacity.</item>
/// <item>A learner is never both enrolled and waitlisted.</item>
/// <item>Neither list contains duplicate learner IDs, compared case-insensitively.</item>
/// <item>Waitlist entries are in strictly ascending sequence order.</item>
/// <item>After every validated Enroll and every Drop of an enrolled learner, a free seat and a currently eligible waitlisted learner do not coexist. InvalidRequest, NotEnrolled and a Drop of a waitlisted learner do not reconcile free seats; seats are reconciled at the next validated Enroll or enrolled-learner Drop.</item>
/// </list>
/// </summary>
public class EnrollmentService
{
    private sealed record WaitlistEntry(string LearnerId, long Sequence);

    private readonly OfferingOptions _options;
    private readonly ILearnerRecords _records;
    // Single lock guards all mutable state; eligibility is evaluated inside it.
    private readonly object _gate = new();
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
        lock (_gate)
        {
            var normalizedLearnerId = NormalizeLearnerId(learnerId);
            if (normalizedLearnerId is null)
            {
                return new EnrollResult(EnrollOutcome.InvalidRequest, null);
            }

            FillFreeSeats();

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
    }

    public DropResult Drop(string? learnerId)
    {
        lock (_gate)
        {
            var normalizedLearnerId = NormalizeLearnerId(learnerId);
            if (normalizedLearnerId is null)
            {
                return new DropResult(DropOutcome.InvalidRequest, Array.Empty<string>());
            }

            var enrolledIndex = _enrolled.FindIndex(
                enrolledLearnerId => StringComparer.OrdinalIgnoreCase.Equals(enrolledLearnerId, normalizedLearnerId));
            if (enrolledIndex >= 0)
            {
                _enrolled.RemoveAt(enrolledIndex);
                return new DropResult(DropOutcome.Dropped, FillFreeSeats());
            }

            var waitlistIndex = _waitlist.FindIndex(
                entry => StringComparer.OrdinalIgnoreCase.Equals(entry.LearnerId, normalizedLearnerId));
            if (waitlistIndex >= 0)
            {
                _waitlist.RemoveAt(waitlistIndex);
                return new DropResult(DropOutcome.RemovedFromWaitlist, Array.Empty<string>());
            }

            return new DropResult(DropOutcome.NotEnrolled, Array.Empty<string>());
        }
    }

    public OfferingSnapshot GetSnapshot()
    {
        lock (_gate)
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

    private List<string> FillFreeSeats()
    {
        var promotedLearnerIds = new List<string>();
        while (_enrolled.Count < _options.Capacity)
        {
            var eligibleIndex = _waitlist.FindIndex(entry => IsEligible(entry.LearnerId));
            if (eligibleIndex < 0)
            {
                break;
            }

            var nextWaitlistEntry = _waitlist[eligibleIndex];
            _waitlist.RemoveAt(eligibleIndex);
            _enrolled.Add(nextWaitlistEntry.LearnerId);
            promotedLearnerIds.Add(nextWaitlistEntry.LearnerId);
        }

        return promotedLearnerIds;
    }
}
