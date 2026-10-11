using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class NotificationService
{
    private readonly FolderManagementDbContext _context;

    public NotificationService(FolderManagementDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> CreateAsync(
        int userId,
        string title,
        string message,
        string type,
        int? relatedId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            NotificationTitle = title?.Trim() ?? string.Empty,
            NotificationMessage = message?.Trim() ?? string.Empty,
            NotificationType = string.IsNullOrWhiteSpace(type) ? null : type.Trim(),
            RelatedId = relatedId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task NotifyHodUsersAsync(
        string title,
        string message,
        string type,
        int? relatedId = null)
    {
        var hodUserIds = await _context.Users
            .AsNoTracking()
            .Where(u => u.Role.RoleName == "HOD")
            .Select(u => u.UserId)
            .ToListAsync();

        foreach (var hodUserId in hodUserIds)
        {
            await CreateAsync(hodUserId, title, message, type, relatedId);
        }
    }
}
