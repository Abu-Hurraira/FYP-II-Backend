using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

[Table("HodUploadAllocations")]
public class HodUploadAllocation
{
    [Key]
    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    public bool Clos { get; set; }

    public bool CloPloMapping { get; set; }

    public bool CloTaskMapping { get; set; }

    public bool Mid { get; set; }

    public bool Final { get; set; }

    public bool FolderTemplate { get; set; }

    [Column("UpdatedByHodUserID")]
    public int? UpdatedByHodUserId { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(OfferedCourseId))]
    public virtual OfferedCourse OfferedCourse { get; set; } = null!;
}
