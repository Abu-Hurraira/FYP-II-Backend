using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

public partial class AcademicSession
{
    [Key]
    [Column("SessionID")]
    public int SessionId { get; set; }

    [Column("ProgramID")]
    public int ProgramId { get; set; }

    [StringLength(100)]
    public string SessionTitle { get; set; } = null!;

    [StringLength(50)]
    public string? AcademicYear { get; set; }

    [ForeignKey("ProgramId")]
    [InverseProperty("AcademicSessions")]
    public virtual AcademicProgram Program { get; set; } = null!;

    [InverseProperty("Session")]
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
}
