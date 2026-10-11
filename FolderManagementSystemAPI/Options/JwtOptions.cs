namespace FolderManagementSystemAPI.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "FolderManagementSystemAPI";
    public string Audience { get; set; } = "FolderManagementSystemApp";
    /// <summary>Access token lifetime in hours.</summary>
    public int ExpireHours { get; set; } = 12;
}
