using FolderManagementSystemAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Services;

public class SessionScopeService
{
    private readonly FolderManagementDbContext _context;

    public SessionScopeService(FolderManagementDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, int SessionId, string Message)> TryResolveSessionIdAsync(
        int programCourseId,
        int? sessionId = null,
        int? offeredCourseId = null)
    {
        var programCourse = await _context.AcademicProgramCourses
            .AsNoTracking()
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return (false, 0, "Program course not found.");
        }

        if (sessionId is > 0)
        {
            var sessionValid = await _context.AcademicSessions.AnyAsync(s =>
                s.SessionId == sessionId.Value &&
                s.ProgramId == programCourse.ProgramId);

            if (!sessionValid)
            {
                return (false, 0, "Session not found for this program.");
            }

            return (true, sessionId.Value, string.Empty);
        }

        if (offeredCourseId is > 0)
        {
            var resolvedSessionId = await (
                from oc in _context.OfferedCourses
                join section in _context.Sections on oc.SectionId equals section.SectionId
                join session in _context.AcademicSessions on section.SessionId equals session.SessionId
                where oc.OfferedCourseId == offeredCourseId.Value
                      && oc.CourseId == programCourse.CourseId
                      && session.ProgramId == programCourse.ProgramId
                select section.SessionId
            ).FirstOrDefaultAsync();

            if (resolvedSessionId == 0)
            {
                return (false, 0, "Offered course not found for this program course.");
            }

            return (true, resolvedSessionId, string.Empty);
        }

        var fallbackSessionId = await _context.AcademicSessions
            .Where(s => s.ProgramId == programCourse.ProgramId)
            .OrderBy(s => s.SessionId)
            .Select(s => (int?)s.SessionId)
            .FirstOrDefaultAsync();

        if (!fallbackSessionId.HasValue)
        {
            return (false, 0, "No academic session found for this program.");
        }

        return (true, fallbackSessionId.Value, string.Empty);
    }
}
