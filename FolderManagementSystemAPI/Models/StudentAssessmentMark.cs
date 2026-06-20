using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class StudentAssessmentMark
{
    public int StudentAssessmentMarkId { get; set; }

    public int AssessmentId { get; set; }

    public int StudentId { get; set; }

    public decimal ObtainedMarks { get; set; }

    public virtual Assessment Assessment { get; set; } = null!;
}
