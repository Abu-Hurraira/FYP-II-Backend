using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FolderManagementSystemAPI.Services;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Models;

public partial class ProgramCourseContentSubmission
{
    [Key]
    [Column("SubmissionID")]
    public int SubmissionId { get; set; }

    [Column("ProgramCourseID")]
    public int ProgramCourseId { get; set; }

    [Column("SessionID")]
    public int SessionId { get; set; }

    [Column("ContentTypeID")]
    public int ContentTypeId { get; set; }

    [Column("SubmittedByUserID")]
    public int SubmittedByUserId { get; set; }

    public DateTime SubmittedAt { get; set; }

    [StringLength(30)]
    public string SubmissionStatus { get; set; } = SubmissionStatuses.Submitted;

    [Column("ReviewedByUserID")]
    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [StringLength(1000)]
    public string? HodFeedback { get; set; }

    public int VersionNo { get; set; }

    public bool IsCurrentApproved { get; set; }

    [ForeignKey("ContentTypeId")]
    public virtual ContentSubmissionType ContentType { get; set; } = null!;

    [ForeignKey("ProgramCourseId")]
    public virtual AcademicProgramCourse ProgramCourse { get; set; } = null!;

    [ForeignKey("SessionId")]
    public virtual AcademicSession Session { get; set; } = null!;

    [ForeignKey("SubmittedByUserId")]
    public virtual User SubmittedByUser { get; set; } = null!;

    [ForeignKey("ReviewedByUserId")]
    public virtual User? ReviewedByUser { get; set; }
}
