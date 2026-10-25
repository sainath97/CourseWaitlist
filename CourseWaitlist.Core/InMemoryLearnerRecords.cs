namespace CourseWaitlist.Core;

public sealed class InMemoryLearnerRecords : ILearnerRecords
{
    private readonly object _lock = new();
    private readonly Dictionary<string, HashSet<string>> _completedCourses =
        new(StringComparer.OrdinalIgnoreCase);

    public void Add(string learnerId, string courseCode)
    {
        lock (_lock)
        {
            if (!_completedCourses.TryGetValue(learnerId, out var courses))
            {
                courses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _completedCourses.Add(learnerId, courses);
            }

            courses.Add(courseCode);
        }
    }

    public void Remove(string learnerId, string courseCode)
    {
        lock (_lock)
        {
            if (_completedCourses.TryGetValue(learnerId, out var courses))
            {
                courses.Remove(courseCode);

                if (courses.Count == 0)
                {
                    _completedCourses.Remove(learnerId);
                }
            }
        }
    }

    public bool HasCompleted(string learnerId, string courseCode)
    {
        lock (_lock)
        {
            return _completedCourses.TryGetValue(learnerId, out var courses)
                && courses.Contains(courseCode);
        }
    }
}
