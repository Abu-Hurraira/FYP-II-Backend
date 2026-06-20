using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

public partial class AcademicProgram
{
    [Key]
    [Column("ProgramID")]
    public int ProgramId { get; set; }

    [StringLength(150)]
    public string ProgramName { get; set; } = null!;

    [StringLength(30)]
    public string? ProgramCode { get; set; }

    [InverseProperty("Program")]
    public virtual ICollection<AcademicSession> AcademicSessions { get; set; } = new List<AcademicSession>();

    [InverseProperty("Program")]
    public virtual ICollection<Peo> Peos { get; set; } = new List<Peo>();

    [InverseProperty("Program")]
    public virtual ICollection<Plo> Plos { get; set; } = new List<Plo>();
}
