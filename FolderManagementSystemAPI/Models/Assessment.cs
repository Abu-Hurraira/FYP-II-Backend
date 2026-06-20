using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class Assessment
{
    public int AssessmentId { get; set; }

    public int OfferedCourseId { get; set; }

    public int AssessmentTypeId { get; set; }

    public int? AssessmentNo { get; set; }

    public string Title { get; set; } = null!;

    public DateOnly? DueDate { get; set; }

    public decimal? TotalMarks { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AssessmentFile> AssessmentFiles { get; set; } = new List<AssessmentFile>();

    public virtual ICollection<AssessmentQuestion> AssessmentQuestions { get; set; } = new List<AssessmentQuestion>();

    public virtual AssessmentType AssessmentType { get; set; } = null!;

    public virtual ICollection<GraderTask> GraderTasks { get; set; } = new List<GraderTask>();

    public virtual ICollection<StudentAssessmentMark> StudentAssessmentMarks { get; set; } = new List<StudentAssessmentMark>();
}
