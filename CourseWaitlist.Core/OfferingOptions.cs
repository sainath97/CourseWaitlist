namespace CourseWaitlist.Core;

public sealed class OfferingOptions
{
    public string Id { get; set; } = "";

    public string CourseCode { get; set; } = "";

    public int Capacity { get; set; }

    public string? PrerequisiteCourseCode { get; set; }
}
