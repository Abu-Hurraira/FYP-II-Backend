using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class TeacherPermission
{
    public int PermissionId { get; set; }

    public int TeacherUserId { get; set; }

    public int ProgramCourseId { get; set; }

    public int PermissionTypeId { get; set; }

    public int? HoduserId { get; set; }

    public bool IsAllowed { get; set; }

    public virtual TeacherPermissionType PermissionType { get; set; } = null!;

    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;
}
