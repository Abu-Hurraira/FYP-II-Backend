using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class CloAttainmentService
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;
    private readonly SessionScopeService _sessionScopeService;
    private readonly ContentSubmissionService _contentSubmissionService;

    public CloAttainmentService(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService,
        SessionScopeService sessionScopeService,
        ContentSubmissionService contentSubmissionService)
    {
        _context = context;
        _permissionService = permissionService;
        _sessionScopeService = sessionScopeService;
        _contentSubmissionService = contentSubmissionService;
    }

    public async Task<(bool Ok, string Message, OfferedCourseScope? Scope)> TryResolveScopeAsync(
        int offeredCourseId,
        int teacherUserId)
    {
        var row = await (
            from oc in _context.OfferedCourses
            join course in _context.Courses on oc.CourseId equals course.CourseId
            join section in _context.Sections on oc.SectionId equals section.SectionId
            join session in _context.AcademicSessions on section.SessionId equals session.SessionId
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
                ProgramCode = session.Program != null ? (session.Program.ProgramCode ?? session.Program.ProgramName ?? string.Empty) : string.Empty,
                TeacherName = oc.TeacherUser != null ? oc.TeacherUser.FullName : string.Empty,
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
            // HOD can view/calculate attainment for any section in the system.
            canAccess = await _context.Users
                .AsNoTracking()
                .AnyAsync(u =>
                    u.UserId == teacherUserId &&
                    u.Role.RoleName == "HOD");
        }

        if (!canAccess)
        {
            return (false, "You do not have access to this course section.", null);
        }

        return (true, string.Empty, new OfferedCourseScope
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

    public async Task<List<CloAttainmentStudentDto>> GetStudentsAsync(OfferedCourseScope scope)
    {
        // Roster = students enrolled in this offered course (StudentOfferedCourses).
        return await (
            from soc in _context.StudentOfferedCourses.AsNoTracking()
            join s in _context.Students.AsNoTracking() on soc.StudentId equals s.StudentId
            where soc.OfferedCourseId == scope.OfferedCourseId
            orderby s.AridNo
            select new CloAttainmentStudentDto
            {
                StudentId = s.StudentId,
                FullName = s.FullName,
                AridNo = s.AridNo
            }).ToListAsync();
    }

    public async Task<(bool Ok, string Message, AssessmentMarksSheetDto? Sheet)> GetMarksSheetAsync(
        int assessmentId,
        OfferedCourseScope scope)
    {
        var assessment = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
                    .ThenInclude(m => m.Clo)
            .FirstOrDefaultAsync(a =>
                a.AssessmentId == assessmentId &&
                a.OfferedCourseId == scope.OfferedCourseId);

        if (assessment == null)
        {
            return (false, "Assessment not found for this section.", null);
        }

        if (!assessment.IsSetupComplete)
        {
            return (
                false,
                "Complete the assessment setup (questions, marks, and CLO mapping) before opening the mark sheet.",
                null);
        }

        var students = await GetStudentsAsync(scope);

        var parentIdsWithChildren = assessment.AssessmentQuestions
            .Where(q => q.ParentQuestionId != null)
            .Select(q => q.ParentQuestionId!.Value)
            .ToHashSet();

        // Grade leaf nodes when parts exist; otherwise grade the root questions.
        var gradeable = assessment.AssessmentQuestions
            .Where(q => !parentIdsWithChildren.Contains(q.QuestionId))
            .OrderBy(q => q.DisplayOrder ?? int.MaxValue)
            .ThenBy(q => q.QuestionNo)
            .ThenBy(q => q.QuestionId)
            .ToList();

        var questionIds = gradeable.Select(q => q.QuestionId).ToList();
        var marks = await _context.StudentQuestionMarks
            .AsNoTracking()
            .Where(m => questionIds.Contains(m.QuestionId))
            .ToListAsync();

        var marksLookup = marks.ToDictionary(m => $"{m.StudentId}|{m.QuestionId}", m => (decimal?)m.ObtainedMarks);

        Dictionary<int, List<string>> ResolveCloCodes()
        {
            var byId = assessment.AssessmentQuestions.ToDictionary(q => q.QuestionId);
            var cache = new Dictionary<int, List<string>>();

            List<string> ForQuestion(int questionId)
            {
                if (cache.TryGetValue(questionId, out var cached))
                {
                    return cached;
                }

                if (!byId.TryGetValue(questionId, out var q))
                {
                    return new List<string>();
                }

                var own = q.AssessmentQuestionClomappings
                    .Select(m => m.Clo?.Clocode ?? string.Empty)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .OrderBy(c => c)
                    .ToList();

                if (own.Count > 0)
                {
                    cache[questionId] = own;
                    return own;
                }

                if (q.ParentQuestionId is int parentId)
                {
                    var inherited = ForQuestion(parentId);
                    cache[questionId] = inherited;
                    return inherited;
                }

                cache[questionId] = own;
                return own;
            }

            foreach (var q in gradeable)
            {
                ForQuestion(q.QuestionId);
            }

            return cache;
        }

        var cloByQuestion = ResolveCloCodes();

        var questions = gradeable
            .Select(q => new MarksSheetQuestionDto
            {
                QuestionId = q.QuestionId,
                QuestionNo = q.QuestionNo,
                QuestionText = q.QuestionText,
                MaxMarks = q.Marks is > 0 ? q.Marks.Value : 0,
                CloCodes = cloByQuestion.TryGetValue(q.QuestionId, out var codes) ? codes : new List<string>()
            })
            .ToList();

        var paperMax = questions.Sum(q => q.MaxMarks);
        var assessmentTotal = assessment.TotalMarks is > 0
            ? assessment.TotalMarks.Value
            : paperMax;

        var studentRows = students.Select(s =>
        {
            var byQuestion = questions.ToDictionary(
                q => q.QuestionId,
                q => marksLookup.TryGetValue($"{s.StudentId}|{q.QuestionId}", out var value) ? value : null);

            var entered = byQuestion.Values.Where(v => v != null).Select(v => v!.Value).ToList();
            decimal? totalObtained = entered.Count > 0 ? entered.Sum() : null;
            decimal? converted = null;
            decimal? percentage = null;
            if (totalObtained != null && paperMax > 0)
            {
                percentage = Math.Round(totalObtained.Value * 100m / paperMax, 2);
                converted = assessment.TotalMarks is > 0
                    ? Math.Round(totalObtained.Value / paperMax * assessment.TotalMarks.Value, 2, MidpointRounding.AwayFromZero)
                    : totalObtained;
            }

            return new MarksSheetStudentRowDto
            {
                StudentId = s.StudentId,
                FullName = s.FullName,
                AridNo = s.AridNo,
                MarksByQuestionId = byQuestion,
                TotalObtained = totalObtained,
                ConvertedObtained = converted,
                Percentage = percentage
            };
        }).ToList();

        return (true, "OK", new AssessmentMarksSheetDto
        {
            AssessmentId = assessment.AssessmentId,
            AssessmentTitle = assessment.Title,
            AssessmentTypeName = assessment.AssessmentType.AssessmentTypeName,
            AssessmentNo = assessment.AssessmentNo,
            TotalMarks = assessment.TotalMarks,
            PaperMaxMarks = paperMax,
            IsSetupComplete = assessment.IsSetupComplete,
            Questions = questions,
            Students = studentRows
        });
    }

    public async Task<(bool Ok, string Message)> SaveMarksSheetAsync(
        int assessmentId,
        OfferedCourseScope scope,
        SaveAssessmentMarksDto dto)
    {
        var assessment = await _context.Assessments
            .Include(a => a.AssessmentQuestions)
            .FirstOrDefaultAsync(a =>
                a.AssessmentId == assessmentId &&
                a.OfferedCourseId == scope.OfferedCourseId);

        if (assessment == null)
        {
            return (false, "Assessment not found.");
        }

        if (!assessment.IsSetupComplete)
        {
            return (false, "Complete the assessment setup before entering marks.");
        }

        var parentIdsWithChildren = assessment.AssessmentQuestions
            .Where(q => q.ParentQuestionId != null)
            .Select(q => q.ParentQuestionId!.Value)
            .ToHashSet();
        var gradeableIds = assessment.AssessmentQuestions
            .Where(q => !parentIdsWithChildren.Contains(q.QuestionId))
            .Select(q => q.QuestionId)
            .ToHashSet();

        var questionMap = assessment.AssessmentQuestions.ToDictionary(q => q.QuestionId);
        var studentIds = await _context.StudentOfferedCourses
            .Where(soc => soc.OfferedCourseId == scope.OfferedCourseId)
            .Select(soc => soc.StudentId)
            .ToListAsync();
        var studentSet = studentIds.ToHashSet();

        foreach (var entry in dto.Marks)
        {
            if (!questionMap.TryGetValue(entry.QuestionId, out var question))
            {
                return (false, $"Question {entry.QuestionId} does not belong to this assessment.");
            }

            if (!gradeableIds.Contains(entry.QuestionId))
            {
                return (false, $"Question {question.QuestionNo} is not gradeable (it has parts). Enter marks on the parts.");
            }

            if (!studentSet.Contains(entry.StudentId))
            {
                return (false, "One or more students are not enrolled in this offered course.");
            }

            var maxMarks = question.Marks is > 0 ? question.Marks.Value : 0;
            if (entry.ObtainedMarks is < 0)
            {
                return (false, "Obtained marks cannot be negative.");
            }

            if (maxMarks > 0 && entry.ObtainedMarks > maxMarks)
            {
                return (false, $"Marks for {question.QuestionNo} cannot exceed {maxMarks}.");
            }
        }

        var questionIds = gradeableIds.ToList();
        var existing = await _context.StudentQuestionMarks
            .Where(m => questionIds.Contains(m.QuestionId))
            .ToListAsync();
        var existingLookup = existing.ToDictionary(m => $"{m.StudentId}|{m.QuestionId}");

        foreach (var entry in dto.Marks)
        {
            var key = $"{entry.StudentId}|{entry.QuestionId}";
            if (entry.ObtainedMarks == null)
            {
                if (existingLookup.TryGetValue(key, out var removeRow))
                {
                    _context.StudentQuestionMarks.Remove(removeRow);
                    existingLookup.Remove(key);
                }

                continue;
            }

            if (existingLookup.TryGetValue(key, out var row))
            {
                row.ObtainedMarks = entry.ObtainedMarks.Value;
            }
            else
            {
                var added = new StudentQuestionMark
                {
                    StudentId = entry.StudentId,
                    QuestionId = entry.QuestionId,
                    ObtainedMarks = entry.ObtainedMarks.Value
                };
                _context.StudentQuestionMarks.Add(added);
                existingLookup[key] = added;
            }
        }

        // Sync assessment-level totals: convert paper-scale scores into assessment TotalMarks weight.
        var gradeableList = assessment.AssessmentQuestions
            .Where(q => gradeableIds.Contains(q.QuestionId))
            .ToList();
        var paperMax = gradeableList.Sum(q => q.Marks ?? 0);
        var assessmentWeight = assessment.TotalMarks is > 0 ? assessment.TotalMarks.Value : paperMax;

        var remainingMarks = existingLookup.Values
            .GroupBy(m => m.StudentId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ObtainedMarks));

        var assessmentMarks = await _context.StudentAssessmentMarks
            .Where(m => m.AssessmentId == assessmentId)
            .ToListAsync();
        var assessmentLookup = assessmentMarks.ToDictionary(m => m.StudentId);

        foreach (var studentId in studentSet)
        {
            if (!remainingMarks.TryGetValue(studentId, out var rawTotal) || rawTotal <= 0)
            {
                if (assessmentLookup.TryGetValue(studentId, out var removeTotal))
                {
                    _context.StudentAssessmentMarks.Remove(removeTotal);
                }

                continue;
            }

            var converted = paperMax > 0 && assessmentWeight > 0
                ? Math.Round(rawTotal / paperMax * assessmentWeight, 2, MidpointRounding.AwayFromZero)
                : rawTotal;

            if (assessmentLookup.TryGetValue(studentId, out var row))
            {
                row.ObtainedMarks = converted;
            }
            else
            {
                _context.StudentAssessmentMarks.Add(new StudentAssessmentMark
                {
                    AssessmentId = assessmentId,
                    StudentId = studentId,
                    ObtainedMarks = converted
                });
            }
        }

        await _context.SaveChangesAsync();
        return (true, "Marks saved.");
    }

    public async Task<CloAssessmentDesignDto> GetDesignAsync(OfferedCourseScope scope)
    {
        var clos = await _context.Clos
            .AsNoTracking()
            .Where(c =>
                c.ProgramCourseId == scope.ProgramCourseId &&
                c.SessionId == scope.SessionId &&
                c.ContentStatus == ContentStatuses.Approved)
            .OrderBy(c => c.Clocode)
            .ToListAsync();

        var builtInNames = new[] { "Assignment", "Quiz", "Mid", "Final" };
        var builtInTypes = await _context.AssessmentTypes
            .AsNoTracking()
            .Where(t => builtInNames.Contains(t.AssessmentTypeName))
            .OrderBy(t => t.AssessmentTypeId)
            .ToListAsync();

        var customTypeIds = await _context.CourseSessionTaskTypes
            .AsNoTracking()
            .Where(t =>
                t.ProgramCourseId == scope.ProgramCourseId &&
                t.SessionId == scope.SessionId)
            .Select(t => t.AssessmentTypeId)
            .ToListAsync();

        var customTypes = await _context.AssessmentTypes
            .AsNoTracking()
            .Where(t => customTypeIds.Contains(t.AssessmentTypeId))
            .OrderBy(t => t.AssessmentTypeId)
            .ToListAsync();

        var types = builtInTypes
            .Concat(customTypes.Where(c =>
                !builtInNames.Any(n =>
                    string.Equals(n, c.AssessmentTypeName, StringComparison.OrdinalIgnoreCase))))
            .GroupBy(t => t.AssessmentTypeId)
            .Select(g => g.First())
            .ToList();

        var mappings = await _context.CourseAssessmentTypeClomappings
            .AsNoTracking()
            .Where(m =>
                m.ProgramCourseId == scope.ProgramCourseId &&
                m.SessionId == scope.SessionId &&
                m.ContentStatus == ContentStatuses.Approved)
            .ToListAsync();

        var lookup = mappings.ToDictionary(m => $"{m.AssessmentTypeId}|{m.Cloid}", m => m.Percentage);

        var columns = clos.Select(c => new CloAssessmentDesignColumnDto
        {
            CloId = c.Cloid,
            CloCode = c.Clocode,
            CloDescription = c.Clodescription
        }).ToList();

        var rows = types.Select(t => new CloAssessmentDesignRowDto
        {
            AssessmentTypeId = t.AssessmentTypeId,
            AssessmentTypeName = t.AssessmentTypeName,
            PercentagesByCloId = clos.ToDictionary(
                c => c.Cloid,
                c => lookup.TryGetValue($"{t.AssessmentTypeId}|{c.Cloid}", out var pct) ? pct : null)
        }).ToList();

        var totals = clos.ToDictionary(
            c => c.Cloid,
            c => Math.Round(
                rows.Sum(r => r.PercentagesByCloId.TryGetValue(c.Cloid, out var v) ? v ?? 0 : 0),
                2));

        return new CloAssessmentDesignDto
        {
            ProgramCourseId = scope.ProgramCourseId,
            SessionId = scope.SessionId,
            Clos = columns,
            Rows = rows,
            CloColumnTotals = totals
        };
    }

    public async Task<(bool Ok, string Message, CloAssessmentDesignDto? Design)> SaveDesignCellAsync(
        OfferedCourseScope scope,
        SaveCloAssessmentDesignCellDto dto)
    {
        return (
            false,
            "CLO assessment design is managed in Upload under CLO and Task mapping. Edit and get HOD approval there.",
            null);
    }

    /// <summary>
    /// Calculates CLO attainment for a section, then PLO attainment.
    /// Formulas (applied below):
    ///   local%  = avg student scores on CLO-mapped questions of that type (weighted by coverage %)
    ///   CLO%    = Σ (local% × CLO and Task type share / 100)
    ///   PLO%    = Σ (CLO% × CLO and PLO mapping % / 100)
    /// </summary>
    public async Task<CloAttainmentResultsDto> CalculateAsync(OfferedCourseScope scope, int teacherUserId)
    {
        var warnings = new List<string>();

        if (!await _contentSubmissionService.HasApprovedClosAsync(scope.ProgramCourseId, scope.SessionId))
        {
            warnings.Add("No HOD-approved CLOs found. Approve CLOs before calculating attainment.");
        }

        if (!await _contentSubmissionService.HasApprovedMappingAsync(
                scope.ProgramCourseId,
                scope.SessionId,
                ContentSubmissionTypeCodes.CloTaskMapping))
        {
            warnings.Add("CLO and Task mapping is not approved yet. Approve it with HOD before calculating.");
        }

        var design = await GetDesignAsync(scope);
        var students = await GetStudentsAsync(scope);

        if (design.Clos.Count == 0)
        {
            warnings.Add("No approved CLOs found for this subject/session.");
        }

        if (students.Count == 0)
        {
            warnings.Add("No students are enrolled in this offered course. Attainment will be empty.");
        }

        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .Where(a => a.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();

        var allQuestionIds = assessments.SelectMany(a => a.AssessmentQuestions).Select(q => q.QuestionId).ToList();
        var allMarks = await _context.StudentQuestionMarks
            .AsNoTracking()
            .Where(m => allQuestionIds.Contains(m.QuestionId))
            .ToListAsync();

        var results = new List<CloAttainmentResultItemDto>();

        foreach (var clo in design.Clos)
        {
            var contributions = new List<CloTypeContributionDto>();
            decimal overall = 0;

            var columnTotal = design.CloColumnTotals.TryGetValue(clo.CloId, out var total) ? total : 0;
            if (columnTotal <= 0)
            {
                warnings.Add($"{clo.CloCode}: no assessment-type shares configured (should total 100%).");
            }
            else if (Math.Abs(columnTotal - 100m) > 0.01m)
            {
                warnings.Add($"{clo.CloCode}: assessment-type shares total {columnTotal}% (expected 100%).");
            }

            // CLO% = Σ (localAttainment_type × CLO and Task designShare_type / 100)
            // Example: Quiz local 75%, design share 20% → contributes 15 toward CLO%.
            foreach (var row in design.Rows)
            {
                var share = row.PercentagesByCloId.TryGetValue(clo.CloId, out var pct) ? pct ?? 0 : 0;
                if (share <= 0)
                {
                    continue;
                }

                // Local % for this CLO within one assessment type (Quiz / Assignment / Mid / Final).
                var local = CalculateLocalAttainment(
                    assessments,
                    allMarks,
                    students,
                    row.AssessmentTypeName,
                    clo.CloId,
                    out var note);

                // Weight local attainment by that type's approved CLO and Task share.
                var weighted = local.HasValue
                    ? Math.Round(local.Value * (share / 100m), 2)
                    : 0;

                if (local.HasValue)
                {
                    overall += weighted;
                }
                else
                {
                    warnings.Add($"{clo.CloCode}/{row.AssessmentTypeName}: {note}");
                }

                contributions.Add(new CloTypeContributionDto
                {
                    AssessmentTypeName = row.AssessmentTypeName,
                    DesignSharePercent = share,
                    LocalAttainmentPercent = local,
                    WeightedContribution = weighted,
                    Note = note
                });
            }

            // Final CLO attainment for this section (stored in CloAttainmentResults).
            overall = Math.Round(overall, 2);
            results.Add(new CloAttainmentResultItemDto
            {
                CloId = clo.CloId,
                CloCode = clo.CloCode,
                CloDescription = clo.CloDescription,
                AttainmentPercentage = overall,
                Contributions = contributions
            });
        }

        var existing = await _context.CloattainmentResults
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();
        _context.CloattainmentResults.RemoveRange(existing);

        var calculatedAt = DateTime.UtcNow;
        foreach (var item in results)
        {
            _context.CloattainmentResults.Add(new CloattainmentResult
            {
                OfferedCourseId = scope.OfferedCourseId,
                Cloid = item.CloId,
                AttainmentPercentage = item.AttainmentPercentage,
                CalculatedAt = calculatedAt,
                CalculatedByUserId = teacherUserId
            });
        }

        await _context.SaveChangesAsync();

        var cloDto = new CloAttainmentResultsDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseTitle = scope.CourseTitle,
            SectionName = scope.SectionName,
            CalculatedAt = calculatedAt,
            Results = results,
            Warnings = warnings.Distinct().ToList()
        };

        var ploDto = await CalculateAndPersistPloAsync(
            scope,
            teacherUserId,
            results.ToDictionary(r => r.CloId, r => r.AttainmentPercentage),
            calculatedAt);

        return cloDto;
    }

    public async Task<SubjectAttainmentBundleDto> CalculateAllAsync(OfferedCourseScope scope, int teacherUserId)
    {
        var clo = await CalculateAsync(scope, teacherUserId);
        var plo = await GetSavedPloResultsAsync(scope);
        return new SubjectAttainmentBundleDto { Clo = clo, Plo = plo };
    }

    /// <summary>
    /// Builds PLO attainment from saved/just-calculated CLO% values and approved CLO and PLO weights.
    /// </summary>
    public async Task<PloAttainmentResultsDto> CalculateAndPersistPloAsync(
        OfferedCourseScope scope,
        int teacherUserId,
        Dictionary<int, decimal>? cloAttainmentById = null,
        DateTime? calculatedAt = null)
    {
        var warnings = new List<string>();
        calculatedAt ??= DateTime.UtcNow;

        if (cloAttainmentById == null)
        {
            cloAttainmentById = await _context.CloattainmentResults
                .AsNoTracking()
                .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
                .ToDictionaryAsync(r => r.Cloid, r => r.AttainmentPercentage);
        }

        var hasCloPlo = await _contentSubmissionService.HasApprovedMappingAsync(
            scope.ProgramCourseId,
            scope.SessionId,
            ContentSubmissionTypeCodes.CloPloMapping);

        if (!hasCloPlo)
        {
            warnings.Add("CLO and PLO mapping is not approved yet. Approve it with HOD before PLO attainment is meaningful.");
        }

        var mappings = await _context.Ploclomappings
            .AsNoTracking()
            .Include(m => m.Clo)
            .Include(m => m.Plo)
            .Where(m =>
                m.ProgramCourseId == scope.ProgramCourseId &&
                m.SessionId == scope.SessionId &&
                m.ContentStatus == ContentStatuses.Approved &&
                m.Percentage != null &&
                m.Percentage > 0)
            .ToListAsync();

        var ploGroups = mappings
            .GroupBy(m => m.Ploid)
            .OrderBy(g => g.First().Plo.Plocode)
            .ToList();

        var results = new List<PloAttainmentResultItemDto>();

        foreach (var group in ploGroups)
        {
            var sample = group.First().Plo;
            var contributions = new List<PloCloContributionDto>();
            decimal overall = 0;

            var columnTotal = group.Sum(m => m.Percentage ?? 0);
            if (columnTotal > 100.01m)
            {
                warnings.Add($"{sample.Plocode}: CLO mapping shares total {columnTotal}% (expected ≤ 100%).");
            }

            // PLO% = Σ (CLO% × CLO and PLO mappingWeight / 100)
            // Example: CLO1 attainment 80%, mapped 40% to PLO2 → contributes 32 toward PLO2%.
            foreach (var map in group.OrderBy(m => m.Clo.Clocode))
            {
                var weight = map.Percentage ?? 0;
                cloAttainmentById.TryGetValue(map.Cloid, out var cloPct);
                var hasClo = cloAttainmentById.ContainsKey(map.Cloid);

                // Weight this CLO's attainment by its approved share of the PLO.
                var weighted = hasClo
                    ? Math.Round(cloPct * (weight / 100m), 2)
                    : 0m;

                if (hasClo)
                {
                    overall += weighted;
                }
                else
                {
                    warnings.Add($"{sample.Plocode}/{map.Clo.Clocode}: no CLO attainment calculated yet.");
                }

                contributions.Add(new PloCloContributionDto
                {
                    CloId = map.Cloid,
                    CloCode = map.Clo.Clocode,
                    MappingPercent = weight,
                    CloAttainmentPercent = hasClo ? cloPct : null,
                    WeightedContribution = weighted,
                    Note = hasClo ? null : "CLO attainment missing"
                });
            }

            // Final PLO attainment for this section (stored in PloAttainmentResults).
            results.Add(new PloAttainmentResultItemDto
            {
                PloId = group.Key,
                PloCode = sample.Plocode,
                PloDescription = sample.Plodescription ?? string.Empty,
                AttainmentPercentage = Math.Round(overall, 2),
                Contributions = contributions
            });
        }

        if (results.Count == 0)
        {
            warnings.Add("No approved CLO and PLO mappings found for this subject/session.");
        }

        var existing = await _context.PloattainmentResults
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();
        _context.PloattainmentResults.RemoveRange(existing);

        foreach (var item in results)
        {
            _context.PloattainmentResults.Add(new PloattainmentResult
            {
                OfferedCourseId = scope.OfferedCourseId,
                Ploid = item.PloId,
                AttainmentPercentage = item.AttainmentPercentage,
                CalculatedAt = calculatedAt.Value,
                CalculatedByUserId = teacherUserId
            });
        }

        await _context.SaveChangesAsync();

        return new PloAttainmentResultsDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseTitle = scope.CourseTitle,
            SectionName = scope.SectionName,
            CalculatedAt = calculatedAt,
            HasCloPloMapping = hasCloPlo && results.Count > 0,
            Results = results,
            Warnings = warnings.Distinct().ToList()
        };
    }

    public async Task<PloAttainmentResultsDto> GetSavedPloResultsAsync(OfferedCourseScope scope)
    {
        var saved = await _context.PloattainmentResults
            .AsNoTracking()
            .Include(r => r.Plo)
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .OrderBy(r => r.Plo.Plocode)
            .ToListAsync();

        var hasCloPlo = await _contentSubmissionService.HasApprovedMappingAsync(
            scope.ProgramCourseId,
            scope.SessionId,
            ContentSubmissionTypeCodes.CloPloMapping);

        if (saved.Count == 0)
        {
            return new PloAttainmentResultsDto
            {
                OfferedCourseId = scope.OfferedCourseId,
                CourseTitle = scope.CourseTitle,
                SectionName = scope.SectionName,
                HasCloPloMapping = hasCloPlo,
                Results = new List<PloAttainmentResultItemDto>(),
                Warnings = new List<string>
                {
                    hasCloPlo
                        ? "No PLO attainment has been calculated yet. Run Calculate on Results."
                        : "Approve CLO and PLO mapping first, then calculate attainment."
                }
            };
        }

        // Rebuild CLO contributions from current approved mapping + saved CLO %.
        var cloPct = await _context.CloattainmentResults
            .AsNoTracking()
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .ToDictionaryAsync(r => r.Cloid, r => r.AttainmentPercentage);

        var mappings = await _context.Ploclomappings
            .AsNoTracking()
            .Include(m => m.Clo)
            .Where(m =>
                m.ProgramCourseId == scope.ProgramCourseId &&
                m.SessionId == scope.SessionId &&
                m.ContentStatus == ContentStatuses.Approved &&
                m.Percentage != null &&
                m.Percentage > 0)
            .ToListAsync();

        return new PloAttainmentResultsDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseTitle = scope.CourseTitle,
            SectionName = scope.SectionName,
            CalculatedAt = saved.Max(r => r.CalculatedAt),
            HasCloPloMapping = hasCloPlo,
            Results = saved.Select(r =>
            {
                var contrib = mappings
                    .Where(m => m.Ploid == r.Ploid)
                    .OrderBy(m => m.Clo.Clocode)
                    .Select(m =>
                    {
                        var has = cloPct.TryGetValue(m.Cloid, out var pct);
                        var weight = m.Percentage ?? 0;
                        return new PloCloContributionDto
                        {
                            CloId = m.Cloid,
                            CloCode = m.Clo.Clocode,
                            MappingPercent = weight,
                            CloAttainmentPercent = has ? pct : null,
                            WeightedContribution = has ? Math.Round(pct * (weight / 100m), 2) : 0
                        };
                    })
                    .ToList();

                return new PloAttainmentResultItemDto
                {
                    PloId = r.Ploid,
                    PloCode = r.Plo.Plocode,
                    PloDescription = r.Plo.Plodescription ?? string.Empty,
                    AttainmentPercentage = r.AttainmentPercentage,
                    Contributions = contrib
                };
            }).ToList()
        };
    }

    /// <summary>
    /// Per-student PLO % using the same CLO-Task and CLO-PLO weights as class attainment.
    /// Does not write CloAttainmentResults / PloAttainmentResults.
    /// Fail = student PLO% below 50 (C cut-off).
    /// </summary>
    public async Task<StudentPloResultsDto> GetStudentPloResultsAsync(OfferedCourseScope scope)
    {
        const decimal failThreshold = 50m;
        var warnings = new List<string>();
        var design = await GetDesignAsync(scope);
        var students = await GetStudentsAsync(scope);

        var mappings = await _context.Ploclomappings
            .AsNoTracking()
            .Include(m => m.Clo)
            .Include(m => m.Plo)
            .Where(m =>
                m.ProgramCourseId == scope.ProgramCourseId &&
                m.SessionId == scope.SessionId &&
                m.ContentStatus == ContentStatuses.Approved &&
                m.Percentage != null &&
                m.Percentage > 0)
            .ToListAsync();

        var ploGroups = mappings
            .GroupBy(m => m.Ploid)
            .OrderBy(g => g.First().Plo.Plocode)
            .ToList();

        if (design.Clos.Count == 0)
        {
            warnings.Add("No HOD-approved CLOs found for this subject.");
        }

        if (ploGroups.Count == 0)
        {
            warnings.Add("No approved CLO-PLO mapping found. Approve CLO-PLO mapping first.");
        }

        if (students.Count == 0)
        {
            warnings.Add("No students are enrolled in this offered course.");
        }

        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .Where(a => a.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();

        var allQuestionIds = assessments.SelectMany(a => a.AssessmentQuestions).Select(q => q.QuestionId).ToList();
        var allMarks = allQuestionIds.Count == 0
            ? new List<StudentQuestionMark>()
            : await _context.StudentQuestionMarks
                .AsNoTracking()
                .Where(m => allQuestionIds.Contains(m.QuestionId))
                .ToListAsync();

        var marksByStudent = allMarks
            .GroupBy(m => m.StudentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.QuestionId, x => x.ObtainedMarks));

        var ploColumns = ploGroups.Select(g => new StudentPloScoreDto
        {
            PloId = g.Key,
            PloCode = g.First().Plo.Plocode,
            PloDescription = g.First().Plo.Plodescription ?? string.Empty,
            Detail = g.OrderBy(m => m.Clo.Clocode)
                .Select(m => new StudentPloDetailItemDto
                {
                    Label = m.Clo.Clocode,
                    CloId = m.Cloid,
                    CloCode = m.Clo.Clocode,
                    CloDescription = m.Clo.Clodescription ?? string.Empty
                })
                .ToList()
        }).ToList();

        var rows = new List<StudentPloResultRowDto>();
        foreach (var student in students)
        {
            marksByStudent.TryGetValue(student.StudentId, out var studentMarks);
            studentMarks ??= new Dictionary<int, decimal>();

            var cloPct = new Dictionary<int, decimal?>();
            foreach (var clo in design.Clos)
            {
                decimal overall = 0;
                var hasAny = false;
                foreach (var row in design.Rows)
                {
                    var share = row.PercentagesByCloId.TryGetValue(clo.CloId, out var pct) ? pct ?? 0 : 0;
                    if (share <= 0)
                    {
                        continue;
                    }

                    var local = CalculateStudentLocalAttainment(
                        assessments,
                        studentMarks,
                        row.AssessmentTypeName,
                        clo.CloId);
                    if (!local.HasValue)
                    {
                        continue;
                    }

                    hasAny = true;
                    overall += Math.Round(local.Value * (share / 100m), 2);
                }

                cloPct[clo.CloId] = hasAny ? Math.Round(overall, 2) : null;
            }

            var ploScores = new List<StudentPloScoreDto>();
            foreach (var group in ploGroups)
            {
                var sample = group.First().Plo;
                decimal ploTotal = 0;
                var hasClo = false;
                var detail = new List<StudentPloDetailItemDto>();

                foreach (var map in group.OrderBy(m => m.Clo.Clocode))
                {
                    var weight = map.Percentage ?? 0;
                    cloPct.TryGetValue(map.Cloid, out var studentClo);
                    if (studentClo.HasValue)
                    {
                        hasClo = true;
                        ploTotal += Math.Round(studentClo.Value * (weight / 100m), 2);
                    }

                    var (obtained, maximum) = SumCloMarks(assessments, studentMarks, map.Cloid);
                    var marksPercent = maximum > 0 ? Math.Round(obtained / maximum * 100m, 2) : (decimal?)null;
                    var cloAttainment = studentClo;
                    var cloFailed = (cloAttainment ?? marksPercent).HasValue
                        && (cloAttainment ?? marksPercent)!.Value < failThreshold;
                    detail.Add(new StudentPloDetailItemDto
                    {
                        Label = map.Clo.Clocode,
                        CloId = map.Cloid,
                        CloCode = map.Clo.Clocode,
                        CloDescription = map.Clo.Clodescription ?? string.Empty,
                        Obtained = obtained,
                        Maximum = maximum,
                        Percent = marksPercent,
                        MappingPercent = weight,
                        AttainmentPercent = cloAttainment,
                        Failed = cloFailed
                    });
                }

                var percent = hasClo ? Math.Round(ploTotal, 2) : (decimal?)null;
                var ploFailed = percent.HasValue && percent.Value < failThreshold;
                var closToPass = ploFailed
                    ? detail
                        .Where(d => d.Failed)
                        .Select(d => d.CloCode)
                        .Where(code => !string.IsNullOrWhiteSpace(code))
                        .Cast<string>()
                        .ToList()
                    : new List<string>();
                if (ploFailed && closToPass.Count == 0)
                {
                    closToPass = detail
                        .Select(d => d.CloCode)
                        .Where(code => !string.IsNullOrWhiteSpace(code))
                        .Cast<string>()
                        .ToList();
                }

                var ploAdvice = ploFailed && closToPass.Count > 0
                    ? $"{student.FullName} should pass {FormatCodeList(closToPass)} to pass {sample.Plocode}."
                    : string.Empty;

                ploScores.Add(new StudentPloScoreDto
                {
                    PloId = group.Key,
                    PloCode = sample.Plocode,
                    PloDescription = sample.Plodescription ?? string.Empty,
                    Percent = percent,
                    Failed = ploFailed,
                    Detail = detail,
                    ClosToPass = closToPass,
                    PassAdvice = ploAdvice
                });
            }

            var failedCodes = ploScores.Where(p => p.Failed).Select(p => p.PloCode).ToList();
            var studentClosToPass = ploScores
                .Where(p => p.Failed)
                .SelectMany(p => p.ClosToPass)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var studentAdvice = failedCodes.Count == 0
                ? string.Empty
                : studentClosToPass.Count > 0
                    ? $"{student.FullName} should pass {FormatCodeList(studentClosToPass)} to pass {FormatCodeList(failedCodes)}."
                    : $"{student.FullName} should improve mapped CLO attainment to pass {FormatCodeList(failedCodes)}.";

            rows.Add(new StudentPloResultRowDto
            {
                StudentId = student.StudentId,
                FullName = student.FullName,
                AridNo = student.AridNo,
                Plos = ploScores,
                FailedPloCodes = failedCodes,
                ClosToPass = studentClosToPass,
                FailedByPlo = failedCodes.Count > 0,
                PassAdvice = studentAdvice
            });
        }

        return new StudentPloResultsDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseTitle = scope.CourseTitle,
            SectionName = scope.SectionName,
            FailThresholdPercent = failThreshold,
            PloColumns = ploColumns,
            Students = rows,
            Warnings = warnings
        };
    }

    public async Task<SubjectAttainmentDashboardDto> GetDashboardAsync(OfferedCourseScope scope)
    {
        var clo = await GetSavedResultsAsync(scope);
        var design = await GetDesignAsync(scope);
        var plo = await GetSavedPloResultsAsync(scope);
        var (attainmentCells, attainmentWarnings) = await BuildMarksAttainmentCellsAsync(scope, design);

        var cloPct = clo.Results.ToDictionary(r => r.CloId, r => r.AttainmentPercentage);
        var hasTask = design.Clos.Count > 0 && design.Rows.Count > 0;

        var cloProgress = new List<CloProgressChartItemDto>();
        foreach (var col in design.Clos)
        {
            cloPct.TryGetValue(col.CloId, out var attainment);
            var breakdown = attainmentCells
                .Where(c => c.CloId == col.CloId)
                .Select(c => new CloTypeContributionDto
                {
                    AssessmentTypeName = c.AssessmentTypeName,
                    DesignSharePercent = c.DesignSharePercent,
                    LocalAttainmentPercent = c.LocalAttainmentPercent,
                    WeightedContribution = c.WeightedContribution,
                    Note = c.Note
                })
                .ToList();

            // Prefer persisted overall CLO% when calculated; otherwise sum live weighted cells.
            if (!cloPct.ContainsKey(col.CloId) && breakdown.Count > 0)
            {
                attainment = Math.Round(breakdown.Sum(b => b.WeightedContribution), 2);
            }

            cloProgress.Add(new CloProgressChartItemDto
            {
                CloId = col.CloId,
                CloCode = col.CloCode,
                CloDescription = col.CloDescription,
                AttainmentPercentage = attainment,
                IsMapped = breakdown.Count > 0,
                MappedBreakdown = breakdown
            });
        }

        foreach (var item in clo.Results.Where(r => cloProgress.All(p => p.CloId != r.CloId)))
        {
            cloProgress.Add(new CloProgressChartItemDto
            {
                CloId = item.CloId,
                CloCode = item.CloCode,
                CloDescription = item.CloDescription,
                AttainmentPercentage = item.AttainmentPercentage,
                IsMapped = item.Contributions.Count > 0,
                MappedBreakdown = item.Contributions
            });
        }

        var ploProgress = plo.Results.Select(r => new PloProgressChartItemDto
        {
            PloId = r.PloId,
            PloCode = r.PloCode,
            PloDescription = r.PloDescription,
            AttainmentPercentage = r.AttainmentPercentage,
            IsMapped = r.Contributions.Count > 0,
            MappedBreakdown = r.Contributions
        }).ToList();

        var warnings = clo.Warnings
            .Concat(plo.Warnings)
            .Concat(attainmentWarnings)
            .Distinct()
            .ToList();

        return new SubjectAttainmentDashboardDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseTitle = scope.CourseTitle,
            SectionName = scope.SectionName,
            CalculatedAt = clo.CalculatedAt ?? plo.CalculatedAt,
            HasCloTaskMapping = hasTask,
            HasCloPloMapping = plo.HasCloPloMapping,
            Design = design,
            AttainmentCells = attainmentCells,
            CloProgress = cloProgress.OrderBy(c => c.CloCode).ToList(),
            PloProgress = ploProgress,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Assessment folder overview: design + question-coverage completion (not student marks).
    /// Local % = sum of question CLO coverage % for that type×CLO (capped at 100).
    /// Weighted = local × designShare / 100.
    /// </summary>
    public async Task<AssessmentFolderOverviewDto> GetAssessmentFolderOverviewAsync(OfferedCourseScope scope)
    {
        var warnings = new List<string>();
        var design = await GetDesignAsync(scope);

        if (design.Clos.Count == 0)
        {
            warnings.Add("No approved CLOs found for this subject/session.");
        }

        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .Where(a => a.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();

        var completionCells = new List<AssessmentFolderCompletionCellDto>();
        var cloSummaries = new List<AssessmentFolderCloSummaryDto>();

        foreach (var clo in design.Clos)
        {
            decimal overallWeighted = 0;
            var localValues = new List<decimal>();

            foreach (var row in design.Rows)
            {
                var share = row.PercentagesByCloId.TryGetValue(clo.CloId, out var pct) ? pct ?? 0 : 0;
                if (share <= 0)
                {
                    continue;
                }

                var local = CalculateLocalQuestionCoverage(
                    assessments,
                    row.AssessmentTypeName,
                    clo.CloId,
                    out var note);

                var weighted = local.HasValue
                    ? Math.Round(local.Value * (share / 100m), 2)
                    : 0m;

                if (local.HasValue)
                {
                    overallWeighted += weighted;
                    localValues.Add(local.Value);
                }

                completionCells.Add(new AssessmentFolderCompletionCellDto
                {
                    AssessmentTypeId = row.AssessmentTypeId,
                    AssessmentTypeName = row.AssessmentTypeName,
                    CloId = clo.CloId,
                    CloCode = clo.CloCode,
                    DesignSharePercent = share,
                    LocalAttainmentPercent = local,
                    WeightedContribution = weighted,
                    Note = note
                });
            }

            cloSummaries.Add(new AssessmentFolderCloSummaryDto
            {
                CloId = clo.CloId,
                CloCode = clo.CloCode,
                CloDescription = clo.CloDescription,
                OverallWeightedPercent = Math.Round(overallWeighted, 2),
                OverallLocalPercent = localValues.Count > 0
                    ? Math.Round(localValues.Average(), 2)
                    : null
            });
        }

        var assessmentCounts = design.Rows
            .Select(row => new AssessmentFolderTypeCountDto
            {
                AssessmentTypeId = row.AssessmentTypeId,
                AssessmentTypeName = row.AssessmentTypeName,
                Count = assessments.Count(a =>
                    a.AssessmentType.AssessmentTypeName == row.AssessmentTypeName)
            })
            .ToList();

        foreach (var group in assessments.GroupBy(a => a.AssessmentType.AssessmentTypeName))
        {
            if (assessmentCounts.Any(c =>
                    string.Equals(c.AssessmentTypeName, group.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var sample = group.First().AssessmentType;
            assessmentCounts.Add(new AssessmentFolderTypeCountDto
            {
                AssessmentTypeId = sample.AssessmentTypeId,
                AssessmentTypeName = sample.AssessmentTypeName,
                Count = group.Count()
            });
        }

        return new AssessmentFolderOverviewDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseTitle = scope.CourseTitle,
            SectionName = scope.SectionName,
            Design = design,
            CompletionCells = completionCells,
            CloSummaries = cloSummaries,
            AssessmentCounts = assessmentCounts,
            Warnings = warnings.Distinct().ToList()
        };
    }

    /// <summary>
    /// Live marks-based Task×CLO cells for Results (same local/weighted logic as Calculate).
    /// </summary>
    private async Task<(List<AssessmentFolderCompletionCellDto> Cells, List<string> Warnings)> BuildMarksAttainmentCellsAsync(
        OfferedCourseScope scope,
        CloAssessmentDesignDto design)
    {
        var warnings = new List<string>();
        var students = await GetStudentsAsync(scope);

        if (students.Count == 0)
        {
            warnings.Add("No students are enrolled in this offered course.");
        }

        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .Where(a => a.OfferedCourseId == scope.OfferedCourseId)
            .ToListAsync();

        var allQuestionIds = assessments.SelectMany(a => a.AssessmentQuestions).Select(q => q.QuestionId).ToList();
        var allMarks = allQuestionIds.Count == 0
            ? new List<StudentQuestionMark>()
            : await _context.StudentQuestionMarks
                .AsNoTracking()
                .Where(m => allQuestionIds.Contains(m.QuestionId))
                .ToListAsync();

        var cells = new List<AssessmentFolderCompletionCellDto>();

        foreach (var clo in design.Clos)
        {
            foreach (var row in design.Rows)
            {
                var share = row.PercentagesByCloId.TryGetValue(clo.CloId, out var pct) ? pct ?? 0 : 0;
                if (share <= 0)
                {
                    continue;
                }

                var local = CalculateLocalAttainment(
                    assessments,
                    allMarks,
                    students,
                    row.AssessmentTypeName,
                    clo.CloId,
                    out var note);

                var weighted = local.HasValue
                    ? Math.Round(local.Value * (share / 100m), 2)
                    : 0m;

                cells.Add(new AssessmentFolderCompletionCellDto
                {
                    AssessmentTypeId = row.AssessmentTypeId,
                    AssessmentTypeName = row.AssessmentTypeName,
                    CloId = clo.CloId,
                    CloCode = clo.CloCode,
                    DesignSharePercent = share,
                    LocalAttainmentPercent = local,
                    WeightedContribution = weighted,
                    Note = note
                });
            }
        }

        return (cells, warnings);
    }

    public async Task<CloAttainmentResultsDto> GetSavedResultsAsync(OfferedCourseScope scope)
    {
        var saved = await _context.CloattainmentResults
            .AsNoTracking()
            .Include(r => r.Clo)
            .Where(r => r.OfferedCourseId == scope.OfferedCourseId)
            .OrderBy(r => r.Clo.Clocode)
            .ToListAsync();

        if (saved.Count == 0)
        {
            return new CloAttainmentResultsDto
            {
                OfferedCourseId = scope.OfferedCourseId,
                CourseTitle = scope.CourseTitle,
                SectionName = scope.SectionName,
                Results = new List<CloAttainmentResultItemDto>(),
                Warnings = new List<string> { "No CLO attainment has been calculated yet." }
            };
        }

        return new CloAttainmentResultsDto
        {
            OfferedCourseId = scope.OfferedCourseId,
            CourseTitle = scope.CourseTitle,
            SectionName = scope.SectionName,
            CalculatedAt = saved.Max(r => r.CalculatedAt),
            Results = saved.Select(r => new CloAttainmentResultItemDto
            {
                CloId = r.Cloid,
                CloCode = r.Clo.Clocode,
                CloDescription = r.Clo.Clodescription,
                AttainmentPercentage = r.AttainmentPercentage,
                Contributions = new List<CloTypeContributionDto>()
            }).ToList()
        };
    }

    /// <summary>
    /// Question coverage of one assessment-type bucket for a CLO (0–100 of that type’s design share).
    /// Sums AssessmentQuestionCLOMappings.Percentage across questions of that type.
    /// </summary>
    private static decimal? CalculateLocalQuestionCoverage(
        List<Assessment> assessments,
        string assessmentTypeName,
        int cloId,
        out string? note)
    {
        note = null;

        var used = assessments
            .Where(a => a.AssessmentType.AssessmentTypeName == assessmentTypeName)
            .SelectMany(a => a.AssessmentQuestions)
            .SelectMany(q => q.AssessmentQuestionClomappings)
            .Where(m => m.Cloid == cloId && m.Percentage != null && m.Percentage > 0)
            .Sum(m => m.Percentage ?? 0);

        if (used <= 0)
        {
            note = "No question coverage mapped to this CLO for this assessment type.";
            return null;
        }

        if (used > 100m)
        {
            used = 100m;
        }

        return Math.Round(used, 2);
    }

    /// <summary>
    /// Local attainment for one CLO within one assessment type (e.g. CLO1 on Quizzes).
    /// student% = (Σ scoreRatio × questionWeight) / (Σ questionWeight) × 100
    /// local%   = average of student% across students who have scores.
    /// Question weight prefers AssessmentQuestionCLOMappings.Percentage (coverage of the type bucket).
    /// </summary>
    private static decimal? CalculateLocalAttainment(
        List<Assessment> assessments,
        List<StudentQuestionMark> allMarks,
        List<CloAttainmentStudentDto> students,
        string assessmentTypeName,
        int cloId,
        out string? note)
    {
        note = null;

        // Only questions of this type that are mapped to this CLO.
        var relevantQuestions = assessments
            .Where(a => a.AssessmentType.AssessmentTypeName == assessmentTypeName)
            .SelectMany(a => a.AssessmentQuestions)
            .Where(q => q.AssessmentQuestionClomappings.Any(m => m.Cloid == cloId))
            .ToList();

        if (relevantQuestions.Count == 0)
        {
            note = "No questions mapped to this CLO for this assessment type.";
            return null;
        }

        if (students.Count == 0)
        {
            note = "No students available.";
            return null;
        }

        // Weight each question: coverage % of type bucket → else max marks → else equal (1).
        var questionWeights = new Dictionary<int, decimal>();
        foreach (var question in relevantQuestions)
        {
            var mappingPct = question.AssessmentQuestionClomappings
                .Where(m => m.Cloid == cloId)
                .Select(m => m.Percentage)
                .FirstOrDefault();

            if (mappingPct is > 0)
            {
                questionWeights[question.QuestionId] = mappingPct.Value;
            }
            else if (question.Marks is > 0)
            {
                questionWeights[question.QuestionId] = question.Marks.Value;
            }
            else
            {
                questionWeights[question.QuestionId] = 1m;
            }
        }

        var marksLookup = allMarks
            .Where(m => questionWeights.ContainsKey(m.QuestionId))
            .GroupBy(m => m.StudentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.QuestionId, x => x.ObtainedMarks));

        var studentPercents = new List<decimal>();

        foreach (var student in students)
        {
            if (!marksLookup.TryGetValue(student.StudentId, out var studentMarks))
            {
                // Skip students with no marks on these questions.
                continue;
            }

            decimal obtained = 0;
            decimal max = 0;
            var answered = false;

            foreach (var question in relevantQuestions)
            {
                var weight = questionWeights[question.QuestionId];
                max += weight;

                if (studentMarks.TryGetValue(question.QuestionId, out var got))
                {
                    answered = true;
                    if (question.Marks is > 0)
                    {
                        // scoreRatio = obtainedMarks / maxMarks; scale into coverage weight.
                        var ratio = Math.Min(got, question.Marks.Value) / question.Marks.Value;
                        obtained += ratio * weight;
                    }
                    else
                    {
                        // No max marks: score is 0–100% of the question (or 0–1 if binary).
                        var ratio = got <= 1m
                            ? Math.Max(0m, got)
                            : Math.Min(got, 100m) / 100m;
                        obtained += ratio * weight;
                    }
                }
            }

            if (!answered || max <= 0)
            {
                continue;
            }

            // One student's attainment % on this CLO for this assessment type.
            studentPercents.Add(Math.Round((obtained / max) * 100m, 2));
        }

        if (studentPercents.Count == 0)
        {
            note = "No student marks entered for CLO-mapped questions.";
            return null;
        }

        note = null;
        // Class local attainment = mean of student percentages.
        return Math.Round(studentPercents.Average(), 2);
    }

    private static decimal? CalculateStudentLocalAttainment(
        List<Assessment> assessments,
        Dictionary<int, decimal> studentMarks,
        string assessmentTypeName,
        int cloId)
    {
        var relevantQuestions = assessments
            .Where(a => a.AssessmentType.AssessmentTypeName == assessmentTypeName)
            .SelectMany(a => a.AssessmentQuestions)
            .Where(q => q.AssessmentQuestionClomappings.Any(m => m.Cloid == cloId))
            .ToList();

        if (relevantQuestions.Count == 0 || studentMarks.Count == 0)
        {
            return null;
        }

        decimal obtained = 0;
        decimal max = 0;
        var answered = false;

        foreach (var question in relevantQuestions)
        {
            var mappingPct = question.AssessmentQuestionClomappings
                .Where(m => m.Cloid == cloId)
                .Select(m => m.Percentage)
                .FirstOrDefault();
            var weight = mappingPct is > 0
                ? mappingPct.Value
                : question.Marks is > 0 ? question.Marks.Value : 1m;
            max += weight;

            if (!studentMarks.TryGetValue(question.QuestionId, out var got))
            {
                continue;
            }

            answered = true;
            if (question.Marks is > 0)
            {
                var ratio = Math.Min(got, question.Marks.Value) / question.Marks.Value;
                obtained += ratio * weight;
            }
            else
            {
                var ratio = got <= 1m ? Math.Max(0m, got) : Math.Min(got, 100m) / 100m;
                obtained += ratio * weight;
            }
        }

        if (!answered || max <= 0)
        {
            return null;
        }

        return Math.Round((obtained / max) * 100m, 2);
    }

    private static string FormatCodeList(IReadOnlyList<string> codes)
    {
        if (codes == null || codes.Count == 0)
        {
            return string.Empty;
        }

        if (codes.Count == 1)
        {
            return codes[0];
        }

        if (codes.Count == 2)
        {
            return $"{codes[0]} and {codes[1]}";
        }

        return $"{string.Join(", ", codes.Take(codes.Count - 1))}, and {codes[^1]}";
    }

    private static (decimal Obtained, decimal Maximum) SumCloMarks(
        List<Assessment> assessments,
        Dictionary<int, decimal> studentMarks,
        int cloId)
    {
        decimal obtained = 0;
        decimal maximum = 0;
        foreach (var question in assessments.SelectMany(a => a.AssessmentQuestions)
                     .Where(q => q.AssessmentQuestionClomappings.Any(m => m.Cloid == cloId)))
        {
            var max = question.Marks is > 0 ? question.Marks.Value : 0m;
            if (max <= 0)
            {
                continue;
            }

            maximum += max;
            if (studentMarks.TryGetValue(question.QuestionId, out var got))
            {
                obtained += Math.Clamp(got, 0m, max);
            }
        }

        return (Math.Round(obtained, 2), Math.Round(maximum, 2));
    }

    public sealed class OfferedCourseScope
    {
        public int OfferedCourseId { get; set; }
        public int TeacherUserId { get; set; }
        public int CourseId { get; set; }
        public int SectionId { get; set; }
        public int SessionId { get; set; }
        public int ProgramId { get; set; }
        public int ProgramCourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string SessionTitle { get; set; } = string.Empty;
        public string ProgramCode { get; set; } = string.Empty;
        public int ClassCreditHours { get; set; }
        public int LabCreditHours { get; set; }
    }
}
