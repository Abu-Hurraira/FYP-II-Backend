using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class CourseFolderDirectory
{
    public int DirectoryId { get; set; }

    public int CourseFolderId { get; set; }

    public int? ParentDirectoryId { get; set; }

    public int? TemplateItemId { get; set; }

    public string DirectoryName { get; set; } = null!;

    public string DirectoryPath { get; set; } = null!;

    public virtual CourseFolder CourseFolder { get; set; } = null!;

    public virtual ICollection<CourseFolderFile> CourseFolderFiles { get; set; } = new List<CourseFolderFile>();

    public virtual ICollection<CourseFolderDirectory> InverseParentDirectory { get; set; } = new List<CourseFolderDirectory>();

    public virtual CourseFolderDirectory? ParentDirectory { get; set; }

    public virtual CourseFolderTemplateItem? TemplateItem { get; set; }
}
