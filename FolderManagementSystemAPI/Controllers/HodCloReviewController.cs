using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HodCloReviewController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly ContentSubmissionService _contentSubmissionService;
    private readonly NotificationService _notificationService;

    public HodCloReviewController(
        FolderManagementDbContext context,
        ContentSubmissionService contentSubmissionService,
        NotificationService notificationService)
    {
        _context = context;
        _contentSubmissionService = contentSubmissionService;
        _notificationService = notificationService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        await _contentSubmissionService.EnsureContentTypesAsync();

        var pendingSubmissions = await (
            from submission in _context.ProgramCourseContentSubmissions.AsNoTracking()
            join type in _context.ContentSubmissionTypes.AsNoTracking()
                on submission.ContentTypeId equals type.ContentTypeId
            join programCourse in _context.AcademicProgramCourses.AsNoTracking()
                on submission.ProgramCourseId equals programCourse.ProgramCourseId
            join course in _context.Courses.AsNoTracking()
                on programCourse.CourseId equals course.CourseId
            join program in _context.AcademicPrograms.AsNoTracking()
                on programCourse.ProgramId equals program.ProgramId
            join submitter in _context.Users.AsNoTracking()
                on submission.SubmittedByUserId equals submitter.UserId
            where submission.SubmissionStatus == SubmissionStatuses.Submitted
                  && type.ContentTypeCode == ContentSubmissionTypeCodes.Clos
            orderby submission.SubmittedAt descending
            select new
            {
                submission.SubmissionId,
                submission.ProgramCourseId,
                submission.SessionId,
                submission.SubmittedByUserId,
                submission.SubmittedAt,
                CourseId = course.CourseId,
                CourseTitle = course.CourseTitle,
                CourseCode = course.CourseCode,
                ProgramId = program.ProgramId,
                ProgramName = program.ProgramName,
                SubmittedByName = submitter.FullName
            }).ToListAsync();

        // One card per program course (latest submitted CLOs package).
        var latestByProgramCourse = pendingSubmissions
            .GroupBy(x => x.ProgramCourseId)
            .Select(g => g.OrderByDescending(x => x.SubmittedAt).First())
            .ToList();

        var results = new List<object>();

        foreach (var row in latestByProgramCourse)
        {
            var pendingClos = await _context.Clos
                .AsNoTracking()
                .Where(c =>
                    c.ProgramCourseId == row.ProgramCourseId &&
                    c.SessionId == row.SessionId &&
                    c.ContentStatus == ContentStatuses.PendingReview)
                .OrderBy(c => c.Clocode)
                .Select(c => new
                {
                    cloId = c.Cloid,
                    cloCode = c.Clocode,
                    cloDescription = c.Clodescription,
                    approvalStatus = "Pending",
                    submittedAt = row.SubmittedAt
                })
                .ToListAsync();

            results.Add(new
            {
                programCourseId = row.ProgramCourseId,
                courseId = row.CourseId,
                courseTitle = row.CourseTitle,
                courseCode = row.CourseCode,
                programId = row.ProgramId,
                programName = row.ProgramName,
                submittedByUserId = row.SubmittedByUserId,
                submittedByName = row.SubmittedByName,
                submittedAt = row.SubmittedAt,
                pendingCount = pendingClos.Count,
                clos = pendingClos
            });
        }

        return Ok(results);
    }

    [HttpPost("program-course/{programCourseId:int}/approve")]
    public async Task<IActionResult> Approve(
        int programCourseId,
        [FromQuery] int hodUserId)
    {
        try
        {
            var submission = await FindLatestSubmittedClosAsync(programCourseId);
            if (submission == null)
            {
                return BadRequest(new { message = "No pending CLO submission found for this program course." });
            }

            var result = await _contentSubmissionService.ApproveAsync(
                submission.SubmissionId,
                new ReviewContentSubmissionDto { HodUserId = hodUserId });

            await NotifySubmitterAsync(
                submission,
                "CLOs approved",
                "Your CLO submission was approved by the HOD.");

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("program-course/{programCourseId:int}/reject")]
    public async Task<IActionResult> Reject(
        int programCourseId,
        [FromQuery] int hodUserId,
        [FromBody] HodCloRejectDto? body)
    {
        try
        {
            var submission = await FindLatestSubmittedClosAsync(programCourseId);
            if (submission == null)
            {
                return BadRequest(new { message = "No pending CLO submission found for this program course." });
            }

            var reason = body?.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { message = "Rejection reason is required." });
            }

            var result = await _contentSubmissionService.RejectAsync(
                submission.SubmissionId,
                new ReviewContentSubmissionDto
                {
                    HodUserId = hodUserId,
                    HodFeedback = reason
                });

            await NotifySubmitterAsync(
                submission,
                "CLOs rejected",
                $"Your CLO submission was rejected: {reason}");

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<ProgramCourseContentSubmission?> FindLatestSubmittedClosAsync(int programCourseId)
    {
        await _contentSubmissionService.EnsureContentTypesAsync();

        return await (
            from submission in _context.ProgramCourseContentSubmissions
            join type in _context.ContentSubmissionTypes on submission.ContentTypeId equals type.ContentTypeId
            where submission.ProgramCourseId == programCourseId
                  && type.ContentTypeCode == ContentSubmissionTypeCodes.Clos
                  && submission.SubmissionStatus == SubmissionStatuses.Submitted
            orderby submission.SubmittedAt descending
            select submission).FirstOrDefaultAsync();
    }

    private async Task NotifySubmitterAsync(
        ProgramCourseContentSubmission submission,
        string title,
        string message)
    {
        await _notificationService.CreateAsync(
            submission.SubmittedByUserId,
            title,
            message,
            ContentSubmissionTypeCodes.Clos,
            submission.SubmissionId);
    }
}

public class HodCloRejectDto
{
    public string? Reason { get; set; }
}
