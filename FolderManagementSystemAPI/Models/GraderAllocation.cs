using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

public partial class GraderAllocation
{
    [Key]
    [Column("GraderAllocationID")]
    public int GraderAllocationId { get; set; }

    [Column("TeacherUserID")]
    public int TeacherUserId { get; set; }

    [Column("GraderUserID")]
    public int GraderUserId { get; set; }

    [ForeignKey("GraderUserId")]
    [InverseProperty("GraderAllocationGraderUsers")]
    public virtual User GraderUser { get; set; } = null!;

    [ForeignKey("TeacherUserId")]
    [InverseProperty("GraderAllocationTeacherUsers")]
    public virtual User TeacherUser { get; set; } = null!;

    public virtual ICollection<GraderCourseTask> GraderCourseTasks { get; set; } = new List<GraderCourseTask>();
}
