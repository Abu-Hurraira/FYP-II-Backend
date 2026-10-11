using FolderManagementSystemAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

/// <summary>
/// Seeds required empty assessment templates and syncs question PDFs
/// into the master folder (g-Quiz-Assignments-Exams).
/// </summary>
public class AssessmentFolderSyncService
{
    public const string PurposeQuestion = "Question Paper";

    public const int RequiredQuizCount = 8;
    public const int RequiredAssignmentCount = 4;
    public const int RequiredMidCount = 1;
    public const int RequiredFinalCount = 1;
    public const int RequiredFinalLabCount = 2;
    public const string FinalDefaultTitle = "Final Exams";
    public const string FinalTheoryTitle = "Final Theory";
    public const string FinalLabTitle = "Final Lab";

    private readonly FolderManagementDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly string _connectionString;

    public AssessmentFolderSyncService(
        FolderManagementDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    public static int GetRequiredFinalCount(bool hasLab) =>
        hasLab ? RequiredFinalLabCount : RequiredFinalCount;

    public static string GetFinalTitle(int assessmentNo, bool hasLab)
    {
        if (!hasLab)
        {
            return FinalDefaultTitle;
        }

        return assessmentNo == 2 ? FinalLabTitle : FinalTheoryTitle;
    }

    public static int GetRequiredTemplateCount(string assessmentTypeName, bool hasLab = false)
    {
        return assessmentTypeName switch
        {
            AssessmentsControllerTypeNames.Quiz => RequiredQuizCount,
            AssessmentsControllerTypeNames.Assignment => RequiredAssignmentCount,
            AssessmentsControllerTypeNames.Mid => RequiredMidCount,
            AssessmentsControllerTypeNames.Final => GetRequiredFinalCount(hasLab),
            _ => 0
        };
    }

    public static bool IsRequiredTemplate(string assessmentTypeName, int? assessmentNo, bool hasLab = false)
    {
        if (assessmentNo is null or <= 0)
        {
            return false;
        }

        return assessmentNo.Value <= GetRequiredTemplateCount(assessmentTypeName, hasLab);
    }

    public static string GetQuestionFileCategory(string assessmentTypeName)
    {
        return assessmentTypeName switch
        {
            AssessmentsControllerTypeNames.Quiz => "Quiz Question",
            AssessmentsControllerTypeNames.Mid => "Mid Exam Question",
            AssessmentsControllerTypeNames.Final => "Final Exam Question",
            _ => "Assignment Question"
        };
    }

    public async Task EnsureDefaultTemplatesAsync(int offeredCourseId, int createdByUserId)
    {
        var types = await _context.AssessmentTypes.AsNoTracking().ToListAsync();
        var quizType = types.FirstOrDefault(t => t.AssessmentTypeName == AssessmentsControllerTypeNames.Quiz);
        var assignmentType = types.FirstOrDefault(t => t.AssessmentTypeName == AssessmentsControllerTypeNames.Assignment);
        var midType = types.FirstOrDefault(t => t.AssessmentTypeName == AssessmentsControllerTypeNames.Mid);
        var finalType = types.FirstOrDefault(t => t.AssessmentTypeName == AssessmentsControllerTypeNames.Final);

        if (quizType != null)
        {
            await EnsureNumberedTemplatesAsync(
                offeredCourseId,
                quizType.AssessmentTypeId,
                RequiredQuizCount,
                _ => string.Empty,
                createdByUserId);
            await ClearLegacyNumberedTitlesAsync(
                offeredCourseId,
                quizType.AssessmentTypeId,
                AssessmentsControllerTypeNames.Quiz);
        }

        if (assignmentType != null)
        {
            await EnsureNumberedTemplatesAsync(
                offeredCourseId,
                assignmentType.AssessmentTypeId,
                RequiredAssignmentCount,
                _ => string.Empty,
                createdByUserId);
            await ClearLegacyNumberedTitlesAsync(
                offeredCourseId,
                assignmentType.AssessmentTypeId,
                AssessmentsControllerTypeNames.Assignment);
        }

        if (midType != null)
        {
            await EnsureNumberedTemplatesAsync(
                offeredCourseId,
                midType.AssessmentTypeId,
                RequiredMidCount,
                _ => "Mid Exams",
                createdByUserId);
        }

        var hasLab = await CourseHasLabAsync(offeredCourseId);

        if (finalType != null)
        {
            await EnsureNumberedTemplatesAsync(
                offeredCourseId,
                finalType.AssessmentTypeId,
                GetRequiredFinalCount(hasLab),
                n => GetFinalTitle(n, hasLab),
                createdByUserId);
        }

        await NormalizeExamDefaultsAsync(
            offeredCourseId,
            midType?.AssessmentTypeId,
            finalType?.AssessmentTypeId,
            hasLab);
    }

    public async Task<bool> CourseHasLabAsync(int offeredCourseId)
    {
        var hours = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join course in _context.Courses.AsNoTracking() on oc.CourseId equals course.CourseId
            where oc.OfferedCourseId == offeredCourseId
            select course.LabCreditHours).FirstOrDefaultAsync();

        return (hours ?? 0) > 0;
    }

