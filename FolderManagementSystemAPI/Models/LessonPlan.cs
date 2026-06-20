using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class LessonPlan
{
    public int LessonPlanId { get; set; }

    public int OfferedCourseId { get; set; }

    public string LessonPlanNo { get; set; } = null!;

    public string LessonPlanTitle { get; set; } = null!;

    public string? Topics { get; set; }

    public int? FileId { get; set; }

    public int UploadedByUserId { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual CourseFolderFile? File { get; set; }
}
