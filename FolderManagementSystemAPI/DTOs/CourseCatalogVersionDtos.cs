namespace FolderManagementSystemAPI.DTOs;

public class CourseCatalogVersionListItemDto
{
    public int CatalogVersionId { get; set; }
    public string VersionLabel { get; set; } = string.Empty;
    public string? SourceFileName { get; set; }
    public int CourseCount { get; set; }
    public int SourceProgramId { get; set; }
    public string SourceProgramName { get; set; } = string.Empty;
    public string? SourceProgramCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsReuse { get; set; }
}

public class CourseCatalogVersionItemDto
{
    public int CatalogVersionItemId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int? ClassCreditHours { get; set; }
    public int? LabCreditHours { get; set; }
    public int DisplayOrder { get; set; }
    public bool AlreadyInProgram { get; set; }
}

public class CourseCatalogVersionDetailDto
{
    public int CatalogVersionId { get; set; }
    public string VersionLabel { get; set; } = string.Empty;
    public string? SourceFileName { get; set; }
    public int CourseCount { get; set; }
    public int SourceProgramId { get; set; }
    public string SourceProgramName { get; set; } = string.Empty;
    public string? SourceProgramCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<CourseCatalogVersionItemDto> Items { get; set; } = new();
}

public class ApplyCourseCatalogVersionDto
{
    public int TargetProgramId { get; set; }
    /// <summary>Version item IDs to include. Null/empty = include all not already linked.</summary>
    public List<int>? SelectedItemIds { get; set; }
}

public class ApplyCourseCatalogVersionResultDto
{
    public int AddedCount { get; set; }
    public int AlreadyLinkedCount { get; set; }
    public int SkippedCount { get; set; }
    public int InsertedCourseCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CatalogVersionSnapshotItem
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int? ClassCreditHours { get; set; }
    public int? LabCreditHours { get; set; }
}

/// <summary>Source program that has offered courses in at least one scheme of study.</summary>
public class CourseLoadSourceProgramDto
{
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string? ProgramCode { get; set; }
    public int SchemeCount { get; set; }
    public int CourseCount { get; set; }
}

/// <summary>Academic session used as scheme of study (e.g. Spring 2026).</summary>
public class CourseLoadSchemeDto
{
    public int SessionId { get; set; }
    public string SchemeLabel { get; set; } = string.Empty;
    public string? AcademicYear { get; set; }
    public DateTime? StartDate { get; set; }
    public int CourseCount { get; set; }
}

public class CourseLoadSchemeCourseDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int? ClassCreditHours { get; set; }
    public int? LabCreditHours { get; set; }
    public bool AlreadyInProgram { get; set; }
}

public class CourseLoadSchemeDetailDto
{
    public int SessionId { get; set; }
    public string SchemeLabel { get; set; } = string.Empty;
    public int SourceProgramId { get; set; }
    public string SourceProgramName { get; set; } = string.Empty;
    public string? SourceProgramCode { get; set; }
    public List<CourseLoadSchemeCourseDto> Items { get; set; } = new();
}

public class ApplyCoursesFromSchemeDto
{
    public int TargetProgramId { get; set; }
    public List<int> CourseIds { get; set; } = new();
}
