using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class FolderSubmissionIssue
{
    public int IssueId { get; set; }

    public int SubmissionId { get; set; }

    public string IssueMessage { get; set; } = null!;

    public bool IsResolved { get; set; }

    public virtual FolderSubmission Submission { get; set; } = null!;
}
