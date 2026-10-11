using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

[Table("CourseCatalogVersionItems")]
public class CourseCatalogVersionItem
{
    [Key]
    [Column("CatalogVersionItemID")]
    public int CatalogVersionItemId { get; set; }

    [Column("CatalogVersionID")]
    public int CatalogVersionId { get; set; }

    [Required]
    [StringLength(50)]
    public string CourseCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string CourseTitle { get; set; } = string.Empty;

    public int? ClassCreditHours { get; set; }

    public int? LabCreditHours { get; set; }

    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(CatalogVersionId))]
    public virtual CourseCatalogVersion? Version { get; set; }
}
