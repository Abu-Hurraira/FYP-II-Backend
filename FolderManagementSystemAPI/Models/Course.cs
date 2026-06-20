using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

public partial class Course
{
    [Key]
    [Column("CourseID")]
    public int CourseId { get; set; }

    [StringLength(200)]
    public string CourseTitle { get; set; } = null!;

    [StringLength(50)]
    public string CourseCode { get; set; } = null!;

    public int? ClassCreditHours { get; set; }

    public int? LabCreditHours { get; set; }

    [InverseProperty("Course")]
    public virtual ICollection<OfferedCourse> OfferedCourses { get; set; } = new List<OfferedCourse>();
}
