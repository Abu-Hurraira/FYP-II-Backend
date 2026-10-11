namespace FolderManagementSystemAPI.DTOs;

public class PeoPloMatrixColumnDto
{
    public int PloId { get; set; }
    public string PloCode { get; set; } = string.Empty;
    public string ColumnLabel { get; set; } = string.Empty;
    public string PloDescription { get; set; } = string.Empty;
}

public class PeoPloMatrixCellDto
{
    public int PloId { get; set; }
    public int? MappingId { get; set; }
    public bool IsMapped { get; set; }
}

public class PeoPloMatrixRowDto
{
    public int PeoId { get; set; }
    public string PeoCode { get; set; } = string.Empty;
    public string RowLabel { get; set; } = string.Empty;
    public string PeoDescription { get; set; } = string.Empty;
    public List<PeoPloMatrixCellDto> Cells { get; set; } = new();
}

public class PeoPloMatrixDto
{
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public List<PeoPloMatrixColumnDto> Plos { get; set; } = new();
    public List<PeoPloMatrixRowDto> Peos { get; set; } = new();
}

public class SavePeoPloMatrixCellDto
{
    public int PeoId { get; set; }
    public int PloId { get; set; }
    public bool IsMapped { get; set; }
}

public class SavePeoPloMatrixDto
{
    public int MappedByUserId { get; set; }
    public List<SavePeoPloMatrixCellDto> Cells { get; set; } = new();
}

public class SavePeoPloSingleCellDto
{
    public int MappedByUserId { get; set; }
    public int PeoId { get; set; }
    public int PloId { get; set; }
    public bool IsMapped { get; set; }
}
