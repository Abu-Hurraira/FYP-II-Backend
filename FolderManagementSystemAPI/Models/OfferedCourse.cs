using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

public partial class OfferedCourse
{
    [Key]
    [Column("OfferedCourseID")]
    public int OfferedCourseId { get; set; }

    [Column("SectionID")]
    public int SectionId { get; set; }

    [Column("CourseID")]
    public int CourseId { get; set; }

    [Column("TeacherUserID")]
    public int TeacherUserId { get; set; }

    [ForeignKey("CourseId")]
    [InverseProperty("OfferedCourses")]
    public virtual Course Course { get; set; } = null!;

    [ForeignKey("SectionId")]
    [InverseProperty("OfferedCourses")]
    public virtual Section Section { get; set; } = null!;

    [ForeignKey("TeacherUserId")]
    [InverseProperty("OfferedCourses")]
    public virtual User TeacherUser { get; set; } = null!;

    public virtual ICollection<StudentOfferedCourse> StudentOfferedCourses { get; set; } =
        new List<StudentOfferedCourse>();

    public virtual ICollection<GraderCourseTask> GraderCourseTasks { get; set; } = new List<GraderCourseTask>();
}
