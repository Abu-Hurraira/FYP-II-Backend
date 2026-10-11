using System;
using System.Collections.Generic;
using FolderManagementSystemAPI.Models;

namespace WebApplication1.Models.TeacherScaffold;

public partial class Ploclomapping
{
    public int MappingId { get; set; }

    public int ProgramCourseId { get; set; }

    public int SessionId { get; set; }

    public int Ploid { get; set; }

    public int Cloid { get; set; }

    public decimal? Percentage { get; set; }

    public int? UpdatedByUserId { get; set; }

    public string ContentStatus { get; set; } = "Draft";

    public int? SubmissionId { get; set; }

    public virtual Clo Clo { get; set; } = null!;

    public virtual Plo Plo { get; set; } = null!;

    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;
}
