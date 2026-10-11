using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

[Table("CourseCatalogVersions")]
public class CourseCatalogVersion
{
    [Key]
    [Column("CatalogVersionID")]
    public int CatalogVersionId { get; set; }

    [Column("SourceProgramID")]
    public int SourceProgramId { get; set; }

    [Required]
    [StringLength(200)]
    public string VersionLabel { get; set; } = string.Empty;

    [StringLength(260)]
    public string? SourceFileName { get; set; }

    public int CourseCount { get; set; }

    [Required]
    [StringLength(64)]
    public string ContentFingerprint { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    [Column("CreatedByUserID")]
    public int? CreatedByUserId { get; set; }

    [ForeignKey(nameof(SourceProgramId))]
    public virtual AcademicProgram? SourceProgram { get; set; }

    public virtual ICollection<CourseCatalogVersionItem> Items { get; set; } =
        new List<CourseCatalogVersionItem>();
}
