using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

public partial class Section
{
    [Key]
    [Column("SectionID")]
    public int SectionId { get; set; }

    [Column("SessionID")]
    public int SessionId { get; set; }

    [StringLength(50)]
    public string SectionName { get; set; } = null!;

    public int? Semester { get; set; }

    [StringLength(500)]
    public string? StudentListFilePath { get; set; }

    [StringLength(500)]
    public string? AttendanceSheetPath { get; set; }

    [InverseProperty("Section")]
    public virtual ICollection<OfferedCourse> OfferedCourses { get; set; } = new List<OfferedCourse>();

    [ForeignKey("SessionId")]
    [InverseProperty("Sections")]
    public virtual AcademicSession Session { get; set; } = null!;

    [InverseProperty("Section")]
    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}
