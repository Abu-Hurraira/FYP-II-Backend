using System;
using System.Collections.Generic;

namespace WebApplication1.Models.TeacherScaffold;

public partial class AssessmentFile
{
    public int AssessmentFileId { get; set; }

    public int AssessmentId { get; set; }

    public int FileId { get; set; }

    public string FilePurpose { get; set; } = null!;

    public virtual Assessment Assessment { get; set; } = null!;

    public virtual CourseFolderFile File { get; set; } = null!;
}
