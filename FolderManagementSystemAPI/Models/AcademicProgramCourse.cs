using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class AcademicProgramCourse
{
    public int ProgramCourseId { get; set; }

    public int ProgramId { get; set; }

    public int CourseId { get; set; }

    public virtual ICollection<Clo> Clos { get; set; } = new List<Clo>();

    public virtual ICollection<CourseAssessmentTypeClomapping> CourseAssessmentTypeClomappings { get; set; } = new List<CourseAssessmentTypeClomapping>();

    public virtual ICollection<CourseTopic> CourseTopics { get; set; } = new List<CourseTopic>();

    public virtual ICollection<Ploclomapping> Ploclomappings { get; set; } = new List<Ploclomapping>();

    public virtual ICollection<ProgramCoursePlomapping> ProgramCoursePlomappings { get; set; } = new List<ProgramCoursePlomapping>();

    public virtual ICollection<TeacherPermission> TeacherPermissions { get; set; } = new List<TeacherPermission>();
}
