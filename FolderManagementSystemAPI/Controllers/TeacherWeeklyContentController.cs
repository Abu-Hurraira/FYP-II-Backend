using ClosedXML.Excel;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TeacherWeeklyContentController : ControllerBase
{
    // Holds database context, environment info, and connection string for SQL operations
    private readonly FolderManagementDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly string _connectionString;
    private readonly TeacherPermissionService _permissionService;
    private readonly ContentSubmissionService _contentSubmissionService;
    private readonly CourseFolderProvisioningService _folderProvisioning;

    // Constructor initializes DB context, environment, and connection string
    public TeacherWeeklyContentController(
        FolderManagementDbContext context,
        IWebHostEnvironment environment,
        TeacherPermissionService permissionService,
        ContentSubmissionService contentSubmissionService,
        CourseFolderProvisioningService folderProvisioning)
    {
        _context = context;
        _environment = environment;
        _permissionService = permissionService;
        _contentSubmissionService = contentSubmissionService;
        _folderProvisioning = folderProvisioning;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    // DTO used internally to hold offered course related context data
    private class OfferedCourseContextDto
    {
        public int ProgramCourseId { get; set; }
        public int ProgramId { get; set; }
        public int SessionId { get; set; }
        public int CourseId { get; set; }
        public int TeacherUserId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
    }

    // Retrieves weekly content structure for a specific offered course
    [HttpGet("{offeredCourseId:int}")]
    public async Task<ActionResult<WeeklyContentDto>> GetWeeklyContent(
        int offeredCourseId,
        [FromQuery] int? teacherUserId = null)
    {
        var courseContext = await GetOfferedCourseContextAsync(offeredCourseId);

        if (courseContext == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        return Ok(await BuildWeeklyContentResponseAsync(
            offeredCourseId,
            courseContext,
            teacherUserId));
    }

    private async Task<WeeklyContentDto> BuildWeeklyContentResponseAsync(
        int offeredCourseId,
        OfferedCourseContextDto courseContext,
        int? teacherUserId = null)
    {
        var effectiveTeacherUserId = teacherUserId ?? courseContext.TeacherUserId;
        var permissions = await _permissionService.GetTeacherFlagsAsync(
            effectiveTeacherUserId,
            courseContext.ProgramCourseId);
        var canViewAll = _contentSubmissionService.CanViewAllStatuses(
            permissions,
            ContentSubmissionTypeCodes.WeeklyContent);
        var visibleStatuses = _contentSubmissionService.VisibleContentStatuses(canViewAll);

        await InitializeProgressRowsAsync(
            offeredCourseId,
            courseContext.ProgramCourseId,
            courseContext.SessionId,
            visibleStatuses);

        var topics = await LoadTopicTreeAsync(
            offeredCourseId,
            courseContext.ProgramCourseId,
            courseContext.SessionId,
            visibleStatuses);
        var progress = CalculateTreeProgress(topics);
        var contentStatuses = await _contentSubmissionService.GetStatusesAsync(
            courseContext.ProgramCourseId,
            effectiveTeacherUserId,
            courseContext.SessionId);

        return new WeeklyContentDto
        {
            OfferedCourseId = offeredCourseId,
            ProgramCourseId = courseContext.ProgramCourseId,
            CourseTitle = courseContext.CourseTitle,
            SectionName = courseContext.SectionName,
            HasWeeklyContent = topics.Any(),
            ProgressPercentage = topics.Any() ? progress : null,
            Permissions = permissions,
            ContentStatuses = contentStatuses,
            Weeks = topics
        };
    }

    // Loads weekly content data directly from database and initializes progress
    [HttpPost("{offeredCourseId:int}/load-from-database")]
    public async Task<ActionResult<WeeklyContentDto>> LoadFromDatabase(int offeredCourseId)
    {
        var courseContext = await GetOfferedCourseContextAsync(offeredCourseId);

        if (courseContext == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        return await GetWeeklyContent(offeredCourseId);
    }

    /// <summary>
    /// Previous schemes of study that have weekly topics for the same subject.
    /// </summary>
    [HttpGet("{offeredCourseId:int}/load-sources/schemes")]
    public async Task<ActionResult<List<WeeklyTopicLoadSchemeDto>>> ListLoadSourceSchemes(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var courseContext = await GetOfferedCourseContextAsync(offeredCourseId);
        if (courseContext == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await _permissionService.HasPermissionAsync(
                teacherUserId,
                courseContext.ProgramCourseId,
                TeacherPermissionTypes.UploadWeeklyContent))
        {
            return BadRequest(new { message = "You do not have permission to upload or edit course content." });
        }

        var schemesRaw = await (
            from topic in _context.CourseTopics.AsNoTracking()
            join session in _context.AcademicSessions.AsNoTracking()
                on topic.SessionId equals session.SessionId
            where topic.ProgramCourseId == courseContext.ProgramCourseId
                  && topic.SessionId != courseContext.SessionId
            group topic by new
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
                TopicCount = g.Count(),
                WeekCount = g.Count(t => t.ParentTopicId == null)
            }
        ).ToListAsync();

        var schemes = schemesRaw
            .Where(s => s.WeekCount > 0)
            .OrderByDescending(s => s.StartDate)
            .ThenByDescending(s => s.SessionTitle)
            .Select(s => new WeeklyTopicLoadSchemeDto
            {
                SessionId = s.SessionId,
                SchemeLabel = s.SessionTitle,
                AcademicYear = s.AcademicYear,
                StartDate = s.StartDate,
                TopicCount = s.TopicCount,
                WeekCount = s.WeekCount
            })
            .ToList();

        return Ok(schemes);
    }

    /// <summary>
    /// Top-level week topics from a previous scheme (with children included on apply).
    /// </summary>
    [HttpGet("{offeredCourseId:int}/load-sources/schemes/{sourceSessionId:int}/topics")]
    public async Task<ActionResult<WeeklyTopicLoadSchemeDetailDto>> GetLoadSourceTopics(
        int offeredCourseId,
        int sourceSessionId,
        [FromQuery] int teacherUserId)
    {
        var courseContext = await GetOfferedCourseContextAsync(offeredCourseId);
        if (courseContext == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await _permissionService.HasPermissionAsync(
                teacherUserId,
                courseContext.ProgramCourseId,
                TeacherPermissionTypes.UploadWeeklyContent))
        {
            return BadRequest(new { message = "You do not have permission to upload or edit course content." });
        }

        if (sourceSessionId == courseContext.SessionId)
        {
            return BadRequest(new { message = "Pick a previous scheme of study, not the current session." });
        }

        var session = await _context.AcademicSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sourceSessionId);

        if (session == null)
        {
            return NotFound(new { message = "Scheme of study was not found." });
        }

        var sourceRoots = await _context.CourseTopics
            .AsNoTracking()
            .Where(t =>
                t.ProgramCourseId == courseContext.ProgramCourseId &&
                t.SessionId == sourceSessionId &&
                t.ParentTopicId == null)
            .OrderBy(t => t.WeekNo ?? 999)
            .ThenBy(t => t.DisplayOrder ?? 999)
            .ThenBy(t => t.TopicId)
            .ToListAsync();

        var currentRootTitles = await _context.CourseTopics
            .AsNoTracking()
            .Where(t =>
                t.ProgramCourseId == courseContext.ProgramCourseId &&
                t.SessionId == courseContext.SessionId &&
                t.ParentTopicId == null)
            .Select(t => new { t.WeekNo, t.TopicTitle })
            .ToListAsync();

        var currentKeys = currentRootTitles
            .Select(t => $"{t.WeekNo ?? 0}|{(t.TopicTitle ?? string.Empty).Trim().ToUpperInvariant()}")
            .ToHashSet(StringComparer.Ordinal);

        var rootIds = sourceRoots.Select(t => t.TopicId).ToList();
        var childCounts = await _context.CourseTopics
            .AsNoTracking()
            .Where(t =>
                t.ProgramCourseId == courseContext.ProgramCourseId &&
                t.SessionId == sourceSessionId &&
                t.ParentTopicId != null &&
                rootIds.Contains(t.ParentTopicId.Value))
            .GroupBy(t => t.ParentTopicId!.Value)
            .Select(g => new { TopicId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TopicId, x => x.Count);

        var cloByTopic = await (
            from map in _context.CourseTopicClomappings.AsNoTracking()
            join clo in _context.Clos.AsNoTracking() on map.Cloid equals clo.Cloid
            where rootIds.Contains(map.TopicId)
            select new { map.TopicId, clo.Clocode }
        ).ToListAsync();

        var cloLookup = cloByTopic
            .GroupBy(x => x.TopicId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Clocode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList());

        var items = sourceRoots.Select(t =>
        {
            var key = $"{t.WeekNo ?? 0}|{t.TopicTitle.Trim().ToUpperInvariant()}";
            return new WeeklyTopicLoadItemDto
            {
                TopicId = t.TopicId,
                WeekNo = t.WeekNo,
                TopicTitle = t.TopicTitle,
                TopicDescription = t.TopicDescription,
                ChildCount = childCounts.TryGetValue(t.TopicId, out var count) ? count : 0,
                CloCodes = cloLookup.TryGetValue(t.TopicId, out var codes) ? codes : new List<string>(),
                AlreadyInCurrent = currentKeys.Contains(key)
            };
        }).ToList();

        return Ok(new WeeklyTopicLoadSchemeDetailDto
        {
            SessionId = session.SessionId,
            SchemeLabel = session.SessionTitle,
            Items = items
        });
    }

    [HttpPost("{offeredCourseId:int}/load-sources/apply")]
    public async Task<ActionResult<ApplyWeeklyTopicsFromSchemeResultDto>> ApplyTopicsFromScheme(
        int offeredCourseId,
        [FromBody] ApplyWeeklyTopicsFromSchemeDto dto)
    {
        if (dto.TeacherUserId <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        if (dto.TopicIds == null || dto.TopicIds.Count == 0)
        {
            return BadRequest(new { message = "Select at least one week topic to add." });
        }

        var courseContext = await GetOfferedCourseContextAsync(offeredCourseId);
        if (courseContext == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await _permissionService.HasPermissionAsync(
                dto.TeacherUserId,
                courseContext.ProgramCourseId,
                TeacherPermissionTypes.UploadWeeklyContent))
        {
            return BadRequest(new { message = "You do not have permission to upload or edit course content." });
        }

        try
        {
            await _contentSubmissionService.EnsureContentTypeEditableAsync(
                courseContext.ProgramCourseId,
                courseContext.SessionId,
                ContentSubmissionTypeCodes.WeeklyContent);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (dto.SourceSessionId == courseContext.SessionId)
        {
            return BadRequest(new { message = "Pick a previous scheme of study, not the current session." });
        }

        var canMapClos = await _permissionService.HasPermissionAsync(
            dto.TeacherUserId,
            courseContext.ProgramCourseId,
            TeacherPermissionTypes.ManageClos);

        var sourceTopics = await _context.CourseTopics
            .AsNoTracking()
            .Where(t =>
                t.ProgramCourseId == courseContext.ProgramCourseId &&
                t.SessionId == dto.SourceSessionId)
            .OrderBy(t => t.WeekNo ?? 999)
            .ThenBy(t => t.ParentTopicId == null ? 0 : 1)
            .ThenBy(t => t.DisplayOrder ?? 999)
            .ThenBy(t => t.TopicId)
            .ToListAsync();

        if (sourceTopics.Count == 0)
        {
            return BadRequest(new { message = "No topics found in the selected scheme of study." });
        }

        var byId = sourceTopics.ToDictionary(t => t.TopicId);
        var selectedRoots = dto.TopicIds
            .Distinct()
            .Where(id => byId.TryGetValue(id, out var topic) && topic.ParentTopicId == null)
            .ToList();

        if (selectedRoots.Count == 0)
        {
            return BadRequest(new { message = "Select at least one week topic from the scheme." });
        }

        var toCopyIds = new List<int>();
        var queue = new Queue<int>(selectedRoots);
        var visited = new HashSet<int>();

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id) || !byId.ContainsKey(id))
            {
                continue;
            }

            toCopyIds.Add(id);
            foreach (var child in sourceTopics.Where(t => t.ParentTopicId == id).OrderBy(t => t.DisplayOrder ?? 999).ThenBy(t => t.TopicId))
            {
                queue.Enqueue(child.TopicId);
            }
        }

        var ordered = toCopyIds.Select(id => byId[id]).ToList();

        var sourceCloCodes = await (
            from map in _context.CourseTopicClomappings.AsNoTracking()
            join clo in _context.Clos.AsNoTracking() on map.Cloid equals clo.Cloid
            where ordered.Select(t => t.TopicId).Contains(map.TopicId)
            select new { map.TopicId, clo.Clocode }
        ).ToListAsync();

        var cloLookup = sourceCloCodes
            .GroupBy(x => x.TopicId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Clocode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList());

        var idMap = new Dictionary<int, int>();
        var created = 0;
        var already = 0;
        var mappedClos = 0;

        foreach (var source in ordered)
        {
            int? newParentId = null;
            if (source.ParentTopicId != null)
            {
                if (!idMap.TryGetValue(source.ParentTopicId.Value, out var mappedParent))
                {
                    continue;
                }

                newParentId = mappedParent;
            }

            var beforeExists = await TopicExistsAsync(
                courseContext.ProgramCourseId,
                courseContext.SessionId,
                newParentId,
                source.TopicTitle);

            var newTopicId = await CreateOrGetTopicAsync(
                courseContext.ProgramCourseId,
                courseContext.SessionId,
                newParentId,
                source.WeekNo ?? 1,
                source.TopicTitle.Trim(),
                source.TopicDescription,
                source.DisplayOrder);

            idMap[source.TopicId] = newTopicId;

            if (beforeExists)
            {
                already++;
            }
            else
            {
                created++;
            }

            if (canMapClos && cloLookup.TryGetValue(source.TopicId, out var codes))
            {
                foreach (var code in codes)
                {
                    if (await MapTopicToCloByCodeAsync(
                            courseContext.ProgramCourseId,
                            courseContext.SessionId,
                            newTopicId,
                            code))
                    {
                        mappedClos++;
                    }
                }
            }
        }

        await InitializeProgressRowsAsync(
            offeredCourseId,
            courseContext.ProgramCourseId,
            courseContext.SessionId,
            new[] { ContentStatuses.Draft, ContentStatuses.PendingReview, ContentStatuses.Approved, ContentStatuses.Rejected });

        var result = new ApplyWeeklyTopicsFromSchemeResultDto
        {
            CreatedCount = created,
            AlreadyExistCount = already,
            MappedCloCount = mappedClos,
            Message = $"Added {created} · Already present {already} · CLO links {mappedClos}"
        };

        return Ok(result);
    }

    private async Task<bool> TopicExistsAsync(
        int programCourseId,
        int sessionId,
        int? parentTopicId,
        string topicTitle)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var selectCommand = new SqlCommand(@"
            SELECT TopicID
            FROM CourseTopics
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID
            AND TopicTitle = @TopicTitle
            AND ((@ParentTopicID IS NULL AND ParentTopicID IS NULL) OR ParentTopicID = @ParentTopicID);
        ", connection);

        selectCommand.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        selectCommand.Parameters.AddWithValue("@SessionID", sessionId);
        selectCommand.Parameters.AddWithValue("@TopicTitle", topicTitle.Trim());
        selectCommand.Parameters.AddWithValue("@ParentTopicID", (object?)parentTopicId ?? DBNull.Value);

        var existing = await selectCommand.ExecuteScalarAsync();
        return existing != null && existing != DBNull.Value;
    }

    // Adds a new weekly topic and optionally maps CLO codes to it
    [HttpPost("{offeredCourseId:int}/topics")]
    public async Task<ActionResult<WeeklyContentDto>> AddWeeklyTopic(
        int offeredCourseId,
        AddWeeklyTopicDto dto)
    {
        if (dto.WeekNo <= 0)
        {
            return BadRequest(new { message = "Week number must be greater than 0." });
        }

        if (string.IsNullOrWhiteSpace(dto.TopicTitle))
        {
            return BadRequest(new { message = "Topic title is required." });
        }

        var courseContext = await GetOfferedCourseContextAsync(offeredCourseId);

        if (courseContext == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await _permissionService.HasPermissionAsync(
                dto.TeacherUserId,
                courseContext.ProgramCourseId,
                TeacherPermissionTypes.UploadWeeklyContent))
        {
            return BadRequest(new { message = "You do not have permission to upload or edit course content." });
        }

        try
        {
            await _contentSubmissionService.EnsureContentTypeEditableAsync(
                courseContext.ProgramCourseId,
                courseContext.SessionId,
                ContentSubmissionTypeCodes.WeeklyContent);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var cloCodes = dto.CloCodes?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList() ?? new List<string>();

        if (cloCodes.Count > 0 && !await _permissionService.HasPermissionAsync(
                dto.TeacherUserId,
                courseContext.ProgramCourseId,
                TeacherPermissionTypes.ManageClos))
        {
            return BadRequest(new { message = "You do not have permission to map CLOs." });
        }

        var topicId = await CreateOrGetTopicAsync(
            courseContext.ProgramCourseId,
            courseContext.SessionId,
            dto.ParentTopicId,
            dto.WeekNo,
            dto.TopicTitle.Trim(),
            dto.TopicDescription,
            dto.DisplayOrder);

        if (dto.CloCodes != null && dto.CloCodes.Any())
        {
            foreach (var cloCode in dto.CloCodes.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                await MapTopicToCloByCodeAsync(
                    courseContext.ProgramCourseId,
                    courseContext.SessionId,
                    topicId,
                    cloCode.Trim());
            }
        }

        return await GetWeeklyContent(offeredCourseId, dto.TeacherUserId);
    }

    // Updates completion status of a topic (and its descendants)
    [HttpPut("{offeredCourseId:int}/topics/{topicId:int}/complete")]
    public async Task<ActionResult<WeeklyContentDto>> UpdateTopicProgress(
        int offeredCourseId,
        int topicId,
        UpdateTopicProgressDto dto)
    {
        var courseContext = await GetOfferedCourseContextAsync(offeredCourseId);

        if (courseContext == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var topicExists = await TopicBelongsToProgramCourseAsync(
            topicId,
            courseContext.ProgramCourseId,
            courseContext.SessionId);

        if (!topicExists)
        {
            return BadRequest(new { message = "Topic does not belong to this course." });
        }

        var topicIds = await GetTopicAndDescendantIdsAsync(topicId);

        foreach (var id in topicIds)
        {
            await UpsertTopicProgressAsync(offeredCourseId, id, dto.IsCompleted, dto.TeacherUserId);
        }

        return await GetWeeklyContent(offeredCourseId, dto.TeacherUserId);
    }

    // Uploads an Excel file and imports weekly content structure from it
    [HttpPost("{offeredCourseId:int}/upload")]
    public async Task<ActionResult<UploadWeeklyContentResultDto>> UploadWeeklyContentFile(
        int offeredCourseId,
        [FromForm] int uploadedByUserId,
        [FromForm] IFormFile file)
    {
        var result = new UploadWeeklyContentResultDto();

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please upload a valid .xlsx file." });
        }

        var extension = Path.GetExtension(file.FileName).ToLower();

        if (extension != ".xlsx")
        {
            return BadRequest(new { message = "Only .xlsx files are allowed." });
        }

        var courseContext = await GetOfferedCourseContextAsync(offeredCourseId);

        if (courseContext == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        if (!await _permissionService.HasPermissionAsync(
                uploadedByUserId,
                courseContext.ProgramCourseId,
                TeacherPermissionTypes.UploadWeeklyContent))
        {
            return BadRequest(new { message = "You do not have permission to upload course content." });
        }

        try
        {
            await _contentSubmissionService.EnsureContentTypeEditableAsync(
                courseContext.ProgramCourseId,
                courseContext.SessionId,
                ContentSubmissionTypeCodes.WeeklyContent);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var courseFolder = await _folderProvisioning.EnsureMasterFolderAsync(
            offeredCourseId,
            uploadedByUserId);
        var courseFolderId = courseFolder.CourseFolderId;
        var courseContentDirectoryId = await GetDirectoryIdByTemplateCodeAsync(courseFolderId, "c");

        if (courseContentDirectoryId == 0)
        {
            return BadRequest(new { message = "Course Contents folder was not found." });
        }

        var fileId = await SaveUploadedFileAsync(
            courseContentDirectoryId,
            uploadedByUserId,
            file,
            "Course Content",
            "Weekly content uploaded and parsed from Excel file.");

        result.FileId = fileId;

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            return BadRequest(new { message = "The uploaded Excel file does not contain a worksheet." });
        }

        var headerRow = worksheet.Row(1);

        var weekNoCol = FindColumn(headerRow, "weekno", "week no", "week", "weeknumber", "week number");
        var topicPathCol = FindColumn(headerRow, "topicpath", "topic path", "path", "topic");
        var displayOrderCol = FindColumn(headerRow, "displayorder", "display order", "order");
        var cloCodesCol = FindColumn(headerRow, "clocodes", "clo codes", "clo", "clos");
        var descriptionCol = FindColumn(headerRow, "description", "topicdescription", "topic description");

        if (weekNoCol == 0 || topicPathCol == 0)
        {
            return BadRequest(new { message = "Excel must contain WeekNo and TopicPath columns." });
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            var weekText = GetCellValue(row, weekNoCol);
            var topicPath = GetCellValue(row, topicPathCol);
            var displayOrderText = displayOrderCol > 0 ? GetCellValue(row, displayOrderCol) : "";
            var cloCodesText = cloCodesCol > 0 ? GetCellValue(row, cloCodesCol) : "";
            var description = descriptionCol > 0 ? GetCellValue(row, descriptionCol) : "";

            if (string.IsNullOrWhiteSpace(weekText) && string.IsNullOrWhiteSpace(topicPath))
            {
                continue;
            }

            if (!int.TryParse(weekText, out var weekNo) || weekNo <= 0)
            {
                result.Errors.Add($"Row {rowNumber}: WeekNo is invalid.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(topicPath))
            {
                result.Errors.Add($"Row {rowNumber}: TopicPath is missing.");
                continue;
            }

            int? displayOrder = null;

            if (int.TryParse(displayOrderText, out var parsedOrder))
            {
                displayOrder = parsedOrder;
            }

            var beforeCount = await CountTopicsAsync(
                courseContext.ProgramCourseId,
                courseContext.SessionId);

            var finalTopicId = await CreateTopicPathAsync(
                courseContext.ProgramCourseId,
                courseContext.SessionId,
                weekNo,
                topicPath,
                displayOrder,
                description);

            var afterCount = await CountTopicsAsync(
                courseContext.ProgramCourseId,
                courseContext.SessionId);

            if (afterCount > beforeCount)
            {
                result.CreatedTopics += afterCount - beforeCount;
            }
            else
            {
                result.ExistingTopics++;
            }

            var cloCodes = cloCodesText
                .Split(',', ';', '|')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            foreach (var cloCode in cloCodes)
            {
                var mapped = await MapTopicToCloByCodeAsync(
                    courseContext.ProgramCourseId,
                    courseContext.SessionId,
                    finalTopicId,
                    cloCode);

                if (mapped)
                {
                    result.MappedClos++;
                }
            }
        }

        result.Message = result.Errors.Count == 0
            ? "Weekly content uploaded successfully."
            : "Weekly content uploaded with some skipped rows. Check Errors list.";

        return Ok(result);
    }

    // Fetches offered course + program context and resolves ProgramCourseId
    private async Task<OfferedCourseContextDto?> GetOfferedCourseContextAsync(int offeredCourseId)
    {
        var oc = await _context.OfferedCourses
            .Include(x => x.Course)
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            return null;
        }

        var programId = oc.Section.Session.ProgramId;
        var programCourseId = await EnsureProgramCourseAsync(programId, oc.CourseId);

        return new OfferedCourseContextDto
        {
            ProgramCourseId = programCourseId,
            ProgramId = programId,
            SessionId = oc.Section.SessionId,
            CourseId = oc.CourseId,
            TeacherUserId = oc.TeacherUserId,
            CourseTitle = oc.Course.CourseTitle,
            SectionName = oc.Section.SectionName
        };
    }

    // Ensures AcademicProgramCourses record exists and returns its ID
    private async Task<int> EnsureProgramCourseAsync(int programId, int courseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var selectCommand = new SqlCommand(@"
            SELECT ProgramCourseID
            FROM AcademicProgramCourses
            WHERE ProgramID = @ProgramID AND CourseID = @CourseID;
        ", connection);

        selectCommand.Parameters.AddWithValue("@ProgramID", programId);
        selectCommand.Parameters.AddWithValue("@CourseID", courseId);

        var existing = await selectCommand.ExecuteScalarAsync();

        if (existing != null && existing != DBNull.Value)
        {
            return Convert.ToInt32(existing);
        }

        var insertCommand = new SqlCommand(@"
            INSERT INTO AcademicProgramCourses (ProgramID, CourseID)
            OUTPUT INSERTED.ProgramCourseID
            VALUES (@ProgramID, @CourseID);
        ", connection);

        insertCommand.Parameters.AddWithValue("@ProgramID", programId);
        insertCommand.Parameters.AddWithValue("@CourseID", courseId);

        return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
    }

    // Loads topic tree with completion status for a course
    private async Task<List<WeeklyTopicDto>> LoadTopicTreeAsync(
        int offeredCourseId,
        int programCourseId,
        int sessionId,
        IReadOnlyCollection<string> visibleStatuses)
    {
        var flatTopics = new List<WeeklyTopicDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT
                t.TopicID,
                t.ProgramCourseID,
                t.ParentTopicID,
                t.WeekNo,
                t.TopicTitle,
                t.TopicDescription,
                t.DisplayOrder,
                ISNULL(p.IsCompleted, 0) AS IsCompleted
            FROM CourseTopics t
            LEFT JOIN OfferedCourseTopicProgress p
                ON p.TopicID = t.TopicID
                AND p.OfferedCourseID = @OfferedCourseID
            WHERE t.ProgramCourseID = @ProgramCourseID
            AND t.SessionID = @SessionID";

        var command = new SqlCommand();
        command.Connection = connection;
        command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);
        AppendContentStatusFilter(command, visibleStatuses, ref sql);
        sql += " ORDER BY ISNULL(t.WeekNo, 999), ISNULL(t.ParentTopicID, 0), ISNULL(t.DisplayOrder, 999), t.TopicID;";
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            flatTopics.Add(new WeeklyTopicDto
            {
                TopicId = Convert.ToInt32(reader["TopicID"]),
                ProgramCourseId = Convert.ToInt32(reader["ProgramCourseID"]),
                ParentTopicId = reader["ParentTopicID"] == DBNull.Value ? null : Convert.ToInt32(reader["ParentTopicID"]),
                WeekNo = reader["WeekNo"] == DBNull.Value ? null : Convert.ToInt32(reader["WeekNo"]),
                TopicTitle = Convert.ToString(reader["TopicTitle"]) ?? "",
                TopicDescription = reader["TopicDescription"] == DBNull.Value ? null : Convert.ToString(reader["TopicDescription"]),
                DisplayOrder = reader["DisplayOrder"] == DBNull.Value ? null : Convert.ToInt32(reader["DisplayOrder"]),
                IsCompleted = Convert.ToBoolean(reader["IsCompleted"])
            });
        }

        var topicMap = flatTopics.ToDictionary(x => x.TopicId);

        foreach (var topic in flatTopics)
        {
            if (topic.ParentTopicId != null && topicMap.TryGetValue(topic.ParentTopicId.Value, out var parent))
            {
                parent.Children.Add(topic);
            }
        }

        var roots = flatTopics
            .Where(x => x.ParentTopicId == null)
            .OrderBy(x => x.WeekNo ?? 999)
            .ThenBy(x => x.DisplayOrder ?? 999)
            .ThenBy(x => x.TopicId)
            .ToList();

        foreach (var root in roots)
        {
            ApplyParentCompletion(root);
        }

        return roots;
    }

    // Recursively marks parent topics completed if all children are completed
    private static bool ApplyParentCompletion(WeeklyTopicDto topic)
    {
        if (topic.Children.Count == 0)
        {
            return topic.IsCompleted;
        }

        var childrenCompleted = topic.Children.Select(ApplyParentCompletion).ToList();

        if (childrenCompleted.Count > 0 && childrenCompleted.All(x => x))
        {
            topic.IsCompleted = true;
        }

        return topic.IsCompleted;
    }

    // Calculates completion percentage of leaf topics in the tree
    private static decimal CalculateTreeProgress(List<WeeklyTopicDto> roots)
    {
        var leaves = new List<WeeklyTopicDto>();

        void CollectLeaves(WeeklyTopicDto node)
        {
            if (node.Children.Count == 0)
            {
                leaves.Add(node);
                return;
            }

            foreach (var child in node.Children)
            {
                CollectLeaves(child);
            }
        }

        foreach (var root in roots)
        {
            CollectLeaves(root);
        }

        if (leaves.Count == 0)
        {
            return 0;
        }

        var completed = leaves.Count(x => x.IsCompleted);
        return Math.Round((decimal)completed / leaves.Count * 100, 2);
    }

    // Creates hierarchical topic path (parent-child structure)
    private async Task<int> CreateTopicPathAsync(
        int programCourseId,
        int sessionId,
        int weekNo,
        string topicPath,
        int? displayOrder,
        string? description)
    {
        var parts = topicPath
            .Split(new[] { '>', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        int? parentTopicId = null;
        int finalTopicId = 0;

        for (var index = 0; index < parts.Count; index++)
        {
            var isLast = index == parts.Count - 1;

            finalTopicId = await CreateOrGetTopicAsync(
                programCourseId,
                sessionId,
                parentTopicId,
                weekNo,
                parts[index],
                isLast ? description : null,
                isLast ? displayOrder : index + 1);

            parentTopicId = finalTopicId;
        }

        return finalTopicId;
    }

    // Creates or retrieves a topic from database
    private async Task<int> CreateOrGetTopicAsync(
        int programCourseId,
        int sessionId,
        int? parentTopicId,
        int weekNo,
        string topicTitle,
        string? topicDescription,
        int? displayOrder)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var selectCommand = new SqlCommand(@"
            SELECT TopicID
            FROM CourseTopics
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID
            AND TopicTitle = @TopicTitle
            AND ((@ParentTopicID IS NULL AND ParentTopicID IS NULL) OR ParentTopicID = @ParentTopicID);
        ", connection);

        selectCommand.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        selectCommand.Parameters.AddWithValue("@SessionID", sessionId);
        selectCommand.Parameters.AddWithValue("@TopicTitle", topicTitle);
        selectCommand.Parameters.AddWithValue("@ParentTopicID", (object?)parentTopicId ?? DBNull.Value);

        var existing = await selectCommand.ExecuteScalarAsync();

        if (existing != null && existing != DBNull.Value)
        {
            return Convert.ToInt32(existing);
        }

        var insertCommand = new SqlCommand(@"
            INSERT INTO CourseTopics (ProgramCourseID, SessionID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, ContentStatus)
            OUTPUT INSERTED.TopicID
            VALUES (@ProgramCourseID, @SessionID, @ParentTopicID, @WeekNo, @TopicTitle, @TopicDescription, @DisplayOrder, @ContentStatus);
        ", connection);

        insertCommand.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        insertCommand.Parameters.AddWithValue("@SessionID", sessionId);
        insertCommand.Parameters.AddWithValue("@ParentTopicID", (object?)parentTopicId ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("@WeekNo", weekNo);
        insertCommand.Parameters.AddWithValue("@TopicTitle", topicTitle);
        insertCommand.Parameters.AddWithValue("@TopicDescription", (object?)topicDescription ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("@DisplayOrder", (object?)displayOrder ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("@ContentStatus", ContentStatuses.Draft);

        return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
    }

    // Maps a topic to a CLO using CLO code
    private async Task<bool> MapTopicToCloByCodeAsync(
        int programCourseId,
        int sessionId,
        int topicId,
        string cloCode)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var selectCloCommand = new SqlCommand(@"
            SELECT CLOID, CLOCode
            FROM CLOs
            WHERE ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID;
        ", connection);

        selectCloCommand.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        selectCloCommand.Parameters.AddWithValue("@SessionID", sessionId);

        var want = CloCodeFormatter.Canonical(cloCode);
        int? cloId = null;
        await using (var reader = await selectCloCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var stored = Convert.ToString(reader["CLOCode"]) ?? "";
                if (!CloCodeFormatter.Equals(stored, want))
                {
                    continue;
                }

                cloId = Convert.ToInt32(reader["CLOID"]);
                break;
            }
        }

        if (cloId == null)
        {
            return false;
        }

        var insertCommand = new SqlCommand(@"
            IF NOT EXISTS (SELECT 1 FROM CourseTopicCLOMappings WHERE TopicID = @TopicID AND CLOID = @CLOID)
            BEGIN
                INSERT INTO CourseTopicCLOMappings (TopicID, CLOID)
                VALUES (@TopicID, @CLOID);
            END
        ", connection);

        insertCommand.Parameters.AddWithValue("@TopicID", topicId);
        insertCommand.Parameters.AddWithValue("@CLOID", cloId.Value);

        await insertCommand.ExecuteNonQueryAsync();
        return true;
    }

    // Checks if topic belongs to a specific program course
    private async Task<bool> TopicBelongsToProgramCourseAsync(
        int topicId,
        int programCourseId,
        int sessionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT COUNT(1)
            FROM CourseTopics
            WHERE TopicID = @TopicID
            AND ProgramCourseID = @ProgramCourseID
            AND SessionID = @SessionID;
        ", connection);

        command.Parameters.AddWithValue("@TopicID", topicId);
        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    // Gets all descendant topic IDs of a given topic
    private async Task<List<int>> GetTopicAndDescendantIdsAsync(int topicId)
    {
        var topicIds = new List<int>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            ;WITH TopicTree AS
            (
                SELECT TopicID FROM CourseTopics WHERE TopicID = @TopicID
                UNION ALL
                SELECT child.TopicID
                FROM CourseTopics child
                INNER JOIN TopicTree parent ON child.ParentTopicID = parent.TopicID
            )
            SELECT TopicID FROM TopicTree;
        ", connection);

        command.Parameters.AddWithValue("@TopicID", topicId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            topicIds.Add(Convert.ToInt32(reader["TopicID"]));
        }

        return topicIds;
    }

    // Inserts or updates topic completion progress
    private async Task UpsertTopicProgressAsync(int offeredCourseId, int topicId, bool isCompleted, int teacherUserId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            IF EXISTS (SELECT 1 FROM OfferedCourseTopicProgress WHERE OfferedCourseID = @OfferedCourseID AND TopicID = @TopicID)
            BEGIN
                UPDATE OfferedCourseTopicProgress
                SET IsCompleted = @IsCompleted,
                    CompletedAt = CASE WHEN @IsCompleted = 1 THEN GETDATE() ELSE NULL END,
                    CompletedByUserID = CASE WHEN @IsCompleted = 1 THEN @TeacherUserID ELSE NULL END
                WHERE OfferedCourseID = @OfferedCourseID AND TopicID = @TopicID;
            END
            ELSE
            BEGIN
                INSERT INTO OfferedCourseTopicProgress (OfferedCourseID, TopicID, IsCompleted, CompletedAt, CompletedByUserID)
                VALUES (@OfferedCourseID, @TopicID, @IsCompleted, CASE WHEN @IsCompleted = 1 THEN GETDATE() ELSE NULL END, CASE WHEN @IsCompleted = 1 THEN @TeacherUserID ELSE NULL END);
            END
        ", connection);

        command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
        command.Parameters.AddWithValue("@TopicID", topicId);
        command.Parameters.AddWithValue("@IsCompleted", isCompleted);
        command.Parameters.AddWithValue("@TeacherUserID", teacherUserId);

        await command.ExecuteNonQueryAsync();
    }

    // Initializes progress rows for all topics in a course
    private async Task InitializeProgressRowsAsync(
        int offeredCourseId,
        int programCourseId,
        int sessionId,
        IReadOnlyCollection<string> visibleStatuses)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO OfferedCourseTopicProgress (OfferedCourseID, TopicID, IsCompleted)
            SELECT @OfferedCourseID, t.TopicID, 0
            FROM CourseTopics t
            WHERE t.ProgramCourseID = @ProgramCourseID
            AND t.SessionID = @SessionID";

        var command = new SqlCommand();
        command.Connection = connection;
        command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);
        AppendContentStatusFilter(command, visibleStatuses, ref sql);
        sql += @"
            AND NOT EXISTS
            (
                SELECT 1 FROM OfferedCourseTopicProgress p
                WHERE p.OfferedCourseID = @OfferedCourseID AND p.TopicID = t.TopicID
            );";
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }

    private static void AppendContentStatusFilter(
        SqlCommand command,
        IReadOnlyCollection<string> visibleStatuses,
        ref string sql)
    {
        if (visibleStatuses.Count == 0)
        {
            sql += " AND 1 = 0";
            return;
        }

        var parts = visibleStatuses.Select((_, index) => $"@ContentStatus{index}").ToList();
        var includeNullAsDraft = visibleStatuses.Contains(ContentStatuses.Draft);
        if (includeNullAsDraft)
        {
            sql += $" AND (t.ContentStatus IN ({string.Join(", ", parts)}) OR t.ContentStatus IS NULL)";
        }
        else
        {
            sql += $" AND t.ContentStatus IN ({string.Join(", ", parts)})";
        }

        var statusIndex = 0;
        foreach (var status in visibleStatuses)
        {
            command.Parameters.AddWithValue($"@ContentStatus{statusIndex++}", status);
        }
    }

    // Counts total topics in a program course for a session
    private async Task<int> CountTopicsAsync(int programCourseId, int sessionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(
            "SELECT COUNT(1) FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseID AND SessionID = @SessionID;",
            connection);
        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@SessionID", sessionId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    // Ensures course folder exists and creates it if missing
    private async Task<int> EnsureCourseFolderAsync(int offeredCourseId, int createdByUserId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var selectExisting = new SqlCommand("SELECT CourseFolderID FROM CourseFolders WHERE OfferedCourseID = @OfferedCourseID;", connection);
        selectExisting.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);

        var existing = await selectExisting.ExecuteScalarAsync();

        if (existing != null && existing != DBNull.Value)
        {
            var existingFolderId = Convert.ToInt32(existing);
            await EnsureTemplateDirectoriesAsync(existingFolderId);
            return existingFolderId;
        }

        var oc = await _context.OfferedCourses
            .Include(x => x.Course)
            .Include(x => x.Section)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            throw new InvalidOperationException("Offered course not found.");
        }

        var rootFolderName = SanitizeFileName($"OC-{offeredCourseId}-{oc.Course.CourseTitle}-{oc.Section.SectionName}");
        var relativeRootPath = $"course-folders/{rootFolderName}";
        var absoluteRootPath = GetAbsolutePath(relativeRootPath);

        Directory.CreateDirectory(absoluteRootPath);

        var insertCommand = new SqlCommand(@"
            INSERT INTO CourseFolders (OfferedCourseID, RootFolderName, RootFolderPath, CreatedByUserID, FolderStatus)
            OUTPUT INSERTED.CourseFolderID
            VALUES (@OfferedCourseID, @RootFolderName, @RootFolderPath, @CreatedByUserID, 'Generated');
        ", connection);

        insertCommand.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
        insertCommand.Parameters.AddWithValue("@RootFolderName", rootFolderName);
        insertCommand.Parameters.AddWithValue("@RootFolderPath", relativeRootPath);
        insertCommand.Parameters.AddWithValue("@CreatedByUserID", createdByUserId);

        var courseFolderId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
        await EnsureTemplateDirectoriesAsync(courseFolderId);

        return courseFolderId;
    }

    // Ensures template directory structure exists for a course folder
    private async Task EnsureTemplateDirectoriesAsync(int courseFolderId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var folderCommand = new SqlCommand("SELECT RootFolderPath FROM CourseFolders WHERE CourseFolderID = @CourseFolderID;", connection);
        folderCommand.Parameters.AddWithValue("@CourseFolderID", courseFolderId);

        var rootPathObj = await folderCommand.ExecuteScalarAsync();

        if (rootPathObj == null || rootPathObj == DBNull.Value)
        {
            throw new InvalidOperationException("Course folder not found.");
        }

        var rootPath = Convert.ToString(rootPathObj) ?? "";

        var templateCommand = new SqlCommand(@"
            SELECT TemplateItemID, TemplateCode, FolderItemName, SortOrder
            FROM CourseFolderTemplateItems
            ORDER BY SortOrder;
        ", connection);

        var templates = new List<(int Id, string Code, string Name)>();

        await using (var reader = await templateCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                templates.Add((
                    Convert.ToInt32(reader["TemplateItemID"]),
                    Convert.ToString(reader["TemplateCode"]) ?? "",
                    Convert.ToString(reader["FolderItemName"]) ?? ""
                ));
            }
        }

        foreach (var template in templates)
        {
            var directoryName = $"{template.Code}-{template.Name}";
            var relativeDirectoryPath = $"{rootPath}/{directoryName}";
            var absoluteDirectoryPath = GetAbsolutePath(relativeDirectoryPath);

            Directory.CreateDirectory(absoluteDirectoryPath);

            var existsCommand = new SqlCommand(@"
                SELECT COUNT(1)
                FROM CourseFolderDirectories
                WHERE CourseFolderID = @CourseFolderID AND TemplateItemID = @TemplateItemID;
            ", connection);

            existsCommand.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
            existsCommand.Parameters.AddWithValue("@TemplateItemID", template.Id);

            var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0;

            if (exists)
            {
                continue;
            }

            var insertCommand = new SqlCommand(@"
                INSERT INTO CourseFolderDirectories (CourseFolderID, ParentDirectoryID, TemplateItemID, DirectoryName, DirectoryPath)
                VALUES (@CourseFolderID, NULL, @TemplateItemID, @DirectoryName, @DirectoryPath);
            ", connection);

            insertCommand.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
            insertCommand.Parameters.AddWithValue("@TemplateItemID", template.Id);
            insertCommand.Parameters.AddWithValue("@DirectoryName", directoryName);
            insertCommand.Parameters.AddWithValue("@DirectoryPath", relativeDirectoryPath);

            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    // Gets directory ID based on template code
    private async Task<int> GetDirectoryIdByTemplateCodeAsync(int courseFolderId, string templateCode)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT d.DirectoryID
            FROM CourseFolderDirectories d
            INNER JOIN CourseFolderTemplateItems t ON t.TemplateItemID = d.TemplateItemID
            WHERE d.CourseFolderID = @CourseFolderID AND t.TemplateCode = @TemplateCode;
        ", connection);

        command.Parameters.AddWithValue("@CourseFolderID", courseFolderId);
        command.Parameters.AddWithValue("@TemplateCode", templateCode);

        var result = await command.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    // Saves uploaded file into directory and database record
    private async Task<int> SaveUploadedFileAsync(int directoryId, int uploadedByUserId, IFormFile file, string fileCategory, string? remarks)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var directoryCommand = new SqlCommand("SELECT DirectoryPath FROM CourseFolderDirectories WHERE DirectoryID = @DirectoryID;", connection);
        directoryCommand.Parameters.AddWithValue("@DirectoryID", directoryId);

        var directoryPathObj = await directoryCommand.ExecuteScalarAsync();

        if (directoryPathObj == null || directoryPathObj == DBNull.Value)
        {
            throw new InvalidOperationException("Directory not found.");
        }

        var relativeDirectoryPath = Convert.ToString(directoryPathObj) ?? "";
        var absoluteDirectoryPath = GetAbsolutePath(relativeDirectoryPath);

        Directory.CreateDirectory(absoluteDirectoryPath);

        var safeFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{SanitizeFileName(file.FileName)}";
        var relativeFilePath = $"{relativeDirectoryPath}/{safeFileName}";
        var absoluteFilePath = GetAbsolutePath(relativeFilePath);

        await using (var fileStream = new FileStream(absoluteFilePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        var insertCommand = new SqlCommand(@"
            INSERT INTO CourseFolderFiles (DirectoryID, UploadedByUserID, FileName, FilePath, FileCategory, Remarks)
            OUTPUT INSERTED.FileID
            VALUES (@DirectoryID, @UploadedByUserID, @FileName, @FilePath, @FileCategory, @Remarks);
        ", connection);

        insertCommand.Parameters.AddWithValue("@DirectoryID", directoryId);
        insertCommand.Parameters.AddWithValue("@UploadedByUserID", uploadedByUserId);
        insertCommand.Parameters.AddWithValue("@FileName", safeFileName);
        insertCommand.Parameters.AddWithValue("@FilePath", relativeFilePath);
        insertCommand.Parameters.AddWithValue("@FileCategory", fileCategory);
        insertCommand.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);

        return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
    }

    // Gets application web root path safely
    private string GetWebRootPath()
    {
        var webRoot = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        Directory.CreateDirectory(webRoot);
        return webRoot;
    }

    // Converts relative path into absolute server path
    private string GetAbsolutePath(string relativePath)
        => FolderStoragePath.Resolve(_environment, relativePath);

    // Cleans file name by removing invalid characters
    private static string SanitizeFileName(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '-');
        }

        return value.Trim();
    }

    // Finds Excel column index based on possible header names
    private static int FindColumn(IXLRow headerRow, params string[] possibleNames)
    {
        var lastCellNumber = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var col = 1; col <= lastCellNumber; col++)
        {
            var headerText = NormalizeHeader(headerRow.Cell(col).GetString());

            foreach (var name in possibleNames)
            {
                if (headerText == NormalizeHeader(name))
                {
                    return col;
                }
            }
        }

        return 0;
    }

    // Normalizes header text for comparison
    private static string NormalizeHeader(string value)
    {
        return value.Trim().ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");
    }

    // Safely gets cell value from Excel row
    private static string GetCellValue(IXLRow row, int columnNumber)
    {
        if (columnNumber <= 0)
        {
            return string.Empty;
        }

        return row.Cell(columnNumber).GetFormattedString().Trim();
    }
}