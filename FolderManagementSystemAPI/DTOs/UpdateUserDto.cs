namespace FolderManagementSystemAPI.DTOs
{
    public class UpdateUserDto
    {
        public string FullName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public int RoleId { get; set; }

        public string? Password { get; set; }

        public string? Rank { get; set; }

        public string? AridNo { get; set; }
    }
}
