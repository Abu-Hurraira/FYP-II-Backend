using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

[Table("OutcomeCatalogVersionItems")]
public class OutcomeCatalogVersionItem
{
    [Key]
    [Column("OutcomeCatalogVersionItemID")]
    public int OutcomeCatalogVersionItemId { get; set; }

    [Column("OutcomeCatalogVersionID")]
    public int OutcomeCatalogVersionId { get; set; }

    [Required]
    [StringLength(20)]
    public string ItemCode { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string ItemDescription { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(OutcomeCatalogVersionId))]
    public virtual OutcomeCatalogVersion? Version { get; set; }
}
