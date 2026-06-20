using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class ProgramCoursePlomapping
{
    public int MappingId { get; set; }

    public int ProgramCourseId { get; set; }

    public int Ploid { get; set; }

    public decimal? Percentage { get; set; }

    public int? MappedByUserId { get; set; }

    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;
}
