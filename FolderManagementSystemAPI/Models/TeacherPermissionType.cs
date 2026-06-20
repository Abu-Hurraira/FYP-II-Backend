using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class TeacherPermissionType
{
    public int PermissionTypeId { get; set; }

    public string PermissionType { get; set; } = null!;

    public virtual ICollection<TeacherPermission> TeacherPermissions { get; set; } = new List<TeacherPermission>();
}
