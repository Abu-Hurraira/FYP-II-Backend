using FolderManagementSystemAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NotificationsController : ControllerBase
{
    private readonly FolderManagementDbContext _context;

    public NotificationsController(FolderManagementDbContext context)
    {
        _context = context;
    }

    [HttpGet("user/{userId:int}")]
    public async Task<IActionResult> GetUserNotifications(int userId)
    {
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.NotificationId,
                n.UserId,
                n.NotificationTitle,
                n.NotificationMessage,
                n.NotificationType,
                n.RelatedId,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPatch("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

        if (notification == null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Marked as read.", notification.NotificationId, notification.IsRead });
    }

    [HttpPatch("user/{userId:int}/read-all")]
    public async Task<IActionResult> MarkAllRead(int userId)
    {
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        if (unread.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "All notifications marked as read.", updatedCount = unread.Count });
    }
}
