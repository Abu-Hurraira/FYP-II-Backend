using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class CourseTopic
{
    public int TopicId { get; set; }

    public int ProgramCourseId { get; set; }

    public int? ParentTopicId { get; set; }

    public int? WeekNo { get; set; }

    public string TopicTitle { get; set; } = null!;

    public string? TopicDescription { get; set; }

    public int? DisplayOrder { get; set; }

    public virtual ICollection<CourseTopicClomapping> CourseTopicClomappings { get; set; } = new List<CourseTopicClomapping>();

    public virtual ICollection<CourseTopic> InverseParentTopic { get; set; } = new List<CourseTopic>();

    public virtual ICollection<OfferedCourseTopicProgress> OfferedCourseTopicProgresses { get; set; } = new List<OfferedCourseTopicProgress>();

    public virtual CourseTopic? ParentTopic { get; set; }

    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;
}
