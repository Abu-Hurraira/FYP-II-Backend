using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

public class OfferedCourseResultScheme
{
    [Key]
    [Column("SchemeID")]
    public int SchemeId { get; set; }

    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    public decimal TotalMarks { get; set; }

    public decimal QuizMarks { get; set; }

    public decimal HomeworkMarks { get; set; }

    public decimal MidMarks { get; set; }

    public decimal LabMarks { get; set; }

    public decimal FinalTheoryMarks { get; set; }

    public decimal FinalLabMarks { get; set; }

    public int AssignmentBestOf { get; set; }

    public int QuizBestOf { get; set; }

    public decimal AttendanceHomeworkMarks { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [Column("UpdatedByUserID")]
    public int? UpdatedByUserId { get; set; }

    public DateTime? LastGeneratedAt { get; set; }

    [Column("LastGeneratedByUserID")]
    public int? LastGeneratedByUserId { get; set; }
}
