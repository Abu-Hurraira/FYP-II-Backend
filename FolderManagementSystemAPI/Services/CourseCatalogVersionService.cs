using System.Security.Cryptography;
using System.Text;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Services;

public class CourseCatalogVersionService
{
    private readonly FolderManagementDbContext _context;

    public CourseCatalogVersionService(FolderManagementDbContext context)
    {
        _context = context;
    }

    public async Task<List<CourseCatalogVersionListItemDto>> ListVersionsAsync()
    {
        return await _context.CourseCatalogVersions
            .AsNoTracking()
            .Include(v => v.SourceProgram)
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.CatalogVersionId)
            .Select(v => new CourseCatalogVersionListItemDto
            {
                CatalogVersionId = v.CatalogVersionId,
                VersionLabel = v.VersionLabel,
                SourceFileName = v.SourceFileName,
                CourseCount = v.CourseCount,
                SourceProgramId = v.SourceProgramId,
                SourceProgramName = v.SourceProgram != null ? v.SourceProgram.ProgramName : string.Empty,
                SourceProgramCode = v.SourceProgram != null ? v.SourceProgram.ProgramCode : null,
                CreatedAt = v.CreatedAt,
                IsReuse = false
            })
            .ToListAsync();
    }

    public async Task<CourseCatalogVersionDetailDto?> GetVersionDetailAsync(
        int catalogVersionId,
        int? targetProgramId)
    {
        var version = await _context.CourseCatalogVersions
            .AsNoTracking()
            .Include(v => v.SourceProgram)
            .Include(v => v.Items)
            .FirstOrDefaultAsync(v => v.CatalogVersionId == catalogVersionId);

        if (version == null)
        {
            return null;
        }

        HashSet<string> linkedKeys = new(StringComparer.Ordinal);
        if (targetProgramId.HasValue)
        {
            var linkedCodes = await (
                from pc in _context.AcademicProgramCourses.AsNoTracking()
                join c in _context.Courses.AsNoTracking() on pc.CourseId equals c.CourseId
                where pc.ProgramId == targetProgramId.Value
                select c.CourseCode
            ).ToListAsync();

            linkedKeys = linkedCodes
                .Select(ExcelImportHelper.CourseMatchKey)
                .Where(k => k.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
        }

        return new CourseCatalogVersionDetailDto
        {
            CatalogVersionId = version.CatalogVersionId,
            VersionLabel = version.VersionLabel,
            SourceFileName = version.SourceFileName,
            CourseCount = version.CourseCount,
            SourceProgramId = version.SourceProgramId,
            SourceProgramName = version.SourceProgram?.ProgramName ?? string.Empty,
            SourceProgramCode = version.SourceProgram?.ProgramCode,
            CreatedAt = version.CreatedAt,
            Items = version.Items
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.CourseCode)
                .Select(i => new CourseCatalogVersionItemDto
                {
                    CatalogVersionItemId = i.CatalogVersionItemId,
                    CourseCode = i.CourseCode,
                    CourseTitle = i.CourseTitle,
                    ClassCreditHours = i.ClassCreditHours,
                    LabCreditHours = i.LabCreditHours,
                    DisplayOrder = i.DisplayOrder,
                    AlreadyInProgram = linkedKeys.Contains(ExcelImportHelper.CourseMatchKey(i.CourseCode))
                })
                .ToList()
        };
    }

    /// <summary>
    /// Creates a version snapshot, or returns the existing one when fingerprint matches
    /// the latest version for the same source program.
    /// </summary>
    public async Task<(CourseCatalogVersion Version, bool CreatedNew)> EnsureVersionFromSnapshotAsync(
        int sourceProgramId,
        string? sourceFileName,
        IReadOnlyList<CatalogVersionSnapshotItem> items,
        int? createdByUserId = null)
    {
        var normalized = items
            .Where(i => !string.IsNullOrWhiteSpace(i.CourseCode) && !string.IsNullOrWhiteSpace(i.CourseTitle))
            .Select(i => new CatalogVersionSnapshotItem
            {
                CourseCode = i.CourseCode.Trim(),
                CourseTitle = i.CourseTitle.Trim(),
                ClassCreditHours = i.ClassCreditHours,
                LabCreditHours = i.LabCreditHours
            })
            .GroupBy(i => ExcelImportHelper.CourseMatchKey(i.CourseCode))
            .Select(g => g.First())
            .OrderBy(i => ExcelImportHelper.CourseMatchKey(i.CourseCode), StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            throw new InvalidOperationException("Cannot create a catalog version with no courses.");
        }

        var fingerprint = BuildFingerprint(normalized);
        var latest = await _context.CourseCatalogVersions
            .Where(v => v.SourceProgramId == sourceProgramId)
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.CatalogVersionId)
            .FirstOrDefaultAsync();

        if (latest != null &&
            string.Equals(latest.ContentFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return (latest, false);
        }

        var program = await _context.AcademicPrograms
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProgramId == sourceProgramId);

        var programTag = !string.IsNullOrWhiteSpace(program?.ProgramCode)
            ? program!.ProgramCode
            : (program?.ProgramName ?? $"Program {sourceProgramId}");

        var createdAt = DateTime.UtcNow;
        var label = $"{programTag} · {normalized.Count} courses · {createdAt:dd MMM yyyy}";

        var version = new CourseCatalogVersion
        {
            SourceProgramId = sourceProgramId,
            VersionLabel = label,
            SourceFileName = string.IsNullOrWhiteSpace(sourceFileName)
                ? null
                : Path.GetFileName(sourceFileName.Trim()),
            CourseCount = normalized.Count,
            ContentFingerprint = fingerprint,
            CreatedAt = createdAt,
            CreatedByUserId = createdByUserId
        };

        for (var i = 0; i < normalized.Count; i++)
        {
            var item = normalized[i];
            version.Items.Add(new CourseCatalogVersionItem
            {
                CourseCode = item.CourseCode,
                CourseTitle = item.CourseTitle,
                ClassCreditHours = item.ClassCreditHours,
                LabCreditHours = item.LabCreditHours,
                DisplayOrder = i + 1
            });
        }

        _context.CourseCatalogVersions.Add(version);
        await _context.SaveChangesAsync();
        return (version, true);
    }

    public async Task<List<CourseLoadSourceProgramDto>> ListSourceProgramsAsync()
    {
        var rows = await (
            from sess in _context.AcademicSessions.AsNoTracking()
            join section in _context.Sections.AsNoTracking() on sess.SessionId equals section.SessionId
            join oc in _context.OfferedCourses.AsNoTracking() on section.SectionId equals oc.SectionId
            join program in _context.AcademicPrograms.AsNoTracking() on sess.ProgramId equals program.ProgramId
            group new { sess.SessionId, oc.CourseId } by new
            {
                program.ProgramId,
                program.ProgramName,
                program.ProgramCode
            }
            into g
            orderby g.Key.ProgramName
            select new CourseLoadSourceProgramDto
            {
                ProgramId = g.Key.ProgramId,
                ProgramName = g.Key.ProgramName,
                ProgramCode = g.Key.ProgramCode,
                SchemeCount = g.Select(x => x.SessionId).Distinct().Count(),
                CourseCount = g.Select(x => x.CourseId).Distinct().Count()
            }
        ).ToListAsync();

        return rows;
    }

    public async Task<List<CourseLoadSchemeDto>> ListSchemesForProgramAsync(int sourceProgramId)
    {
        return await (
            from sess in _context.AcademicSessions.AsNoTracking()
            join section in _context.Sections.AsNoTracking() on sess.SessionId equals section.SessionId
            join oc in _context.OfferedCourses.AsNoTracking() on section.SectionId equals oc.SectionId
            where sess.ProgramId == sourceProgramId
            group oc.CourseId by new
            {
                sess.SessionId,
                sess.SessionTitle,
                sess.AcademicYear,
                sess.StartDate
            }
            into g
            orderby g.Key.StartDate descending, g.Key.SessionTitle descending
            select new CourseLoadSchemeDto
            {
                SessionId = g.Key.SessionId,
                SchemeLabel = g.Key.SessionTitle,
                AcademicYear = g.Key.AcademicYear,
                StartDate = g.Key.StartDate,
                CourseCount = g.Distinct().Count()
            }
        ).ToListAsync();
    }

    public async Task<CourseLoadSchemeDetailDto?> GetSchemeCoursesAsync(
        int sessionId,
        int? targetProgramId)
    {
        var session = await _context.AcademicSessions
            .AsNoTracking()
            .Include(s => s.Program)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null)
        {
            return null;
        }

        HashSet<string> linkedKeys = new(StringComparer.Ordinal);
        if (targetProgramId.HasValue)
        {
            var linkedCodes = await (
                from pc in _context.AcademicProgramCourses.AsNoTracking()
                join c in _context.Courses.AsNoTracking() on pc.CourseId equals c.CourseId
                where pc.ProgramId == targetProgramId.Value
                select c.CourseCode
            ).ToListAsync();

            linkedKeys = linkedCodes
                .Select(ExcelImportHelper.CourseMatchKey)
                .Where(k => k.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
        }

        var courses = await (
            from section in _context.Sections.AsNoTracking()
            join oc in _context.OfferedCourses.AsNoTracking() on section.SectionId equals oc.SectionId
            join c in _context.Courses.AsNoTracking() on oc.CourseId equals c.CourseId
            where section.SessionId == sessionId
            select new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseTitle,
                c.ClassCreditHours,
                c.LabCreditHours
            }
        )
            .Distinct()
            .OrderBy(c => c.CourseCode)
            .ToListAsync();

        return new CourseLoadSchemeDetailDto
        {
            SessionId = session.SessionId,
            SchemeLabel = session.SessionTitle,
            SourceProgramId = session.ProgramId,
            SourceProgramName = session.Program?.ProgramName ?? string.Empty,
            SourceProgramCode = session.Program?.ProgramCode,
            Items = courses.Select(c => new CourseLoadSchemeCourseDto
            {
                CourseId = c.CourseId,
                CourseCode = c.CourseCode,
                CourseTitle = c.CourseTitle,
                ClassCreditHours = c.ClassCreditHours,
                LabCreditHours = c.LabCreditHours,
                AlreadyInProgram = linkedKeys.Contains(ExcelImportHelper.CourseMatchKey(c.CourseCode))
            }).ToList()
        };
    }

    public async Task<(bool Ok, string Message, ApplyCourseCatalogVersionResultDto? Result)> ApplyCoursesFromSchemeAsync(
        ApplyCoursesFromSchemeDto dto)
    {
        if (dto.TargetProgramId <= 0)
        {
            return (false, "Target program is required.", null);
        }

        if (dto.CourseIds == null || dto.CourseIds.Count == 0)
        {
            return (false, "Select at least one course to add.", null);
        }

        var programExists = await _context.AcademicPrograms
            .AnyAsync(p => p.ProgramId == dto.TargetProgramId);
        if (!programExists)
        {
            return (false, "Target program was not found.", null);
        }

        var courseIds = dto.CourseIds.Distinct().ToList();
        var courses = await _context.Courses
            .Where(c => courseIds.Contains(c.CourseId))
            .ToListAsync();

        if (courses.Count == 0)
        {
            return (false, "No matching courses were found.", null);
        }

        var alreadyLinkedIds = await _context.AcademicProgramCourses
            .Where(pc => pc.ProgramId == dto.TargetProgramId)
            .Select(pc => pc.CourseId)
            .ToListAsync();
        var alreadyLinkedSet = alreadyLinkedIds.ToHashSet();

        var linkedCodeKeys = (
            from pc in alreadyLinkedIds
            join c in _context.Courses.AsNoTracking() on pc equals c.CourseId
            select ExcelImportHelper.CourseMatchKey(c.CourseCode)
        ).Where(k => k.Length > 0).ToHashSet(StringComparer.Ordinal);

        var added = 0;
        var alreadyLinked = 0;
        var skipped = 0;

        foreach (var course in courses.OrderBy(c => c.CourseCode))
        {
            var key = ExcelImportHelper.CourseMatchKey(course.CourseCode);
            if (key.Length == 0)
            {
                skipped++;
                continue;
            }

            if (alreadyLinkedSet.Contains(course.CourseId) || linkedCodeKeys.Contains(key))
            {
                alreadyLinked++;
                continue;
            }

            _context.AcademicProgramCourses.Add(new AcademicProgramCourse
            {
                ProgramId = dto.TargetProgramId,
                CourseId = course.CourseId
            });
            alreadyLinkedSet.Add(course.CourseId);
            linkedCodeKeys.Add(key);
            added++;
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync();
        }

        var result = new ApplyCourseCatalogVersionResultDto
        {
            AddedCount = added,
            AlreadyLinkedCount = alreadyLinked,
            SkippedCount = skipped,
            InsertedCourseCount = 0,
            Message = $"Added {added} · Already linked {alreadyLinked} · Skipped {skipped}"
        };

        return (true, result.Message, result);
    }

    public async Task<(bool Ok, string Message, ApplyCourseCatalogVersionResultDto? Result)> ApplyVersionAsync(
        int catalogVersionId,
        ApplyCourseCatalogVersionDto dto)
    {
        if (dto.TargetProgramId <= 0)
        {
            return (false, "Target program is required.", null);
        }

        var programExists = await _context.AcademicPrograms
            .AnyAsync(p => p.ProgramId == dto.TargetProgramId);
        if (!programExists)
        {
            return (false, "Target program was not found.", null);
        }

        var version = await _context.CourseCatalogVersions
            .AsNoTracking()
            .Include(v => v.Items)
            .FirstOrDefaultAsync(v => v.CatalogVersionId == catalogVersionId);

        if (version == null)
        {
            return (false, "Catalog version was not found.", null);
        }

        var items = version.Items.OrderBy(i => i.DisplayOrder).ToList();
        if (dto.SelectedItemIds != null && dto.SelectedItemIds.Count > 0)
        {
            var selected = dto.SelectedItemIds.ToHashSet();
            items = items.Where(i => selected.Contains(i.CatalogVersionItemId)).ToList();
        }

        if (items.Count == 0)
        {
            return (false, "Select at least one course to add.", null);
        }

        var existingCourses = await _context.Courses.ToListAsync();
        var alreadyLinkedIds = await _context.AcademicProgramCourses
            .Where(pc => pc.ProgramId == dto.TargetProgramId)
            .Select(pc => pc.CourseId)
            .ToListAsync();
        var alreadyLinkedSet = alreadyLinkedIds.ToHashSet();

        var linkedCodeKeys = (
            from pc in alreadyLinkedIds
            join c in existingCourses on pc equals c.CourseId
            select ExcelImportHelper.CourseMatchKey(c.CourseCode)
        ).Where(k => k.Length > 0).ToHashSet(StringComparer.Ordinal);

        var added = 0;
        var alreadyLinked = 0;
        var skipped = 0;
        var insertedCourses = 0;

        foreach (var item in items)
        {
            var key = ExcelImportHelper.CourseMatchKey(item.CourseCode);
            if (key.Length == 0)
            {
                skipped++;
                continue;
            }

            if (linkedCodeKeys.Contains(key))
            {
                alreadyLinked++;
                continue;
            }

            var course = existingCourses.FirstOrDefault(c =>
                ExcelImportHelper.CourseEquals(c.CourseCode, item.CourseCode));

            if (course == null)
            {
                // Title clash with a different code → skip to avoid inconsistency
                if (existingCourses.Any(c =>
                        string.Equals(
                            c.CourseTitle.Trim(),
                            item.CourseTitle.Trim(),
                            StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    continue;
                }

                course = new Course
                {
                    CourseCode = item.CourseCode.Trim(),
                    CourseTitle = item.CourseTitle.Trim(),
                    ClassCreditHours = item.ClassCreditHours,
                    LabCreditHours = item.LabCreditHours
                };
                _context.Courses.Add(course);
                await _context.SaveChangesAsync();
                existingCourses.Add(course);
                insertedCourses++;
            }

            if (alreadyLinkedSet.Contains(course.CourseId))
            {
                alreadyLinked++;
                linkedCodeKeys.Add(key);
                continue;
            }

            _context.AcademicProgramCourses.Add(new AcademicProgramCourse
            {
                ProgramId = dto.TargetProgramId,
                CourseId = course.CourseId
            });
            alreadyLinkedSet.Add(course.CourseId);
            linkedCodeKeys.Add(key);
            added++;
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync();
        }

        var result = new ApplyCourseCatalogVersionResultDto
        {
            AddedCount = added,
            AlreadyLinkedCount = alreadyLinked,
            SkippedCount = skipped,
            InsertedCourseCount = insertedCourses,
            Message =
                $"Added {added} · Already linked {alreadyLinked} · Skipped {skipped}"
        };

        return (true, result.Message, result);
    }

    public static string BuildFingerprint(IEnumerable<CatalogVersionSnapshotItem> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items
                     .OrderBy(i => ExcelImportHelper.CourseMatchKey(i.CourseCode), StringComparer.Ordinal))
        {
            sb.Append(ExcelImportHelper.CourseMatchKey(item.CourseCode));
            sb.Append('|');
            sb.Append((item.CourseTitle ?? string.Empty).Trim().ToUpperInvariant());
            sb.Append('|');
            sb.Append(item.ClassCreditHours?.ToString() ?? "");
            sb.Append('|');
            sb.Append(item.LabCreditHours?.ToString() ?? "");
            sb.Append(';');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }
}
