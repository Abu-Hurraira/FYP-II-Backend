using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class FolderSubmission
{
    public int SubmissionId { get; set; }

    public int CourseFolderId { get; set; }

    public int SubmittedByUserId { get; set; }

    public string SubmissionStatus { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public string? Remarks { get; set; }

    public virtual CourseFolder CourseFolder { get; set; } = null!;

    public virtual ICollection<FolderSubmissionIssue> FolderSubmissionIssues { get; set; } = new List<FolderSubmissionIssue>();
}
