namespace CourseWaitlist.Core;

public interface ILearnerRecords
{
    bool HasCompleted(string learnerId, string courseCode);
}
