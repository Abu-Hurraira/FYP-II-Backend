using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

public class StudentCourseResult
{
    [Key]
    [Column("ResultID")]
    public int ResultId { get; set; }

    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    [Column("StudentID")]
    public int StudentId { get; set; }

    public decimal QuizMarks { get; set; }

    public decimal HomeworkMarks { get; set; }

    public decimal MidMarks { get; set; }

    public decimal LabMarks { get; set; }

    public decimal FinalTheoryMarks { get; set; }

    public decimal FinalLabMarks { get; set; }

    public decimal TotalObtained { get; set; }

    public decimal TotalMax { get; set; }

    public decimal Percentage { get; set; }

    public DateTime GeneratedAt { get; set; }

    [Column("GeneratedByUserID")]
    public int GeneratedByUserId { get; set; }
}
