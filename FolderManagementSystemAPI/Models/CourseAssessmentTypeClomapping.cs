using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class CourseAssessmentTypeClomapping
{
    public int MappingId { get; set; }

    public int ProgramCourseId { get; set; }

    public int SessionId { get; set; }

    public int AssessmentTypeId { get; set; }

    public int Cloid { get; set; }

    public decimal? Percentage { get; set; }

    public int? UpdatedByUserId { get; set; }

    public string ContentStatus { get; set; } = "Draft";

    public int? SubmissionId { get; set; }

    public virtual AssessmentType AssessmentType { get; set; } = null!;

    public virtual Clo Clo { get; set; } = null!;

    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;
}
