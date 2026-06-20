using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class AttendanceRecord
{
    public int AttendanceId { get; set; }

    public int OfferedCourseId { get; set; }

    public int StudentId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public string Status { get; set; } = null!;
}
