using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HodUploadAllocationController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;

    public HodUploadAllocationController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllocation()
    {
        var config = await BuildConfigAsync();
        return Ok(config);
    }

    [HttpPut]
    public async Task<IActionResult> SaveAllocation([FromBody] SaveHodUploadAllocationDto dto)
    {
        if (dto.HodUserId <= 0)
        {
            return BadRequest(new { message = "hodUserId is required." });
        }

        var hodExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId == dto.HodUserId && u.Role.RoleName == "HOD");

        if (!hodExists)
        {
            return BadRequest(new { message = "hodUserId must be a valid HOD user." });
        }

        var items = NormalizeItems(dto);
        var resolved = new List<(HodUploadAllocationItemDto Item, OfferedCourseAllocationContext Context)>();
        foreach (var item in items)
        {
            var context = await ResolveOfferedCourseAsync(item.OfferedCourseId);
            if (context == null)
            {
                continue;
            }

            resolved.Add((item, context));
        }

        await PersistSectionAllocationsAsync(resolved.Select(x => x.Item).ToList(), dto.HodUserId);

        foreach (var group in resolved.GroupBy(x => (
                     x.Context.ProgramId,
                     x.Context.CourseId,
                     x.Context.TeacherUserId)))
        {
            var clos = group.Any(x => x.Item.Clos || x.Item.FolderTemplate);
            var cloPlo = group.Any(x => x.Item.CloPloMapping);
            var cloTask = group.Any(x => x.Item.CloTaskMapping);
            var mid = group.Any(x => x.Item.Mid);
            var final = group.Any(x => x.Item.Final);
            var weekly = group.Any(x => x.Item.FolderTemplate || x.Item.Clos);
            var key = group.Key;

            await _permissionService.SetPermissionAsync(
                key.ProgramId, key.CourseId, key.TeacherUserId,
                TeacherPermissionTypes.ManageClos, clos, dto.HodUserId, exclusive: false);
            await _permissionService.SetPermissionAsync(
                key.ProgramId, key.CourseId, key.TeacherUserId,
                TeacherPermissionTypes.ManageCloPloMapping, cloPlo, dto.HodUserId, exclusive: false);
            await _permissionService.SetPermissionAsync(
                key.ProgramId, key.CourseId, key.TeacherUserId,
                TeacherPermissionTypes.ManageCloTaskMapping, cloTask, dto.HodUserId, exclusive: false);
            await _permissionService.SetPermissionAsync(
                key.ProgramId, key.CourseId, key.TeacherUserId,
                TeacherPermissionTypes.UploadMidExamQuestions, mid, dto.HodUserId, exclusive: false);
            await _permissionService.SetPermissionAsync(
                key.ProgramId, key.CourseId, key.TeacherUserId,
                TeacherPermissionTypes.UploadFinalExamQuestions, final, dto.HodUserId, exclusive: false);
            await _permissionService.SetPermissionAsync(
                key.ProgramId, key.CourseId, key.TeacherUserId,
                TeacherPermissionTypes.UploadWeeklyContent, weekly, dto.HodUserId, exclusive: false);
        }

        var config = await BuildConfigAsync();
        return Ok(config);
    }

    [HttpDelete]
    public async Task<IActionResult> ClearAllocation()
    {
        var allocations = await _context.HodUploadAllocations.ToListAsync();
        if (allocations.Count > 0)
        {
            _context.HodUploadAllocations.RemoveRange(allocations);
        }

        var rows = await _context.TeacherPermissions.ToListAsync();
        foreach (var row in rows)
        {
            row.IsAllowed = false;
        }

        if (allocations.Count > 0 || rows.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            configured = false,
            allocations = new Dictionary<string, object>(),
            updatedAt = DateTime.UtcNow
        });
    }

    [HttpGet("offered-course/{offeredCourseId:int}")]
    public async Task<IActionResult> GetOfferedCoursePermissions(
        int offeredCourseId,
        [FromQuery] int? teacherUserId = null)
    {
        var context = await ResolveOfferedCourseAsync(offeredCourseId);
        if (context == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var effectiveTeacherId = teacherUserId is > 0 ? teacherUserId.Value : context.TeacherUserId;
        var saved = await _context.HodUploadAllocations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        HodUploadAllocationFlagsDto row;
        if (saved != null)
        {
            row = MapSaved(saved);
        }
        else
        {
            var flags = await _permissionService.GetTeacherFlagsAsync(effectiveTeacherId, context.ProgramCourseId);
            row = MapFlags(flags);
        }

        var configured = row.Clos || row.CloPloMapping || row.CloTaskMapping || row.Mid || row.Final || row.FolderTemplate;

        return Ok(new
        {
            offeredCourseId,
            configured,
            clos = row.Clos,
            cloPloMapping = row.CloPloMapping,
            cloTaskMapping = row.CloTaskMapping,
            mid = row.Mid,
            final = row.Final,
            folderTemplate = row.FolderTemplate,
            isAllocatedTeacher = configured && effectiveTeacherId == context.TeacherUserId
        });
    }

    private async Task PersistSectionAllocationsAsync(
        List<HodUploadAllocationItemDto> items,
        int hodUserId)
    {
        var now = DateTime.UtcNow;
        var incomingIds = items.Select(x => x.OfferedCourseId).ToHashSet();
        var existing = await _context.HodUploadAllocations.ToListAsync();
        var existingById = existing.ToDictionary(x => x.OfferedCourseId);

        foreach (var row in existing.Where(x => !incomingIds.Contains(x.OfferedCourseId)))
        {
            _context.HodUploadAllocations.Remove(row);
        }

        foreach (var item in items)
        {
            if (existingById.TryGetValue(item.OfferedCourseId, out var row))
            {
                CopyFlags(row, item, hodUserId, now);
            }
            else
            {
                var created = new HodUploadAllocation
                {
                    OfferedCourseId = item.OfferedCourseId
                };
                CopyFlags(created, item, hodUserId, now);
                _context.HodUploadAllocations.Add(created);
            }
        }

        await _context.SaveChangesAsync();
    }

    private static void CopyFlags(
        HodUploadAllocation row,
        HodUploadAllocationItemDto item,
        int hodUserId,
        DateTime now)
    {
        row.Clos = item.Clos;
        row.CloPloMapping = item.CloPloMapping;
        row.CloTaskMapping = item.CloTaskMapping;
        row.Mid = item.Mid;
        row.Final = item.Final;
        row.FolderTemplate = item.FolderTemplate;
        row.UpdatedByHodUserId = hodUserId;
        row.UpdatedAt = now;
    }

    private async Task<object> BuildConfigAsync()
    {
        var offerings = await _context.OfferedCourses
            .AsNoTracking()
            .Include(oc => oc.Section)
                .ThenInclude(s => s.Session)
            .ToListAsync();

        var savedRows = await _context.HodUploadAllocations
            .AsNoTracking()
            .ToListAsync();
        var savedById = savedRows.ToDictionary(x => x.OfferedCourseId);
        var useSaved = savedRows.Count > 0;

        var allocations = new Dictionary<string, HodUploadAllocationFlagsDto>();
        var anyAllowed = false;
        DateTime? updatedAt = savedRows.Count > 0
            ? savedRows.Max(x => (DateTime?)x.UpdatedAt)
            : null;

        foreach (var oc in offerings)
        {
            HodUploadAllocationFlagsDto row;
            if (useSaved)
            {
                row = savedById.TryGetValue(oc.OfferedCourseId, out var saved)
                    ? MapSaved(saved)
                    : new HodUploadAllocationFlagsDto();
            }
            else
            {
                // Do not call GetTeacherFlagsAsync here — it seeds Section A and
                // made refresh look like only ~one row per course survived.
                row = new HodUploadAllocationFlagsDto();
            }

            allocations[oc.OfferedCourseId.ToString()] = row;
            if (row.Clos || row.CloPloMapping || row.CloTaskMapping || row.Mid || row.Final || row.FolderTemplate)
            {
                anyAllowed = true;
            }
        }

        return new
        {
            configured = anyAllowed,
            allocations,
            updatedAt
        };
    }

    private static List<HodUploadAllocationItemDto> NormalizeItems(SaveHodUploadAllocationDto dto)
    {
        var items = dto.Items ?? new List<HodUploadAllocationItemDto>();
        if (items.Count == 0 && dto.Allocations != null)
        {
            items = dto.Allocations
                .Select(kv =>
                {
                    var row = kv.Value ?? new HodUploadAllocationFlagsDto();
                    return new HodUploadAllocationItemDto
                    {
                        OfferedCourseId = int.TryParse(kv.Key, out var id) ? id : 0,
                        Clos = row.Clos,
                        CloPloMapping = row.CloPloMapping,
                        CloTaskMapping = row.CloTaskMapping,
                        Mid = row.Mid,
                        Final = row.Final,
                        FolderTemplate = row.FolderTemplate
                    };
                })
                .Where(x => x.OfferedCourseId > 0)
                .ToList();
        }

        return items
            .Where(x => x.OfferedCourseId > 0)
            .GroupBy(x => x.OfferedCourseId)
            .Select(g => g.Last())
            .ToList();
    }

    private static HodUploadAllocationFlagsDto MapSaved(HodUploadAllocation row) =>
        new()
        {
            Clos = row.Clos,
            CloPloMapping = row.CloPloMapping,
            CloTaskMapping = row.CloTaskMapping,
            Mid = row.Mid,
            Final = row.Final,
            FolderTemplate = row.FolderTemplate
        };

    private static HodUploadAllocationFlagsDto MapFlags(TeacherPermissionFlagsDto flags) =>
        new()
        {
            Clos = flags.CanManageClos,
            CloPloMapping = flags.CanManageCloPloMapping,
            CloTaskMapping = flags.CanManageCloTaskMapping,
            Mid = flags.CanUploadMidExamQuestions,
            Final = flags.CanUploadFinalExamQuestions,
            FolderTemplate = flags.CanManageClos || flags.CanUploadWeeklyContent
        };

    private async Task<OfferedCourseAllocationContext?> ResolveOfferedCourseAsync(int offeredCourseId)
    {
        var row = await (
            from oc in _context.OfferedCourses.AsNoTracking()
            join section in _context.Sections.AsNoTracking() on oc.SectionId equals section.SectionId
            join session in _context.AcademicSessions.AsNoTracking() on section.SessionId equals session.SessionId
            where oc.OfferedCourseId == offeredCourseId
            select new
            {
                oc.OfferedCourseId,
                oc.CourseId,
                oc.TeacherUserId,
                session.ProgramId
            }).FirstOrDefaultAsync();

        if (row == null)
        {
            return null;
        }

        var programCourseId = await _permissionService.EnsureProgramCourseAsync(row.ProgramId, row.CourseId);
        return new OfferedCourseAllocationContext
        {
            OfferedCourseId = row.OfferedCourseId,
            CourseId = row.CourseId,
            TeacherUserId = row.TeacherUserId,
            ProgramId = row.ProgramId,
            ProgramCourseId = programCourseId
        };
    }

    private sealed class OfferedCourseAllocationContext
    {
        public int OfferedCourseId { get; set; }
        public int CourseId { get; set; }
        public int TeacherUserId { get; set; }
        public int ProgramId { get; set; }
        public int ProgramCourseId { get; set; }
    }
}
