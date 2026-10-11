namespace FolderManagementSystemAPI.DTOs;

public class HodUploadAllocationFlagsDto
{
    public bool Clos { get; set; }
    public bool CloPloMapping { get; set; }
    public bool CloTaskMapping { get; set; }
    public bool Mid { get; set; }
    public bool Final { get; set; }
    public bool FolderTemplate { get; set; }
}

public class HodUploadAllocationItemDto : HodUploadAllocationFlagsDto
{
    public int OfferedCourseId { get; set; }
}

public class SaveHodUploadAllocationDto
{
    public int HodUserId { get; set; }
    public bool Configured { get; set; }
    public List<HodUploadAllocationItemDto>? Items { get; set; }
    public Dictionary<string, HodUploadAllocationFlagsDto>? Allocations { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
