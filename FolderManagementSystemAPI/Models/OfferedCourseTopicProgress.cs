using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class OfferedCourseTopicProgress
{
    public int ProgressId { get; set; }

    public int OfferedCourseId { get; set; }

    public int TopicId { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? CompletedByUserId { get; set; }

    public virtual CourseTopic Topic { get; set; } = null!;
}
