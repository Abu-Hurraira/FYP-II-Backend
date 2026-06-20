using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class AssessmentQuestion
{
    public int QuestionId { get; set; }

    public int AssessmentId { get; set; }

    public string QuestionNo { get; set; } = null!;

    public string QuestionText { get; set; } = null!;

    public decimal? Marks { get; set; }

    public virtual Assessment Assessment { get; set; } = null!;

    public virtual ICollection<AssessmentQuestionClomapping> AssessmentQuestionClomappings { get; set; } = new List<AssessmentQuestionClomapping>();

    public virtual ICollection<StudentQuestionMark> StudentQuestionMarks { get; set; } = new List<StudentQuestionMark>();
}
