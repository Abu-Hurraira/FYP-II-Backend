using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

[Index(nameof(GraderAllocationId), nameof(OfferedCourseId), nameof(TaskCode), IsUnique = true)]
public class GraderCourseTask
{
    [Key]
    [Column("GraderCourseTaskID")]
    public int GraderCourseTaskId { get; set; }

    [Column("GraderAllocationID")]
    public int GraderAllocationId { get; set; }

    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    [StringLength(50)]
    public string TaskCode { get; set; } = string.Empty;

    [StringLength(200)]
    public string? TaskTitle { get; set; }

    [Column("AssignedByUserID")]
    public int AssignedByUserId { get; set; }

    public DateTime AssignedAt { get; set; }

    [ForeignKey(nameof(GraderAllocationId))]
    public virtual GraderAllocation GraderAllocation { get; set; } = null!;

    [ForeignKey(nameof(OfferedCourseId))]
    public virtual OfferedCourse OfferedCourse { get; set; } = null!;
}
