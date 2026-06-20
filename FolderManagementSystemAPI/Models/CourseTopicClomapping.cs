using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class CourseTopicClomapping
{
    public int TopicClomappingId { get; set; }

    public int TopicId { get; set; }

    public int Cloid { get; set; }

    public virtual Clo Clo { get; set; } = null!;

    public virtual CourseTopic Topic { get; set; } = null!;
}
