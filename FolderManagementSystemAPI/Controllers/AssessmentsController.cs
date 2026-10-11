using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AssessmentsController : ControllerBase
{
    public const string AssignmentTypeName = "Assignment";
    public const string QuizTypeName = "Quiz";
    public const string MidTypeName = "Mid";
    public const string FinalTypeName = "Final";

    public const string PurposeSolution = "Solution";
    public const string PurposeBestSample = "Best Sample";
    public const string PurposeAverageSample = "Average Sample";
    public const string PurposeWorstSample = "Worst Sample";

    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;
    private readonly ContentSubmissionService _contentSubmissionService;
    private readonly AssessmentFolderSyncService _assessmentFolderSync;
    private readonly IWebHostEnvironment _environment;
    private readonly string _connectionString;

    public AssessmentsController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService,
        ContentSubmissionService contentSubmissionService,
        AssessmentFolderSyncService assessmentFolderSync,
        IWebHostEnvironment environment)
    {
        _context = context;
        _permissionService = permissionService;
        _contentSubmissionService = contentSubmissionService;
        _assessmentFolderSync = assessmentFolderSync;
        _environment = environment;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    [HttpGet("offered-course/{offeredCourseId:int}/summary")]
    public async Task<ActionResult<AssessmentSummaryDto>> GetOfferedCourseSummary(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var context = await ResolveOfferedCourseContextAsync(offeredCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await CanAccessOfferedCourseAsync(context, teacherUserId))
        {
            return BadRequest(new { message = "You do not have access to this course section." });
        }

        var offered = await _context.OfferedCourses
            .AsNoTracking()
            .Include(oc => oc.Course)
            .Include(oc => oc.Section)
            .Include(oc => oc.TeacherUser)
            .FirstOrDefaultAsync(oc => oc.OfferedCourseId == offeredCourseId);

        if (offered == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var flags = await _permissionService.GetTeacherFlagsAsync(teacherUserId, context.ProgramCourseId);
        var canViewAll = _contentSubmissionService.CanViewAllStatuses(
            flags,
            ContentSubmissionTypeCodes.CloTaskMapping);
        var visibleStatuses = _contentSubmissionService.VisibleContentStatuses(canViewAll);

        var clos = await _context.Clos
            .AsNoTracking()
            .Where(c =>
                c.ProgramCourseId == context.ProgramCourseId &&
                c.SessionId == context.SessionId &&
                visibleStatuses.Contains(c.ContentStatus ?? ContentStatuses.Draft))
            .OrderBy(c => c.Clocode)
            .Select(c => new AssessmentSummaryCloDto
            {
                CloId = c.Cloid,
                CloCode = c.Clocode
            })
            .ToListAsync();

        var cloCodes = clos.Select(c => c.CloCode).ToList();
        var cloIdToCode = clos.ToDictionary(c => c.CloId, c => c.CloCode);

        var builtInNames = new[] { AssignmentTypeName, QuizTypeName, MidTypeName, FinalTypeName };
        var builtInTypes = (await _context.AssessmentTypes
            .AsNoTracking()
            .Where(t => builtInNames.Contains(t.AssessmentTypeName))
            .ToListAsync())
            .OrderBy(t => t.AssessmentTypeName switch
            {
                AssignmentTypeName => 1,
                QuizTypeName => 2,
                MidTypeName => 3,
                _ => 4
            })
            .ToList();

        var customTypeIds = await _context.CourseSessionTaskTypes
            .AsNoTracking()
            .Where(t =>
                t.ProgramCourseId == context.ProgramCourseId &&
                t.SessionId == context.SessionId)
            .OrderBy(t => t.DisplayOrder ?? 999)
            .ThenBy(t => t.CourseSessionTaskTypeId)
            .Select(t => t.AssessmentTypeId)
            .ToListAsync();

        var customTypes = await _context.AssessmentTypes
            .AsNoTracking()
            .Where(t => customTypeIds.Contains(t.AssessmentTypeId))
            .ToListAsync();

        var customOrdered = new List<AssessmentType>();
        foreach (var id in customTypeIds)
        {
            var type = customTypes.FirstOrDefault(t => t.AssessmentTypeId == id);
            if (type != null && !builtInNames.Contains(type.AssessmentTypeName))
            {
                customOrdered.Add(type);
            }
        }

        var taskTypes = builtInTypes.Concat(customOrdered).ToList();

        var designMappings = await _context.CourseAssessmentTypeClomappings
            .AsNoTracking()
            .Where(m =>
                m.ProgramCourseId == context.ProgramCourseId &&
                m.SessionId == context.SessionId &&
                visibleStatuses.Contains(m.ContentStatus ?? ContentStatuses.Draft) &&
                m.Percentage != null)
            .ToListAsync();

        var completedRows = await _context.AssessmentQuestionClomappings
            .AsNoTracking()
            .Where(m =>
                m.Question.Assessment.OfferedCourseId == offeredCourseId &&
                m.Percentage != null &&
                m.Percentage > 0)
            .Select(m => new
            {
                m.Question.Assessment.AssessmentTypeId,
                m.Cloid,
                Percentage = m.Percentage ?? 0
            })
            .ToListAsync();

        var completedByTypeClo = completedRows
            .GroupBy(x => (x.AssessmentTypeId, x.Cloid))
            .ToDictionary(
                g => g.Key,
                g => Math.Min(100m, Math.Round(g.Sum(x => x.Percentage), 2)));

        var rows = new List<AssessmentSummaryRowDto>();
        var designTotalsByClo = cloCodes.ToDictionary(c => c, _ => 0m, StringComparer.OrdinalIgnoreCase);

        foreach (var type in taskTypes)
        {
            var designShares = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var completedLocal = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var remainingLocal = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var clo in clos)
            {
                var design = designMappings
                    .Where(m => m.AssessmentTypeId == type.AssessmentTypeId && m.Cloid == clo.CloId)
                    .Select(m => m.Percentage ?? 0)
                    .DefaultIfEmpty(0)
                    .Sum();
                design = Math.Round(design, 2);
                designShares[clo.CloCode] = design;
                designTotalsByClo[clo.CloCode] = designTotalsByClo.GetValueOrDefault(clo.CloCode) + design;

                var completed = completedByTypeClo.TryGetValue((type.AssessmentTypeId, clo.CloId), out var used)
                    ? used
                    : 0m;
                completedLocal[clo.CloCode] = completed;
                remainingLocal[clo.CloCode] = Math.Round(Math.Max(0, design - completed), 2);
            }

            rows.Add(new AssessmentSummaryRowDto
            {
                TaskName = type.AssessmentTypeName,
                AssessmentTypeId = type.AssessmentTypeId,
                DesignShares = designShares,
                CompletedLocal = completedLocal,
                CompletedTotals = new Dictionary<string, decimal>(completedLocal, StringComparer.OrdinalIgnoreCase),
                RemainingLocal = remainingLocal,
                RemainingTotals = new Dictionary<string, decimal>(remainingLocal, StringComparer.OrdinalIgnoreCase)
            });
        }

        var warnings = new List<string>();
        foreach (var cloCode in cloCodes)
        {
            var total = Math.Round(designTotalsByClo.GetValueOrDefault(cloCode), 2);
            if (Math.Abs(total - 100m) > 0.01m && total > 0)
            {
                warnings.Add($"{cloCode} design shares across Assignment/Quiz/Mid/Final total {total:0.##}% (expected 100%).");
            }
        }

        var cloTaskStatus = await _context.ProgramCourseContentSubmissions
            .AsNoTracking()
            .Where(s =>
                s.ProgramCourseId == context.ProgramCourseId &&
                s.SessionId == context.SessionId &&
                s.ContentType.ContentTypeCode == ContentSubmissionTypeCodes.CloTaskMapping)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => s.SubmissionStatus)
            .FirstOrDefaultAsync();

        var mappingStatuses = designMappings
            .Select(m => m.ContentStatus ?? ContentStatuses.Draft)
            .Distinct()
            .ToList();

        var contentStatus = mappingStatuses.Contains(ContentStatuses.PendingReview)
            ? ContentStatuses.PendingReview
            : mappingStatuses.Contains(ContentStatuses.Approved) && mappingStatuses.Count == 1
                ? ContentStatuses.Approved
                : mappingStatuses.Contains(ContentStatuses.Rejected)
                    ? ContentStatuses.Rejected
                    : string.IsNullOrWhiteSpace(cloTaskStatus)
                        ? ContentStatuses.Draft
                        : cloTaskStatus;

        return Ok(new AssessmentSummaryDto
        {
            ProgramCourseId = context.ProgramCourseId,
            CourseTitle = offered.Course.CourseTitle,
            SectionName = offered.Section.SectionName,
            TeacherName = offered.TeacherUser.FullName,
            ContentStatus = contentStatus,
            Warnings = warnings,
            Clos = clos,
            CloCodes = cloCodes,
            Rows = rows
        });
    }

    [HttpGet("offered-course/{offeredCourseId:int}")]
    public async Task<ActionResult<IEnumerable<AssessmentListItemDto>>> ListByOfferedCourse(
        int offeredCourseId,
        [FromQuery] int teacherUserId,
        [FromQuery] string assessmentTypeName = AssignmentTypeName)
    {
        var context = await ResolveOfferedCourseContextAsync(offeredCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await CanAccessOfferedCourseAsync(context, teacherUserId))
        {
            return BadRequest(new { message = "You do not have access to this course section." });
        }

        var type = await GetAssessmentTypeAsync(assessmentTypeName);
        if (type == null)
        {
            return BadRequest(new { message = $"Assessment type '{assessmentTypeName}' was not found." });
        }

        await _assessmentFolderSync.EnsureDefaultTemplatesAsync(offeredCourseId, teacherUserId);

        var hasLab = await _assessmentFolderSync.CourseHasLabAsync(offeredCourseId);

        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
                    .ThenInclude(m => m.Clo)
            .Where(a => a.OfferedCourseId == offeredCourseId && a.AssessmentTypeId == type.AssessmentTypeId)
            .OrderBy(a => a.AssessmentNo ?? int.MaxValue)
            .ThenBy(a => a.AssessmentId)
            .ToListAsync();

        var items = assessments.Select(a => new AssessmentListItemDto
        {
            AssessmentId = a.AssessmentId,
            OfferedCourseId = a.OfferedCourseId,
            AssessmentTypeId = a.AssessmentTypeId,
            AssessmentTypeName = type.AssessmentTypeName,
            AssessmentNo = a.AssessmentNo,
            Title = a.Title,
            DueDate = a.DueDate.HasValue ? a.DueDate.Value.ToString("yyyy-MM-dd") : null,
            TotalMarks = a.TotalMarks,
            QuestionCount = a.AssessmentQuestions.Count(q => q.ParentQuestionId == null),
            IsEmpty = !a.AssessmentQuestions.Any(q => q.ParentQuestionId == null),
            IsSetupComplete = a.IsSetupComplete,
            IsRequired = a.AssessmentNo != null
                && a.AssessmentNo <= AssessmentFolderSyncService.GetRequiredTemplateCount(type.AssessmentTypeName, hasLab),
            CloCodes = a.AssessmentQuestions
                .SelectMany(q => q.AssessmentQuestionClomappings)
                .Select(m => m.Clo?.Clocode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code)
                .Cast<string>()
                .ToList()
        }).ToList();

        return Ok(items);
    }

    [HttpGet("{assessmentId:int}")]
    public async Task<ActionResult<AssessmentDetailDto>> GetAssessment(
        int assessmentId,
        [FromQuery] int teacherUserId)
    {
        var assessment = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
                    .ThenInclude(m => m.Clo)
            .Include(a => a.AssessmentFiles)
                .ThenInclude(f => f.File)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);

        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        var context = await ResolveOfferedCourseContextAsync(assessment.OfferedCourseId);
        if (context == null || !await CanAccessOfferedCourseAsync(context, teacherUserId))
        {
            return BadRequest(new { message = "You do not have access to this assessment." });
        }

        var designShares = await GetDesignSharesByCloAsync(
            context.ProgramCourseId,
            context.SessionId,
            assessment.AssessmentTypeId);
        var hasLab = await _assessmentFolderSync.CourseHasLabAsync(assessment.OfferedCourseId);
        return Ok(MapDetail(assessment, designShares, hasLab));
    }

    [HttpPost("{assessmentId:int}/questions")]
    public async Task<ActionResult<AssessmentDetailDto>> AddQuestion(
        int assessmentId,
        AddAssessmentQuestionDto dto)
    {
        var assessment = await _context.Assessments
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);

        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        if (assessment.IsSetupComplete)
        {
            return BadRequest(new
            {
                message = "Setup is complete and the mark sheet is unlocked. Tap Reopen setup before editing questions."
            });
        }

        var context = await ResolveOfferedCourseContextAsync(assessment.OfferedCourseId);
        if (context == null || !await CanManageAssessmentsAsync(
                context,
                dto.TeacherUserId,
                assessment.AssessmentType.AssessmentTypeName))
        {
            return BadRequest(new { message = "You do not have permission to edit this assessment." });
        }

        try
        {
            await _contentSubmissionService.EnsureMappingsApprovedForAssessmentsAsync(
                context.ProgramCourseId,
                context.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(dto.QuestionNo) || string.IsNullOrWhiteSpace(dto.QuestionText))
        {
            return BadRequest(new { message = "Question number and text are required." });
        }

        if (dto.Marks is null or <= 0)
        {
            return BadRequest(new { message = "Marks are required and must be greater than 0." });
        }

        AssessmentQuestion? parent = null;
        if (dto.ParentQuestionId is > 0)
        {
            parent = assessment.AssessmentQuestions.FirstOrDefault(q => q.QuestionId == dto.ParentQuestionId.Value);
            if (parent == null)
            {
                return BadRequest(new { message = "Parent question was not found on this assessment." });
            }
        }

        var isRoot = parent == null;
        var cloMappings = NormalizeCloMappings(dto.CloMappings, dto.CloIds) ?? new List<AssessmentQuestionCloInputDto>();
        var isFirstPart = !isRoot &&
            !assessment.AssessmentQuestions.Any(q => q.ParentQuestionId == parent!.QuestionId);

        // First part under a simple question: inherit CLO coverage from the stem, then stem becomes non-graded.
        if (!isRoot && isFirstPart && cloMappings.Count == 0 && parent!.AssessmentQuestionClomappings.Any())
        {
            cloMappings = parent.AssessmentQuestionClomappings
                .Where(m => m.Percentage is > 0)
                .Select(m => new AssessmentQuestionCloInputDto
                {
                    CloId = m.Cloid,
                    Percentage = m.Percentage ?? 0
                })
                .ToList();
        }

        if (isRoot || !isRoot)
        {
            if (cloMappings.Count == 0)
            {
                return BadRequest(new
                {
                    message = isRoot
                        ? "Select at least one CLO and enter its coverage % (0–100 of this assessment type’s share)."
                        : "Select at least one CLO and coverage % on this part (stems with parts are not graded)."
                });
            }

            var pctError = ValidateCloMappingPercentages(cloMappings);
            if (pctError != null)
            {
                return BadRequest(new { message = pctError });
            }

            var cloIds = cloMappings.Select(m => m.CloId).ToList();
            var cloValidation = await ValidateApprovedClosAsync(
                context,
                cloIds,
                assessment.AssessmentType.AssessmentTypeName);
            if (cloValidation != null)
            {
                return BadRequest(new { message = cloValidation });
            }

            var coverageError = await ValidateCloCoverageBudgetAsync(
                assessment.OfferedCourseId,
                assessment.AssessmentTypeId,
                cloMappings,
                // Stem CLO is cleared when parts exist — treat as moving coverage off the parent.
                excludeQuestionId: !isRoot && parent!.AssessmentQuestionClomappings.Count > 0
                    ? parent.QuestionId
                    : null);
            if (coverageError != null)
            {
                return BadRequest(new { message = coverageError });
            }
        }

        var duplicate = assessment.AssessmentQuestions.Any(q =>
            q.QuestionNo.Equals(dto.QuestionNo.Trim(), StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            return BadRequest(new { message = $"Question number '{dto.QuestionNo.Trim()}' already exists." });
        }

        var marksError = ValidateQuestionMarksBudget(
            assessment,
            parentQuestionId: dto.ParentQuestionId,
            proposedMarks: dto.Marks.Value,
            excludeQuestionId: null);
        if (marksError != null)
        {
            return BadRequest(new { message = marksError });
        }

        var siblings = assessment.AssessmentQuestions
            .Where(q => q.ParentQuestionId == dto.ParentQuestionId)
            .ToList();
        var displayOrder = siblings.Count == 0
            ? 1
            : siblings.Max(q => q.DisplayOrder ?? 0) + 1;

        var question = new AssessmentQuestion
        {
            AssessmentId = assessmentId,
            ParentQuestionId = dto.ParentQuestionId,
            DisplayOrder = displayOrder,
            QuestionNo = dto.QuestionNo.Trim(),
            QuestionText = dto.QuestionText.Trim(),
            Marks = dto.Marks
        };

        foreach (var mapping in cloMappings)
        {
            question.AssessmentQuestionClomappings.Add(new AssessmentQuestionClomapping
            {
                Cloid = mapping.CloId,
                Percentage = Math.Round(mapping.Percentage, 2)
            });
        }

        _context.AssessmentQuestions.Add(question);
        await _context.SaveChangesAsync();

        // Stem + parts: parent loses CLO/marks ownership; only parts are graded.
        if (parent != null)
        {
            if (parent.AssessmentQuestionClomappings.Count > 0)
            {
                _context.AssessmentQuestionClomappings.RemoveRange(parent.AssessmentQuestionClomappings);
                parent.AssessmentQuestionClomappings.Clear();
            }

            var orphanParentMarks = await _context.StudentQuestionMarks
                .Where(m => m.QuestionId == parent.QuestionId)
                .ToListAsync();
            if (orphanParentMarks.Count > 0)
            {
                _context.StudentQuestionMarks.RemoveRange(orphanParentMarks);
            }

            // Reload children including the new part for sync.
            await _context.Entry(assessment).Collection(a => a.AssessmentQuestions).LoadAsync();
            SyncStemMarksFromParts(assessment, parent.QuestionId);
            await _context.SaveChangesAsync();
        }

        await _assessmentFolderSync.SyncQuestionPdfAsync(assessmentId, dto.TeacherUserId);
        return await GetAssessment(assessmentId, dto.TeacherUserId);
    }

    [HttpPut("{assessmentId:int}/questions/{questionId:int}")]
    public async Task<ActionResult<AssessmentDetailDto>> UpdateQuestion(
        int assessmentId,
        int questionId,
        UpdateAssessmentQuestionDto dto)
    {
        var assessment = await _context.Assessments
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);

        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        var question = assessment.AssessmentQuestions.FirstOrDefault(q => q.QuestionId == questionId);
        if (question == null)
        {
            return NotFound(new { message = "Question not found." });
        }

        if (assessment.IsSetupComplete)
        {
            return BadRequest(new
            {
                message = "Setup is complete and the mark sheet is unlocked. Tap Reopen setup before editing questions."
            });
        }

        var context = await ResolveOfferedCourseContextAsync(assessment.OfferedCourseId);
        if (context == null || !await CanManageAssessmentsAsync(
                context,
                dto.TeacherUserId,
                assessment.AssessmentType.AssessmentTypeName))
        {
            return BadRequest(new { message = "You do not have permission to edit this assessment." });
        }

        if (string.IsNullOrWhiteSpace(dto.QuestionNo) || string.IsNullOrWhiteSpace(dto.QuestionText))
        {
            return BadRequest(new { message = "Question number and text are required." });
        }

        if (dto.Marks is null or <= 0)
        {
            return BadRequest(new { message = "Marks are required and must be greater than 0." });
        }

        var duplicate = assessment.AssessmentQuestions.Any(q =>
            q.QuestionId != questionId &&
            q.QuestionNo.Equals(dto.QuestionNo.Trim(), StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return BadRequest(new { message = $"Question number '{dto.QuestionNo.Trim()}' already exists." });
        }

        var hasChildren = assessment.AssessmentQuestions.Any(q => q.ParentQuestionId == questionId);
        var isRoot = question.ParentQuestionId == null;

        // Stem with parts: edit text/number only. Marks = sum of parts; CLO lives on parts.
        if (hasChildren)
        {
            question.QuestionNo = dto.QuestionNo.Trim();
            question.QuestionText = dto.QuestionText.Trim();
            if (question.AssessmentQuestionClomappings.Count > 0)
            {
                _context.AssessmentQuestionClomappings.RemoveRange(question.AssessmentQuestionClomappings);
                question.AssessmentQuestionClomappings.Clear();
            }

            SyncStemMarksFromParts(assessment, questionId);
            await _context.SaveChangesAsync();

            return await GetAssessment(assessmentId, dto.TeacherUserId);
        }

        var marksError = ValidateQuestionMarksBudget(
            assessment,
            parentQuestionId: question.ParentQuestionId,
            proposedMarks: dto.Marks.Value,
            excludeQuestionId: questionId);
        if (marksError != null)
        {
            return BadRequest(new { message = marksError });
        }

        var cloMappings = NormalizeCloMappings(dto.CloMappings, dto.CloIds) ?? new List<AssessmentQuestionCloInputDto>();

        if (cloMappings.Count == 0)
        {
            return BadRequest(new
            {
                message = isRoot
                    ? "Select at least one CLO and enter its coverage %."
                    : "Select at least one CLO and coverage % on this part."
            });
        }

        var pctError = ValidateCloMappingPercentages(cloMappings);
        if (pctError != null)
        {
            return BadRequest(new { message = pctError });
        }

        var cloValidation = await ValidateApprovedClosAsync(
            context,
            cloMappings.Select(m => m.CloId).ToList(),
            assessment.AssessmentType.AssessmentTypeName);
        if (cloValidation != null)
        {
            return BadRequest(new { message = cloValidation });
        }

        var coverageError = await ValidateCloCoverageBudgetAsync(
            assessment.OfferedCourseId,
            assessment.AssessmentTypeId,
            cloMappings,
            excludeQuestionId: questionId);
        if (coverageError != null)
        {
            return BadRequest(new { message = coverageError });
        }

        question.QuestionNo = dto.QuestionNo.Trim();
        question.QuestionText = dto.QuestionText.Trim();
        question.Marks = dto.Marks;

        _context.AssessmentQuestionClomappings.RemoveRange(question.AssessmentQuestionClomappings);
        question.AssessmentQuestionClomappings.Clear();
        foreach (var mapping in cloMappings)
        {
            question.AssessmentQuestionClomappings.Add(new AssessmentQuestionClomapping
            {
                QuestionId = question.QuestionId,
                Cloid = mapping.CloId,
                Percentage = Math.Round(mapping.Percentage, 2)
            });
        }

        if (question.ParentQuestionId is int parentId)
        {
            SyncStemMarksFromParts(assessment, parentId);
        }

        await _context.SaveChangesAsync();
        await _assessmentFolderSync.SyncQuestionPdfAsync(assessmentId, dto.TeacherUserId);

        return await GetAssessment(assessmentId, dto.TeacherUserId);
    }

    [HttpPost("{assessmentId:int}/files")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AssessmentFileUploadResultDto>> UploadAssessmentFile(
        int assessmentId,
        [FromForm] int teacherUserId,
        [FromForm] string filePurpose,
        [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please select a valid file." });
        }

        var purpose = NormalizeFilePurpose(filePurpose);
        if (purpose == null)
        {
            return BadRequest(new
            {
                message = "Invalid file purpose. Use Question Paper, Solution, Best Sample, Average Sample, or Worst Sample."
            });
        }

        var assessment = await _context.Assessments
            .Include(a => a.AssessmentType)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);

        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        var context = await ResolveOfferedCourseContextAsync(assessment.OfferedCourseId);
        if (context == null || !await CanManageAssessmentsAsync(
                context,
                teacherUserId,
                assessment.AssessmentType.AssessmentTypeName))
        {
            return BadRequest(new { message = "You do not have permission to upload files for this assessment." });
        }

        var typePrefix = GetFileCategoryPrefix(assessment.AssessmentType.AssessmentTypeName);
        var (templateCode, fileCategory) = ResolveFolderTarget(typePrefix, purpose);

        try
        {
            var directory = await EnsureTemplateDirectoryAsync(
                assessment.OfferedCourseId,
                teacherUserId,
                templateCode);

            var absoluteDirectoryPath = GetAbsolutePath(directory.DirectoryPath);
            Directory.CreateDirectory(absoluteDirectoryPath);

            var safeOriginalName = SanitizeFileName(file.FileName);

            var safeFileName =
                $"A{assessment.AssessmentNo ?? assessment.AssessmentId}_{purpose.Replace(" ", "")}_{DateTime.Now:yyyyMMddHHmmssfff}_{safeOriginalName}";
            var relativeFilePath = $"{directory.DirectoryPath}/{safeFileName}";
            var absoluteFilePath = GetAbsolutePath(relativeFilePath);

            await using (var stream = new FileStream(absoluteFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            int fileId;
            await using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var insertFile = new SqlCommand(@"
                    INSERT INTO CourseFolderFiles
                    (
                        DirectoryID,
                        UploadedByUserID,
                        FileName,
                        FilePath,
                        FileCategory,
                        Remarks
                    )
                    OUTPUT INSERTED.FileID
                    VALUES
                    (
                        @DirectoryID,
                        @UploadedByUserID,
                        @FileName,
                        @FilePath,
                        @FileCategory,
                        @Remarks
                    );
                ", connection);

                insertFile.Parameters.AddWithValue("@DirectoryID", directory.DirectoryId);
                insertFile.Parameters.AddWithValue("@UploadedByUserID", teacherUserId);
                insertFile.Parameters.AddWithValue("@FileName", safeFileName);
                insertFile.Parameters.AddWithValue("@FilePath", relativeFilePath);
                insertFile.Parameters.AddWithValue("@FileCategory", fileCategory);
                insertFile.Parameters.AddWithValue(
                    "@Remarks",
                    $"AssessmentID={assessmentId};Purpose={purpose}");

                fileId = Convert.ToInt32(await insertFile.ExecuteScalarAsync());
            }

            var existingLinks = await _context.AssessmentFiles
                .Where(af => af.AssessmentId == assessmentId && af.FilePurpose == purpose)
                .ToListAsync();

            if (existingLinks.Count > 0)
            {
                _context.AssessmentFiles.RemoveRange(existingLinks);
            }

            _context.AssessmentFiles.Add(new AssessmentFile
            {
                AssessmentId = assessmentId,
                FileId = fileId,
                FilePurpose = purpose
            });

            await _context.SaveChangesAsync();

            var detailResult = await GetAssessment(assessmentId, teacherUserId);
            if (detailResult.Result is not OkObjectResult ok || ok.Value is not AssessmentDetailDto detail)
            {
                return detailResult.Result ?? BadRequest(new { message = "Upload saved but detail reload failed." });
            }

            return Ok(new AssessmentFileUploadResultDto
            {
                Message = $"{purpose} uploaded to master folder ({templateCode} — {fileCategory}).",
                Assessment = detail
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{assessmentId:int}/parse-question-paper")]
    public async Task<ActionResult<ParsedQuestionPaperDto>> ParseQuestionPaper(
        int assessmentId,
        [FromQuery] int teacherUserId)
    {
        var assessment = await _context.Assessments
            .Include(a => a.AssessmentType)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);
        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        var context = await ResolveOfferedCourseContextAsync(assessment.OfferedCourseId);
        if (context == null || !await CanAccessOfferedCourseAsync(context, teacherUserId))
        {
            return BadRequest(new { message = "You do not have access to this assessment." });
        }

        var file = await (
            from link in _context.AssessmentFiles.AsNoTracking()
            join folderFile in _context.CourseFolderFiles.AsNoTracking() on link.FileId equals folderFile.FileId
            where link.AssessmentId == assessmentId
                && link.FilePurpose == AssessmentFolderSyncService.PurposeQuestion
            orderby folderFile.UploadedAt descending
            select folderFile).FirstOrDefaultAsync();

        if (file == null)
        {
            return BadRequest(new { message = "Upload a question paper first, then read questions from it." });
        }

        var absolute = FolderStoragePath.ResolveExistingFile(_environment, file.FilePath);
        if (absolute == null || !System.IO.File.Exists(absolute))
        {
            return BadRequest(new { message = "The uploaded question paper file was not found on the server." });
        }

        var parsed = QuestionPaperParser.Parse(await System.IO.File.ReadAllBytesAsync(absolute), file.FileName);
        var clos = await _context.Clos
            .AsNoTracking()
            .Where(c => c.ProgramCourseId == context.ProgramCourseId)
            .Select(c => new { c.Cloid, c.Clocode })
            .ToListAsync();

        AssessmentQuestionCloDto[] MapClos(IReadOnlyList<string> codes)
        {
            var mapped = new List<AssessmentQuestionCloDto>();
            foreach (var code in codes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var clo = clos.FirstOrDefault(c =>
                    string.Equals(c.Clocode, code, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Clocode.Replace(" ", ""), code.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (clo == null)
                {
                    continue;
                }

                mapped.Add(new AssessmentQuestionCloDto
                {
                    CloId = clo.Cloid,
                    CloCode = clo.Clocode,
                    Percentage = 0
                });
            }

            if (mapped.Count == 1)
            {
                mapped[0].Percentage = 100;
            }

            return mapped.ToArray();
        }

        return Ok(new ParsedQuestionPaperDto
        {
            Message = parsed.Message,
            Questions = parsed.Questions.Select(q => new ParsedQuestionDraftDto
            {
                QuestionNo = q.QuestionNo,
                QuestionText = q.QuestionText,
                Marks = q.Marks,
                CloMappings = MapClos(q.CloCodes).ToList(),
                Parts = q.Parts.Select(p => new ParsedQuestionDraftDto
                {
                    QuestionNo = p.QuestionNo,
                    QuestionText = p.QuestionText,
                    Marks = p.Marks,
                    CloMappings = MapClos(p.CloCodes).ToList()
                }).ToList()
            }).ToList()
        });
    }

    [HttpGet("offered-course/{offeredCourseId:int}/available-clos")]
    public async Task<ActionResult<IEnumerable<AssessmentAvailableCloDto>>> GetAvailableClos(
        int offeredCourseId,
        [FromQuery] int teacherUserId,
        [FromQuery] string? assessmentTypeName = null)
    {
        var context = await ResolveOfferedCourseContextAsync(offeredCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await CanAccessOfferedCourseAsync(context, teacherUserId))
        {
            return BadRequest(new { message = "You do not have access to this course section." });
        }

        var typeName = string.IsNullOrWhiteSpace(assessmentTypeName)
            ? null
            : assessmentTypeName.Trim();

        int? assessmentTypeId = null;
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var type = await GetAssessmentTypeAsync(typeName);
            if (type == null)
            {
                return BadRequest(new { message = $"Assessment type '{typeName}' was not found." });
            }

            assessmentTypeId = type.AssessmentTypeId;
        }

        var query = _context.Clos
            .AsNoTracking()
            .Where(c =>
                c.ProgramCourseId == context.ProgramCourseId &&
                c.SessionId == context.SessionId &&
                c.ContentStatus == ContentStatuses.Approved);

        if (assessmentTypeId.HasValue)
        {
            query = query.Where(c =>
                _context.CourseAssessmentTypeClomappings.Any(m =>
                    m.Cloid == c.Cloid &&
                    m.ProgramCourseId == context.ProgramCourseId &&
                    m.SessionId == context.SessionId &&
                    m.AssessmentTypeId == assessmentTypeId.Value &&
                    m.ContentStatus == ContentStatuses.Approved &&
                    m.Percentage != null &&
                    m.Percentage > 0));
        }

        var clos = await query
            .OrderBy(c => c.Clocode)
            .Select(c => new AssessmentAvailableCloDto
            {
                CloId = c.Cloid,
                CloCode = c.Clocode,
                CloDescription = c.Clodescription
            })
            .ToListAsync();

        if (assessmentTypeId.HasValue && clos.Count > 0)
        {
            var shares = await GetDesignSharesByCloAsync(
                context.ProgramCourseId,
                context.SessionId,
                assessmentTypeId.Value);

            var cloIds = clos.Select(c => c.CloId).ToList();
            var usedRows = await _context.AssessmentQuestionClomappings
                .AsNoTracking()
                .Where(m =>
                    m.Question.Assessment.OfferedCourseId == offeredCourseId &&
                    m.Question.Assessment.AssessmentTypeId == assessmentTypeId.Value &&
                    cloIds.Contains(m.Cloid) &&
                    m.Percentage != null &&
                    m.Percentage > 0)
                .GroupBy(m => m.Cloid)
                .Select(g => new { CloId = g.Key, Total = g.Sum(x => x.Percentage ?? 0) })
                .ToListAsync();

            var usedByClo = usedRows.ToDictionary(x => x.CloId, x => x.Total);

            foreach (var clo in clos)
            {
                if (shares.TryGetValue(clo.CloId, out var share))
                {
                    clo.DesignSharePercent = share;
                }

                var used = usedByClo.TryGetValue(clo.CloId, out var total) ? total : 0m;
                if (used < 0) used = 0;
                if (used > 100) used = 100;
                clo.UsedLocalPercent = Math.Round(used, 2);
                clo.RemainingLocalPercent = Math.Round(Math.Max(0, 100m - used), 2);
            }
        }
        else
        {
            foreach (var clo in clos)
            {
                clo.RemainingLocalPercent = 100;
            }
        }

        return Ok(clos);
    }

    [HttpPut("{assessmentId:int}/meta")]
    public async Task<ActionResult<AssessmentDetailDto>> UpdateAssessmentMeta(
        int assessmentId,
        [FromBody] UpdateAssessmentMetaDto dto)
    {
        if (dto.TeacherUserId <= 0)
        {
            return BadRequest(new { message = "Teacher is required." });
        }

        if (dto.TotalMarks <= 0)
        {
            return BadRequest(new { message = "Total marks are required and must be greater than 0." });
        }

        var assessment = await _context.Assessments
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);

        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        if (assessment.IsSetupComplete)
        {
            return BadRequest(new
            {
                message = "Setup is complete. Tap Reopen setup before changing title, due date, or total marks."
            });
        }

        var context = await ResolveOfferedCourseContextAsync(assessment.OfferedCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var typeName = assessment.AssessmentType?.AssessmentTypeName ?? AssignmentTypeName;
        if (!await CanManageAssessmentsAsync(context, dto.TeacherUserId, typeName))
        {
            return BadRequest(new { message = "You do not have permission to update this assessment." });
        }

        var isExamType =
            string.Equals(typeName, MidTypeName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, FinalTypeName, StringComparison.OrdinalIgnoreCase);

        string title;
        DateOnly? dueDate;

        if (isExamType)
        {
            if (string.Equals(typeName, MidTypeName, StringComparison.OrdinalIgnoreCase))
            {
                title = "Mid Exams";
            }
            else
            {
                var hasLab = await _assessmentFolderSync.CourseHasLabAsync(assessment.OfferedCourseId);
                title = AssessmentFolderSyncService.GetFinalTitle(assessment.AssessmentNo ?? 1, hasLab);
            }
            dueDate = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(new { message = "Title is required." });
            }

            if (string.IsNullOrWhiteSpace(dto.DueDate))
            {
                return BadRequest(new { message = "Due date is required." });
            }

            if (!DateOnly.TryParse(dto.DueDate, out var parsedDueDate))
            {
                return BadRequest(new { message = "Due date must be a valid date (YYYY-MM-DD)." });
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (parsedDueDate < today)
            {
                return BadRequest(new { message = "Due date cannot be in the past." });
            }

            title = dto.Title.Trim();
            dueDate = parsedDueDate;
        }

        var rootMarks = assessment.AssessmentQuestions
            .Where(q => q.ParentQuestionId == null)
            .Sum(q => q.Marks ?? 0);
        // Assessment TotalMarks is the course weight; question marks are a separate paper scale.

        assessment.Title = title;
        assessment.DueDate = dueDate;
        assessment.TotalMarks = dto.TotalMarks;
        await _context.SaveChangesAsync();

        return await GetAssessment(assessmentId, dto.TeacherUserId);
    }

    [HttpPost("{assessmentId:int}/reopen-setup")]
    public async Task<ActionResult<AssessmentDetailDto>> ReopenAssessmentSetup(
        int assessmentId,
        [FromBody] ReopenAssessmentSetupDto dto)
    {
        if (dto.TeacherUserId <= 0)
        {
            return BadRequest(new { message = "Teacher is required." });
        }

        var assessment = await _context.Assessments
            .Include(a => a.AssessmentType)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);

        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        var context = await ResolveOfferedCourseContextAsync(assessment.OfferedCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var typeName = assessment.AssessmentType?.AssessmentTypeName ?? AssignmentTypeName;
        if (!await CanManageAssessmentsAsync(context, dto.TeacherUserId, typeName))
        {
            return BadRequest(new { message = "You do not have permission to reopen setup for this assessment." });
        }

        assessment.IsSetupComplete = false;
        assessment.SetupCompletedAt = null;
        await _context.SaveChangesAsync();

        return await GetAssessment(assessmentId, dto.TeacherUserId);
    }

    [HttpPost("{assessmentId:int}/complete-setup")]
    public async Task<ActionResult<AssessmentDetailDto>> CompleteAssessmentSetup(
        int assessmentId,
        [FromBody] CompleteAssessmentSetupDto dto)
    {
        if (dto.TeacherUserId <= 0)
        {
            return BadRequest(new { message = "Teacher is required." });
        }

        var assessment = await _context.Assessments
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);

        if (assessment == null)
        {
            return NotFound(new { message = "Assessment not found." });
        }

        var context = await ResolveOfferedCourseContextAsync(assessment.OfferedCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var typeName = assessment.AssessmentType?.AssessmentTypeName ?? AssignmentTypeName;
        if (!await CanManageAssessmentsAsync(context, dto.TeacherUserId, typeName))
        {
            return BadRequest(new { message = "You do not have permission to complete setup for this assessment." });
        }

        if (assessment.IsSetupComplete)
        {
            await _assessmentFolderSync.SyncQuestionPdfAsync(assessmentId, dto.TeacherUserId);
            return await GetAssessment(assessmentId, dto.TeacherUserId);
        }

        if (assessment.TotalMarks is null or <= 0)
        {
            return BadRequest(new { message = "Set total marks before completing setup." });
        }

        var isExamType =
            string.Equals(typeName, MidTypeName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, FinalTypeName, StringComparison.OrdinalIgnoreCase);

        if (!isExamType)
        {
            if (string.IsNullOrWhiteSpace(assessment.Title) ||
                assessment.Title.StartsWith("Untitled", StringComparison.OrdinalIgnoreCase) ||
                assessment.Title.StartsWith("New ", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Set a proper title before completing setup." });
            }

            if (assessment.DueDate == null)
            {
                return BadRequest(new { message = "Set a due date before completing setup." });
            }
        }

        var questions = assessment.AssessmentQuestions.ToList();
        var parentIdsWithChildren = questions
            .Where(q => q.ParentQuestionId != null)
            .Select(q => q.ParentQuestionId!.Value)
            .ToHashSet();
        var gradeable = questions
            .Where(q => !parentIdsWithChildren.Contains(q.QuestionId))
            .ToList();

        if (gradeable.Count == 0)
        {
            return BadRequest(new { message = "Add at least one question with marks before completing setup." });
        }

        if (gradeable.Any(q => q.Marks is null or <= 0))
        {
            return BadRequest(new { message = "Every gradeable question/part must have marks greater than 0." });
        }

        var roots = questions.Where(q => q.ParentQuestionId == null).ToList();

        // CLO on every gradeable leaf (simple question or part). Stems with parts are not graded.
        foreach (var leaf in gradeable)
        {
            if (!leaf.AssessmentQuestionClomappings.Any())
            {
                return BadRequest(new
                {
                    message =
                        $"Map at least one CLO on {leaf.QuestionNo} before completing setup."
                });
            }
        }

        var paperMarks = gradeable.Sum(q => q.Marks ?? 0);
        if (paperMarks <= 0)
        {
            return BadRequest(new { message = "Question marks must be greater than 0 before completing setup." });
        }

        // Keep stem marks in sync with parts (display / paper weight of the compound question).
        foreach (var parent in questions.Where(q => parentIdsWithChildren.Contains(q.QuestionId)))
        {
            var partSum = questions
                .Where(q => q.ParentQuestionId == parent.QuestionId)
                .Sum(q => q.Marks ?? 0);
            parent.Marks = partSum;
        }

        // Every CLO with a design share for this assessment type must have 100% local coverage
        // (Mid, Final, Assignment, Quiz — same rule).
        var designShares = await GetDesignSharesByCloAsync(
            context.ProgramCourseId,
            context.SessionId,
            assessment.AssessmentTypeId);

        if (designShares.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    $"No approved CLO and Task share for {typeName}. Complete CLO and Task mapping before finishing setup."
            });
        }

        var usedByClo = await _context.AssessmentQuestionClomappings
            .AsNoTracking()
            .Where(m =>
                m.Question.AssessmentId == assessmentId &&
                m.Percentage != null &&
                m.Percentage > 0)
            .GroupBy(m => m.Cloid)
            .Select(g => new { CloId = g.Key, Total = g.Sum(x => x.Percentage ?? 0) })
            .ToDictionaryAsync(x => x.CloId, x => x.Total);

        var cloCodes = await _context.Clos
            .AsNoTracking()
            .Where(c => designShares.Keys.Contains(c.Cloid))
            .ToDictionaryAsync(c => c.Cloid, c => c.Clocode);

        foreach (var (cloId, share) in designShares.OrderBy(kv => cloCodes.GetValueOrDefault(kv.Key)))
        {
            if (share <= 0)
            {
                continue;
            }

            var used = usedByClo.TryGetValue(cloId, out var total) ? total : 0m;
            if (used > 100m)
            {
                used = 100m;
            }

            if (Math.Abs(used - 100m) > 0.01m)
            {
                var code = cloCodes.TryGetValue(cloId, out var cloCode) ? cloCode : $"CLO {cloId}";
                return BadRequest(new
                {
                    message =
                        $"{code} coverage for {typeName} is {used:0.##}% (need 100% of that CLO’s {typeName} share). Finish mapping question coverage before completing setup."
                });
            }
        }

        assessment.IsSetupComplete = true;
        assessment.SetupCompletedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        await _assessmentFolderSync.SyncQuestionPdfAsync(assessmentId, dto.TeacherUserId);

        return await GetAssessment(assessmentId, dto.TeacherUserId);
    }

    [HttpPost]
    public async Task<ActionResult<AssessmentDetailDto>> CreateAssessment(CreateAssessmentDto dto)
    {
        if (dto.TeacherUserId <= 0 || dto.OfferedCourseId <= 0)
        {
            return BadRequest(new { message = "Teacher and offered course are required." });
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new { message = "Title is required." });
        }

        var context = await ResolveOfferedCourseContextAsync(dto.OfferedCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var typeName = string.IsNullOrWhiteSpace(dto.AssessmentTypeName)
            ? AssignmentTypeName
            : dto.AssessmentTypeName.Trim();

        if (!await CanManageAssessmentsAsync(context, dto.TeacherUserId, typeName))
        {
            return BadRequest(new { message = "You do not have permission to create assessments for this section." });
        }

        try
        {
            await _contentSubmissionService.EnsureMappingsApprovedForAssessmentsAsync(
                context.ProgramCourseId,
                context.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var type = await GetAssessmentTypeAsync(typeName);
        if (type == null)
        {
            return BadRequest(new { message = $"Assessment type '{typeName}' was not found." });
        }

        DateOnly? dueDate = null;
        if (!string.IsNullOrWhiteSpace(dto.DueDate))
        {
            if (!DateOnly.TryParse(dto.DueDate, out var parsedDueDate))
            {
                return BadRequest(new { message = "Due date must be a valid date (YYYY-MM-DD)." });
            }

            if (parsedDueDate < DateOnly.FromDateTime(DateTime.Today))
            {
                return BadRequest(new { message = "Due date cannot be in the past." });
            }

            dueDate = parsedDueDate;
        }

        if (dto.TotalMarks is null or <= 0)
        {
            return BadRequest(new { message = "Total marks are required and must be greater than 0." });
        }

        var questions = dto.Questions ?? new List<CreateAssessmentQuestionDto>();
        var normalizedByQuestion = new Dictionary<string, List<AssessmentQuestionCloInputDto>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var question in questions)
        {
            if (string.IsNullOrWhiteSpace(question.QuestionNo))
            {
                return BadRequest(new { message = "Each question needs a question number." });
            }

            if (string.IsNullOrWhiteSpace(question.QuestionText))
            {
                return BadRequest(new { message = "Each question needs question text." });
            }

            if (question.Marks is < 0)
            {
                return BadRequest(new { message = "Question marks cannot be negative." });
            }

            var cloMappings = NormalizeCloMappings(question.CloMappings, question.CloIds);
            if (cloMappings == null || cloMappings.Count == 0)
            {
                return BadRequest(new
                {
                    message = $"Select at least one CLO and coverage % for question {question.QuestionNo}."
                });
            }

            var pctError = ValidateCloMappingPercentages(cloMappings);
            if (pctError != null)
            {
                return BadRequest(new { message = $"{pctError} (question {question.QuestionNo})" });
            }

            normalizedByQuestion[question.QuestionNo.Trim()] = cloMappings;
        }

        var duplicateQuestionNos = questions
            .GroupBy(q => q.QuestionNo.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateQuestionNos.Count > 0)
        {
            return BadRequest(new { message = $"Duplicate question numbers: {string.Join(", ", duplicateQuestionNos)}" });
        }

        var requestedCloIds = normalizedByQuestion.Values
            .SelectMany(m => m.Select(x => x.CloId))
            .Distinct()
            .ToList();

        if (requestedCloIds.Count > 0)
        {
            var cloValidation = await ValidateApprovedClosAsync(context, requestedCloIds, typeName);
            if (cloValidation != null)
            {
                return BadRequest(new { message = cloValidation });
            }
        }

        // Within this create payload, coverage for the same CLO cannot exceed 100%.
        var draftCoverageTotals = normalizedByQuestion.Values
            .SelectMany(m => m)
            .GroupBy(m => m.CloId)
            .Select(g => new { CloId = g.Key, Total = g.Sum(x => x.Percentage) })
            .ToList();

        foreach (var row in draftCoverageTotals.Where(r => r.Total > 100m))
        {
            return BadRequest(new
            {
                message = $"CLO coverage across questions cannot exceed 100% of the {type.AssessmentTypeName} share (CLO id {row.CloId} totals {row.Total:0.##}%)."
            });
        }

        if (draftCoverageTotals.Count > 0)
        {
            var coverageError = await ValidateCloCoverageBudgetAsync(
                dto.OfferedCourseId,
                type.AssessmentTypeId,
                draftCoverageTotals.Select(r => new AssessmentQuestionCloInputDto
                {
                    CloId = r.CloId,
                    Percentage = r.Total
                }),
                excludeQuestionId: null);
            if (coverageError != null)
            {
                return BadRequest(new { message = coverageError });
            }
        }

        var assessmentNo = dto.AssessmentNo;
        if (assessmentNo is null or <= 0)
        {
            var maxNo = await _context.Assessments
                .Where(a =>
                    a.OfferedCourseId == dto.OfferedCourseId &&
                    a.AssessmentTypeId == type.AssessmentTypeId)
                .Select(a => (int?)a.AssessmentNo)
                .MaxAsync() ?? 0;

            assessmentNo = maxNo + 1;
        }

        var duplicateNo = await _context.Assessments.AnyAsync(a =>
            a.OfferedCourseId == dto.OfferedCourseId &&
            a.AssessmentTypeId == type.AssessmentTypeId &&
            a.AssessmentNo == assessmentNo);

        if (duplicateNo)
        {
            return BadRequest(new { message = $"{type.AssessmentTypeName} number {assessmentNo} already exists for this section." });
        }

        var assessment = new Assessment
        {
            OfferedCourseId = dto.OfferedCourseId,
            AssessmentTypeId = type.AssessmentTypeId,
            AssessmentNo = assessmentNo,
            Title = dto.Title.Trim(),
            DueDate = dueDate,
            TotalMarks = dto.TotalMarks,
            CreatedByUserId = dto.TeacherUserId,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var questionDto in questions)
        {
            var question = new AssessmentQuestion
            {
                QuestionNo = questionDto.QuestionNo.Trim(),
                QuestionText = questionDto.QuestionText.Trim(),
                Marks = questionDto.Marks
            };

            foreach (var mapping in normalizedByQuestion[questionDto.QuestionNo.Trim()])
            {
                question.AssessmentQuestionClomappings.Add(new AssessmentQuestionClomapping
                {
                    Cloid = mapping.CloId,
                    Percentage = Math.Round(mapping.Percentage, 2)
                });
            }

            assessment.AssessmentQuestions.Add(question);
        }

        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        var created = await LoadAssessmentDetailAsync(assessment.AssessmentId);
        var designShares = await GetDesignSharesByCloAsync(
            context.ProgramCourseId,
            context.SessionId,
            type.AssessmentTypeId);
        return Ok(MapDetail(
            created!,
            designShares,
            await _assessmentFolderSync.CourseHasLabAsync(dto.OfferedCourseId)));
    }

    private async Task<string?> ValidateApprovedClosAsync(
        OfferedCourseAssessmentContext context,
        IEnumerable<int> cloIds,
        string? assessmentTypeName = null)
    {
        var requestedCloIds = cloIds.Distinct().ToList();
        if (requestedCloIds.Count == 0)
        {
            return "Select at least one CLO.";
        }

        var validCloIds = await _context.Clos
            .AsNoTracking()
            .Where(c =>
                requestedCloIds.Contains(c.Cloid) &&
                c.ProgramCourseId == context.ProgramCourseId &&
                c.SessionId == context.SessionId &&
                c.ContentStatus == ContentStatuses.Approved)
            .Select(c => c.Cloid)
            .ToListAsync();

        if (requestedCloIds.Except(validCloIds).Any())
        {
            return "One or more selected CLOs are invalid for this course. Only approved CLOs for this subject can be mapped.";
        }

        if (string.IsNullOrWhiteSpace(assessmentTypeName))
        {
            return null;
        }

        var type = await GetAssessmentTypeAsync(assessmentTypeName.Trim());
        if (type == null)
        {
            return $"Assessment type '{assessmentTypeName}' was not found.";
        }

        var mappedCloIds = await _context.CourseAssessmentTypeClomappings
            .AsNoTracking()
            .Where(m =>
                m.ProgramCourseId == context.ProgramCourseId &&
                m.SessionId == context.SessionId &&
                m.AssessmentTypeId == type.AssessmentTypeId &&
                m.ContentStatus == ContentStatuses.Approved &&
                m.Percentage != null &&
                m.Percentage > 0 &&
                requestedCloIds.Contains(m.Cloid))
            .Select(m => m.Cloid)
            .Distinct()
            .ToListAsync();

        if (requestedCloIds.Except(mappedCloIds).Any())
        {
            return $"Only CLOs mapped to {type.AssessmentTypeName} in the approved CLO and Task matrix can be selected for this question.";
        }

        return null;
    }

    private async Task<Assessment?> LoadAssessmentDetailAsync(int assessmentId)
    {
        return await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
                    .ThenInclude(m => m.Clo)
            .Include(a => a.AssessmentFiles)
                .ThenInclude(f => f.File)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);
    }

    private static List<AssessmentQuestionCloInputDto>? NormalizeCloMappings(
        List<AssessmentQuestionCloInputDto>? cloMappings,
        List<int>? cloIds)
    {
        if (cloMappings != null && cloMappings.Count > 0)
        {
            return cloMappings
                .GroupBy(m => m.CloId)
                .Select(g => new AssessmentQuestionCloInputDto
                {
                    CloId = g.Key,
                    Percentage = g.Last().Percentage
                })
                .Where(m => m.CloId > 0)
                .ToList();
        }

        // Legacy CloIds-only requests are rejected — coverage % is required.
        if (cloIds != null && cloIds.Count > 0)
        {
            return null;
        }

        return null;
    }

    private static string? ValidateCloMappingPercentages(IEnumerable<AssessmentQuestionCloInputDto> mappings)
    {
        foreach (var mapping in mappings)
        {
            if (mapping.Percentage <= 0 || mapping.Percentage > 100)
            {
                return "Each CLO coverage % must be greater than 0 and at most 100 (of this assessment type’s share).";
            }
        }

        return null;
    }

    private async Task<Dictionary<int, decimal>> GetDesignSharesByCloAsync(
        int programCourseId,
        int sessionId,
        int assessmentTypeId)
    {
        return await _context.CourseAssessmentTypeClomappings
            .AsNoTracking()
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == sessionId &&
                m.AssessmentTypeId == assessmentTypeId &&
                m.ContentStatus == ContentStatuses.Approved &&
                m.Percentage != null &&
                m.Percentage > 0)
            .ToDictionaryAsync(m => m.Cloid, m => m.Percentage ?? 0);
    }

    private async Task<string?> ValidateCloCoverageBudgetAsync(
        int offeredCourseId,
        int assessmentTypeId,
        IEnumerable<AssessmentQuestionCloInputDto> additionalMappings,
        int? excludeQuestionId)
    {
        var additions = additionalMappings
            .GroupBy(m => m.CloId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Percentage));

        if (additions.Count == 0)
        {
            return null;
        }

        var cloIds = additions.Keys.ToList();
        var existing = await _context.AssessmentQuestionClomappings
            .AsNoTracking()
            .Where(m =>
                m.Question.Assessment.OfferedCourseId == offeredCourseId &&
                m.Question.Assessment.AssessmentTypeId == assessmentTypeId &&
                (excludeQuestionId == null || m.QuestionId != excludeQuestionId.Value) &&
                cloIds.Contains(m.Cloid) &&
                m.Percentage != null &&
                m.Percentage > 0)
            .GroupBy(m => m.Cloid)
            .Select(g => new { CloId = g.Key, Total = g.Sum(x => x.Percentage ?? 0) })
            .ToListAsync();

        var usedByClo = existing.ToDictionary(x => x.CloId, x => x.Total);

        foreach (var (cloId, add) in additions)
        {
            var used = usedByClo.TryGetValue(cloId, out var existingTotal) ? existingTotal : 0m;
            var next = used + add;
            if (next > 100.01m)
            {
                var cloCode = await _context.Clos
                    .AsNoTracking()
                    .Where(c => c.Cloid == cloId)
                    .Select(c => c.Clocode)
                    .FirstOrDefaultAsync() ?? $"CLO {cloId}";

                return $"{cloCode} coverage for this assessment type would be {next:0.##}% (already used {used:0.##}%). Total cannot exceed 100% of the type share.";
            }
        }

        return null;
    }

    private static string? ValidateQuestionMarksBudget(
        Assessment assessment,
        int? parentQuestionId,
        decimal proposedMarks,
        int? excludeQuestionId)
    {
        if (proposedMarks <= 0)
        {
            return "Marks must be greater than 0.";
        }

        // Roots and parts use a free paper scale — not capped by assessment TotalMarks
        // or by a parent marks budget. Parent marks become the sum of parts.
        if (parentQuestionId == null)
        {
            if (assessment.TotalMarks is null or <= 0)
            {
                return "Set the assessment total marks before adding questions.";
            }

            return null;
        }

        var parent = assessment.AssessmentQuestions.FirstOrDefault(q => q.QuestionId == parentQuestionId.Value);
        if (parent == null)
        {
            return "Parent question was not found.";
        }

        return null;
    }

    /// <summary>When a question has parts, stem marks = sum of direct parts (display only; grading is on leaves).</summary>
    private static void SyncStemMarksFromParts(Assessment assessment, int parentQuestionId)
    {
        var parent = assessment.AssessmentQuestions.FirstOrDefault(q => q.QuestionId == parentQuestionId);
        if (parent == null)
        {
            return;
        }

        var children = assessment.AssessmentQuestions
            .Where(q => q.ParentQuestionId == parentQuestionId)
            .ToList();
        if (children.Count == 0)
        {
            return;
        }

        parent.Marks = children.Sum(c => c.Marks ?? 0);
    }

    private AssessmentDetailDto MapDetail(
        Assessment assessment,
        IReadOnlyDictionary<int, decimal>? designShares = null,
        bool hasLab = false)
    {
        designShares ??= new Dictionary<int, decimal>();

        AssessmentQuestionDto MapQuestion(AssessmentQuestion q)
        {
            var cloMappings = q.AssessmentQuestionClomappings
                .Select(m =>
                {
                    var pct = m.Percentage ?? 0;
                    var design = designShares.TryGetValue(m.Cloid, out var share) ? share : 0;
                    return new AssessmentQuestionCloDto
                    {
                        CloId = m.Cloid,
                        CloCode = m.Clo?.Clocode ?? string.Empty,
                        Percentage = pct,
                        DesignSharePercent = design,
                        EffectiveCloPercent = Math.Round(pct * design / 100m, 2)
                    };
                })
                .OrderBy(m => m.CloCode)
                .ToList();

            return new AssessmentQuestionDto
            {
                QuestionId = q.QuestionId,
                ParentQuestionId = q.ParentQuestionId,
                QuestionNo = q.QuestionNo,
                QuestionText = q.QuestionText,
                Marks = q.Marks,
                CloIds = cloMappings.Select(m => m.CloId).ToList(),
                CloCodes = cloMappings
                    .Select(m => m.CloCode)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .ToList(),
                CloMappings = cloMappings,
                Parts = new List<AssessmentQuestionDto>()
            };
        }

        var byId = assessment.AssessmentQuestions
            .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
            .ThenBy(q => q.QuestionNo)
            .ThenBy(q => q.QuestionId)
            .ToDictionary(q => q.QuestionId, MapQuestion);

        foreach (var q in assessment.AssessmentQuestions)
        {
            if (q.ParentQuestionId is int parentId &&
                byId.TryGetValue(parentId, out var parentDto) &&
                byId.TryGetValue(q.QuestionId, out var childDto))
            {
                parentDto.Parts.Add(childDto);
            }
        }

        var roots = assessment.AssessmentQuestions
            .Where(q => q.ParentQuestionId == null)
            .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
            .ThenBy(q => q.QuestionNo)
            .ThenBy(q => q.QuestionId)
            .Select(q => byId[q.QuestionId])
            .ToList();

        AssessmentLinkedFileDto? MapFile(string purpose)
        {
            var link = assessment.AssessmentFiles
                .Where(f => f.FilePurpose == purpose)
                .OrderByDescending(f => f.AssessmentFileId)
                .FirstOrDefault();

            if (link?.File == null)
            {
                return null;
            }

            return new AssessmentLinkedFileDto
            {
                AssessmentFileId = link.AssessmentFileId,
                FileId = link.FileId,
                FilePurpose = link.FilePurpose,
                FileName = link.File.FileName,
                FileCategory = link.File.FileCategory,
                DownloadUrl = $"/api/TeacherSubmission/files/{link.FileId}/download"
            };
        }

        var questionPaper = MapFile(AssessmentFolderSyncService.PurposeQuestion);
        var solution = MapFile(PurposeSolution);
        var best = MapFile(PurposeBestSample);
        var average = MapFile(PurposeAverageSample);
        var worst = MapFile(PurposeWorstSample);

        return new AssessmentDetailDto
        {
            AssessmentId = assessment.AssessmentId,
            OfferedCourseId = assessment.OfferedCourseId,
            AssessmentTypeId = assessment.AssessmentTypeId,
            AssessmentTypeName = assessment.AssessmentType?.AssessmentTypeName ?? string.Empty,
            AssessmentNo = assessment.AssessmentNo,
            Title = assessment.Title,
            DueDate = assessment.DueDate?.ToString("yyyy-MM-dd"),
            TotalMarks = assessment.TotalMarks,
            QuestionCount = roots.Count,
            IsEmpty = roots.Count == 0,
            IsSetupComplete = assessment.IsSetupComplete,
            IsRequired = AssessmentFolderSyncService.IsRequiredTemplate(
                assessment.AssessmentType?.AssessmentTypeName ?? string.Empty,
                assessment.AssessmentNo,
                hasLab),
            CloCodes = byId.Values
                .SelectMany(q => q.CloCodes)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code)
                .ToList(),
            Questions = roots,
            QuestionPaperFile = questionPaper,
            SolutionFile = solution,
            BestSampleFile = best,
            AverageSampleFile = average,
            WorstSampleFile = worst,
            SampleUploadCount = new[] { best, average, worst }.Count(x => x != null)
        };
    }

    private async Task<(int DirectoryId, string DirectoryPath)> EnsureTemplateDirectoryAsync(
        int offeredCourseId,
        int teacherUserId,
        string templateCode)
    {
        var folder = await EnsureCourseFolderAsync(offeredCourseId, teacherUserId);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var templateCommand = new SqlCommand(@"
            SELECT TemplateItemID, TemplateCode, FolderItemName
            FROM CourseFolderTemplateItems
            WHERE TemplateCode = @TemplateCode;
        ", connection);
        templateCommand.Parameters.AddWithValue("@TemplateCode", templateCode);

        int templateItemId;
        string folderItemName;

        await using (var reader = await templateCommand.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException($"Folder template '{templateCode}' was not found.");
            }

            templateItemId = Convert.ToInt32(reader["TemplateItemID"]);
            folderItemName = Convert.ToString(reader["FolderItemName"]) ?? templateCode;
        }

        var findDir = new SqlCommand(@"
            SELECT DirectoryID, DirectoryPath
            FROM CourseFolderDirectories
            WHERE CourseFolderID = @CourseFolderID
            AND TemplateItemID = @TemplateItemID;
        ", connection);
        findDir.Parameters.AddWithValue("@CourseFolderID", folder.CourseFolderId);
        findDir.Parameters.AddWithValue("@TemplateItemID", templateItemId);

        await using (var reader = await findDir.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                return (
                    Convert.ToInt32(reader["DirectoryID"]),
                    Convert.ToString(reader["DirectoryPath"]) ?? string.Empty);
            }
        }

        var directoryName = $"{templateCode}-{folderItemName}";
        var relativeDirectoryPath = $"{folder.RootFolderPath}/{directoryName}";
        Directory.CreateDirectory(GetAbsolutePath(relativeDirectoryPath));

        var insertDir = new SqlCommand(@"
            INSERT INTO CourseFolderDirectories
            (
                CourseFolderID,
                ParentDirectoryID,
                TemplateItemID,
                DirectoryName,
                DirectoryPath
            )
            OUTPUT INSERTED.DirectoryID
            VALUES
            (
                @CourseFolderID,
                NULL,
                @TemplateItemID,
                @DirectoryName,
                @DirectoryPath
            );
        ", connection);

        insertDir.Parameters.AddWithValue("@CourseFolderID", folder.CourseFolderId);
        insertDir.Parameters.AddWithValue("@TemplateItemID", templateItemId);
        insertDir.Parameters.AddWithValue("@DirectoryName", directoryName);
        insertDir.Parameters.AddWithValue("@DirectoryPath", relativeDirectoryPath);

        var directoryId = Convert.ToInt32(await insertDir.ExecuteScalarAsync());
        return (directoryId, relativeDirectoryPath);
    }

    private async Task<(int CourseFolderId, string RootFolderPath)> EnsureCourseFolderAsync(
        int offeredCourseId,
        int teacherUserId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var existing = new SqlCommand(@"
            SELECT CourseFolderID, RootFolderPath, FolderStatus
            FROM CourseFolders
            WHERE OfferedCourseID = @OfferedCourseID;
        ", connection);
        existing.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);

        await using (var reader = await existing.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                var status = Convert.ToString(reader["FolderStatus"]) ?? "";
                if (status == "Submitted")
                {
                    throw new InvalidOperationException(
                        "Course folder is submitted and locked. Upload is not allowed.");
                }

                return (
                    Convert.ToInt32(reader["CourseFolderID"]),
                    Convert.ToString(reader["RootFolderPath"]) ?? string.Empty);
            }
        }

        var oc = await _context.OfferedCourses
            .Include(x => x.Course)
            .Include(x => x.Section)
            .Include(x => x.TeacherUser)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId)
            ?? throw new InvalidOperationException("Offered course not found.");

        var ownerFolderName =
            $"{SanitizeCompactName(oc.TeacherUser.FullName)}" +
            $"{SanitizeCompactName(oc.Course.CourseTitle)}" +
            $"{SanitizeCompactName(oc.Section.SectionName)}";

        var relativeRootPath = $"src/Folders/{ownerFolderName}/MasterFolder";
        Directory.CreateDirectory(GetAbsolutePath(relativeRootPath));

        var insert = new SqlCommand(@"
            INSERT INTO CourseFolders
            (
                OfferedCourseID,
                RootFolderName,
                RootFolderPath,
                CreatedByUserID,
                FolderStatus
            )
            OUTPUT INSERTED.CourseFolderID
            VALUES
            (
                @OfferedCourseID,
                @RootFolderName,
                @RootFolderPath,
                @CreatedByUserID,
                'Generated'
            );
        ", connection);

        insert.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
        insert.Parameters.AddWithValue("@RootFolderName", ownerFolderName);
        insert.Parameters.AddWithValue("@RootFolderPath", relativeRootPath);
        insert.Parameters.AddWithValue("@CreatedByUserID", teacherUserId);

        var courseFolderId = Convert.ToInt32(await insert.ExecuteScalarAsync());
        return (courseFolderId, relativeRootPath);
    }

    private static string GetFileCategoryPrefix(string assessmentTypeName)
    {
        return assessmentTypeName switch
        {
            QuizTypeName => "Quiz",
            MidTypeName => "Mid Exam",
            FinalTypeName => "Final Exam",
            _ => "Assignment"
        };
    }

    private static (string TemplateCode, string FileCategory) ResolveFolderTarget(
        string typePrefix,
        string purpose)
    {
        return purpose switch
        {
            AssessmentFolderSyncService.PurposeQuestion => ("g", $"{typePrefix} Question"),
            PurposeSolution => ("h", $"{typePrefix} Solution"),
            PurposeBestSample => ("i", $"{typePrefix} Best Sample"),
            PurposeAverageSample => ("i", $"{typePrefix} Average Sample"),
            PurposeWorstSample => ("i", $"{typePrefix} Worst Sample"),
            _ => throw new InvalidOperationException("Unsupported file purpose.")
        };
    }

    private static string? NormalizeFilePurpose(string? filePurpose)
    {
        if (string.IsNullOrWhiteSpace(filePurpose))
        {
            return null;
        }

        var normalized = filePurpose.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "question paper" or "questionpaper" or "question" => AssessmentFolderSyncService.PurposeQuestion,
            "solution" => PurposeSolution,
            "best sample" or "bestsample" or "best" => PurposeBestSample,
            "average sample" or "averagesample" or "average" => PurposeAverageSample,
            "worst sample" or "worstsample" or "worst" => PurposeWorstSample,
            _ when string.Equals(normalized, AssessmentFolderSyncService.PurposeQuestion, StringComparison.OrdinalIgnoreCase)
                => AssessmentFolderSyncService.PurposeQuestion,
            _ when string.Equals(normalized, PurposeSolution, StringComparison.OrdinalIgnoreCase)
                => PurposeSolution,
            _ when string.Equals(normalized, PurposeBestSample, StringComparison.OrdinalIgnoreCase)
                => PurposeBestSample,
            _ when string.Equals(normalized, PurposeAverageSample, StringComparison.OrdinalIgnoreCase)
                => PurposeAverageSample,
            _ when string.Equals(normalized, PurposeWorstSample, StringComparison.OrdinalIgnoreCase)
                => PurposeWorstSample,
            _ => null
        };
    }

    private string GetAbsolutePath(string relativePath)
        => FolderStoragePath.Resolve(_environment, relativePath);

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value.Trim();
    }

    private static string SanitizeCompactName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    private async Task<AssessmentType?> GetAssessmentTypeAsync(string assessmentTypeName)
    {
        return await _context.AssessmentTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.AssessmentTypeName == assessmentTypeName);
    }

    /// <summary>
    /// Previous schemes of study that have assessments of this type for the same subject.
    /// Locked until CLOs + CLO-PLO + CLO-Task are approved (same gate as create).
    /// </summary>
    [HttpGet("offered-course/{offeredCourseId:int}/load-sources/schemes")]
    public async Task<ActionResult<List<AssessmentLoadSchemeDto>>> ListLoadSourceSchemes(
        int offeredCourseId,
        [FromQuery] int teacherUserId,
        [FromQuery] string assessmentTypeName = AssignmentTypeName)
    {
        var context = await ResolveOfferedCourseContextAsync(offeredCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await CanManageAssessmentsAsync(context, teacherUserId, assessmentTypeName))
        {
            return BadRequest(new { message = "You do not have permission to manage assessments for this section." });
        }

        try
        {
            await _contentSubmissionService.EnsureMappingsApprovedForAssessmentsAsync(
                context.ProgramCourseId,
                context.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var type = await GetAssessmentTypeAsync(assessmentTypeName.Trim());
        if (type == null)
        {
            return BadRequest(new { message = $"Assessment type '{assessmentTypeName}' was not found." });
        }

        var schemesRaw = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join section in _context.Sections.AsNoTracking() on oc.SectionId equals section.SectionId
            join session in _context.AcademicSessions.AsNoTracking() on section.SessionId equals session.SessionId
            join assessment in _context.Assessments.AsNoTracking() on oc.OfferedCourseId equals assessment.OfferedCourseId
            where oc.CourseId == context.CourseId
                  && session.ProgramId == context.ProgramId
                  && session.SessionId != context.SessionId
                  && assessment.AssessmentTypeId == type.AssessmentTypeId
                  && assessment.AssessmentQuestions.Any()
            group assessment by new
            {
                session.SessionId,
                session.SessionTitle,
                session.AcademicYear,
                session.StartDate
            }
            into g
            select new
            {
                g.Key.SessionId,
                g.Key.SessionTitle,
                g.Key.AcademicYear,
                g.Key.StartDate,
                AssessmentCount = g.Select(a => a.AssessmentId).Distinct().Count()
            }
        ).ToListAsync();

        var schemes = schemesRaw
            .OrderByDescending(s => s.StartDate)
            .ThenByDescending(s => s.SessionTitle)
            .Select(s => new AssessmentLoadSchemeDto
            {
                SessionId = s.SessionId,
                SchemeLabel = s.SessionTitle,
                AcademicYear = s.AcademicYear,
                StartDate = s.StartDate,
                AssessmentCount = s.AssessmentCount
            })
            .ToList();

        return Ok(schemes);
    }

    [HttpGet("offered-course/{offeredCourseId:int}/load-sources/schemes/{sourceSessionId:int}/items")]
    public async Task<ActionResult<AssessmentLoadSchemeDetailDto>> GetLoadSourceItems(
        int offeredCourseId,
        int sourceSessionId,
        [FromQuery] int teacherUserId,
        [FromQuery] string assessmentTypeName = AssignmentTypeName)
    {
        var context = await ResolveOfferedCourseContextAsync(offeredCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await CanManageAssessmentsAsync(context, teacherUserId, assessmentTypeName))
        {
            return BadRequest(new { message = "You do not have permission to manage assessments for this section." });
        }

        try
        {
            await _contentSubmissionService.EnsureMappingsApprovedForAssessmentsAsync(
                context.ProgramCourseId,
                context.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (sourceSessionId == context.SessionId)
        {
            return BadRequest(new { message = "Pick a previous scheme of study, not the current session." });
        }

        var type = await GetAssessmentTypeAsync(assessmentTypeName.Trim());
        if (type == null)
        {
            return BadRequest(new { message = $"Assessment type '{assessmentTypeName}' was not found." });
        }

        var session = await _context.AcademicSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sourceSessionId);

        if (session == null)
        {
            return NotFound(new { message = "Scheme of study was not found." });
        }

        var sourceItems = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join section in _context.Sections.AsNoTracking() on oc.SectionId equals section.SectionId
            join assessment in _context.Assessments.AsNoTracking() on oc.OfferedCourseId equals assessment.OfferedCourseId
            where oc.CourseId == context.CourseId
                  && section.SessionId == sourceSessionId
                  && assessment.AssessmentTypeId == type.AssessmentTypeId
                  && assessment.AssessmentQuestions.Any()
            orderby assessment.AssessmentNo ?? int.MaxValue, assessment.AssessmentId
            select new
            {
                assessment.AssessmentId,
                assessment.AssessmentNo,
                assessment.Title,
                assessment.TotalMarks,
                SectionName = section.SectionName,
                QuestionCount = assessment.AssessmentQuestions.Count(q => q.ParentQuestionId == null)
            }
        ).ToListAsync();

        var currentFilledNos = await _context.Assessments
            .AsNoTracking()
            .Where(a =>
                a.OfferedCourseId == offeredCourseId &&
                a.AssessmentTypeId == type.AssessmentTypeId &&
                a.AssessmentQuestions.Any())
            .Select(a => a.AssessmentNo)
            .ToListAsync();

        var filledNoSet = currentFilledNos
            .Where(n => n != null)
            .Select(n => n!.Value)
            .ToHashSet();

        var assessmentIds = sourceItems.Select(i => i.AssessmentId).ToList();
        var cloRows = await (
            from q in _context.AssessmentQuestions.AsNoTracking()
            join map in _context.AssessmentQuestionClomappings.AsNoTracking() on q.QuestionId equals map.QuestionId
            join clo in _context.Clos.AsNoTracking() on map.Cloid equals clo.Cloid
            where assessmentIds.Contains(q.AssessmentId)
            select new { q.AssessmentId, clo.Clocode }
        ).ToListAsync();

        var cloLookup = cloRows
            .GroupBy(x => x.AssessmentId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Clocode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList());

        var items = sourceItems.Select(i => new AssessmentLoadItemDto
        {
            AssessmentId = i.AssessmentId,
            AssessmentNo = i.AssessmentNo,
            Title = i.Title,
            TotalMarks = i.TotalMarks,
            QuestionCount = i.QuestionCount,
            SectionName = i.SectionName,
            CloCodes = cloLookup.TryGetValue(i.AssessmentId, out var codes) ? codes : new List<string>(),
            AlreadyInCurrent = i.AssessmentNo != null && filledNoSet.Contains(i.AssessmentNo.Value)
        }).ToList();

        return Ok(new AssessmentLoadSchemeDetailDto
        {
            SessionId = session.SessionId,
            SchemeLabel = session.SessionTitle,
            AssessmentTypeName = type.AssessmentTypeName,
            Items = items
        });
    }

    [HttpPost("offered-course/{offeredCourseId:int}/load-sources/apply")]
    public async Task<ActionResult<ApplyAssessmentsFromSchemeResultDto>> ApplyAssessmentsFromScheme(
        int offeredCourseId,
        [FromBody] ApplyAssessmentsFromSchemeDto dto)
    {
        if (dto.TeacherUserId <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        if (dto.AssessmentIds == null || dto.AssessmentIds.Count == 0)
        {
            return BadRequest(new { message = "Select at least one assessment to add." });
        }

        var context = await ResolveOfferedCourseContextAsync(offeredCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var typeName = string.IsNullOrWhiteSpace(dto.AssessmentTypeName)
            ? AssignmentTypeName
            : dto.AssessmentTypeName.Trim();

        if (!await CanManageAssessmentsAsync(context, dto.TeacherUserId, typeName))
        {
            return BadRequest(new { message = "You do not have permission to manage assessments for this section." });
        }

        try
        {
            await _contentSubmissionService.EnsureMappingsApprovedForAssessmentsAsync(
                context.ProgramCourseId,
                context.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (dto.SourceSessionId == context.SessionId)
        {
            return BadRequest(new { message = "Pick a previous scheme of study, not the current session." });
        }

        var type = await GetAssessmentTypeAsync(typeName);
        if (type == null)
        {
            return BadRequest(new { message = $"Assessment type '{typeName}' was not found." });
        }

        var selectedIds = dto.AssessmentIds.Distinct().ToList();
        var sourceAssessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .Where(a =>
                selectedIds.Contains(a.AssessmentId) &&
                a.AssessmentTypeId == type.AssessmentTypeId)
            .ToListAsync();

        // Ensure each source belongs to same course + source session
        var validSourceIds = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join section in _context.Sections.AsNoTracking() on oc.SectionId equals section.SectionId
            where oc.CourseId == context.CourseId
                  && section.SessionId == dto.SourceSessionId
            select oc.OfferedCourseId
        ).ToListAsync();

        sourceAssessments = sourceAssessments
            .Where(a => validSourceIds.Contains(a.OfferedCourseId) && a.AssessmentQuestions.Any())
            .OrderBy(a => a.AssessmentNo ?? int.MaxValue)
            .ThenBy(a => a.AssessmentId)
            .ToList();

        if (sourceAssessments.Count == 0)
        {
            return BadRequest(new { message = "No matching assessments found in the selected scheme." });
        }

        var currentClos = await _context.Clos
            .AsNoTracking()
            .Where(c =>
                c.ProgramCourseId == context.ProgramCourseId &&
                c.SessionId == context.SessionId &&
                c.ContentStatus == ContentStatuses.Approved)
            .ToListAsync();

        var cloIdByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var clo in currentClos)
        {
            var key = CloCodeFormatter.Canonical(clo.Clocode);
            if (!string.IsNullOrWhiteSpace(key) && !cloIdByCode.ContainsKey(key))
            {
                cloIdByCode[key] = clo.Cloid;
            }
        }

        var created = 0;
        var filled = 0;
        var skipped = 0;
        var mappedClos = 0;
        var unmappedCloSkipped = 0;

        foreach (var source in sourceAssessments)
        {
            Assessment? target = null;
            var reusedEmpty = false;

            if (source.AssessmentNo != null)
            {
                target = await _context.Assessments
                    .Include(a => a.AssessmentQuestions)
                    .FirstOrDefaultAsync(a =>
                        a.OfferedCourseId == offeredCourseId &&
                        a.AssessmentTypeId == type.AssessmentTypeId &&
                        a.AssessmentNo == source.AssessmentNo);

                if (target != null && target.AssessmentQuestions.Any())
                {
                    skipped++;
                    continue;
                }

                if (target != null)
                {
                    reusedEmpty = true;
                }
            }

            if (target == null)
            {
                var maxNo = await _context.Assessments
                    .Where(a =>
                        a.OfferedCourseId == offeredCourseId &&
                        a.AssessmentTypeId == type.AssessmentTypeId)
                    .Select(a => (int?)a.AssessmentNo)
                    .MaxAsync() ?? 0;

                target = new Assessment
                {
                    OfferedCourseId = offeredCourseId,
                    AssessmentTypeId = type.AssessmentTypeId,
                    AssessmentNo = source.AssessmentNo is > 0 ? source.AssessmentNo : maxNo + 1,
                    Title = string.IsNullOrWhiteSpace(source.Title)
                        ? type.AssessmentTypeName
                        : source.Title.Trim(),
                    DueDate = null,
                    TotalMarks = source.TotalMarks,
                    IsSetupComplete = false,
                    SetupCompletedAt = null,
                    CreatedByUserId = dto.TeacherUserId,
                    CreatedAt = DateTime.UtcNow
                };

                // Avoid AssessmentNo clash when source No is taken by a filled paper we skipped above
                var noClash = await _context.Assessments.AnyAsync(a =>
                    a.OfferedCourseId == offeredCourseId &&
                    a.AssessmentTypeId == type.AssessmentTypeId &&
                    a.AssessmentNo == target.AssessmentNo);

                if (noClash)
                {
                    target.AssessmentNo = maxNo + 1;
                }

                _context.Assessments.Add(target);
                await _context.SaveChangesAsync();
                created++;
            }
            else
            {
                target.Title = string.IsNullOrWhiteSpace(source.Title)
                    ? target.Title
                    : source.Title.Trim();
                target.TotalMarks = source.TotalMarks;
                target.DueDate = null;
                target.IsSetupComplete = false;
                target.SetupCompletedAt = null;
                filled++;
            }

            var roots = source.AssessmentQuestions
                .Where(q => q.ParentQuestionId == null)
                .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
                .ThenBy(q => q.QuestionId)
                .ToList();

            var idMap = new Dictionary<int, int>();
            var queue = new Queue<AssessmentQuestion>(roots);

            while (queue.Count > 0)
            {
                var sourceQuestion = queue.Dequeue();
                int? newParentId = null;
                if (sourceQuestion.ParentQuestionId != null)
                {
                    if (!idMap.TryGetValue(sourceQuestion.ParentQuestionId.Value, out var mappedParent))
                    {
                        continue;
                    }

                    newParentId = mappedParent;
                }

                var newQuestion = new AssessmentQuestion
                {
                    AssessmentId = target.AssessmentId,
                    ParentQuestionId = newParentId,
                    QuestionNo = sourceQuestion.QuestionNo,
                    QuestionText = sourceQuestion.QuestionText,
                    Marks = sourceQuestion.Marks,
                    DisplayOrder = sourceQuestion.DisplayOrder
                };

                _context.AssessmentQuestions.Add(newQuestion);
                await _context.SaveChangesAsync();
                idMap[sourceQuestion.QuestionId] = newQuestion.QuestionId;

                foreach (var map in sourceQuestion.AssessmentQuestionClomappings)
                {
                    var sourceClo = await _context.Clos.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Cloid == map.Cloid);
                    if (sourceClo == null)
                    {
                        unmappedCloSkipped++;
                        continue;
                    }

                    var key = CloCodeFormatter.Canonical(sourceClo.Clocode);
                    if (!cloIdByCode.TryGetValue(key, out var currentCloId))
                    {
                        unmappedCloSkipped++;
                        continue;
                    }

                    _context.AssessmentQuestionClomappings.Add(new AssessmentQuestionClomapping
                    {
                        QuestionId = newQuestion.QuestionId,
                        Cloid = currentCloId,
                        Percentage = map.Percentage
                    });
                    mappedClos++;
                }

                foreach (var child in source.AssessmentQuestions
                             .Where(q => q.ParentQuestionId == sourceQuestion.QuestionId)
                             .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
                             .ThenBy(q => q.QuestionId))
                {
                    queue.Enqueue(child);
                }
            }

            await _context.SaveChangesAsync();
            _ = reusedEmpty;
        }

        var result = new ApplyAssessmentsFromSchemeResultDto
        {
            CreatedCount = created,
            FilledCount = filled,
            SkippedCount = skipped,
            MappedCloCount = mappedClos,
            UnmappedCloSkipped = unmappedCloSkipped,
            Message =
                $"Added {created + filled} · Skipped {skipped} (already filled) · CLO links {mappedClos} · unmatched CLO maps dropped {unmappedCloSkipped}"
        };

        return Ok(result);
    }

    private async Task<OfferedCourseAssessmentContext?> ResolveOfferedCourseContextAsync(int offeredCourseId)
    {
        var row = await (
            from oc in _context.OfferedCourses
            join section in _context.Sections on oc.SectionId equals section.SectionId
            join session in _context.AcademicSessions on section.SessionId equals session.SessionId
            where oc.OfferedCourseId == offeredCourseId
            select new
            {
                oc.OfferedCourseId,
                oc.CourseId,
                oc.TeacherUserId,
                session.ProgramId,
                SessionId = section.SessionId
            }).FirstOrDefaultAsync();

        if (row == null)
        {
            return null;
        }

        var programCourseId = await _permissionService.EnsureProgramCourseAsync(row.ProgramId, row.CourseId);

        return new OfferedCourseAssessmentContext
        {
            OfferedCourseId = row.OfferedCourseId,
            CourseId = row.CourseId,
            TeacherUserId = row.TeacherUserId,
            ProgramId = row.ProgramId,
            SessionId = row.SessionId,
            ProgramCourseId = programCourseId
        };
    }

    private async Task<bool> CanAccessOfferedCourseAsync(
        OfferedCourseAssessmentContext context,
        int teacherUserId)
    {
        if (context.TeacherUserId == teacherUserId)
        {
            return true;
        }

        var flags = await _permissionService.GetTeacherFlagsAsync(teacherUserId, context.ProgramCourseId);
        return flags.CanUploadExamQuestions
            || flags.CanManageClos
            || flags.CanUploadCourseContent
            || flags.CanManageCloPloMapping
            || flags.CanManageCloTaskMapping;
    }

    private async Task<bool> CanManageAssessmentsAsync(
        OfferedCourseAssessmentContext context,
        int teacherUserId,
        string? assessmentTypeName = null)
    {
        if (GraderAccessService.IsGrader(User))
        {
            return false;
        }

        if (context.TeacherUserId == teacherUserId)
        {
            return true;
        }

        var flags = await _permissionService.GetTeacherFlagsAsync(teacherUserId, context.ProgramCourseId);
        var typeName = assessmentTypeName?.Trim() ?? string.Empty;

        return typeName switch
        {
            MidTypeName => flags.CanUploadMidExamQuestions,
            FinalTypeName => flags.CanUploadFinalExamQuestions,
            _ => flags.CanUploadMidExamQuestions || flags.CanUploadFinalExamQuestions
        };
    }

    private sealed class OfferedCourseAssessmentContext
    {
        public int OfferedCourseId { get; set; }
        public int CourseId { get; set; }
        public int TeacherUserId { get; set; }
        public int ProgramId { get; set; }
        public int SessionId { get; set; }
        public int ProgramCourseId { get; set; }
    }
}
