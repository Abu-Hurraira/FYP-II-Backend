using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class CourseFolder
{
    public int CourseFolderId { get; set; }

    public int OfferedCourseId { get; set; }

    public string RootFolderName { get; set; } = null!;

    public string RootFolderPath { get; set; } = null!;

    public int CreatedByUserId { get; set; }

    public string FolderStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CourseFolderDirectory> CourseFolderDirectories { get; set; } = new List<CourseFolderDirectory>();

    public virtual ICollection<FolderSubmission> FolderSubmissions { get; set; } = new List<FolderSubmission>();
}
