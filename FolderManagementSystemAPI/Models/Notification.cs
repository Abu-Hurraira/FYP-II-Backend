using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string NotificationTitle { get; set; } = null!;

    public string NotificationMessage { get; set; } = null!;

    public string? NotificationType { get; set; }

    public int? RelatedId { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
