using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

/// <summary>
/// Teacher-entered solid components that are not derived from assessment papers:
/// attendance %, lab-task marks, project marks.
/// </summary>
public class StudentComponentMark
{
    [Key]
    [Column("ComponentMarkID")]
    public int ComponentMarkId { get; set; }

    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    [Column("StudentID")]
    public int StudentId { get; set; }

    /// <summary>0–100 attendance performance used for Homework/Lab attendance shares.</summary>
    public decimal? AttendancePercent { get; set; }

    /// <summary>Obtained lab-task marks (capped by lab scheme LabTaskMarks).</summary>
    public decimal? LabTaskObtained { get; set; }

    /// <summary>Obtained project marks (capped by lab scheme ProjectMarks).</summary>
    public decimal? ProjectObtained { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ExtraMarksJson { get; set; }

    public DateTime UpdatedAt { get; set; }

    [Column("UpdatedByUserID")]
    public int? UpdatedByUserId { get; set; }
}
