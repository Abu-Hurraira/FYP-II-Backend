using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.TeacherScaffold;

/// <summary>
/// Extra CLO–Task matrix task types enabled for a program-course + session
/// (beyond the built-in Assignment / Quiz / Mid / Final).
/// </summary>
public partial class CourseSessionTaskType
{
    [Key]
    [Column("CourseSessionTaskTypeID")]
    public int CourseSessionTaskTypeId { get; set; }

    [Column("ProgramCourseID")]
    public int ProgramCourseId { get; set; }

    [Column("SessionID")]
    public int SessionId { get; set; }

    [Column("AssessmentTypeID")]
    public int AssessmentTypeId { get; set; }

    public int? DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AssessmentType AssessmentType { get; set; } = null!;

    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;
}
