using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

public class StudentCourseResultSelection
{
    [Key]
    [Column("SelectionID")]
    public int SelectionId { get; set; }

    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    [Column("StudentID")]
    public int StudentId { get; set; }

    [Column("AssessmentID")]
    public int AssessmentId { get; set; }

    [StringLength(20)]
    public string Category { get; set; } = string.Empty;
}
