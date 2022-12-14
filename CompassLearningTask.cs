using System;
using System.Linq;

namespace CompassApi; 

public class CompassLearningTask : IComparable<CompassLearningTask> {
    public long Id { get; set; }
    public long ClassId { get; set; }
    public string ClassShortName { get; set; }
    public string ClassName { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreationDateTime { get; set; }
    public CompassLearningTaskAttachment[] Attachments { get; set; }
    public CompassLearningTaskSubmission[] Submissions { get; set; }
    public CompassLearningTaskSubmissionStatus SubmissionStatus { get; set; }
    public bool IsCompleted() => Submissions.Any();
    public bool IsLate() => Submissions.All(s => s.SubmissionTimestamp > DueDate);
    
    public static CompassLearningTaskSubmissionStatus CompassLearningTaskSubmissionStatusFromNumber(int number) {
        return number switch {
            1 => CompassLearningTaskSubmissionStatus.NotSubmitted,
            2 => CompassLearningTaskSubmissionStatus.Overdue,
            3 => CompassLearningTaskSubmissionStatus.OnTime,
            4 => CompassLearningTaskSubmissionStatus.Late,
            _ => CompassLearningTaskSubmissionStatus.Unknown
        };
    }

    public int CompareTo(CompassLearningTask? other) {
        if (other is null) return 1;
        
        // First compare by submission status then due date
        int submissionStatusComparison = CompareStatus(SubmissionStatus, other.SubmissionStatus);
        if (submissionStatusComparison != 0) return submissionStatusComparison;
        
        return DueDate.CompareTo(other.DueDate);
    }

    private static int CompareStatus(CompassLearningTaskSubmissionStatus s1, CompassLearningTaskSubmissionStatus s2) {
        if (s1 == s2) return 0;  // If they are the same, they are equal
        if (s1 == CompassLearningTaskSubmissionStatus.Unknown) return 1;  // Unknown is always less important than anything else
        if (s2 == CompassLearningTaskSubmissionStatus.Unknown) return -1;  // Unknown is always less important than anything else
        
        // Check if any of them are completed
        bool isS1Completed = s1 is CompassLearningTaskSubmissionStatus.OnTime or CompassLearningTaskSubmissionStatus.Late;
        bool isS2Completed = s2 is CompassLearningTaskSubmissionStatus.OnTime or CompassLearningTaskSubmissionStatus.Late;
        
        if (isS1Completed && !isS2Completed) return 1;  // The one that isn't completed is more important
        if (!isS1Completed && isS2Completed) return -1;  // The one that isn't completed is more important
        if (isS1Completed && isS2Completed) return 0;  // If they are both completed, they are equal

        if (s1 == CompassLearningTaskSubmissionStatus.Overdue) return -1;  // Overdue is more important than not submitted
        if (s2 == CompassLearningTaskSubmissionStatus.Overdue) return 1;  // Overdue is more important than not submitted
        
        return 0;
    }
}

public class CompassLearningTaskAttachment {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string FileName { get; set; }
}

public class CompassLearningTaskSubmission {
    public string Id { get; set; }
    public string FileName { get; set; }
    public string DownloadUrl { get; set; }
    public DateTime SubmissionTimestamp { get; set; }
    public CompassLearningTaskSubmissionType Type { get; set; }

    public static CompassLearningTaskSubmissionType CompassLearningTaskSubmissionTypeFromNumber(int number) {
        return number switch {
            4 => CompassLearningTaskSubmissionType.Url,
            1 => CompassLearningTaskSubmissionType.File,
            _ => CompassLearningTaskSubmissionType.Unknown
        };
    }
}

public enum CompassLearningTaskSubmissionType {
    File,
    Url,
    Unknown
}

public enum CompassLearningTaskSubmissionStatus {
    OnTime,
    Late,
    NotSubmitted,
    Overdue,
    Unknown
}