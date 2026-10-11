namespace FolderManagementSystemAPI.DTOs;

public static class OutcomeCatalogTypes
{
    public const string Peo = "PEO";
    public const string Plo = "PLO";

    public static bool IsValid(string? type) =>
        string.Equals(type, Peo, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, Plo, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string type) =>
        string.Equals(type, Plo, StringComparison.OrdinalIgnoreCase) ? Plo : Peo;
}

public class OutcomeCatalogVersionListItemDto
{
    public int OutcomeCatalogVersionId { get; set; }
    public string OutcomeType { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string? SourceFileName { get; set; }
    public int ItemCount { get; set; }
    public int SourceProgramId { get; set; }
    public string SourceProgramName { get; set; } = string.Empty;
    public string? SourceProgramCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OutcomeCatalogVersionItemDto
{
    public int OutcomeCatalogVersionItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool AlreadyInProgram { get; set; }
}

public class OutcomeCatalogVersionDetailDto
{
    public int OutcomeCatalogVersionId { get; set; }
    public string OutcomeType { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string? SourceFileName { get; set; }
    public int ItemCount { get; set; }
    public int SourceProgramId { get; set; }
    public string SourceProgramName { get; set; } = string.Empty;
    public string? SourceProgramCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OutcomeCatalogVersionItemDto> Items { get; set; } = new();
}

public class ApplyOutcomeCatalogVersionDto
{
    public int TargetProgramId { get; set; }
    public List<int>? SelectedItemIds { get; set; }
}

public class ApplyOutcomeCatalogVersionResultDto
{
    public int AddedCount { get; set; }
    public int AlreadyLinkedCount { get; set; }
    public int SkippedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class OutcomeCatalogSnapshotItem
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
}
