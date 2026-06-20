using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class CourseFolderFile
{
    public int FileId { get; set; }

    public int DirectoryId { get; set; }

    public int UploadedByUserId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string FileCategory { get; set; } = null!;

    public string? Remarks { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual ICollection<AssessmentFile> AssessmentFiles { get; set; } = new List<AssessmentFile>();

    public virtual CourseFolderDirectory Directory { get; set; } = null!;

    public virtual ICollection<GraderTask> GraderTasks { get; set; } = new List<GraderTask>();

    public virtual ICollection<LessonPlan> LessonPlans { get; set; } = new List<LessonPlan>();
}
