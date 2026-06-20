using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class AssessmentWeightage
{
    public int WeightageId { get; set; }

    public int OfferedCourseId { get; set; }

    public int AssessmentTypeId { get; set; }

    public decimal WeightagePercentage { get; set; }

    public virtual AssessmentType AssessmentType { get; set; } = null!;
}
