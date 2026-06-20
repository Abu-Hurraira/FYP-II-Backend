using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class GraderTask
{
    public int GraderTaskId { get; set; }

    public int AssessmentId { get; set; }

    public int? SourceFileId { get; set; }

    public int AssignedByUserId { get; set; }

    public int GraderAllocationId { get; set; }

    public string? Instructions { get; set; }

    public string TaskStatus { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

    public virtual Assessment Assessment { get; set; } = null!;

    public virtual CourseFolderFile? SourceFile { get; set; }
}
