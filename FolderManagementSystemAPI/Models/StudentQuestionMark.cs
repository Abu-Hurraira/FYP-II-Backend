using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class StudentQuestionMark
{
    public int StudentQuestionMarkId { get; set; }

    public int QuestionId { get; set; }

    public int StudentId { get; set; }

    public decimal ObtainedMarks { get; set; }

    public virtual AssessmentQuestion Question { get; set; } = null!;
}
