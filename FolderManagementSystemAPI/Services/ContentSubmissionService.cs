using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class ContentSubmissionService
{
    private readonly FolderManagementDbContext _context;
    private readonly SessionScopeService _sessionScopeService;
    private readonly TeacherPermissionService _permissionService;
    private readonly string _connectionString;

    public ContentSubmissionService(
        FolderManagementDbContext context,
        SessionScopeService sessionScopeService,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _sessionScopeService = sessionScopeService;
        _permissionService = permissionService;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    public async Task EnsureContentTypesAsync()
    {
        var seed = new (string Code, string Name, int Order)[]
        {
            (ContentSubmissionTypeCodes.Clos, "Course Learning Outcomes", 1),
            (ContentSubmissionTypeCodes.WeeklyContent, "Weekly / Course Content", 2),
            (ContentSubmissionTypeCodes.CloPloMapping, "CLO and PLO Mapping", 3),
            (ContentSubmissionTypeCodes.CloTaskMapping, "CLO and Task Mapping", 4),
            (ContentSubmissionTypeCodes.CourseFolder, "Course Master Folder", 5),
        };

        foreach (var item in seed)
        {
            var existing = await _context.ContentSubmissionTypes
                .FirstOrDefaultAsync(x => x.ContentTypeCode == item.Code);

            if (existing == null)
            {
                _context.ContentSubmissionTypes.Add(new ContentSubmissionType
                {
                    ContentTypeCode = item.Code,
                    ContentTypeName = item.Name,
                    SortOrder = item.Order
                });
            }
            else if (!string.Equals(existing.ContentTypeName, item.Name, StringComparison.Ordinal))
            {
                existing.ContentTypeName = item.Name;
                existing.SortOrder = item.Order;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<ContentTypeStatusDto>> GetStatusesAsync(
        int programCourseId,
        int teacherUserId,
        int? sessionId = null,
        int? offeredCourseId = null)
    {
        await EnsureContentTypesAsync();

        var resolved = await _sessionScopeService.TryResolveSessionIdAsync(
            programCourseId,
            sessionId,
            offeredCourseId);

        if (!resolved.Success)
        {
            throw new InvalidOperationException(resolved.Message);
        }

        var effectiveSessionId = resolved.SessionId;
        var flags = await _permissionService.GetTeacherFlagsAsync(teacherUserId, programCourseId);
        var types = await _context.ContentSubmissionTypes
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        var results = new List<ContentTypeStatusDto>();

        foreach (var type in types)
        {
            if (type.ContentTypeCode == ContentSubmissionTypeCodes.CourseFolder)
            {
                continue;
            }

            results.Add(await BuildStatusAsync(
                programCourseId,
                effectiveSessionId,
                type,
                flags));
        }

        return results;
    }

    public async Task<SubmitContentResultDto> SubmitForReviewAsync(
        int programCourseId,
        SubmitContentForReviewDto dto)
    {
        await EnsureContentTypesAsync();

        var resolved = await _sessionScopeService.TryResolveSessionIdAsync(
            programCourseId,
            dto.SessionId,
            dto.OfferedCourseId);

        if (!resolved.Success)
        {
            throw new InvalidOperationException(resolved.Message);
        }

        var sessionId = resolved.SessionId;
        await ValidateSubmitPermissionAsync(dto.TeacherUserId, programCourseId, dto.ContentTypeCode);

        var contentType = await _context.ContentSubmissionTypes
            .FirstOrDefaultAsync(x => x.ContentTypeCode == dto.ContentTypeCode)
            ?? throw new InvalidOperationException("Unknown content type.");

        var pendingSubmission = await _context.ProgramCourseContentSubmissions.AnyAsync(x =>
            x.ProgramCourseId == programCourseId &&
            x.SessionId == sessionId &&
            x.ContentTypeId == contentType.ContentTypeId &&
            x.SubmissionStatus == SubmissionStatuses.Submitted);

        if (pendingSubmission)
        {
            throw new InvalidOperationException("This content is already awaiting HOD review.");
        }

        var itemCount = await CountSubmittableItemsAsync(
            programCourseId,
            sessionId,
            dto.ContentTypeCode);

        if (itemCount == 0)
        {
            throw new InvalidOperationException("There is no new or revised content to submit.");
        }

        if (dto.ContentTypeCode is ContentSubmissionTypeCodes.CloPloMapping
            or ContentSubmissionTypeCodes.CloTaskMapping)
        {
            await EnsureApprovedClosAsync(programCourseId, sessionId);
        }

        if (dto.ContentTypeCode == ContentSubmissionTypeCodes.CloPloMapping)
        {
            await ValidateCloPloOneToOneAsync(programCourseId, sessionId);
        }

        if (dto.ContentTypeCode == ContentSubmissionTypeCodes.CloTaskMapping)
        {
            await ValidateCloTaskMappingCompleteAsync(programCourseId, sessionId);
        }

        var versionNo = await _context.ProgramCourseContentSubmissions
            .Where(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                x.ContentTypeId == contentType.ContentTypeId)
            .Select(x => (int?)x.VersionNo)
            .MaxAsync() ?? 0;

        var submission = new ProgramCourseContentSubmission
        {
            ProgramCourseId = programCourseId,
            SessionId = sessionId,
            ContentTypeId = contentType.ContentTypeId,
            SubmittedByUserId = dto.TeacherUserId,
            SubmittedAt = DateTime.UtcNow,
            SubmissionStatus = SubmissionStatuses.Submitted,
            VersionNo = versionNo + 1,
            IsCurrentApproved = false
        };

        _context.ProgramCourseContentSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        await MarkEntitiesPendingReviewAsync(
            programCourseId,
            sessionId,
            dto.ContentTypeCode,
            submission.SubmissionId);

        var flags = await _permissionService.GetTeacherFlagsAsync(dto.TeacherUserId, programCourseId);
        var status = await BuildStatusAsync(programCourseId, sessionId, contentType, flags);

        return new SubmitContentResultDto
        {
            Message = $"{contentType.ContentTypeName} submitted for HOD review.",
            SubmissionId = submission.SubmissionId,
            ItemsSubmitted = itemCount,
            Status = status
        };
    }

    public async Task<List<HodContentReviewItemDto>> GetReviewQueueAsync(int hodUserId)
    {
        if (!await IsValidHodAsync(hodUserId))
        {
            throw new InvalidOperationException("HOD user not found.");
        }

        await EnsureContentTypesAsync();

        var queue = await (
            from submission in _context.ProgramCourseContentSubmissions
            join type in _context.ContentSubmissionTypes on submission.ContentTypeId equals type.ContentTypeId
            join programCourse in _context.AcademicProgramCourses on submission.ProgramCourseId equals programCourse.ProgramCourseId
            join course in _context.Courses on programCourse.CourseId equals course.CourseId
            join program in _context.AcademicPrograms on programCourse.ProgramId equals program.ProgramId
            join session in _context.AcademicSessions on submission.SessionId equals session.SessionId
            join submitter in _context.Users on submission.SubmittedByUserId equals submitter.UserId
            where submission.SubmissionStatus == SubmissionStatuses.Submitted
                  && type.ContentTypeCode != ContentSubmissionTypeCodes.CourseFolder
            orderby submission.SubmittedAt descending
            select new
            {
                submission,
                type,
                course,
                program,
                session,
                submitter
            }).ToListAsync();

        var results = new List<HodContentReviewItemDto>();

        foreach (var row in queue)
        {
            var itemCount = await CountItemsForSubmissionAsync(
                row.submission.ProgramCourseId,
                row.submission.SessionId,
                row.type.ContentTypeCode,
                row.submission.SubmissionId);

            results.Add(new HodContentReviewItemDto
            {
                SubmissionId = row.submission.SubmissionId,
                ProgramCourseId = row.submission.ProgramCourseId,
                SessionId = row.submission.SessionId,
                SessionTitle = row.session.SessionTitle,
                AcademicYear = row.session.AcademicYear ?? string.Empty,
                ProgramId = row.program.ProgramId,
                ProgramName = row.program.ProgramName,
                CourseTitle = row.course.CourseTitle,
                CourseCode = row.course.CourseCode,
                ContentTypeCode = row.type.ContentTypeCode,
                ContentTypeName = row.type.ContentTypeName,
                SubmittedByName = row.submitter.FullName,
                SubmittedAt = row.submission.SubmittedAt,
                VersionNo = row.submission.VersionNo,
                ItemCount = itemCount
            });
        }

        return results;
    }

    public async Task<HodContentSubmissionDetailDto> GetSubmissionDetailsAsync(int submissionId, int hodUserId)
    {
        if (!await IsValidHodAsync(hodUserId))
        {
            throw new InvalidOperationException("HOD user not found.");
        }

        var submission = await _context.ProgramCourseContentSubmissions
            .Include(x => x.ContentType)
            .FirstOrDefaultAsync(x => x.SubmissionId == submissionId)
            ?? throw new InvalidOperationException("Submission not found.");

        if (submission.SubmissionStatus != SubmissionStatuses.Submitted)
        {
            throw new InvalidOperationException("This submission is no longer pending review.");
        }

        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == submission.ProgramCourseId)
            ?? throw new InvalidOperationException("Program course not found.");

        var course = await _context.Courses.FindAsync(programCourse.CourseId)
            ?? throw new InvalidOperationException("Course not found.");

        var program = await _context.AcademicPrograms.FindAsync(programCourse.ProgramId)
            ?? throw new InvalidOperationException("Program not found.");

        var session = await _context.AcademicSessions.FindAsync(submission.SessionId)
            ?? throw new InvalidOperationException("Session not found.");

        var submitter = await _context.Users.FindAsync(submission.SubmittedByUserId)
            ?? throw new InvalidOperationException("Submitter not found.");

        var itemCount = await CountItemsForSubmissionAsync(
            submission.ProgramCourseId,
            submission.SessionId,
            submission.ContentType.ContentTypeCode,
            submission.SubmissionId);

        var detail = new HodContentSubmissionDetailDto
        {
            SubmissionId = submission.SubmissionId,
            ProgramCourseId = submission.ProgramCourseId,
            SessionId = submission.SessionId,
            SessionTitle = session.SessionTitle,
            AcademicYear = session.AcademicYear ?? string.Empty,
            ProgramId = program.ProgramId,
            ProgramName = program.ProgramName,
            CourseTitle = course.CourseTitle,
            CourseCode = course.CourseCode,
            ContentTypeCode = submission.ContentType.ContentTypeCode,
            ContentTypeName = submission.ContentType.ContentTypeName,
            SubmittedByName = submitter.FullName,
            SubmittedAt = submission.SubmittedAt,
            VersionNo = submission.VersionNo,
            ItemCount = itemCount
        };

        switch (submission.ContentType.ContentTypeCode)
        {
            case ContentSubmissionTypeCodes.Clos:
                detail.Clos = await LoadSubmissionClosAsync(
                    submission.ProgramCourseId,
                    submission.SessionId,
                    submission.SubmissionId);
                break;

            case ContentSubmissionTypeCodes.WeeklyContent:
                detail.Weeks = await LoadSubmissionTopicTreeAsync(
                    submission.ProgramCourseId,
                    submission.SessionId,
                    submission.SubmissionId);
                break;

            case ContentSubmissionTypeCodes.CloPloMapping:
                detail.CloPloMatrix = await BuildFullCloPloMatrixAsync(
                    submission.ProgramCourseId,
                    submission.SessionId);
                break;

            case ContentSubmissionTypeCodes.CloTaskMapping:
                detail.CloTaskMatrix = await BuildFullCloTaskMatrixAsync(
                    submission.ProgramCourseId,
                    submission.SessionId);
                break;

            default:
                throw new InvalidOperationException("Unsupported content type for review.");
        }

        return detail;
    }

    public async Task<ReviewContentResultDto> ApproveAsync(
        int submissionId,
        ReviewContentSubmissionDto dto)
    {
        return await ReviewAsync(submissionId, dto, approved: true);
    }

    public async Task<ReviewContentResultDto> RejectAsync(
        int submissionId,
        ReviewContentSubmissionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.HodFeedback))
        {
            throw new InvalidOperationException("Feedback is required when rejecting content.");
        }

        return await ReviewAsync(submissionId, dto, approved: false);
    }

    public bool CanViewAllStatuses(TeacherPermissionFlagsDto flags, string contentTypeCode)
    {
        return contentTypeCode switch
        {
            ContentSubmissionTypeCodes.Clos => flags.CanManageClos,
            ContentSubmissionTypeCodes.WeeklyContent => flags.CanUploadWeeklyContent,
            ContentSubmissionTypeCodes.CloPloMapping => flags.CanManageCloPloMapping,
            ContentSubmissionTypeCodes.CloTaskMapping => flags.CanManageCloTaskMapping,
            _ => false
        };
    }

    public IReadOnlyCollection<string> VisibleContentStatuses(bool canViewAll)
    {
        return canViewAll
            ? new[]
            {
                ContentStatuses.Draft,
                ContentStatuses.PendingReview,
                ContentStatuses.Approved,
                ContentStatuses.Rejected
            }
            : new[] { ContentStatuses.Approved };
    }

    private async Task<ReviewContentResultDto> ReviewAsync(
        int submissionId,
        ReviewContentSubmissionDto dto,
        bool approved)
    {
        if (!await IsValidHodAsync(dto.HodUserId))
        {
            throw new InvalidOperationException("HOD user not found.");
        }

        var submission = await _context.ProgramCourseContentSubmissions
            .Include(x => x.ContentType)
            .FirstOrDefaultAsync(x => x.SubmissionId == submissionId)
            ?? throw new InvalidOperationException("Submission not found.");

        if (submission.SubmissionStatus != SubmissionStatuses.Submitted)
        {
            throw new InvalidOperationException("Only pending submissions can be reviewed.");
        }

        submission.SubmissionStatus = approved
            ? SubmissionStatuses.Approved
            : SubmissionStatuses.Rejected;
        submission.ReviewedByUserId = dto.HodUserId;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.HodFeedback = dto.HodFeedback?.Trim();
        submission.IsCurrentApproved = approved;

        if (approved)
        {
            await ClearPreviousApprovedFlagsAsync(submission);
        }

        await _context.SaveChangesAsync();

        var entityStatus = approved
            ? ContentStatuses.Approved
            : ContentStatuses.Rejected;

        await UpdateLinkedEntityStatusesAsync(
            submission.ProgramCourseId,
            submission.SessionId,
            submission.ContentType.ContentTypeCode,
            submission.SubmissionId,
            entityStatus);

        return new ReviewContentResultDto
        {
            Message = approved
                ? "Content approved. Other teachers can now view it."
                : "Content rejected. The submitting teacher can revise and resubmit.",
            SubmissionId = submission.SubmissionId,
            SubmissionStatus = submission.SubmissionStatus
        };
    }

    private async Task ClearPreviousApprovedFlagsAsync(ProgramCourseContentSubmission submission)
    {
        var previous = await _context.ProgramCourseContentSubmissions
            .Where(x =>
                x.ProgramCourseId == submission.ProgramCourseId &&
                x.SessionId == submission.SessionId &&
                x.ContentTypeId == submission.ContentTypeId &&
                x.SubmissionId != submission.SubmissionId &&
                x.IsCurrentApproved)
            .ToListAsync();

        foreach (var item in previous)
        {
            item.IsCurrentApproved = false;
        }
    }

    private async Task ValidateSubmitPermissionAsync(
        int teacherUserId,
        int programCourseId,
        string contentTypeCode)
    {
        var permissionType = contentTypeCode switch
        {
            ContentSubmissionTypeCodes.Clos => TeacherPermissionTypes.ManageClos,
            ContentSubmissionTypeCodes.WeeklyContent => TeacherPermissionTypes.UploadWeeklyContent,
            ContentSubmissionTypeCodes.CloPloMapping => TeacherPermissionTypes.ManageCloPloMapping,
            ContentSubmissionTypeCodes.CloTaskMapping => TeacherPermissionTypes.ManageCloTaskMapping,
            _ => null
        };

        if (permissionType == null)
        {
            throw new InvalidOperationException("This content type cannot be submitted for review.");
        }

        if (!await _permissionService.HasPermissionAsync(teacherUserId, programCourseId, permissionType))
        {
            throw new InvalidOperationException("You do not have permission to submit this content.");
        }
    }

    private async Task<ContentTypeStatusDto> BuildStatusAsync(
        int programCourseId,
        int sessionId,
        ContentSubmissionType type,
        TeacherPermissionFlagsDto flags)
    {
        var canEdit = CanViewAllStatuses(flags, type.ContentTypeCode);
        var latestSubmission = await _context.ProgramCourseContentSubmissions
            .Where(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                x.ContentTypeId == type.ContentTypeId)
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync();

        var itemCount = await CountItemsAsync(programCourseId, sessionId, type.ContentTypeCode);
        var submittableCount = await CountSubmittableItemsAsync(programCourseId, sessionId, type.ContentTypeCode);
        var approvedCount = await CountItemsWithStatusAsync(
            programCourseId,
            sessionId,
            type.ContentTypeCode,
            ContentStatuses.Approved);

        var contentStatus = ContentStatuses.Draft;
        var canSubmit = canEdit && submittableCount > 0;
        var prerequisiteMet = true;
        string? blockedReason = null;

        if (type.ContentTypeCode is ContentSubmissionTypeCodes.CloPloMapping
            or ContentSubmissionTypeCodes.CloTaskMapping)
        {
            var closApproved = await HasApprovedClosAsync(programCourseId, sessionId);
            prerequisiteMet = closApproved;
            if (!closApproved)
            {
                canEdit = false;
                canSubmit = false;
                blockedReason = "HOD must approve CLOs before this mapping can be edited or submitted.";
            }
        }

        if (type.ContentTypeCode == ContentSubmissionTypeCodes.CloTaskMapping && canSubmit)
        {
            canSubmit = await IsCloTaskMappingCompleteAsync(programCourseId, sessionId);
            if (!canSubmit && prerequisiteMet)
            {
                blockedReason =
                    "Every approved CLO must total exactly 100% across Assignment, Quiz, Mid, and Final before submission.";
            }
        }

        if (latestSubmission?.SubmissionStatus == SubmissionStatuses.Submitted)
        {
            contentStatus = ContentStatuses.PendingReview;
            canSubmit = false;
            canEdit = false;
        }
        else if (latestSubmission?.SubmissionStatus == SubmissionStatuses.Rejected)
        {
            contentStatus = ContentStatuses.Rejected;
            canSubmit = canEdit && submittableCount > 0;
        }
        else if (latestSubmission?.SubmissionStatus == SubmissionStatuses.Approved ||
                 (approvedCount > 0 && submittableCount == 0))
        {
            // Once HOD has approved and there is no rejected/unlocked draft revision,
            // teachers cannot edit or resubmit until HOD rejects/unlocks.
            contentStatus = ContentStatuses.Approved;
            canEdit = false;
            canSubmit = false;
        }
        else if (itemCount == 0)
        {
            contentStatus = "NotStarted";
            canSubmit = false;
        }

        return new ContentTypeStatusDto
        {
            ContentTypeCode = type.ContentTypeCode,
            ContentTypeName = type.ContentTypeName,
            ContentStatus = contentStatus,
            SubmissionId = latestSubmission?.SubmissionId,
            SubmissionStatus = latestSubmission?.SubmissionStatus,
            HodFeedback = latestSubmission?.HodFeedback,
            SubmittedAt = latestSubmission?.SubmittedAt,
            ReviewedAt = latestSubmission?.ReviewedAt,
            ItemCount = itemCount,
            CanSubmit = canSubmit,
            CanEdit = canEdit,
            IsVisibleToAllTeachers = approvedCount > 0,
            PrerequisiteMet = prerequisiteMet,
            BlockedReason = blockedReason
        };
    }

    private async Task<int> CountSubmittableItemsAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode)
    {
        return contentTypeCode switch
        {
            ContentSubmissionTypeCodes.Clos => await _context.Clos.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                (x.ContentStatus == ContentStatuses.Draft ||
                 x.ContentStatus == ContentStatuses.Rejected ||
                 x.ContentStatus == null)),
            ContentSubmissionTypeCodes.WeeklyContent => await CountTopicsWithStatusesAsync(
                programCourseId,
                sessionId,
                ContentStatuses.Draft,
                ContentStatuses.Rejected),
            ContentSubmissionTypeCodes.CloPloMapping => await _context.Ploclomappings.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                (x.ContentStatus == ContentStatuses.Draft ||
                 x.ContentStatus == ContentStatuses.Rejected ||
                 x.ContentStatus == null)),
            ContentSubmissionTypeCodes.CloTaskMapping => await _context.CourseAssessmentTypeClomappings.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                (x.ContentStatus == ContentStatuses.Draft ||
                 x.ContentStatus == ContentStatuses.Rejected ||
                 x.ContentStatus == null)),
            _ => 0
        };
    }

    private async Task<int> CountItemsAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode)
    {
        return contentTypeCode switch
        {
            ContentSubmissionTypeCodes.Clos => await _context.Clos.CountAsync(x =>
                x.ProgramCourseId == programCourseId && x.SessionId == sessionId),
            ContentSubmissionTypeCodes.WeeklyContent => await CountTopicsWithStatusesAsync(
                programCourseId,
                sessionId),
            ContentSubmissionTypeCodes.CloPloMapping => await _context.Ploclomappings.CountAsync(x =>
                x.ProgramCourseId == programCourseId && x.SessionId == sessionId),
            ContentSubmissionTypeCodes.CloTaskMapping => await _context.CourseAssessmentTypeClomappings.CountAsync(x =>
                x.ProgramCourseId == programCourseId && x.SessionId == sessionId),
            _ => 0
        };
    }

    private async Task<int> CountItemsWithStatusAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode,
        string status)
    {
        return contentTypeCode switch
        {
            ContentSubmissionTypeCodes.Clos => await _context.Clos.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                x.ContentStatus == status),
            ContentSubmissionTypeCodes.WeeklyContent => await CountTopicsWithStatusesAsync(
                programCourseId,
                sessionId,
                status),
            ContentSubmissionTypeCodes.CloPloMapping => await _context.Ploclomappings.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                x.ContentStatus == status),
            ContentSubmissionTypeCodes.CloTaskMapping => await _context.CourseAssessmentTypeClomappings.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                x.ContentStatus == status),
            _ => 0
        };
    }

    private async Task<int> CountItemsForSubmissionAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode,
        int submissionId)
    {
        return contentTypeCode switch
        {
            ContentSubmissionTypeCodes.Clos => await _context.Clos.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                x.SubmissionId == submissionId),
            ContentSubmissionTypeCodes.WeeklyContent => await CountTopicsForSubmissionAsync(
                programCourseId,
                sessionId,
                submissionId),
            ContentSubmissionTypeCodes.CloPloMapping => await _context.Ploclomappings.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                x.SubmissionId == submissionId),
            ContentSubmissionTypeCodes.CloTaskMapping => await _context.CourseAssessmentTypeClomappings.CountAsync(x =>
                x.ProgramCourseId == programCourseId &&
                x.SessionId == sessionId &&
                x.SubmissionId == submissionId),
            _ => 0
        };
    }

    private async Task MarkEntitiesPendingReviewAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode,
        int submissionId)
    {
        switch (contentTypeCode)
        {
            case ContentSubmissionTypeCodes.Clos:
                await _context.Clos
                    .Where(x =>
                        x.ProgramCourseId == programCourseId &&
                        x.SessionId == sessionId &&
                        (x.ContentStatus == ContentStatuses.Draft ||
                         x.ContentStatus == ContentStatuses.Rejected))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ContentStatus, ContentStatuses.PendingReview)
                        .SetProperty(x => x.SubmissionId, submissionId));
                break;

            case ContentSubmissionTypeCodes.WeeklyContent:
                await ExecuteTopicStatusUpdateAsync(
                    programCourseId,
                    sessionId,
                    submissionId,
                    ContentStatuses.PendingReview,
                    ContentStatuses.Draft,
                    ContentStatuses.Rejected);
                break;

            case ContentSubmissionTypeCodes.CloPloMapping:
                await _context.Ploclomappings
                    .Where(x =>
                        x.ProgramCourseId == programCourseId &&
                        x.SessionId == sessionId &&
                        (x.ContentStatus == ContentStatuses.Draft ||
                         x.ContentStatus == ContentStatuses.Rejected))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ContentStatus, ContentStatuses.PendingReview)
                        .SetProperty(x => x.SubmissionId, submissionId));
                break;

            case ContentSubmissionTypeCodes.CloTaskMapping:
                await _context.CourseAssessmentTypeClomappings
                    .Where(x =>
                        x.ProgramCourseId == programCourseId &&
                        x.SessionId == sessionId &&
                        (x.ContentStatus == ContentStatuses.Draft ||
                         x.ContentStatus == ContentStatuses.Rejected))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ContentStatus, ContentStatuses.PendingReview)
                        .SetProperty(x => x.SubmissionId, submissionId));
                break;
        }
    }

    private async Task UpdateLinkedEntityStatusesAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode,
        int submissionId,
        string entityStatus)
    {
        switch (contentTypeCode)
        {
            case ContentSubmissionTypeCodes.Clos:
                await _context.Clos
                    .Where(x =>
                        x.ProgramCourseId == programCourseId &&
                        x.SessionId == sessionId &&
                        x.SubmissionId == submissionId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ContentStatus, entityStatus));
                break;

            case ContentSubmissionTypeCodes.WeeklyContent:
                await ExecuteTopicStatusUpdateForSubmissionAsync(
                    programCourseId,
                    sessionId,
                    submissionId,
                    entityStatus);
                break;

            case ContentSubmissionTypeCodes.CloPloMapping:
                await _context.Ploclomappings
                    .Where(x =>
                        x.ProgramCourseId == programCourseId &&
                        x.SessionId == sessionId &&
                        x.SubmissionId == submissionId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ContentStatus, entityStatus));
                break;

            case ContentSubmissionTypeCodes.CloTaskMapping:
                await _context.CourseAssessmentTypeClomappings
                    .Where(x =>
                        x.ProgramCourseId == programCourseId &&
                        x.SessionId == sessionId &&
                        x.SubmissionId == submissionId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ContentStatus, entityStatus));
                break;
        }

        if (entityStatus == ContentStatuses.Rejected)
        {
            // Rejected items should be editable again without keeping the submission link.
            switch (contentTypeCode)
            {
                case ContentSubmissionTypeCodes.Clos:
                    await _context.Clos
                        .Where(x =>
                            x.ProgramCourseId == programCourseId &&
                            x.SessionId == sessionId &&
                            x.SubmissionId == submissionId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.SubmissionId, (int?)null));
                    break;

                case ContentSubmissionTypeCodes.WeeklyContent:
                    await ExecuteClearTopicSubmissionAsync(programCourseId, sessionId, submissionId);
                    break;

                case ContentSubmissionTypeCodes.CloPloMapping:
                    await _context.Ploclomappings
                        .Where(x =>
                            x.ProgramCourseId == programCourseId &&
                            x.SessionId == sessionId &&
                            x.SubmissionId == submissionId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.SubmissionId, (int?)null));
                    break;

                case ContentSubmissionTypeCodes.CloTaskMapping:
                    await _context.CourseAssessmentTypeClomappings
                        .Where(x =>
                            x.ProgramCourseId == programCourseId &&
                            x.SessionId == sessionId &&
                            x.SubmissionId == submissionId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.SubmissionId, (int?)null));
                    break;
            }
        }
    }

    public async Task EnsureApprovedClosAsync(int programCourseId, int sessionId)
    {
        if (!await HasApprovedClosAsync(programCourseId, sessionId))
        {
            throw new InvalidOperationException(
                "HOD must approve CLOs before CLO and PLO or CLO and Task mapping can continue.");
        }
    }

    public async Task EnsureMappingsApprovedForAssessmentsAsync(int programCourseId, int sessionId)
    {
        if (!await HasApprovedClosAsync(programCourseId, sessionId))
        {
            throw new InvalidOperationException(
                "HOD must approve CLOs before quizzes, assignments, or exams can be created.");
        }

        if (!await HasApprovedMappingAsync(programCourseId, sessionId, ContentSubmissionTypeCodes.CloPloMapping))
        {
            throw new InvalidOperationException(
                "HOD must approve CLO and PLO mapping before quizzes, assignments, or exams can be created.");
        }

        if (!await HasApprovedMappingAsync(programCourseId, sessionId, ContentSubmissionTypeCodes.CloTaskMapping))
        {
            throw new InvalidOperationException(
                "HOD must approve CLO and Task mapping before quizzes, assignments, or exams can be created.");
        }

        if (!await IsCloTaskMappingFullyApprovedAsync(programCourseId, sessionId))
        {
            throw new InvalidOperationException(
                "Every approved CLO must total exactly 100% in an approved CLO and Task mapping before assessments can be created.");
        }
    }

    public async Task EnsureMappingEditableAsync(int programCourseId, int sessionId, string contentTypeCode)
    {
        await EnsureApprovedClosAsync(programCourseId, sessionId);
        await EnsureContentTypeEditableAsync(programCourseId, sessionId, contentTypeCode);
    }

    public async Task EnsureContentTypeEditableAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode)
    {
        if (await HasPendingSubmissionAsync(programCourseId, sessionId, contentTypeCode))
        {
            throw new InvalidOperationException(
                "This content is awaiting HOD review and cannot be edited.");
        }

        if (await HasCurrentApprovedSubmissionAsync(programCourseId, sessionId, contentTypeCode))
        {
            throw new InvalidOperationException(
                "This content has been approved by the HOD and is locked. Ask the HOD to reject or unlock it before making changes.");
        }
    }

    public async Task<bool> HasCurrentApprovedSubmissionAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode)
    {
        await EnsureContentTypesAsync();

        var latest = await (
            from submission in _context.ProgramCourseContentSubmissions
            join type in _context.ContentSubmissionTypes on submission.ContentTypeId equals type.ContentTypeId
            where submission.ProgramCourseId == programCourseId
                  && submission.SessionId == sessionId
                  && type.ContentTypeCode == contentTypeCode
            orderby submission.SubmittedAt descending
            select submission).FirstOrDefaultAsync();

        if (latest == null)
        {
            return false;
        }

        return latest.SubmissionStatus == SubmissionStatuses.Approved
               || latest.IsCurrentApproved;
    }

    public async Task<bool> HasApprovedClosAsync(int programCourseId, int sessionId)
    {
        return await _context.Clos.AnyAsync(c =>
            c.ProgramCourseId == programCourseId &&
            c.SessionId == sessionId &&
            c.ContentStatus == ContentStatuses.Approved);
    }

    public async Task<bool> HasApprovedMappingAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode)
    {
        return contentTypeCode switch
        {
            ContentSubmissionTypeCodes.CloPloMapping => await _context.Ploclomappings.AnyAsync(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == sessionId &&
                m.ContentStatus == ContentStatuses.Approved),
            ContentSubmissionTypeCodes.CloTaskMapping => await _context.CourseAssessmentTypeClomappings.AnyAsync(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == sessionId &&
                m.ContentStatus == ContentStatuses.Approved),
            _ => false
        };
    }

    public async Task<bool> HasPendingSubmissionAsync(
        int programCourseId,
        int sessionId,
        string contentTypeCode)
    {
        await EnsureContentTypesAsync();

        return await (
            from submission in _context.ProgramCourseContentSubmissions
            join type in _context.ContentSubmissionTypes on submission.ContentTypeId equals type.ContentTypeId
            where submission.ProgramCourseId == programCourseId
                  && submission.SessionId == sessionId
                  && type.ContentTypeCode == contentTypeCode
                  && submission.SubmissionStatus == SubmissionStatuses.Submitted
            select submission.SubmissionId).AnyAsync();
    }

    public async Task<bool> IsCloTaskMappingFullyApprovedAsync(int programCourseId, int sessionId)
    {
        var approvedClos = await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == sessionId &&
                c.ContentStatus == ContentStatuses.Approved)
            .Select(c => c.Cloid)
            .ToListAsync();

        if (approvedClos.Count == 0)
        {
            return false;
        }

        var mappings = await _context.CourseAssessmentTypeClomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == sessionId &&
                m.ContentStatus == ContentStatuses.Approved &&
                approvedClos.Contains(m.Cloid))
            .ToListAsync();

        foreach (var cloId in approvedClos)
        {
            var total = mappings
                .Where(m => m.Cloid == cloId && m.Percentage is > 0)
                .Sum(m => m.Percentage ?? 0);

            if (Math.Abs(total - 100m) > 0.01m)
            {
                return false;
            }
        }

        return true;
    }

    private async Task ValidateCloPloOneToOneAsync(int programCourseId, int sessionId)
    {
        var approvedClos = await _context.Clos
            .AsNoTracking()
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == sessionId &&
                c.ContentStatus == ContentStatuses.Approved)
            .Select(c => new { c.Cloid, c.Clocode })
            .ToListAsync();

        if (approvedClos.Count == 0)
        {
            throw new InvalidOperationException("Approve CLOs before submitting CLO and PLO mapping.");
        }

        var mappings = await _context.Ploclomappings
            .AsNoTracking()
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == sessionId &&
                m.Percentage != null &&
                m.Percentage > 0 &&
                (m.ContentStatus == ContentStatuses.Draft ||
                 m.ContentStatus == ContentStatuses.Rejected ||
                 m.ContentStatus == null))
            .ToListAsync();

        // Prefer draft/rejected rows being submitted; fall back to any positive mapping for the CLO.
        var allPositive = await _context.Ploclomappings
            .AsNoTracking()
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == sessionId &&
                m.Percentage != null &&
                m.Percentage > 0)
            .ToListAsync();

        foreach (var clo in approvedClos)
        {
            var cloMaps = mappings.Where(m => m.Cloid == clo.Cloid).ToList();
            if (cloMaps.Count == 0)
            {
                cloMaps = allPositive.Where(m => m.Cloid == clo.Cloid).ToList();
            }

            var distinctPlos = cloMaps.Select(m => m.Ploid).Distinct().Count();
            if (distinctPlos == 0)
            {
                throw new InvalidOperationException(
                    $"One CLO may map to only one PLO (a PLO may have many CLOs). '{clo.Clocode}' has no PLO mapping.");
            }

            if (distinctPlos > 1)
            {
                throw new InvalidOperationException(
                    $"One CLO may map to only one PLO (a PLO may have many CLOs). '{clo.Clocode}' is mapped to {distinctPlos} PLOs.");
            }
        }
    }

    private async Task ValidateCloTaskMappingCompleteAsync(int programCourseId, int sessionId)
    {
        if (!await IsCloTaskMappingCompleteAsync(programCourseId, sessionId))
        {
            throw new InvalidOperationException(
                "Every approved CLO must total exactly 100% across Assignment, Quiz, Mid, and Final before submission.");
        }
    }

    private async Task<bool> IsCloTaskMappingCompleteAsync(int programCourseId, int sessionId)
    {
        var approvedClos = await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == sessionId &&
                c.ContentStatus == ContentStatuses.Approved)
            .Select(c => c.Cloid)
            .ToListAsync();

        if (approvedClos.Count == 0)
        {
            return false;
        }

        var mappings = await _context.CourseAssessmentTypeClomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == sessionId &&
                approvedClos.Contains(m.Cloid))
            .ToListAsync();

        foreach (var cloId in approvedClos)
        {
            var total = mappings
                .Where(m => m.Cloid == cloId && m.Percentage is > 0)
                .Sum(m => m.Percentage ?? 0);

            if (Math.Abs(total - 100m) > 0.01m)
            {
                return false;
            }
        }

        return mappings.Any(m =>
            m.ContentStatus == ContentStatuses.Draft ||
            m.ContentStatus == ContentStatuses.Rejected ||
            m.ContentStatus == null);
    }

    private async Task<int> CountTopicsWithStatusesAsync(
        int programCourseId,
        int sessionId,
        params string[] statuses)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var statusList = string.Join(", ", statuses.Select((_, i) => $"@Status{i}"));
        var command = new SqlCommand($@"
            SELECT COUNT(1)
            FROM CourseTopics
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID
            {(statuses.Length > 0 ? $"AND ContentStatus IN ({statusList})" : string.Empty)};
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);

        for (var i = 0; i < statuses.Length; i++)
        {
            command.Parameters.AddWithValue($"@Status{i}", statuses[i]);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> CountTopicsForSubmissionAsync(
        int programCourseId,
        int sessionId,
        int submissionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT COUNT(1)
            FROM CourseTopics
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID
            AND SubmissionID = @SubmissionID;
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);
        command.Parameters.AddWithValue("@SubmissionID", submissionId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteTopicStatusUpdateAsync(
        int programCourseId,
        int sessionId,
        int submissionId,
        string newStatus,
        params string[] currentStatuses)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var statusList = string.Join(", ", currentStatuses.Select((_, i) => $"@Status{i}"));
        var command = new SqlCommand($@"
            UPDATE CourseTopics
            SET ContentStatus = @NewStatus,
                SubmissionID = @SubmissionID
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID
            AND ContentStatus IN ({statusList});
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);
        command.Parameters.AddWithValue("@SubmissionID", submissionId);
        command.Parameters.AddWithValue("@NewStatus", newStatus);

        for (var i = 0; i < currentStatuses.Length; i++)
        {
            command.Parameters.AddWithValue($"@Status{i}", currentStatuses[i]);
        }

        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteTopicStatusUpdateForSubmissionAsync(
        int programCourseId,
        int sessionId,
        int submissionId,
        string newStatus)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            UPDATE CourseTopics
            SET ContentStatus = @NewStatus
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID
            AND SubmissionID = @SubmissionID;
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);
        command.Parameters.AddWithValue("@SubmissionID", submissionId);
        command.Parameters.AddWithValue("@NewStatus", newStatus);

        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteClearTopicSubmissionAsync(
        int programCourseId,
        int sessionId,
        int submissionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            UPDATE CourseTopics
            SET SubmissionID = NULL
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID
            AND SubmissionID = @SubmissionID;
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);
        command.Parameters.AddWithValue("@SubmissionID", submissionId);

        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> IsValidHodAsync(int hodUserId)
    {
        return await _context.Users
            .Include(u => u.Role)
            .AnyAsync(u => u.UserId == hodUserId && u.Role.RoleName == "HOD");
    }

    private async Task<List<CloDto>> LoadSubmissionClosAsync(
        int programCourseId,
        int sessionId,
        int submissionId)
    {
        return await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == sessionId &&
                c.SubmissionId == submissionId)
            .OrderBy(c => c.Clocode)
            .Select(c => new CloDto
            {
                CloId = c.Cloid,
                ProgramCourseId = c.ProgramCourseId,
                CloCode = c.Clocode,
                CloDescription = c.Clodescription,
                ContentStatus = c.ContentStatus ?? ContentStatuses.Draft
            })
            .ToListAsync();
    }

    private async Task<List<WeeklyTopicDto>> LoadSubmissionTopicTreeAsync(
        int programCourseId,
        int sessionId,
        int submissionId)
    {
        var submittedTopics = await LoadTopicsBySubmissionAsync(programCourseId, sessionId, submissionId);
        if (submittedTopics.Count == 0)
        {
            return new List<WeeklyTopicDto>();
        }

        var topicMap = submittedTopics.ToDictionary(x => x.TopicId);
        var neededIds = new HashSet<int>(topicMap.Keys);

        foreach (var topic in submittedTopics.ToList())
        {
            var parentId = topic.ParentTopicId;
            while (parentId != null && neededIds.Add(parentId.Value))
            {
                var parent = await LoadTopicByIdAsync(parentId.Value, programCourseId, sessionId);
                if (parent == null)
                {
                    break;
                }

                topicMap[parent.TopicId] = parent;
                parentId = parent.ParentTopicId;
            }
        }

        var cloCodesByTopic = await LoadTopicCloCodesAsync(topicMap.Keys);

        foreach (var topic in topicMap.Values)
        {
            if (cloCodesByTopic.TryGetValue(topic.TopicId, out var codes))
            {
                topic.CloCodes = codes;
            }
        }

        foreach (var topic in topicMap.Values)
        {
            if (topic.ParentTopicId != null &&
                topicMap.TryGetValue(topic.ParentTopicId.Value, out var parent))
            {
                parent.Children.Add(topic);
            }
        }

        return topicMap.Values
            .Where(x => x.ParentTopicId == null)
            .OrderBy(x => x.WeekNo ?? 999)
            .ThenBy(x => x.DisplayOrder ?? 999)
            .ThenBy(x => x.TopicId)
            .ToList();
    }

    private async Task<List<WeeklyTopicDto>> LoadTopicsBySubmissionAsync(
        int programCourseId,
        int sessionId,
        int submissionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                TopicID,
                ProgramCourseID,
                ParentTopicID,
                WeekNo,
                TopicTitle,
                TopicDescription,
                DisplayOrder
            FROM CourseTopics
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID
            AND SubmissionID = @SubmissionID
            ORDER BY ISNULL(WeekNo, 999), ISNULL(ParentTopicID, 0), ISNULL(DisplayOrder, 999), TopicID;
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);
        command.Parameters.AddWithValue("@SubmissionID", submissionId);

        var topics = new List<WeeklyTopicDto>();

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            topics.Add(new WeeklyTopicDto
            {
                TopicId = Convert.ToInt32(reader["TopicID"]),
                ProgramCourseId = Convert.ToInt32(reader["ProgramCourseID"]),
                ParentTopicId = reader["ParentTopicID"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["ParentTopicID"]),
                WeekNo = reader["WeekNo"] == DBNull.Value ? null : Convert.ToInt32(reader["WeekNo"]),
                TopicTitle = Convert.ToString(reader["TopicTitle"]) ?? string.Empty,
                TopicDescription = reader["TopicDescription"] == DBNull.Value
                    ? null
                    : Convert.ToString(reader["TopicDescription"]),
                DisplayOrder = reader["DisplayOrder"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["DisplayOrder"])
            });
        }

        return topics;
    }

    private async Task<WeeklyTopicDto?> LoadTopicByIdAsync(int topicId, int programCourseId, int sessionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                TopicID,
                ProgramCourseID,
                ParentTopicID,
                WeekNo,
                TopicTitle,
                TopicDescription,
                DisplayOrder
            FROM CourseTopics
            WHERE TopicID = @TopicID
            AND ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID;
        ", connection);

        command.Parameters.AddWithValue("@TopicID", topicId);
        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WeeklyTopicDto
        {
            TopicId = Convert.ToInt32(reader["TopicID"]),
            ProgramCourseId = Convert.ToInt32(reader["ProgramCourseID"]),
            ParentTopicId = reader["ParentTopicID"] == DBNull.Value
                ? null
                : Convert.ToInt32(reader["ParentTopicID"]),
            WeekNo = reader["WeekNo"] == DBNull.Value ? null : Convert.ToInt32(reader["WeekNo"]),
            TopicTitle = Convert.ToString(reader["TopicTitle"]) ?? string.Empty,
            TopicDescription = reader["TopicDescription"] == DBNull.Value
                ? null
                : Convert.ToString(reader["TopicDescription"]),
            DisplayOrder = reader["DisplayOrder"] == DBNull.Value
                ? null
                : Convert.ToInt32(reader["DisplayOrder"])
        };
    }

    private async Task<Dictionary<int, List<string>>> LoadTopicCloCodesAsync(IEnumerable<int> topicIds)
    {
        var ids = topicIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, List<string>>();
        }

        var mappings = await (
            from map in _context.CourseTopicClomappings
            join clo in _context.Clos on map.Cloid equals clo.Cloid
            where ids.Contains(map.TopicId)
            orderby clo.Clocode
            select new { map.TopicId, clo.Clocode }
        ).ToListAsync();

        return mappings
            .GroupBy(x => x.TopicId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Clocode).Distinct().ToList());
    }

    private async Task<CloPloMatrixDto> BuildFullCloPloMatrixAsync(int programCourseId, int sessionId)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId)
            ?? throw new InvalidOperationException("Program course not found.");

        var program = await _context.AcademicPrograms.FindAsync(programCourse.ProgramId)
            ?? throw new InvalidOperationException("Program not found.");

        var course = await _context.Courses.FindAsync(programCourse.CourseId)
            ?? throw new InvalidOperationException("Course not found.");

        var clos = await _context.Clos
            .Where(c => c.ProgramCourseId == programCourseId && c.SessionId == sessionId)
            .OrderBy(c => c.Clocode)
            .ToListAsync();

        var coursePloIds = await _context.ProgramCoursePlomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.Percentage != null &&
                m.Percentage > 0)
            .Select(m => m.Ploid)
            .Distinct()
            .ToListAsync();

        var plos = await _context.Plos
            .Where(p =>
                p.ProgramId == programCourse.ProgramId &&
                coursePloIds.Contains(p.Ploid))
            .OrderBy(p => p.Plocode)
            .ToListAsync();

        var mappings = await _context.Ploclomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == sessionId &&
                coursePloIds.Contains(m.Ploid))
            .ToListAsync();

        var mappingLookup = mappings.ToDictionary(m => $"{m.Cloid}|{m.Ploid}", m => m);

        var columns = plos
            .Select((plo, index) => new CloPloMatrixColumnDto
            {
                PloId = plo.Ploid,
                PloCode = plo.Plocode,
                ColumnLabel = BuildPloColumnLabel(plo.Plocode, index),
                PloDescription = plo.Plodescription
            })
            .ToList();

        var rows = clos
            .Select((clo, index) => new CloPloMatrixRowDto
            {
                CloId = clo.Cloid,
                CloCode = clo.Clocode,
                RowLabel = BuildCloRowLabel(clo.Clocode, index),
                CloDescription = clo.Clodescription,
                Cells = plos.Select(plo =>
                {
                    mappingLookup.TryGetValue($"{clo.Cloid}|{plo.Ploid}", out var mapping);

                    return new CloPloMatrixCellDto
                    {
                        PloId = plo.Ploid,
                        MappingId = mapping?.MappingId,
                        Percentage = mapping?.Percentage
                    };
                }).ToList()
            })
            .ToList();

        return new CloPloMatrixDto
        {
            ProgramCourseId = programCourseId,
            ProgramId = programCourse.ProgramId,
            CourseId = programCourse.CourseId,
            CourseTitle = course.CourseTitle,
            CourseCode = course.CourseCode,
            ProgramName = program.ProgramName,
            Plos = columns,
            Clos = rows,
            PloRowCapacities = BuildPloRowCapacities(columns, mappings)
        };
    }

    private static string BuildPloColumnLabel(string ploCode, int index)
    {
        return string.IsNullOrWhiteSpace(ploCode) ? $"PLO-{index + 1}" : ploCode;
    }

    private static string BuildCloRowLabel(string cloCode, int index)
    {
        return string.IsNullOrWhiteSpace(cloCode) ? $"CLO-{index + 1}" : cloCode;
    }

    private static List<PloRowCapacityDto> BuildPloRowCapacities(
        List<CloPloMatrixColumnDto> plos,
        List<Ploclomapping> mappings)
    {
        return plos.Select(plo =>
        {
            var used = mappings
                .Where(m => m.Ploid == plo.PloId && m.Percentage is > 0)
                .Sum(m => m.Percentage ?? 0);

            return new PloRowCapacityDto
            {
                PloId = plo.PloId,
                UsedPercentage = Math.Round(used, 2),
                RemainingPercentage = Math.Round(100m - used, 2)
            };
        }).ToList();
    }

    private async Task<CloTaskMatrixDto> BuildFullCloTaskMatrixAsync(int programCourseId, int sessionId)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId)
            ?? throw new InvalidOperationException("Program course not found.");

        var program = await _context.AcademicPrograms.FindAsync(programCourse.ProgramId)
            ?? throw new InvalidOperationException("Program not found.");

        var course = await _context.Courses.FindAsync(programCourse.CourseId)
            ?? throw new InvalidOperationException("Course not found.");

        var builtInNames = new[] { "Assignment", "Quiz", "Mid", "Final" };

        var clos = await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == sessionId)
            .OrderBy(c => c.Clocode)
            .ToListAsync();

        var builtInTypes = await _context.AssessmentTypes
            .Where(t => builtInNames.Contains(t.AssessmentTypeName))
            .OrderBy(t => t.AssessmentTypeId)
            .ToListAsync();

        var customLinks = await _context.CourseSessionTaskTypes
            .AsNoTracking()
            .Include(t => t.AssessmentType)
            .Where(t => t.ProgramCourseId == programCourseId && t.SessionId == sessionId)
            .OrderBy(t => t.DisplayOrder ?? int.MaxValue)
            .ThenBy(t => t.CourseSessionTaskTypeId)
            .ToListAsync();

        var assessmentTypes = builtInTypes
            .Select(t => (t.AssessmentTypeId, t.AssessmentTypeName, IsCustom: false))
            .ToList();

        foreach (var link in customLinks)
        {
            if (link.AssessmentType == null)
            {
                continue;
            }

            if (builtInNames.Any(n =>
                    string.Equals(n, link.AssessmentType.AssessmentTypeName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (assessmentTypes.Any(t => t.AssessmentTypeId == link.AssessmentTypeId))
            {
                continue;
            }

            assessmentTypes.Add((
                link.AssessmentTypeId,
                link.AssessmentType.AssessmentTypeName,
                true));
        }

        var mappings = await _context.CourseAssessmentTypeClomappings
            .Where(m => m.ProgramCourseId == programCourseId && m.SessionId == sessionId)
            .ToListAsync();

        var mappingLookup = mappings.ToDictionary(m => $"{m.Cloid}|{m.AssessmentTypeId}", m => m);

        var columns = assessmentTypes
            .Select((type, index) => new CloTaskMatrixColumnDto
            {
                AssessmentTypeId = type.AssessmentTypeId,
                AssessmentTypeName = type.AssessmentTypeName,
                ColumnLabel = string.IsNullOrWhiteSpace(type.AssessmentTypeName)
                    ? $"Task-{index + 1}"
                    : type.AssessmentTypeName,
                IsCustom = type.IsCustom
            })
            .ToList();

        var rows = clos
            .Select((clo, index) => new CloTaskMatrixRowDto
            {
                CloId = clo.Cloid,
                CloCode = clo.Clocode,
                RowLabel = BuildCloRowLabel(clo.Clocode, index),
                CloDescription = clo.Clodescription,
                Cells = assessmentTypes.Select(type =>
                {
                    mappingLookup.TryGetValue($"{clo.Cloid}|{type.AssessmentTypeId}", out var mapping);

                    return new CloTaskMatrixCellDto
                    {
                        AssessmentTypeId = type.AssessmentTypeId,
                        MappingId = mapping?.MappingId,
                        Percentage = mapping?.Percentage
                    };
                }).ToList()
            })
            .ToList();

        return new CloTaskMatrixDto
        {
            ProgramCourseId = programCourseId,
            ProgramId = programCourse.ProgramId,
            CourseId = programCourse.CourseId,
            CourseTitle = course.CourseTitle,
            CourseCode = course.CourseCode,
            ProgramName = program.ProgramName,
            AssessmentTypes = columns,
            Clos = rows,
            CloRowCapacities = rows.Select(clo =>
            {
                var used = mappings
                    .Where(m => m.Cloid == clo.CloId && m.Percentage is > 0)
                    .Sum(m => m.Percentage ?? 0);

                return new CloRowCapacityDto
                {
                    CloId = clo.CloId,
                    UsedPercentage = Math.Round(used, 2),
                    RemainingPercentage = Math.Round(100m - used, 2)
                };
            }).ToList()
        };
    }
}
