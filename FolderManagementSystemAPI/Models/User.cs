using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

[Index("Email", Name = "UQ__Users__A9D10534BC5E061E", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("UserID")]
    public int UserId { get; set; }

    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [StringLength(150)]
    public string Email { get; set; } = null!;

    [StringLength(500)]
    public string PasswordHash { get; set; } = null!;

    [Column("RoleID")]
    public int RoleId { get; set; }

    [StringLength(100)]
    public string? Rank { get; set; }

    [StringLength(50)]
    public string? AridNo { get; set; }

    [InverseProperty("GraderUser")]
    public virtual ICollection<GraderAllocation> GraderAllocationGraderUsers { get; set; } = new List<GraderAllocation>();

    [InverseProperty("TeacherUser")]
    public virtual ICollection<GraderAllocation> GraderAllocationTeacherUsers { get; set; } = new List<GraderAllocation>();

    [InverseProperty("TeacherUser")]
    public virtual ICollection<OfferedCourse> OfferedCourses { get; set; } = new List<OfferedCourse>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role Role { get; set; } = null!;
}
