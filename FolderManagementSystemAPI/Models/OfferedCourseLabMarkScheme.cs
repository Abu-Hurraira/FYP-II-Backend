using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

public class OfferedCourseLabMarkScheme
{
    [Key]
    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    public decimal QuizMarks { get; set; }

    public decimal AssignmentMarks { get; set; }

    public decimal LabTaskMarks { get; set; }

    public decimal ProjectMarks { get; set; }

    public decimal AttendanceMarks { get; set; }

    public decimal ExamMarks { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ExtraItemsJson { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [Column("UpdatedByUserID")]
    public int? UpdatedByUserId { get; set; }
}
