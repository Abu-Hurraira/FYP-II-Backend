using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class PeoPlomapping
{
    public int MappingId { get; set; }

    public int Peoid { get; set; }

    public int Ploid { get; set; }

    public bool IsMapped { get; set; }

    public int? MappedByUserId { get; set; }
}
