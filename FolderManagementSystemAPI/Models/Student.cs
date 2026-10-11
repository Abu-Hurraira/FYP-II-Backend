using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

public partial class Student
{
    [Key]
    [Column("StudentID")]
    public int StudentId { get; set; }

    [Column("SectionID")]
    public int SectionId { get; set; }

    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [StringLength(50)]
    public string AridNo { get; set; } = null!;

    [StringLength(150)]
    public string? Email { get; set; }

    [ForeignKey("SectionId")]
    [InverseProperty("Students")]
    public virtual Section Section { get; set; } = null!;

    public virtual ICollection<StudentOfferedCourse> StudentOfferedCourses { get; set; } =
        new List<StudentOfferedCourse>();
}
