using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

[Table("OutcomeCatalogVersions")]
public class OutcomeCatalogVersion
{
    [Key]
    [Column("OutcomeCatalogVersionID")]
    public int OutcomeCatalogVersionId { get; set; }

    /// <summary>PEO or PLO</summary>
    [Required]
    [StringLength(10)]
    public string OutcomeType { get; set; } = string.Empty;

    [Column("SourceProgramID")]
    public int SourceProgramId { get; set; }

    [Required]
    [StringLength(200)]
    public string VersionLabel { get; set; } = string.Empty;

    [StringLength(260)]
    public string? SourceFileName { get; set; }

    public int ItemCount { get; set; }

    [Required]
    [StringLength(64)]
    public string ContentFingerprint { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    [Column("CreatedByUserID")]
    public int? CreatedByUserId { get; set; }

    [ForeignKey(nameof(SourceProgramId))]
    public virtual AcademicProgram? SourceProgram { get; set; }

    public virtual ICollection<OutcomeCatalogVersionItem> Items { get; set; } =
        new List<OutcomeCatalogVersionItem>();
}
