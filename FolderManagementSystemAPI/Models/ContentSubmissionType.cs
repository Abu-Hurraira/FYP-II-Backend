using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FolderManagementSystemAPI.Models;

public partial class ContentSubmissionType
{
    [Key]
    [Column("ContentTypeID")]
    public int ContentTypeId { get; set; }

    [StringLength(50)]
    public string ContentTypeCode { get; set; } = null!;

    [StringLength(100)]
    public string ContentTypeName { get; set; } = null!;

    public int SortOrder { get; set; }

    [InverseProperty("ContentType")]
    public virtual ICollection<ProgramCourseContentSubmission> Submissions { get; set; } = new List<ProgramCourseContentSubmission>();
}
