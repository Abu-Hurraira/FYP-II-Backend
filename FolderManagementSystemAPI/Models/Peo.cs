using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

[Table("PEOs")]
public partial class Peo
{
    [Key]
    [Column("PEOID")]
    public int Peoid { get; set; }

    [Column("ProgramID")]
    public int ProgramId { get; set; }

    [Column("PEOCode")]
    [StringLength(20)]
    public string Peocode { get; set; } = null!;

    [Column("PEODescription")]
    [StringLength(1000)]
    public string Peodescription { get; set; } = null!;

    [ForeignKey("ProgramId")]
    [InverseProperty("Peos")]
    public virtual AcademicProgram Program { get; set; } = null!;
}
