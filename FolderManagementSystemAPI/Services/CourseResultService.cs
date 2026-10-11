using System.Text.Json;
using System.Text.RegularExpressions;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class CourseResultService
{
    public const string AssignmentType = "Assignment";
    public const string QuizType = "Quiz";
    public const string MidType = "Mid";
    public const string FinalType = "Final";
    public const string SelectionAssignment = "Assignment";
    public const string SelectionQuiz = "Quiz";
    public const decimal LabTargetMarks = 20m;

    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;
    private readonly AssessmentFolderSyncService _assessmentFolderSync;

    public CourseResultService(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService,
        AssessmentFolderSyncService assessmentFolderSync)
    {
        _context = context;
        _permissionService = permissionService;
        _assessmentFolderSync = assessmentFolderSync;
    }

    public async Task<(bool Ok, string Message, CloAttainmentService.OfferedCourseScope? Scope)> TryResolveScopeAsync(
        int offeredCourseId,
        int teacherUserId)
    {
        var row = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join course in _context.Courses.AsNoTracking() on oc.CourseId equals course.CourseId
            join section in _context.Sections.AsNoTracking() on oc.SectionId equals section.SectionId
            join session in _context.AcademicSessions.AsNoTracking() on section.SessionId equals session.SessionId
            join program in _context.AcademicPrograms.AsNoTracking() on session.ProgramId equals program.ProgramId into programs
            from program in programs.DefaultIfEmpty()
            join teacher in _context.Users.AsNoTracking() on oc.TeacherUserId equals teacher.UserId into teachers
            from teacher in teachers.DefaultIfEmpty()
            where oc.OfferedCourseId == offeredCourseId
            select new
            {
                oc.OfferedCourseId,
                oc.TeacherUserId,
                oc.CourseId,
                section.SectionId,
                SessionId = section.SessionId,
                session.ProgramId,
                CourseTitle = course.CourseTitle,
                CourseCode = course.CourseCode ?? string.Empty,
                SectionName = section.SectionName,
                SessionTitle = session.SessionTitle ?? string.Empty,
                ProgramCode = program != null ? (program.ProgramCode ?? program.ProgramName ?? string.Empty) : string.Empty,
                TeacherName = teacher != null ? teacher.FullName : string.Empty,
                ClassCreditHours = course.ClassCreditHours ?? 0,
                LabCreditHours = course.LabCreditHours ?? 0
            }).FirstOrDefaultAsync();

        if (row == null)
        {
            return (false, "Offered course not found.", null);
        }

        var programCourseId = await _permissionService.EnsureProgramCourseAsync(row.ProgramId, row.CourseId);
        var canAccess = row.TeacherUserId == teacherUserId;
        if (!canAccess)
        {
            var flags = await _permissionService.GetTeacherFlagsAsync(teacherUserId, programCourseId);
            canAccess = flags.CanUploadExamQuestions
                || flags.CanManageClos
                || flags.CanUploadCourseContent
                || flags.CanManageCloPloMapping
                || flags.CanManageCloTaskMapping;
        }

        if (!canAccess)
        {
            canAccess = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .AnyAsync(u => u.UserId == teacherUserId && u.Role.RoleName == "HOD");
        }

        if (!canAccess)
        {
            return (false, "You do not have access to this course section.", null);
        }

        return (true, string.Empty, new CloAttainmentService.OfferedCourseScope
        {
            OfferedCourseId = row.OfferedCourseId,
            TeacherUserId = row.TeacherUserId,
            CourseId = row.CourseId,
            SectionId = row.SectionId,
            SessionId = row.SessionId,
            ProgramId = row.ProgramId,
            ProgramCourseId = programCourseId,
            CourseTitle = row.CourseTitle,
            CourseCode = row.CourseCode,
            SectionName = row.SectionName,
            TeacherName = row.TeacherName,
            SessionTitle = row.SessionTitle,
            ProgramCode = row.ProgramCode,
            ClassCreditHours = row.ClassCreditHours,
            LabCreditHours = row.LabCreditHours
        });
    }

    public async Task<CourseFixedMarkingDto> GetFixedMarkingAsync(int offeredCourseId)
    {
        var course = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join c in _context.Courses.AsNoTracking() on oc.CourseId equals c.CourseId
            where oc.OfferedCourseId == offeredCourseId
            select c).FirstOrDefaultAsync();

        if (course == null)
        {
            return BuildFixedMarking(0, 0);
        }

        return BuildFixedMarking(course.ClassCreditHours ?? 0, course.LabCreditHours ?? 0);
    }

    public static CourseFixedMarkingDto BuildFixedMarking(int classCreditHours, int labCreditHours)
    {
        var hasLab = labCreditHours > 0;
        var totalCredits = classCreditHours + labCreditHours;
        if (totalCredits <= 0)
        {
            totalCredits = Math.Max(classCreditHours, 3);
        }

        // 4 CH with lab (e.g. 3+1): HW 12, Mid 18, Lab 20, Final 30 = 80
        // 3 CH with lab (e.g. 2+1): HW 8, Mid 12, Lab 20, Final 20 = 60
        // 3 CH without lab: HW 12, Mid 18, Lab 0, Final 30 = 60
        decimal homework;
        decimal mid;
        decimal lab;
        decimal final;
        decimal total;
        string label;

        if (hasLab && totalCredits >= 4)
        {
            homework = 12;
            mid = 18;
            lab = LabTargetMarks;
            final = 30;
            total = 80;
            label = "4 credit hours with lab";
        }
        else if (hasLab)
        {
            homework = 8;
            mid = 12;
            lab = LabTargetMarks;
            final = 20;
            total = 60;
            label = "3 credit hours with lab";
        }
        else
        {
            homework = 12;
            mid = 18;
            lab = 0;
            final = 30;
            total = 60;
            label = "3 credit hours without lab";
        }

        return new CourseFixedMarkingDto
        {
            ClassCreditHours = classCreditHours,
            LabCreditHours = labCreditHours,
            TotalCreditHours = totalCredits,
            HasLab = hasLab,
            ProfileLabel = label,
            TotalMarks = total,
            HomeworkMarks = homework,
            MidMarks = mid,
            LabMarks = lab,
            FinalMarks = final
        };
    }

    public async Task<CourseResultReadinessDto> GetReadinessAsync(
        CloAttainmentService.OfferedCourseScope scope)
    {
        var fixedMarks = await GetFixedMarkingAsync(scope.OfferedCourseId);
        var scheme = await GetSchemeDtoAsync(scope.OfferedCourseId, fixedMarks);
        var homeworkScheme = await GetHomeworkSchemeDtoAsync(scope.OfferedCourseId, fixedMarks.HomeworkMarks);
        var labScheme = fixedMarks.HasLab
            ? await GetLabSchemeDtoAsync(scope.OfferedCourseId)
            : null;

        var studentCount = await _context.StudentOfferedCourses
            .CountAsync(s => s.OfferedCourseId == scope.OfferedCourseId);
        var groups = await BuildRequiredGroupsAsync(scope.OfferedCourseId, fixedMarks.HasLab);
        var blockers = new List<string>();
        var warnings = new List<string>();

        foreach (var group in groups.Where(g => !g.IsComplete))
        {
            blockers.Add(
                $"{group.AssessmentTypeName}: {group.CompletedCount} of {group.RequiredCount} required papers are complete.");
        }

        if (!homeworkScheme.IsComplete)
        {
            blockers.Add(
                $"Homework marks mapping is incomplete ({homeworkScheme.AssignedTotal:0.##}/{homeworkScheme.TargetMarks:0.##}).");
        }

        if (fixedMarks.HasLab && (labScheme == null || !labScheme.IsComplete))
        {
            blockers.Add(
                $"Lab marks mapping is incomplete ({labScheme?.AssignedTotal ?? 0:0.##}/{LabTargetMarks:0.##}).");
        }

        if (studentCount == 0)
        {
            warnings.Add("No students are enrolled in this section.");
        }

        if (labScheme != null && (labScheme.LabTaskMarks > 0 || labScheme.ProjectMarks > 0))
        {
            var hasComponentRows = await _context.StudentComponentMarks
                .AsNoTracking()
                .AnyAsync(r =>
                    r.OfferedCourseId == scope.OfferedCourseId &&
                    (r.LabTaskObtained != null || r.ProjectObtained != null));
            if (!hasComponentRows)
            {
                warnings.Add(
                    "Lab Tasks / Project marks are allocated. Enter them under Component Marks (or they will count as 0).");
            }
        }

        if (homeworkScheme.ExtraMarksTotal > 0 || (labScheme?.ExtraMarksTotal ?? 0) > 0)
        {
            warnings.Add(
                "Extra mapping items are allocated. Enter those marks under Component Marks (or they will count as 0).");
        }

        var homeworkNeedsAttendance = homeworkScheme.AttendanceMarks > 0;
        var labNeedsAttendance = (labScheme?.AttendanceMarks ?? 0) > 0;
        if (homeworkNeedsAttendance || labNeedsAttendance)
        {
            var hasAttendanceEntered = await _context.StudentComponentMarks
                .AsNoTracking()
                .AnyAsync(r =>
                    r.OfferedCourseId == scope.OfferedCourseId &&
                    r.AttendancePercent != null);
            var hasAttendanceRecords = await _context.AttendanceRecords
                .AsNoTracking()
                .AnyAsync(r => r.OfferedCourseId == scope.OfferedCourseId);
            if (!hasAttendanceEntered && !hasAttendanceRecords)
            {
                warnings.Add(
                    "Attendance is allocated. Enter attendance % under Component Marks (or it will count as 0).");
            }
        }

        var creditHoursDisplay = fixedMarks.HasLab
            ? $"{fixedMarks.TotalCreditHours}({fixedMarks.ClassCreditHours}-{fixedMarks.LabCreditHours})"
            : $"{fixedMarks.TotalCreditHours}({fixedMarks.ClassCreditHours}-0)";

        return new CourseResultReadinessDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseCode = scope.CourseCode,
            CourseTitle = scope.CourseTitle,
            TeacherName = scope.TeacherName,
            SessionTitle = scope.SessionTitle,
            ProgramCode = scope.ProgramCode,
            SectionName = scope.SectionName,
            ClassCreditHours = fixedMarks.ClassCreditHours,
            LabCreditHours = fixedMarks.LabCreditHours,
            TotalCreditHours = fixedMarks.TotalCreditHours,
            CreditHoursDisplay = creditHoursDisplay,
            HasLab = fixedMarks.HasLab,
            StudentCount = studentCount,
            CanGenerate = blockers.Count == 0,
            HomeworkSchemeComplete = homeworkScheme.IsComplete,
            LabSchemeComplete = !fixedMarks.HasLab || (labScheme?.IsComplete == true),
            Blockers = blockers,
            Warnings = warnings,
            RequiredGroups = groups,
            Scheme = scheme,
            HomeworkScheme = homeworkScheme,
            LabScheme = labScheme
        };
    }

    public async Task<(bool Ok, string Message, CourseResultSchemeDto? Scheme)> SaveBestOfAsync(
        CloAttainmentService.OfferedCourseScope scope,
        SaveCourseResultBestOfDto dto)
    {
        if (dto.AssignmentBestOf < 1 || dto.QuizBestOf < 1)
        {
            return (false, "Best-of counts must be at least 1.", null);
        }

        var fixedMarks = await GetFixedMarkingAsync(scope.OfferedCourseId);
        var existing = await EnsureSchemeRowAsync(scope.OfferedCourseId, dto.TeacherUserId, fixedMarks);
        existing.AssignmentBestOf = dto.AssignmentBestOf;
        existing.QuizBestOf = dto.QuizBestOf;
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedByUserId = dto.TeacherUserId;
        await _context.SaveChangesAsync();

        return (true, string.Empty, await GetSchemeDtoAsync(scope.OfferedCourseId, fixedMarks));
    }

    public async Task<LabMarkSchemeDto> GetLabSchemeAsync(CloAttainmentService.OfferedCourseScope scope)
    {
        var fixedMarks = await GetFixedMarkingAsync(scope.OfferedCourseId);
        if (!fixedMarks.HasLab)
        {
            return new LabMarkSchemeDto
            {
                OfferedCourseId = scope.OfferedCourseId,
                TargetMarks = 0,
                RemainingMarks = 0,
                IsComplete = true
            };
        }

        return await GetLabSchemeDtoAsync(scope.OfferedCourseId);
    }

    public async Task<(bool Ok, string Message, LabMarkSchemeDto? Scheme)> SaveLabSchemeAsync(
        CloAttainmentService.OfferedCourseScope scope,
        SaveLabMarkSchemeDto dto)
    {
        var fixedMarks = await GetFixedMarkingAsync(scope.OfferedCourseId);
        if (!fixedMarks.HasLab)
        {
            return (false, "This course has no lab credit hours.", null);
        }

        var extras = NormalizeExtraItems(dto.ExtraItems);
        var values = new[]
        {
            dto.QuizMarks,
            dto.AssignmentMarks,
            dto.LabTaskMarks,
            dto.ProjectMarks,
            dto.AttendanceMarks,
            dto.ExamMarks
        };

        if (values.Any(v => v < 0) || extras.Any(x => x.Marks < 0))
        {
            return (false, "Lab matrix marks cannot be negative.", null);
        }

        var sum = Round(values.Sum() + extras.Sum(x => x.Marks));
        if (sum > LabTargetMarks)
        {
            return (false, $"You cannot enter extra marks above {LabTargetMarks:0.##}. Lab mapping total is {sum:0.##}.", null);
        }

        var existing = await _context.OfferedCourseLabMarkSchemes
            .FirstOrDefaultAsync(s => s.OfferedCourseId == scope.OfferedCourseId);
        if (existing == null)
        {
            existing = new OfferedCourseLabMarkScheme { OfferedCourseId = scope.OfferedCourseId };
            _context.OfferedCourseLabMarkSchemes.Add(existing);
        }

        existing.QuizMarks = Round(dto.QuizMarks);
        existing.AssignmentMarks = Round(dto.AssignmentMarks);
        existing.LabTaskMarks = Round(dto.LabTaskMarks);
        existing.ProjectMarks = Round(dto.ProjectMarks);
        existing.AttendanceMarks = Round(dto.AttendanceMarks);
        existing.ExamMarks = Round(dto.ExamMarks);
        existing.ExtraItemsJson = SerializeExtraItems(extras);
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedByUserId = dto.TeacherUserId;
        await _context.SaveChangesAsync();

        return (true, string.Empty, await GetLabSchemeDtoAsync(scope.OfferedCourseId));
    }

    public async Task<HomeworkMarkSchemeDto> GetHomeworkSchemeAsync(
        CloAttainmentService.OfferedCourseScope scope)
    {
        var fixedMarks = await GetFixedMarkingAsync(scope.OfferedCourseId);
        return await GetHomeworkSchemeDtoAsync(scope.OfferedCourseId, fixedMarks.HomeworkMarks);
    }

    public async Task<(bool Ok, string Message, HomeworkMarkSchemeDto? Scheme)> SaveHomeworkSchemeAsync(
        CloAttainmentService.OfferedCourseScope scope,
        SaveHomeworkMarkSchemeDto dto)
    {
        var fixedMarks = await GetFixedMarkingAsync(scope.OfferedCourseId);
        var extras = NormalizeExtraItems(dto.ExtraItems);
        var values = new[] { dto.AttendanceMarks, dto.QuizMarks, dto.AssignmentMarks };
        if (values.Any(v => v < 0) || extras.Any(x => x.Marks < 0))
        {
            return (false, "Homework matrix marks cannot be negative.", null);
        }

        var sum = Round(values.Sum() + extras.Sum(x => x.Marks));
        if (sum > fixedMarks.HomeworkMarks)
        {
            return (false,
                $"You cannot enter extra marks above {fixedMarks.HomeworkMarks:0.##}. Homework mapping total is {sum:0.##}.",
                null);
        }

        var existing = await _context.OfferedCourseHomeworkMarkSchemes
            .FirstOrDefaultAsync(s => s.OfferedCourseId == scope.OfferedCourseId);
        if (existing == null)
        {
            existing = new OfferedCourseHomeworkMarkScheme { OfferedCourseId = scope.OfferedCourseId };
            _context.OfferedCourseHomeworkMarkSchemes.Add(existing);
        }

        existing.AttendanceMarks = Round(dto.AttendanceMarks);
        existing.QuizMarks = Round(dto.QuizMarks);
        existing.AssignmentMarks = Round(dto.AssignmentMarks);
        existing.ExtraItemsJson = SerializeExtraItems(extras);
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedByUserId = dto.TeacherUserId;
        await _context.SaveChangesAsync();

        return (true, string.Empty,
            await GetHomeworkSchemeDtoAsync(scope.OfferedCourseId, fixedMarks.HomeworkMarks));
    }

    public async Task<(bool Ok, string Message, CourseResultGridDto? Grid)> GenerateAsync(
        CloAttainmentService.OfferedCourseScope scope,
        int teacherUserId)
    {
        var readiness = await GetReadinessAsync(scope);
        if (!readiness.CanGenerate)
        {
            return (false, readiness.Blockers.FirstOrDefault() ?? "Results cannot be generated yet.", null);
        }

        var grid = await BuildGridAsync(scope, readiness);
        var generatedAt = DateTime.Now;

        var existingResults = await _context.StudentCourseResults
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();
        _context.StudentCourseResults.RemoveRange(existingResults);

        var existingSelections = await _context.StudentCourseResultSelections
            .Where(s => s.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();
        _context.StudentCourseResultSelections.RemoveRange(existingSelections);

        foreach (var row in grid.Students)
        {
            _context.StudentCourseResults.Add(new StudentCourseResult
            {
                OfferedCourseId = scope.OfferedCourseId,
                StudentId = row.StudentId,
                QuizMarks = 0,
                HomeworkMarks = row.HomeworkMarks,
                MidMarks = row.MidMarks,
                LabMarks = row.LabMarks,
                FinalTheoryMarks = row.FinalMarks,
                FinalLabMarks = 0,
                TotalObtained = row.TotalObtained,
                TotalMax = row.TotalMax,
                Percentage = row.Percentage,
                GeneratedAt = generatedAt,
                GeneratedByUserId = teacherUserId
            });

            foreach (var assessmentId in row.SelectedAssignmentIds)
            {
                _context.StudentCourseResultSelections.Add(new StudentCourseResultSelection
                {
                    OfferedCourseId = scope.OfferedCourseId,
                    StudentId = row.StudentId,
                    AssessmentId = assessmentId,
                    Category = SelectionAssignment
                });
            }

            foreach (var assessmentId in row.SelectedQuizIds)
            {
                _context.StudentCourseResultSelections.Add(new StudentCourseResultSelection
                {
                    OfferedCourseId = scope.OfferedCourseId,
                    StudentId = row.StudentId,
                    AssessmentId = assessmentId,
                    Category = SelectionQuiz
                });
            }
        }

        var catalogFixed = await GetFixedMarkingAsync(scope.OfferedCourseId);
        var schemeRow = await EnsureSchemeRowAsync(
            scope.OfferedCourseId,
            teacherUserId,
            catalogFixed);
        schemeRow.LastGeneratedAt = generatedAt;
        schemeRow.LastGeneratedByUserId = teacherUserId;
        await _context.SaveChangesAsync();

        grid.GeneratedAt = generatedAt;
        grid.GeneratedByUserId = teacherUserId;
        grid.Scheme = readiness.Scheme;
        return (true, string.Empty, grid);
    }

    public async Task<CourseResultGridDto> GetResultsAsync(CloAttainmentService.OfferedCourseScope scope)
    {
        var fixedMarks = await GetFixedMarkingAsync(scope.OfferedCourseId);
        var scheme = await GetSchemeDtoAsync(scope.OfferedCourseId, fixedMarks);
        var rawRows = await (
            from result in _context.StudentCourseResults.AsNoTracking()
            join student in _context.Students.AsNoTracking() on result.StudentId equals student.StudentId
            where result.OfferedCourseId == scope.OfferedCourseId
            orderby student.AridNo
            select new
            {
                student.StudentId,
                student.FullName,
                student.AridNo,
                result.HomeworkMarks,
                result.MidMarks,
                result.LabMarks,
                result.FinalTheoryMarks,
                result.TotalObtained,
                result.TotalMax,
                result.Percentage
            }).ToListAsync();

        var selections = await _context.StudentCourseResultSelections
            .AsNoTracking()
            .Where(s => s.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();

        var rows = new List<CourseResultStudentRowDto>();
        foreach (var r in rawRows)
        {
            var hw = r.HomeworkMarks;
            var mid = r.MidMarks;
            var final = r.FinalTheoryMarks;
            var theoryTotal = Round(hw + mid + final);
            var prac = r.LabMarks;
            var grand = r.TotalObtained;
            var pct = r.Percentage;
            var (grade, qPoint) = CalculateGradeAndQPoint(pct, fixedMarks.TotalCreditHours);
            var inWords = NumberToWords((int)Math.Round(grand, MidpointRounding.AwayFromZero));

            rows.Add(new CourseResultStudentRowDto
            {
                StudentId = r.StudentId,
                FullName = r.FullName,
                AridNo = r.AridNo,
                HomeworkMarks = hw,
                MidMarks = mid,
                LabMarks = prac,
                FinalMarks = final,
                TotalObtained = grand,
                TotalMax = r.TotalMax,
                Percentage = pct,
                TheoryTotal = theoryTotal,
                PracScore = prac,
                GrandTotal = grand,
                InWords = inWords,
                Grade = grade,
                QPoint = qPoint,
                SelectedAssignmentIds = selections
                    .Where(s => s.StudentId == r.StudentId && s.Category == SelectionAssignment)
                    .Select(s => s.AssessmentId)
                    .ToList(),
                SelectedQuizIds = selections
                    .Where(s => s.StudentId == r.StudentId && s.Category == SelectionQuiz)
                    .Select(s => s.AssessmentId)
                    .ToList()
            });
        }

        var creditHoursDisplay = fixedMarks.HasLab
            ? $"{fixedMarks.TotalCreditHours}({fixedMarks.ClassCreditHours}-{fixedMarks.LabCreditHours})"
            : $"{fixedMarks.TotalCreditHours}({fixedMarks.ClassCreditHours}-0)";

        return new CourseResultGridDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseCode = scope.CourseCode,
            CourseTitle = scope.CourseTitle,
            TeacherName = scope.TeacherName,
            SessionTitle = scope.SessionTitle,
            ProgramCode = scope.ProgramCode,
            SectionName = scope.SectionName,
            ClassCreditHours = fixedMarks.ClassCreditHours,
            LabCreditHours = fixedMarks.LabCreditHours,
            TotalCreditHours = fixedMarks.TotalCreditHours,
            CreditHoursDisplay = creditHoursDisplay,
            GeneratedAt = scheme.LastGeneratedAt,
            Scheme = scheme,
            Students = rows
        };
    }

    private async Task<CourseResultGridDto> BuildGridAsync(
        CloAttainmentService.OfferedCourseScope scope,
        CourseResultReadinessDto readiness)
    {
        var scheme = readiness.Scheme;
        var homeworkScheme = readiness.HomeworkScheme!;
        var labScheme = readiness.LabScheme;
        var warnings = new List<string>(readiness.Warnings);

        var students = await (
            from soc in _context.StudentOfferedCourses.AsNoTracking()
            join student in _context.Students.AsNoTracking() on soc.StudentId equals student.StudentId
            where soc.OfferedCourseId == scope.OfferedCourseId
            orderby student.AridNo
            select new { student.StudentId, student.FullName, student.AridNo }).ToListAsync();

        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Where(a => a.OfferedCourseId == scope.OfferedCourseId && a.IsSetupComplete)
            .ToListAsync();

        var assessmentIds = assessments.Select(a => a.AssessmentId).ToList();
        var marks = await _context.StudentAssessmentMarks
            .AsNoTracking()
            .Where(m => assessmentIds.Contains(m.AssessmentId))
            .ToListAsync();
        var marksLookup = marks.ToDictionary(m => $"{m.StudentId}|{m.AssessmentId}", m => m.ObtainedMarks);

        var attendance = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();

        var componentMarks = await _context.StudentComponentMarks
            .AsNoTracking()
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .ToDictionaryAsync(r => r.StudentId);

        var assignments = assessments
            .Where(a => a.AssessmentType.AssessmentTypeName == AssignmentType)
            .OrderBy(a => a.AssessmentNo ?? int.MaxValue)
            .ToList();
        var quizzes = assessments
            .Where(a => a.AssessmentType.AssessmentTypeName == QuizType)
            .OrderBy(a => a.AssessmentNo ?? int.MaxValue)
            .ToList();
        var mid = assessments
            .Where(a => a.AssessmentType.AssessmentTypeName == MidType)
            .OrderBy(a => a.AssessmentNo ?? int.MaxValue)
            .FirstOrDefault();
        var finals = assessments
            .Where(a => a.AssessmentType.AssessmentTypeName == FinalType)
            .OrderBy(a => a.AssessmentNo ?? int.MaxValue)
            .ToList();
        var finalPaper = finals.FirstOrDefault(a => (a.AssessmentNo ?? 1) == 1) ?? finals.FirstOrDefault();
        var labExamPaper = readiness.HasLab
            ? finals.FirstOrDefault(a => a.AssessmentNo == 2)
            : null;

        var rows = new List<CourseResultStudentRowDto>();
        foreach (var student in students)
        {
            var assignmentPick = BestOf(student.StudentId, assignments, marksLookup, scheme.AssignmentBestOf);
            var quizPick = BestOf(student.StudentId, quizzes, marksLookup, scheme.QuizBestOf);
            componentMarks.TryGetValue(student.StudentId, out var component);

            var attendancePercent = component?.AttendancePercent
                ?? AttendancePercent(student.StudentId, attendance);
            if (attendancePercent < 0) attendancePercent = 0;
            if (attendancePercent > 100) attendancePercent = 100;

            var labExamPercent = PaperPercent(student.StudentId, labExamPaper, marksLookup);

            var extraMarks = ParseExtraMarks(component?.ExtraMarksJson);
            var homework = Round(
                Scale(attendancePercent, homeworkScheme.AttendanceMarks) +
                Scale(quizPick.Percent, homeworkScheme.QuizMarks) +
                Scale(assignmentPick.Percent, homeworkScheme.AssignmentMarks) +
                ExtraContribution(homeworkScheme.ExtraItems, extraMarks, "hw"));

            var lab = 0m;
            if (labScheme != null && readiness.HasLab)
            {
                var labTaskPercent = ObtainedToPercent(component?.LabTaskObtained, labScheme.LabTaskMarks);
                var projectPercent = ObtainedToPercent(component?.ProjectObtained, labScheme.ProjectMarks);

                lab = Round(
                    Scale(quizPick.Percent, labScheme.QuizMarks) +
                    Scale(assignmentPick.Percent, labScheme.AssignmentMarks) +
                    Scale(labTaskPercent, labScheme.LabTaskMarks) +
                    Scale(projectPercent, labScheme.ProjectMarks) +
                    Scale(attendancePercent, labScheme.AttendanceMarks) +
                    Scale(labExamPercent, labScheme.ExamMarks) +
                    ExtraContribution(labScheme.ExtraItems, extraMarks, "lab"));
            }

            var midMarks = Scale(PaperPercent(student.StudentId, mid, marksLookup), scheme.MidMarks);
            var finalMarks = Scale(PaperPercent(student.StudentId, finalPaper, marksLookup), scheme.FinalMarks);
            var theoryTotal = Round(homework + midMarks + finalMarks);
            var pracScore = lab;
            var grandTotal = Round(theoryTotal + pracScore);
            var percentage = scheme.TotalMarks > 0
                ? Round(grandTotal / scheme.TotalMarks * 100m)
                : 0;

            var (grade, qPoint) = CalculateGradeAndQPoint(percentage, scheme.Fixed.TotalCreditHours);
            var inWords = NumberToWords((int)Math.Round(grandTotal, MidpointRounding.AwayFromZero));

            rows.Add(new CourseResultStudentRowDto
            {
                StudentId = student.StudentId,
                FullName = student.FullName,
                AridNo = student.AridNo,
                HomeworkMarks = homework,
                MidMarks = midMarks,
                LabMarks = lab,
                FinalMarks = finalMarks,
                TotalObtained = grandTotal,
                TotalMax = scheme.TotalMarks,
                Percentage = percentage,
                TheoryTotal = theoryTotal,
                PracScore = pracScore,
                GrandTotal = grandTotal,
                InWords = inWords,
                Grade = grade,
                QPoint = qPoint,
                SelectedAssignmentIds = assignmentPick.AssessmentIds,
                SelectedQuizIds = quizPick.AssessmentIds
            });
        }

        var creditHoursDisplay = scheme.Fixed.HasLab
            ? $"{scheme.Fixed.TotalCreditHours}({scheme.Fixed.ClassCreditHours}-{scheme.Fixed.LabCreditHours})"
            : $"{scheme.Fixed.TotalCreditHours}({scheme.Fixed.ClassCreditHours}-0)";

        return new CourseResultGridDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseCode = scope.CourseCode,
            CourseTitle = scope.CourseTitle,
            TeacherName = scope.TeacherName,
            SessionTitle = scope.SessionTitle,
            ProgramCode = scope.ProgramCode,
            SectionName = scope.SectionName,
            ClassCreditHours = scheme.Fixed.ClassCreditHours,
            LabCreditHours = scheme.Fixed.LabCreditHours,
            TotalCreditHours = scheme.Fixed.TotalCreditHours,
            CreditHoursDisplay = creditHoursDisplay,
            Scheme = scheme,
            Warnings = warnings,
            Students = rows
        };
    }

    public async Task<ComponentMarksSheetDto> GetComponentMarksSheetAsync(
        CloAttainmentService.OfferedCourseScope scope)
    {
        var homework = await GetHomeworkSchemeAsync(scope);
        var lab = await GetLabSchemeAsync(scope);
        var fixedMarks = await GetFixedMarkingAsync(scope.OfferedCourseId);

        var hwAttendanceMax = homework.AttendanceMarks;
        var labAttendanceMax = lab.AttendanceMarks;
        var labTaskMax = lab.LabTaskMarks;
        var projectMax = lab.ProjectMarks;

        var extraColumns = homework.ExtraItems
            .Where(x => x.Marks > 0 && !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => new ComponentMarksExtraColumnDto
            {
                Key = ExtraMarkKey("hw", x.Key),
                Label = $"{x.Name} (HW, max {x.Marks:0.##})",
                MaxMarks = x.Marks
            })
            .Concat(
                lab.ExtraItems
                    .Where(x => x.Marks > 0 && !string.IsNullOrWhiteSpace(x.Key))
                    .Select(x => new ComponentMarksExtraColumnDto
                    {
                        Key = ExtraMarkKey("lab", x.Key),
                        Label = $"{x.Name} (Lab, max {x.Marks:0.##})",
                        MaxMarks = x.Marks
                    }))
            .ToList();

        var showAttendance = hwAttendanceMax > 0 || labAttendanceMax > 0;
        var showLabTasks = fixedMarks.HasLab && labTaskMax > 0;
        var showProject = fixedMarks.HasLab && projectMax > 0;

        var existing = await _context.StudentComponentMarks
            .AsNoTracking()
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .ToDictionaryAsync(r => r.StudentId);

        var students = await (
            from soc in _context.StudentOfferedCourses.AsNoTracking()
            join student in _context.Students.AsNoTracking() on soc.StudentId equals student.StudentId
            where soc.OfferedCourseId == scope.OfferedCourseId
            orderby student.AridNo
            select new { student.StudentId, student.FullName, student.AridNo }).ToListAsync();

        return new ComponentMarksSheetDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseTitle = scope.CourseTitle,
            SectionName = scope.SectionName,
            HasLab = fixedMarks.HasLab,
            HomeworkAttendanceMax = hwAttendanceMax,
            LabAttendanceMax = labAttendanceMax,
            LabTaskMax = labTaskMax,
            ProjectMax = projectMax,
            ShowAttendance = showAttendance,
            ShowLabTasks = showLabTasks,
            ShowProject = showProject,
            ExtraColumns = extraColumns,
            Students = students.Select(s =>
            {
                existing.TryGetValue(s.StudentId, out var row);
                var extras = ParseExtraMarks(row?.ExtraMarksJson);
                return new ComponentMarksStudentRowDto
                {
                    StudentId = s.StudentId,
                    FullName = s.FullName,
                    AridNo = s.AridNo,
                    AttendancePercent = row?.AttendancePercent,
                    LabTaskObtained = row?.LabTaskObtained,
                    ProjectObtained = row?.ProjectObtained,
                    ExtraObtained = extraColumns.ToDictionary(
                        col => col.Key,
                        col => extras.TryGetValue(col.Key, out var got) ? got : (decimal?)null)
                };
            }).ToList()
        };
    }

    public async Task<(bool Ok, string Message, ComponentMarksSheetDto? Sheet)> SaveComponentMarksAsync(
        CloAttainmentService.OfferedCourseScope scope,
        SaveComponentMarksDto dto)
    {
        var sheet = await GetComponentMarksSheetAsync(scope);
        var enrolled = await _context.StudentOfferedCourses
            .AsNoTracking()
            .Where(soc => soc.OfferedCourseId == scope.OfferedCourseId)
            .Select(soc => soc.StudentId)
            .ToListAsync();
        var enrolledSet = enrolled.ToHashSet();

        foreach (var entry in dto.Entries)
        {
            if (!enrolledSet.Contains(entry.StudentId))
            {
                return (false, "One or more students are not enrolled in this offered course.", null);
            }

            if (entry.AttendancePercent is < 0 or > 100)
            {
                return (false, "Attendance % must be between 0 and 100.", null);
            }

            if (entry.LabTaskObtained is < 0)
            {
                return (false, "Lab task marks cannot be negative.", null);
            }

            if (sheet.LabTaskMax > 0 &&
                entry.LabTaskObtained is decimal labGot &&
                labGot > sheet.LabTaskMax + 0.01m)
            {
                return (false, $"Lab task marks cannot exceed {sheet.LabTaskMax:0.##}.", null);
            }

            if (entry.ProjectObtained is < 0)
            {
                return (false, "Project marks cannot be negative.", null);
            }

            if (sheet.ProjectMax > 0 &&
                entry.ProjectObtained is decimal projGot &&
                projGot > sheet.ProjectMax + 0.01m)
            {
                return (false, $"Project marks cannot exceed {sheet.ProjectMax:0.##}.", null);
            }

            foreach (var col in sheet.ExtraColumns)
            {
                if (entry.ExtraObtained == null ||
                    !entry.ExtraObtained.TryGetValue(col.Key, out var extraGot) ||
                    extraGot == null)
                {
                    continue;
                }

                if (extraGot < 0)
                {
                    return (false, $"{col.Label} cannot be negative.", null);
                }

                if (extraGot > col.MaxMarks + 0.01m)
                {
                    return (false, $"{col.Label} cannot exceed {col.MaxMarks:0.##}.", null);
                }
            }
        }

        var existing = await _context.StudentComponentMarks
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();
        var existingLookup = existing.ToDictionary(r => r.StudentId);
        var now = DateTime.UtcNow;

        foreach (var entry in dto.Entries)
        {
            var extraJson = SerializeExtraMarks(entry.ExtraObtained, sheet.ExtraColumns);
            var allEmpty =
                entry.AttendancePercent == null &&
                entry.LabTaskObtained == null &&
                entry.ProjectObtained == null &&
                extraJson == null;

            if (allEmpty)
            {
                if (existingLookup.TryGetValue(entry.StudentId, out var removeRow))
                {
                    _context.StudentComponentMarks.Remove(removeRow);
                    existingLookup.Remove(entry.StudentId);
                }

                continue;
            }

            if (existingLookup.TryGetValue(entry.StudentId, out var row))
            {
                row.AttendancePercent = entry.AttendancePercent;
                row.LabTaskObtained = entry.LabTaskObtained;
                row.ProjectObtained = entry.ProjectObtained;
                row.ExtraMarksJson = extraJson;
                row.UpdatedAt = now;
                row.UpdatedByUserId = dto.TeacherUserId;
            }
            else
            {
                var added = new StudentComponentMark
                {
                    OfferedCourseId = scope.OfferedCourseId,
                    StudentId = entry.StudentId,
                    AttendancePercent = entry.AttendancePercent,
                    LabTaskObtained = entry.LabTaskObtained,
                    ProjectObtained = entry.ProjectObtained,
                    ExtraMarksJson = extraJson,
                    UpdatedAt = now,
                    UpdatedByUserId = dto.TeacherUserId
                };
                _context.StudentComponentMarks.Add(added);
                existingLookup[entry.StudentId] = added;
            }
        }

        await _context.SaveChangesAsync();
        return (true, "Saved.", await GetComponentMarksSheetAsync(scope));
    }

    private async Task<List<CourseResultReadinessGroupDto>> BuildRequiredGroupsAsync(
        int offeredCourseId,
        bool hasLab)
    {
        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Where(a => a.OfferedCourseId == offeredCourseId)
            .ToListAsync();

        var typeNames = new[] { AssignmentType, QuizType, MidType, FinalType };
        var groups = new List<CourseResultReadinessGroupDto>();

        foreach (var typeName in typeNames)
        {
            var requiredCount = AssessmentFolderSyncService.GetRequiredTemplateCount(typeName, hasLab);
            var items = assessments
                .Where(a =>
                    a.AssessmentType.AssessmentTypeName == typeName &&
                    AssessmentFolderSyncService.IsRequiredTemplate(typeName, a.AssessmentNo, hasLab))
                .OrderBy(a => a.AssessmentNo ?? int.MaxValue)
                .Select(a => new CourseResultReadinessItemDto
                {
                    AssessmentId = a.AssessmentId,
                    AssessmentNo = a.AssessmentNo,
                    Title = string.IsNullOrWhiteSpace(a.Title) ? typeName : a.Title,
                    IsSetupComplete = a.IsSetupComplete
                })
                .ToList();

            var completed = items.Count(i => i.IsSetupComplete);
            groups.Add(new CourseResultReadinessGroupDto
            {
                AssessmentTypeName = typeName,
                RequiredCount = requiredCount,
                CompletedCount = completed,
                IsComplete = completed >= requiredCount && requiredCount > 0,
                Items = items
            });
        }

        return groups;
    }

    private async Task<CourseResultSchemeDto> GetSchemeDtoAsync(
        int offeredCourseId,
        CourseFixedMarkingDto fixedMarks)
    {
        var row = await _context.OfferedCourseResultSchemes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OfferedCourseId == offeredCourseId);

        return new CourseResultSchemeDto
        {
            Fixed = fixedMarks,
            AssignmentBestOf = row?.AssignmentBestOf > 0 ? row.AssignmentBestOf : 3,
            QuizBestOf = row?.QuizBestOf > 0 ? row.QuizBestOf : 6,
            UpdatedAt = row?.UpdatedAt,
            LastGeneratedAt = row?.LastGeneratedAt
        };
    }

    private async Task<LabMarkSchemeDto> GetLabSchemeDtoAsync(int offeredCourseId)
    {
        var row = await _context.OfferedCourseLabMarkSchemes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OfferedCourseId == offeredCourseId);

        var quiz = row?.QuizMarks ?? 0;
        var assignment = row?.AssignmentMarks ?? 0;
        var labTask = row?.LabTaskMarks ?? 0;
        var project = row?.ProjectMarks ?? 0;
        var attendance = row?.AttendanceMarks ?? 0;
        var exam = row?.ExamMarks ?? 0;
        var extras = ParseExtraItems(row?.ExtraItemsJson);
        var extraTotal = Round(extras.Sum(x => x.Marks));
        var assigned = Round(quiz + assignment + labTask + project + attendance + exam + extraTotal);

        return new LabMarkSchemeDto
        {
            OfferedCourseId = offeredCourseId,
            TargetMarks = LabTargetMarks,
            QuizMarks = quiz,
            AssignmentMarks = assignment,
            LabTaskMarks = labTask,
            ProjectMarks = project,
            AttendanceMarks = attendance,
            ExamMarks = exam,
            ExtraItems = extras,
            ExtraMarksTotal = extraTotal,
            AssignedTotal = assigned,
            RemainingMarks = Round(LabTargetMarks - assigned),
            IsComplete = assigned == LabTargetMarks,
            UpdatedAt = row?.UpdatedAt
        };
    }

    private async Task<HomeworkMarkSchemeDto> GetHomeworkSchemeDtoAsync(
        int offeredCourseId,
        decimal targetMarks)
    {
        var row = await _context.OfferedCourseHomeworkMarkSchemes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OfferedCourseId == offeredCourseId);

        var attendance = row?.AttendanceMarks ?? 0;
        var quiz = row?.QuizMarks ?? 0;
        var assignment = row?.AssignmentMarks ?? 0;
        var extras = ParseExtraItems(row?.ExtraItemsJson);
        var extraTotal = Round(extras.Sum(x => x.Marks));
        var assigned = Round(attendance + quiz + assignment + extraTotal);

        return new HomeworkMarkSchemeDto
        {
            OfferedCourseId = offeredCourseId,
            TargetMarks = targetMarks,
            AttendanceMarks = attendance,
            QuizMarks = quiz,
            AssignmentMarks = assignment,
            ExtraItems = extras,
            ExtraMarksTotal = extraTotal,
            AssignedTotal = assigned,
            RemainingMarks = Round(targetMarks - assigned),
            IsComplete = assigned == targetMarks,
            UpdatedAt = row?.UpdatedAt
        };
    }

    private async Task<OfferedCourseResultScheme> EnsureSchemeRowAsync(
        int offeredCourseId,
        int teacherUserId,
        CourseFixedMarkingDto fixedMarks)
    {
        var existing = await _context.OfferedCourseResultSchemes
            .FirstOrDefaultAsync(s => s.OfferedCourseId == offeredCourseId);
        if (existing != null)
        {
            // Keep stored totals in sync with fixed profile for older columns.
            existing.TotalMarks = fixedMarks.TotalMarks;
            existing.QuizMarks = 0;
            existing.HomeworkMarks = fixedMarks.HomeworkMarks;
            existing.MidMarks = fixedMarks.MidMarks;
            existing.LabMarks = fixedMarks.LabMarks;
            existing.FinalTheoryMarks = fixedMarks.FinalMarks;
            existing.FinalLabMarks = 0;
            existing.AttendanceHomeworkMarks = 0;
            return existing;
        }

        existing = new OfferedCourseResultScheme
        {
            OfferedCourseId = offeredCourseId,
            TotalMarks = fixedMarks.TotalMarks,
            QuizMarks = 0,
            HomeworkMarks = fixedMarks.HomeworkMarks,
            MidMarks = fixedMarks.MidMarks,
            LabMarks = fixedMarks.LabMarks,
            FinalTheoryMarks = fixedMarks.FinalMarks,
            FinalLabMarks = 0,
            AssignmentBestOf = 3,
            QuizBestOf = 6,
            AttendanceHomeworkMarks = 0,
            UpdatedAt = DateTime.Now,
            UpdatedByUserId = teacherUserId
        };
        _context.OfferedCourseResultSchemes.Add(existing);
        return existing;
    }

    private static (decimal Percent, List<int> AssessmentIds) BestOf(
        int studentId,
        List<Assessment> papers,
        Dictionary<string, decimal> marksLookup,
        int keepCount)
    {
        if (papers.Count == 0 || keepCount < 1)
        {
            return (0, new List<int>());
        }

        var ranked = papers
            .Select(paper => new
            {
                paper.AssessmentId,
                Percent = PaperPercent(studentId, paper, marksLookup)
            })
            .OrderByDescending(x => x.Percent)
            .ThenBy(x => x.AssessmentId)
            .Take(Math.Min(keepCount, papers.Count))
            .ToList();

        var percent = ranked.Count == 0 ? 0 : ranked.Average(x => x.Percent);
        return (percent, ranked.Select(x => x.AssessmentId).ToList());
    }

    private static decimal PaperPercent(
        int studentId,
        Assessment? paper,
        Dictionary<string, decimal> marksLookup)
    {
        if (paper == null || paper.TotalMarks is null or <= 0)
        {
            return 0;
        }

        marksLookup.TryGetValue($"{studentId}|{paper.AssessmentId}", out var obtained);
        var percent = obtained / paper.TotalMarks.Value * 100m;
        if (percent < 0) return 0;
        if (percent > 100) return 100;
        return percent;
    }

    private static decimal AttendancePercent(int studentId, List<AttendanceRecord> records)
    {
        var studentRecords = records.Where(r => r.StudentId == studentId).ToList();
        if (studentRecords.Count == 0)
        {
            return 0;
        }

        var present = studentRecords.Count(IsPresent);
        return (decimal)present / studentRecords.Count * 100m;
    }

    private static bool IsPresent(AttendanceRecord record)
    {
        var status = (record.Status ?? string.Empty).Trim();
        return status.Equals("Present", StringComparison.OrdinalIgnoreCase)
            || status.Equals("P", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions ExtraJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static string ExtraMarkKey(string prefix, string key) => $"{prefix}:{key}";

    private static List<MarkSchemeExtraItemDto> ParseExtraItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<MarkSchemeExtraItemDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<MarkSchemeExtraItemDto>>(json, ExtraJsonOptions)
                   ?? new List<MarkSchemeExtraItemDto>();
        }
        catch (JsonException)
        {
            return new List<MarkSchemeExtraItemDto>();
        }
    }

    private static string? SerializeExtraItems(List<MarkSchemeExtraItemDto> items) =>
        items.Count == 0 ? null : JsonSerializer.Serialize(items, ExtraJsonOptions);

    private static List<MarkSchemeExtraItemDto> NormalizeExtraItems(IEnumerable<MarkSchemeExtraItemDto>? items)
    {
        var result = new List<MarkSchemeExtraItemDto>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (items == null)
        {
            return result;
        }

        foreach (var item in items)
        {
            var name = (item.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var baseKey = string.IsNullOrWhiteSpace(item.Key) ? Slug(name) : Slug(item.Key);
            var key = baseKey;
            var n = 2;
            while (!used.Add(key))
            {
                key = $"{baseKey}-{n++}";
            }

            result.Add(new MarkSchemeExtraItemDto
            {
                Key = key,
                Name = name,
                Marks = Round(Math.Max(item.Marks, 0))
            });
        }

        return result;
    }

    private static string Slug(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "item" : slug;
    }

    private static Dictionary<string, decimal?> ParseExtraMarks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, decimal?>>(json, ExtraJsonOptions);
            return raw == null
                ? new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, decimal?>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? SerializeExtraMarks(
        Dictionary<string, decimal?>? extraObtained,
        List<ComponentMarksExtraColumnDto> columns)
    {
        if (extraObtained == null || columns.Count == 0)
        {
            return null;
        }

        var clean = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columns)
        {
            if (!extraObtained.TryGetValue(col.Key, out var got) || got == null)
            {
                continue;
            }

            clean[col.Key] = Round(got.Value);
        }

        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean, ExtraJsonOptions);
    }

    private static decimal ExtraContribution(
        IEnumerable<MarkSchemeExtraItemDto> extras,
        Dictionary<string, decimal?> obtained,
        string prefix)
    {
        decimal sum = 0;
        foreach (var item in extras)
        {
            if (item.Marks <= 0 || string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            obtained.TryGetValue(ExtraMarkKey(prefix, item.Key), out var got);
            if (got == null)
            {
                obtained.TryGetValue(item.Key, out got);
            }

            sum += Scale(ObtainedToPercent(got, item.Marks), item.Marks);
        }

        return sum;
    }

    private static decimal ObtainedToPercent(decimal? obtained, decimal maxMarks)
    {
        if (maxMarks <= 0 || obtained == null)
        {
            return 0;
        }

        var capped = Math.Min(Math.Max(obtained.Value, 0), maxMarks);
        return Round(capped / maxMarks * 100m);
    }

    private static decimal Scale(decimal percent, decimal maxMarks) =>
        Round(percent / 100m * maxMarks);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static (string Grade, decimal QPoint) CalculateGradeAndQPoint(decimal percentage, int totalCreditHours)
    {
        string grade;
        decimal gpa;

        if (percentage >= 80m)
        {
            grade = "A";
            gpa = 4.0m;
        }
        else if (percentage >= 65m)
        {
            grade = "B";
            gpa = Math.Round(3.0m + (percentage - 65m) * 0.07m, 1, MidpointRounding.AwayFromZero);
        }
        else if (percentage >= 50m)
        {
            grade = "C";
            gpa = Math.Round(2.0m + (percentage - 50m) * 0.06m, 1, MidpointRounding.AwayFromZero);
        }
        else if (percentage >= 40m)
        {
            grade = "D";
            gpa = Math.Round(1.0m + (percentage - 40m) * 0.10m, 1, MidpointRounding.AwayFromZero);
        }
        else
        {
            grade = "F";
            gpa = 0.0m;
        }

        var qPoint = Math.Round(gpa * Math.Max(totalCreditHours, 1), 2, MidpointRounding.AwayFromZero);
        return (grade, qPoint);
    }

    public static string NumberToWords(int number)
    {
        if (number <= 0) return "ZERO";
        if (number == 100) return "ONE HUNDRED";

        string[] ones = { "", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN",
                          "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN" };
        string[] tens = { "", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY" };

        if (number < 20) return ones[number];
        if (number < 100)
        {
            int t = number / 10;
            int r = number % 10;
            return r == 0 ? tens[t] : $"{tens[t]}-{ones[r]}";
        }

        return number.ToString();
    }
}
