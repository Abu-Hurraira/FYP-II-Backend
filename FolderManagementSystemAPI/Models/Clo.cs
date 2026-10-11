using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class Clo
{
    public int Cloid { get; set; }

    public int ProgramCourseId { get; set; }

    public int SessionId { get; set; }

    public string Clocode { get; set; } = null!;

    public string ContentStatus { get; set; } = "Draft";

    public int? SubmissionId { get; set; }

    public string Clodescription { get; set; } = null!;

    public virtual ICollection<AssessmentQuestionClomapping> AssessmentQuestionClomappings { get; set; } = new List<AssessmentQuestionClomapping>();

    public virtual ICollection<CloattainmentResult> CloattainmentResults { get; set; } = new List<CloattainmentResult>();

    public virtual ICollection<CourseAssessmentTypeClomapping> CourseAssessmentTypeClomappings { get; set; } = new List<CourseAssessmentTypeClomapping>();

    public virtual ICollection<CourseTopicClomapping> CourseTopicClomappings { get; set; } = new List<CourseTopicClomapping>();

    public virtual ICollection<Ploclomapping> Ploclomappings { get; set; } = new List<Ploclomapping>();

    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;
}