    private async Task NormalizeExamDefaultsAsync(
        int offeredCourseId,
        int? midTypeId,
        int? finalTypeId,
        bool hasLab)
    {
        if (midTypeId != null)
        {
            var mids = await _context.Assessments
                .Where(a => a.OfferedCourseId == offeredCourseId && a.AssessmentTypeId == midTypeId)
                .ToListAsync();
            foreach (var assessment in mids)
            {
                assessment.Title = "Mid Exams";
                assessment.DueDate = null;
            }
        }

        if (finalTypeId != null)
        {
            var finals = await _context.Assessments
                .Where(a => a.OfferedCourseId == offeredCourseId && a.AssessmentTypeId == finalTypeId)
                .ToListAsync();
            foreach (var assessment in finals)
            {
                var no = assessment.AssessmentNo ?? 1;
                if (IsRequiredTemplate(AssessmentsControllerTypeNames.Final, no, hasLab))
                {
                    assessment.Title = GetFinalTitle(no, hasLab);
                }
                assessment.DueDate = null;
            }
        }

        if (midTypeId != null || finalTypeId != null)
        {
            await _context.SaveChangesAsync();
        }
    }

    private async Task EnsureNumberedTemplatesAsync(
        int offeredCourseId,
        int assessmentTypeId,
        int requiredCount,
        Func<int, string> titleFactory,
        int createdByUserId)
    {
        var existingNos = await _context.Assessments
            .AsNoTracking()
            .Where(a =>
                a.OfferedCourseId == offeredCourseId &&
                a.AssessmentTypeId == assessmentTypeId &&
                a.AssessmentNo != null)
            .Select(a => a.AssessmentNo!.Value)
            .ToListAsync();

        var existingSet = existingNos.ToHashSet();
        var toAdd = new List<Assessment>();

        for (var n = 1; n <= requiredCount; n++)
        {
            if (existingSet.Contains(n))
            {
                continue;
            }

            toAdd.Add(new Assessment
            {
                OfferedCourseId = offeredCourseId,
                AssessmentTypeId = assessmentTypeId,
                AssessmentNo = n,
                Title = titleFactory(n),
                DueDate = null,
                TotalMarks = null,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (toAdd.Count == 0)
        {
            return;
        }

        _context.Assessments.AddRange(toAdd);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Clears legacy seeded titles like "Assignment 1" / "Quiz 2" on still-empty templates
    /// so the UI can ask for a real name on first open.
    /// </summary>
    private async Task ClearLegacyNumberedTitlesAsync(
        int offeredCourseId,
        int assessmentTypeId,
        string typePrefix)
    {
        var candidates = await _context.Assessments
            .Include(a => a.AssessmentQuestions)
            .Where(a =>
                a.OfferedCourseId == offeredCourseId &&
                a.AssessmentTypeId == assessmentTypeId &&
                a.AssessmentNo != null &&
                a.AssessmentQuestions.Count == 0)
            .ToListAsync();

        var changed = false;
        foreach (var assessment in candidates)
        {
            var title = (assessment.Title ?? string.Empty).Trim();
            var no = assessment.AssessmentNo!.Value;
            if (IsLegacyNumberedTitle(title, typePrefix, no))
            {
                assessment.Title = string.Empty;
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
    }

    private static bool IsLegacyNumberedTitle(string title, string typePrefix, int assessmentNo)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return string.Equals(title, $"{typePrefix} {assessmentNo}", StringComparison.OrdinalIgnoreCase)
            || string.Equals(title, $"{typePrefix}{assessmentNo}", StringComparison.OrdinalIgnoreCase);
    }

    public async Task PublishQuestionPdfsForCourseAsync(int offeredCourseId, int teacherUserId)
    {
        var assessmentIds = await _context.Assessments
            .AsNoTracking()
            .Where(a => a.OfferedCourseId == offeredCourseId)
            .Select(a => a.AssessmentId)
            .ToListAsync();

        foreach (var assessmentId in assessmentIds)
        {
            await SyncQuestionPdfAsync(assessmentId, teacherUserId);
        }
    }

    /// <summary>
    /// Writes (or replaces) the question paper in master folder template g.
    /// Assignments and quizzes are Word briefs; mid/final exams use the BIIT exam layout PDF.
    /// </summary>
    public async Task SyncQuestionPdfAsync(int assessmentId, int teacherUserId)
    {
        var assessment = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
                    .ThenInclude(m => m.Clo)
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId);

        if (assessment == null || assessment.AssessmentQuestions.Count == 0)
        {
            return;
        }

        if (await HasTeacherUploadedQuestionPaperAsync(assessmentId))
        {
            return;
        }

        var typeName = assessment.AssessmentType?.AssessmentTypeName ?? AssessmentsControllerTypeNames.Assignment;
        var fileCategory = GetQuestionFileCategory(typeName);
        var isAssignment = string.Equals(
            typeName,
            AssessmentsControllerTypeNames.Assignment,
            StringComparison.OrdinalIgnoreCase);
        var isQuiz = string.Equals(
            typeName,
            AssessmentsControllerTypeNames.Quiz,
            StringComparison.OrdinalIgnoreCase);
        var isExam = string.Equals(typeName, AssessmentsControllerTypeNames.Mid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeName, AssessmentsControllerTypeNames.Final, StringComparison.OrdinalIgnoreCase);
        var typePrefix = typeName switch
        {
            AssessmentsControllerTypeNames.Quiz => "Quiz",
            AssessmentsControllerTypeNames.Mid => "MidExam",
            AssessmentsControllerTypeNames.Final => "FinalExam",
            _ => "Assignment"
        };

        var assessmentNo = assessment.AssessmentNo ?? assessment.AssessmentId;
        var oc = await _context.OfferedCourses
            .AsNoTracking()
            .Include(x => x.Course)
            .Include(x => x.TeacherUser)
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
                    .ThenInclude(s => s.Program)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == assessment.OfferedCourseId);
        var courseCode = oc?.Course.CourseCode ?? typePrefix;
        byte[] fileBytes;
        string safeFileName;
        if (isAssignment || isQuiz)
        {
            var kind = isQuiz ? "Quiz" : "Assignment";
            fileBytes = AssignmentWordGenerator.Generate(
                courseCode,
                assessmentNo,
                assessment.Title ?? string.Empty,
                assessment.DueDate,
                assessment.TotalMarks,
                BuildAssignmentQuestions(assessment),
                kind);
            safeFileName = AssignmentWordGenerator.FileName(courseCode, assessmentNo, kind);
        }
        else if (isExam)
        {
            var sessionTitle = oc?.Section?.Session?.SessionTitle ?? string.Empty;
            var examLabel = string.IsNullOrWhiteSpace(assessment.Title)
                ? typeName
                : assessment.Title.Trim();
            var sessionLine = string.IsNullOrWhiteSpace(sessionTitle)
                ? examLabel
                : $"{sessionTitle}: {examLabel}";
            fileBytes = ExamPaperWordGenerator.Generate(
                new ExamPaperWordGenerator.ExamHeader(
                    oc?.Course.CourseTitle ?? string.Empty,
                    courseCode,
                    sessionLine,
                    assessment.DueDate?.ToString("dd-MM-yyyy") ?? "________________",
                    "________________",
                    oc?.Section?.Session?.Program?.ProgramCode
                        ?? oc?.Section?.Session?.Program?.ProgramName
                        ?? string.Empty,
                    assessment.TotalMarks?.ToString("0.##") ?? "—",
                    oc?.TeacherUser?.FullName ?? string.Empty),
                BuildExamQuestions(assessment),
                ReadExamLogo("arid-logo.png"),
                ReadExamLogo("biit-logo.jpeg"));
            safeFileName = SanitizeExamFileName($"{courseCode} {examLabel} {sessionTitle}.docx");
        }
        else
        {
            var title = $"{typeName} {assessmentNo} - {assessment.Title}";
            fileBytes = BuildQuestionPaperPdf(assessment, title);
            safeFileName = $"{typePrefix}{assessmentNo}_Questions.pdf";
        }

        var directory = await EnsureTemplateDirectoryAsync(
            assessment.OfferedCourseId,
            teacherUserId,
            "g");

        var relativeFilePath = $"{directory.DirectoryPath}/{safeFileName}";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Remove prior question PDF links + CourseFolderFiles rows for this assessment.
        var priorLinks = await _context.AssessmentFiles
            .Where(af => af.AssessmentId == assessmentId && af.FilePurpose == PurposeQuestion)
            .ToListAsync();

        var priorFileIds = priorLinks.Select(l => l.FileId).Distinct().ToList();

        if (priorLinks.Count > 0)
        {
            _context.AssessmentFiles.RemoveRange(priorLinks);
            await _context.SaveChangesAsync();
        }

        foreach (var priorFileId in priorFileIds)
        {
            var pathCommand = new SqlCommand(
                "SELECT FilePath FROM CourseFolderFiles WHERE FileID = @FileID;",
                connection);
            pathCommand.Parameters.AddWithValue("@FileID", priorFileId);
            var priorPath = Convert.ToString(await pathCommand.ExecuteScalarAsync());
            if (!string.IsNullOrWhiteSpace(priorPath)
                && !string.Equals(priorPath, relativeFilePath, StringComparison.OrdinalIgnoreCase))
            {
                FolderStoragePath.DeleteExistingFiles(_environment, priorPath);
            }

            await DeleteCourseFolderFileAsync(connection, priorFileId);
        }

        // Replace any same-named file already in the directory (stale auto PDF/Word).
        await DeleteFilesByPathAsync(connection, relativeFilePath);

        var legacyPdf = $"{directory.DirectoryPath}/{typePrefix}{assessmentNo}_Questions.pdf";
        FolderStoragePath.DeleteExistingFiles(_environment, legacyPdf);
        await DeleteFilesByPathAsync(connection, legacyPdf);

        if (isExam)
        {
            var legacyExamPdf = Path.ChangeExtension(relativeFilePath, ".pdf");
            if (!string.Equals(legacyExamPdf, relativeFilePath, StringComparison.OrdinalIgnoreCase))
            {
                FolderStoragePath.DeleteExistingFiles(_environment, legacyExamPdf);
                await DeleteFilesByPathAsync(connection, legacyExamPdf);
            }
        }

        FolderStoragePath.WriteAllBytes(_environment, relativeFilePath, fileBytes);

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
            $"AssessmentID={assessmentId};Purpose={PurposeQuestion};{FolderAutoContentService.PublishedRemark}");

        var newFileId = Convert.ToInt32(await insertFile.ExecuteScalarAsync());

        _context.AssessmentFiles.Add(new AssessmentFile
        {
            AssessmentId = assessmentId,
            FileId = newFileId,
            FilePurpose = PurposeQuestion
        });

        await _context.SaveChangesAsync();
    }

    private static List<ExamPaperWordGenerator.ExamQuestion> BuildExamQuestions(Assessment assessment)
    {
        ExamPaperWordGenerator.ExamQuestion Map(AssessmentQuestion question)
        {
            var parts = assessment.AssessmentQuestions
                .Where(q => q.ParentQuestionId == question.QuestionId)
                .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
                .ThenBy(q => q.QuestionNo)
                .ThenBy(q => q.QuestionId)
                .Select(Map)
                .ToList();

            var clo = FormatQuestionClos(question.AssessmentQuestionClomappings);
            if (string.IsNullOrWhiteSpace(clo) || clo == "—")
            {
                var rolled = parts
                    .Select(p => p.Clo)
                    .Where(c => !string.IsNullOrWhiteSpace(c) && c != "—")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                clo = rolled.Count == 0 ? "—" : string.Join(", ", rolled);
            }

            var number = (question.QuestionNo ?? string.Empty).Trim();
            if (number.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
            {
                number = number[1..].TrimStart('.', ')', ' ');
            }

            return new ExamPaperWordGenerator.ExamQuestion(
                string.IsNullOrWhiteSpace(number) ? "—" : number,
                question.QuestionText ?? string.Empty,
                clo,
                question.Marks?.ToString("0.##") ?? "—",
                parts);
        }

        return assessment.AssessmentQuestions
            .Where(q => q.ParentQuestionId == null)
            .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
            .ThenBy(q => q.QuestionNo)
            .ThenBy(q => q.QuestionId)
            .Select(Map)
            .ToList();
    }

    private byte[]? ReadExamLogo(string fileName)
    {
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
        var candidates = new[]
        {
            Path.Combine(webRoot, "exam-assets", fileName),
            Path.Combine(_environment.ContentRootPath, "wwwroot", "exam-assets", fileName),
            Path.Combine(_environment.ContentRootPath, "exam-assets", fileName)
        };

        var path = candidates.FirstOrDefault(File.Exists);
        return path == null ? null : File.ReadAllBytes(path);
    }

    private static string SanitizeExamFileName(string fileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '-');
        }

        fileName = string.Join(" ", fileName.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(fileName) ? "Exam.pdf" : fileName;
    }

    private static string FormatQuestionClos(IEnumerable<AssessmentQuestionClomapping> mappings)
    {
        var parts = (mappings ?? Array.Empty<AssessmentQuestionClomapping>())
            .Select(m =>
            {
                var code = m.Clo?.Clocode;
                if (string.IsNullOrWhiteSpace(code))
                {
                    return null;
                }

                return m.Percentage is > 0
                    ? $"{code} {m.Percentage.Value.ToString("0.##")}%"
                    : code;
            })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }

    private async Task<bool> HasTeacherUploadedQuestionPaperAsync(int assessmentId)
    {
        var remarks = await (
            from link in _context.AssessmentFiles.AsNoTracking()
            join file in _context.CourseFolderFiles.AsNoTracking() on link.FileId equals file.FileId
            where link.AssessmentId == assessmentId && link.FilePurpose == PurposeQuestion
            select file.Remarks).ToListAsync();

        return remarks.Any(r =>
            !string.IsNullOrWhiteSpace(r)
            && r.IndexOf(FolderAutoContentService.PublishedRemark, StringComparison.OrdinalIgnoreCase) < 0);
    }

    private static List<AssignmentWordGenerator.QuestionBlock> BuildAssignmentQuestions(Assessment assessment)
    {
        AssignmentWordGenerator.QuestionBlock Map(AssessmentQuestion question)
        {
            var parts = assessment.AssessmentQuestions
                .Where(q => q.ParentQuestionId == question.QuestionId)
                .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
                .ThenBy(q => q.QuestionNo)
                .ThenBy(q => q.QuestionId)
                .Select(Map)
                .ToList();

            var clos = FormatQuestionClos(question.AssessmentQuestionClomappings);
            if (clos == "—")
            {
                clos = null;
            }

            if (string.IsNullOrWhiteSpace(clos) && parts.Count > 0)
            {
                var rolled = parts
                    .Select(p => p.Clos)
                    .Where(c => !string.IsNullOrWhiteSpace(c) && c != "—")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                clos = rolled.Count == 0 ? null : string.Join(", ", rolled);
            }

            return new AssignmentWordGenerator.QuestionBlock(
                question.QuestionNo ?? string.Empty,
                question.QuestionText ?? string.Empty,
                question.Marks?.ToString("0.##"),
                clos,
                parts);
        }

        return assessment.AssessmentQuestions
            .Where(q => q.ParentQuestionId == null)
            .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
            .ThenBy(q => q.QuestionNo)
            .ThenBy(q => q.QuestionId)
            .Select(Map)
            .ToList();
    }

    private static byte[] BuildQuestionPaperPdf(Assessment assessment, string title)
    {
        var meta = new List<(string Label, string Value)>
        {
            ("Title", assessment.Title ?? "—"),
            ("Number", assessment.AssessmentNo?.ToString() ?? "—"),
            ("Due Date", assessment.DueDate?.ToString("yyyy-MM-dd") ?? "—"),
            ("Total Marks", assessment.TotalMarks?.ToString("0.##") ?? "—")
        };

        var questions = new List<(string Number, string Marks, string Clos, string Text)>();

        void AppendQuestions(IEnumerable<AssessmentQuestion> nodes, int level)
        {
            foreach (var question in nodes
                         .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
                         .ThenBy(q => q.QuestionNo)
                         .ThenBy(q => q.QuestionId))
            {
                var cloParts = question.AssessmentQuestionClomappings
                    .Select(m =>
                    {
                        var code = m.Clo?.Clocode;
                        if (string.IsNullOrWhiteSpace(code))
                        {
                            return null;
                        }

                        return m.Percentage is > 0
                            ? $"{code} {m.Percentage:0.##}%"
                            : code;
                    })
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .OrderBy(c => c)
                    .ToList();

                var indent = level > 0 ? new string(' ', level * 2) : string.Empty;
                questions.Add((
                    $"{indent}{question.QuestionNo}",
                    question.Marks?.ToString("0.##") ?? "—",
                    cloParts.Count == 0 ? "—" : string.Join(", ", cloParts),
                    question.QuestionText ?? string.Empty));

                var children = assessment.AssessmentQuestions
                    .Where(q => q.ParentQuestionId == question.QuestionId)
                    .ToList();
                if (children.Count > 0)
                {
                    AppendQuestions(children, level + 1);
                }
            }
        }

        AppendQuestions(
            assessment.AssessmentQuestions.Where(q => q.ParentQuestionId == null),
            0);

        return StructuredPdfGenerator.GenerateQuestionPaper(title, meta, questions);
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
                if (string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase))
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

    private static async Task DeleteCourseFolderFileAsync(SqlConnection connection, int fileId)
    {
        var delete = new SqlCommand(@"
            DELETE FROM AssessmentFiles WHERE FileID = @FileID;
            DELETE FROM CourseFolderFiles WHERE FileID = @FileID;
        ", connection);
        delete.Parameters.AddWithValue("@FileID", fileId);
        await delete.ExecuteNonQueryAsync();
    }

    private static async Task DeleteFilesByPathAsync(SqlConnection connection, string relativeFilePath)
    {
        var delete = new SqlCommand(@"
            DELETE FROM AssessmentFiles
            WHERE FileID IN (SELECT FileID FROM CourseFolderFiles WHERE FilePath = @FilePath);

            DELETE FROM CourseFolderFiles WHERE FilePath = @FilePath;
        ", connection);
        delete.Parameters.AddWithValue("@FilePath", relativeFilePath);
        await delete.ExecuteNonQueryAsync();
    }

    private string GetAbsolutePath(string relativePath)
        => FolderStoragePath.Resolve(_environment, relativePath);

    private static string SanitizeCompactName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }
}

/// <summary>Avoid circular type refs to AssessmentsController constants.</summary>
internal static class AssessmentsControllerTypeNames
{
    public const string Assignment = "Assignment";
    public const string Quiz = "Quiz";
    public const string Mid = "Mid";
    public const string Final = "Final";
}
