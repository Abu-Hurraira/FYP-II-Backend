using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class CourseFolderTemplateItem
{
    public int TemplateItemId { get; set; }

    public string TemplateCode { get; set; } = null!;

    public string FolderItemName { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; }

    public virtual ICollection<CourseFolderDirectory> CourseFolderDirectories { get; set; } = new List<CourseFolderDirectory>();
}
