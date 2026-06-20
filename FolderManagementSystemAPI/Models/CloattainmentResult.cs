using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class CloattainmentResult
{
    public int AttainmentId { get; set; }

    public int OfferedCourseId { get; set; }

    public int Cloid { get; set; }

    public decimal AttainmentPercentage { get; set; }

    public DateTime CalculatedAt { get; set; }

    public int? CalculatedByUserId { get; set; }

    public virtual Clo Clo { get; set; } = null!;
}
