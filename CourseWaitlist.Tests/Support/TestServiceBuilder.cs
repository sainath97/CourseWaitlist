using CourseWaitlist.Core;

namespace CourseWaitlist.Tests.Support;

public sealed class TestServiceBuilder
{
    private readonly OfferingOptions _options = new()
    {
        Id = "offering-1",
        CourseCode = "COURSE-101",
        Capacity = 2
    };

    public InMemoryLearnerRecords Records { get; } = new();

    public TestServiceBuilder WithCapacity(int capacity)
    {
        _options.Capacity = capacity;
        return this;
    }

    public TestServiceBuilder WithPrerequisite(string? courseCode)
    {
        _options.PrerequisiteCourseCode = courseCode;
        return this;
    }

    public TestServiceBuilder WithCompleted(string courseCode, params string[] learnerIds)
    {
        foreach (var learnerId in learnerIds)
        {
            Records.Add(learnerId.Trim(), courseCode);
        }

        return this;
    }

    public OfferingSnapshot ExpectedSnapshot(string[] enrolled, string[]? waitlist = null) =>
        new(
            _options.Id,
            _options.CourseCode,
            _options.Capacity,
            enrolled,
            waitlist ?? Array.Empty<string>());

    public EnrollmentService Build() => new(_options, Records);
}
