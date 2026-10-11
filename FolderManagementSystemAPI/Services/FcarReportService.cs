using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class FcarReportService
{
    private readonly FolderManagementDbContext _context;
    private readonly CourseResultService _courseResultService;

    public FcarReportService(
        FolderManagementDbContext context,
        CourseResultService courseResultService)
    {
        _context = context;
        _courseResultService = courseResultService;
    }

    public async Task<FcarReportDto> GetFcarReportAsync(int offeredCourseId, int requestingUserId)
    {
        var oc = await (
            from o in _context.OfferedCourses.AsNoTracking()
            join c in _context.Courses.AsNoTracking() on o.CourseId equals c.CourseId
            join sec in _context.Sections.AsNoTracking() on o.SectionId equals sec.SectionId
            join sess in _context.AcademicSessions.AsNoTracking() on sec.SessionId equals sess.SessionId
            join prog in _context.AcademicPrograms.AsNoTracking() on sess.ProgramId equals prog.ProgramId into progs
            from prog in progs.DefaultIfEmpty()
            join t in _context.Users.AsNoTracking() on o.TeacherUserId equals t.UserId into teachers
            from t in teachers.DefaultIfEmpty()
            where o.OfferedCourseId == offeredCourseId
            select new
            {
                o.OfferedCourseId,
                o.CourseId,
                o.TeacherUserId,
                sec.SectionId,
                sess.SessionId,
                sess.ProgramId,
                CourseTitle = c.CourseTitle,
                CourseCode = c.CourseCode ?? string.Empty,
                SectionName = sec.SectionName,
                SessionTitle = sess.SessionTitle ?? string.Empty,
                ProgramCode = prog != null ? (prog.ProgramCode ?? prog.ProgramName ?? string.Empty) : string.Empty,
                ProgramName = prog != null ? prog.ProgramName : string.Empty,
                TeacherName = t != null ? t.FullName : string.Empty,
                ClassCreditHours = c.ClassCreditHours ?? 3,
                LabCreditHours = c.LabCreditHours ?? 0,
            }).FirstOrDefaultAsync();

        if (oc == null)
        {
            throw new KeyNotFoundException("Offered course not found.");
        }

        var scopeResult = await _courseResultService.TryResolveScopeAsync(offeredCourseId, requestingUserId);
        var scope = scopeResult.Scope;

        if (scope == null)
        {
            throw new UnauthorizedAccessException(scopeResult.Message);
        }

        var totalCredits = oc.ClassCreditHours + oc.LabCreditHours;
        if (totalCredits <= 0) totalCredits = 3;
        var warnings = new List<string>();

        // 1. Program Outcomes (PLOs)
        var plos = await _context.Plos
            .AsNoTracking()
            .Where(p => p.ProgramId == oc.ProgramId)
            .OrderBy(p => p.Plocode)
            .Select(p => new FcarPloDto
            {
                PloId = p.Ploid,
                PloCode = p.Plocode ?? string.Empty,
                PloDescription = p.Plodescription ?? string.Empty
            }).ToListAsync();

        if (plos.Count == 0) warnings.Add("No program outcomes are configured for this program.");

        // 2. CLOs
        var programCourseId = scope.ProgramCourseId;
        var clos = await _context.Clos
            .AsNoTracking()
            .Where(c => c.ProgramCourseId == programCourseId && c.SessionId == oc.SessionId)
            .OrderBy(c => c.Clocode)
            .ToListAsync();

        if (clos.Count == 0) warnings.Add("No CLOs are configured for this course and academic session.");

        // 3. CLO to PLO mappings
        var cloPloMappings = new List<FcarCloPloMappingRowDto>();
        var mappedPloCodesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var dbPloMappings = await (
            from m in _context.Ploclomappings.AsNoTracking()
            join p in _context.Plos.AsNoTracking() on m.Ploid equals p.Ploid
            where m.ProgramCourseId == programCourseId
                && m.SessionId == oc.SessionId
                && m.ContentStatus == ContentStatuses.Approved
            select new { m.Cloid, p.Plocode }).ToListAsync();

        foreach (var clo in clos)
        {
            var mappedCodes = dbPloMappings
                .Where(x => x.Cloid == clo.Cloid && !string.IsNullOrWhiteSpace(x.Plocode))
                .Select(x => x.Plocode.Trim().ToLower())
                .Distinct()
                .ToList();

            foreach (var code in mappedCodes) mappedPloCodesSet.Add(code);

            cloPloMappings.Add(new FcarCloPloMappingRowDto
            {
                CloId = clo.Cloid,
                CloCode = clo.Clocode,
                CloDescription = clo.Clodescription,
                MappedPloCodes = mappedCodes
            });
        }

        var mappedPloCodes = mappedPloCodesSet.OrderBy(x => x).ToList();
        if (mappedPloCodes.Count == 0)
            warnings.Add("No approved CLO to PLO mappings were found for this academic session.");

        // 4. Grade Distribution from REAL student results
        var resultsGrid = await _courseResultService.GetResultsAsync(scope);
        var students = resultsGrid.Students ?? new List<CourseResultStudentRowDto>();

        int countA = 0, countB = 0, countC = 0, countD = 0, countF = 0;
        foreach (var s in students)
        {
            string g = (s.Grade ?? string.Empty).Trim().ToUpper();
            if (g.Length > 0 && g[0] == 'A') countA++;
            else if (g.Length > 0 && g[0] == 'B') countB++;
            else if (g.Length > 0 && g[0] == 'C') countC++;
            else if (g.Length > 0 && g[0] == 'D') countD++;
            else countF++;
        }

        var gradeDistribution = new FcarGradeDistributionDto
        {
            GradeACount = countA,
            GradeBCount = countB,
            GradeCCount = countC,
            GradeDCount = countD,
            TotalPassing = countA + countB + countC + countD,
            GradeFCount = countF
        };

        // 5. CLO Assessment Breakdown (EEMU Vector & Averages)
        var assessments = await _context.Assessments
            .AsNoTracking()
            .Include(a => a.AssessmentType)
            .Include(a => a.AssessmentQuestions)
                .ThenInclude(q => q.AssessmentQuestionClomappings)
            .Where(a => a.OfferedCourseId == offeredCourseId && a.IsSetupComplete)
            .OrderBy(a => a.AssessmentType.AssessmentTypeId)
            .ThenBy(a => a.AssessmentNo)
            .ToListAsync();

        var questionIds = assessments
            .SelectMany(a => a.AssessmentQuestions)
            .Select(q => q.QuestionId)
            .ToList();
        var marks = questionIds.Count == 0
            ? new List<StudentQuestionMark>()
            : await _context.StudentQuestionMarks
                .AsNoTracking()
                .Where(m => questionIds.Contains(m.QuestionId))
                .ToListAsync();
        var marksByStudent = marks
            .GroupBy(m => m.StudentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(m => m.QuestionId, m => m.ObtainedMarks));
        var eligibleStudents = students
            .Where(s =>
            {
                var grade = (s.Grade ?? string.Empty).Trim().ToUpperInvariant();
                return grade.Length > 0 && grade[0] is 'A' or 'B' or 'C' or 'D';
            })
            .ToList();

        var cloAssessments = new List<FcarCloAssessmentDto>();
        var cloAverageLookup = new Dictionary<int, decimal>();

        foreach (var clo in clos)
        {
            var itemDtos = new List<FcarCloItemDto>();
            var relevantAssessments = assessments
                .Where(a => a.AssessmentQuestions.Any(q =>
                    q.AssessmentQuestionClomappings.Any(m => m.Cloid == clo.Cloid)))
                .ToList();

            foreach (var paper in relevantAssessments)
            {
                var paperTitle = $"{paper.AssessmentType.AssessmentTypeName} {paper.AssessmentNo ?? 1}";
                var relevantQuestions = paper.AssessmentQuestions
                    .Where(q => q.AssessmentQuestionClomappings.Any(m => m.Cloid == clo.Cloid))
                    .ToList();

                int itemA = 0, itemB = 0, itemC = 0, itemD = 0;
                foreach (var student in eligibleStudents)
                {
                    if (!marksByStudent.TryGetValue(student.StudentId, out var studentMarks))
                        continue;

                    decimal obtained = 0;
                    decimal maximum = 0;
                    var hasMark = false;
                    foreach (var question in relevantQuestions)
                    {
                        var mappingWeight = question.AssessmentQuestionClomappings
                            .Where(m => m.Cloid == clo.Cloid)
                            .Select(m => m.Percentage)
                            .FirstOrDefault();
                        var weight = mappingWeight is > 0
                            ? mappingWeight.Value
                            : question.Marks is > 0 ? question.Marks.Value : 1m;
                        maximum += weight;

                        if (!studentMarks.TryGetValue(question.QuestionId, out var mark))
                            continue;

                        hasMark = true;
                        var ratio = question.Marks is > 0
                            ? Math.Clamp(mark / question.Marks.Value, 0m, 1m)
                            : Math.Clamp(mark > 1m ? mark / 100m : mark, 0m, 1m);
                        obtained += ratio * weight;
                    }

                    if (!hasMark || maximum <= 0) continue;
                    var percentage = obtained / maximum * 100m;
                    var (letter, _) = CourseResultService.CalculateGradeAndQPoint(percentage, 1);
                    if (letter.StartsWith('A')) itemA++;
                    else if (letter.StartsWith('B')) itemB++;
                    else if (letter.StartsWith('C')) itemC++;
                    else if (letter.StartsWith('D')) itemD++;
                }

                var totalGraded = itemA + itemB + itemC + itemD;
                decimal itemAvg = totalGraded > 0
                    ? Math.Round((4m * itemA + 3m * itemB + 2m * itemC + 1m * itemD) / totalGraded, 2)
                    : 0m;

                itemDtos.Add(new FcarCloItemDto
                {
                    ItemName = paperTitle,
                    GradeACount = itemA,
                    GradeBCount = itemB,
                    GradeCCount = itemC,
                    GradeDCount = itemD,
                    EemuVector = $"({itemA},{itemB},{itemC},{itemD})",
                    AverageScore = itemAvg
                });
            }

            var measuredItems = itemDtos
                .Where(x => x.GradeACount + x.GradeBCount + x.GradeCCount + x.GradeDCount > 0)
                .ToList();
            decimal overallCloAvg = measuredItems.Count > 0
                ? Math.Round(measuredItems.Average(x => x.AverageScore), 3)
                : 0m;
            if (relevantAssessments.Count == 0)
                warnings.Add($"{clo.Clocode}: no completed assessment questions are mapped to this CLO.");
            else if (measuredItems.Count == 0)
                warnings.Add($"{clo.Clocode}: no eligible student marks are available.");

            cloAverageLookup[clo.Cloid] = overallCloAvg;

            cloAssessments.Add(new FcarCloAssessmentDto
            {
                CloId = clo.Cloid,
                CloCode = clo.Clocode,
                CloDescription = clo.Clodescription,
                Items = itemDtos,
                OverallCloAverage = overallCloAvg
            });
        }

        // 6. PLO EPAN Summaries
        var ploEpans = new List<FcarPloEpanItemDto>();

        foreach (var ploCode in mappedPloCodes)
        {
            var mappedCloIds = cloPloMappings
                .Where(m => m.MappedPloCodes.Contains(ploCode, StringComparer.OrdinalIgnoreCase))
                .Select(m => m.CloId)
                .ToList();

            var scores = mappedCloIds
                .Where(id => cloAverageLookup.ContainsKey(id))
                .Select(id => cloAverageLookup[id])
                .ToList();

            decimal epanAvg = scores.Count > 0 ? Math.Round(scores.Average(), 3) : 0m;

            ploEpans.Add(new FcarPloEpanItemDto
            {
                PloCode = ploCode,
                EpanAverage = epanAvg,
                MetricGoal = 2.0m,
                Status = epanAvg >= 2.0m ? "PROFICIENT" : "DEVELOPING"
            });
        }

        var ploListText = string.Join(", ", mappedPloCodes);
        var totalEnrolled = students.Count;
        var passRate = totalEnrolled > 0 ? Math.Round(((decimal)gradeDistribution.TotalPassing / totalEnrolled) * 100m, 1) : 0m;
        var allProficient = ploEpans.Count > 0 && ploEpans.All(p => p.Status == "PROFICIENT");

        var outcomeAssessmentText = ploEpans.Count == 0
            ? "No EPAN result is available because no approved CLO to PLO mapping is configured."
            : $"Assessing student performance using assessment-level CLO marks, the EPAN averages for program outcome(s) [{ploListText}] are " +
              $"{(allProficient ? "at or above" : "not all at or above")} the metric goal of 2.0. " +
              $"{gradeDistribution.TotalPassing} out of {totalEnrolled} students ({passRate}%) passed the course.";
        var savedInput = await _context.FcarReportInputs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        return new FcarReportDto
        {
            OfferedCourseId = offeredCourseId,
            CourseCode = oc.CourseCode,
            CourseTitle = oc.CourseTitle,
            SectionName = oc.SectionName,
            TeacherName = oc.TeacherName,
            SessionTitle = oc.SessionTitle,
            ProgramName = oc.ProgramName,
            TotalCreditHours = totalCredits,
            CatalogDescription = savedInput?.CatalogDescription ?? string.Empty,
            ProgramOutcomes = plos,
            CloPloMappings = cloPloMappings,
            MappedPloCodes = mappedPloCodes,
            DefaultMetricGoal = 2.0m,
            GradeDistribution = gradeDistribution,
            CourseModifications = savedInput?.CourseModifications ?? "No modification",
            CloAssessments = cloAssessments,
            PloEpans = ploEpans,
            ProgramOutcomeAssessmentText = outcomeAssessmentText,
            StudentFeedbackText = savedInput?.StudentFeedbackText ?? string.Empty,
            ReflectionText = savedInput?.ReflectionText ?? string.Empty,
            ProposedActionsText = savedInput?.ProposedActionsText ?? "None",
            CalculationWarnings = warnings
        };
    }

    public async Task<FcarReportDto> SaveInputAsync(
        int offeredCourseId,
        SaveFcarReportInputDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CatalogDescription))
            throw new ArgumentException("Current catalog description is required.");
        if (string.IsNullOrWhiteSpace(dto.CourseModifications))
            throw new ArgumentException("Course modifications are required.");

        var scopeResult = await _courseResultService.TryResolveScopeAsync(
            offeredCourseId,
            dto.TeacherUserId);
        if (scopeResult.Scope == null)
            throw new UnauthorizedAccessException(scopeResult.Message);

        var input = await _context.FcarReportInputs
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);
        if (input == null)
        {
            input = new FcarReportInput { OfferedCourseId = offeredCourseId };
            _context.FcarReportInputs.Add(input);
        }

        input.CatalogDescription = dto.CatalogDescription.Trim();
        input.CourseModifications = dto.CourseModifications.Trim();
        input.StudentFeedbackText = NormalizeOptional(dto.StudentFeedbackText);
        input.ReflectionText = NormalizeOptional(dto.ReflectionText);
        input.ProposedActionsText = NormalizeOptional(dto.ProposedActionsText) ?? "None";
        input.UpdatedAt = DateTime.UtcNow;
        input.UpdatedByUserId = dto.TeacherUserId;
        await _context.SaveChangesAsync();

        return await GetFcarReportAsync(offeredCourseId, dto.TeacherUserId);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
