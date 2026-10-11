using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

[Table("FcarReportInputs")]
public class FcarReportInput
{
    [Key]
    [Column("FcarReportInputID")]
    public int FcarReportInputId { get; set; }

    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    [Required]
    public string CatalogDescription { get; set; } = string.Empty;

    [Required]
    public string CourseModifications { get; set; } = "No modification";

    public string? StudentFeedbackText { get; set; }
    public string? ReflectionText { get; set; }
    public string? ProposedActionsText { get; set; }
    public DateTime UpdatedAt { get; set; }

    [Column("UpdatedByUserID")]
    public int UpdatedByUserId { get; set; }

    [ForeignKey(nameof(OfferedCourseId))]
    public virtual OfferedCourse OfferedCourse { get; set; } = null!;

    [ForeignKey(nameof(UpdatedByUserId))]
    public virtual User UpdatedByUser { get; set; } = null!;
}
