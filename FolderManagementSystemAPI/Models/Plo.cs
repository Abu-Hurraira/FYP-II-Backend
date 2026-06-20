using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Models;

[Table("PLOs")]
public partial class Plo
{
    [Key]
    [Column("PLOID")]
    public int Ploid { get; set; }

    [Column("ProgramID")]
    public int ProgramId { get; set; }

    [Column("PLOCode")]
    [StringLength(20)]
    public string Plocode { get; set; } = null!;

    [Column("PLODescription")]
    [StringLength(1000)]
    public string Plodescription { get; set; } = null!;

    [ForeignKey("ProgramId")]
    [InverseProperty("Plos")]
    public virtual AcademicProgram Program { get; set; } = null!;
}
