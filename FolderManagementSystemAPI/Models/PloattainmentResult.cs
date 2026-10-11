using System;
using FolderManagementSystemAPI.Models;

namespace WebApplication1.Models.TeacherScaffold;

public partial class PloattainmentResult
{
    public int AttainmentId { get; set; }

    public int OfferedCourseId { get; set; }

    public int Ploid { get; set; }

    public decimal AttainmentPercentage { get; set; }

    public DateTime CalculatedAt { get; set; }

    public int? CalculatedByUserId { get; set; }

    public virtual Plo Plo { get; set; } = null!;
}
