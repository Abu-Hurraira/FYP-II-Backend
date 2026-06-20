using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class AssessmentQuestionClomapping
{
    public int QuestionClomappingId { get; set; }

    public int QuestionId { get; set; }

    public int Cloid { get; set; }

    public decimal? Percentage { get; set; }

    public virtual Clo Clo { get; set; } = null!;

    public virtual AssessmentQuestion Question { get; set; } = null!;
}
