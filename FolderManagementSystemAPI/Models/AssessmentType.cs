using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class AssessmentType
{
    public int AssessmentTypeId { get; set; }

    public string AssessmentTypeName { get; set; } = null!;

    public virtual ICollection<AssessmentWeightage> AssessmentWeightages { get; set; } = new List<AssessmentWeightage>();

    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();

    public virtual ICollection<CourseAssessmentTypeClomapping> CourseAssessmentTypeClomappings { get; set; } = new List<CourseAssessmentTypeClomapping>();
}
