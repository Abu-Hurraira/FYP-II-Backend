using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

/// <summary>
/// Links a student to a specific offered course (section + subject).
/// Mark sheets and attainment use this roster, not the full section list.
/// </summary>
public partial class StudentOfferedCourse
{
    [Key]
    [Column("StudentOfferedCourseID")]
    public int StudentOfferedCourseId { get; set; }

    [Column("StudentID")]
    public int StudentId { get; set; }

    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public virtual Student Student { get; set; } = null!;

    [ForeignKey(nameof(OfferedCourseId))]
    public virtual OfferedCourse OfferedCourse { get; set; } = null!;
}
