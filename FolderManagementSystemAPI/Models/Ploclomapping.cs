using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class Ploclomapping
{
    public int MappingId { get; set; }

    public int ProgramCourseId { get; set; }

    public int Ploid { get; set; }

    public int Cloid { get; set; }

    public decimal? Percentage { get; set; }

    public int? UpdatedByUserId { get; set; }

    public virtual Clo Clo { get; set; } = null!;

    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;
}
