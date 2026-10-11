using System.Globalization;
using ClosedXML.Excel;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AttendanceController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly GraderAccessService _graderAccess;

    public AttendanceController(
        FolderManagementDbContext context,
        IWebHostEnvironment environment,
        GraderAccessService graderAccess)
    {
        _context = context;
        _environment = environment;
        _graderAccess = graderAccess;
    }

    [HttpGet("offered-course/{offeredCourseId:int}")]
    public async Task<ActionResult<AttendanceSheetDto>> GetSheet(
        int offeredCourseId,
        [FromQuery] int? teacherUserId = null)
    {
        var access = await EnsureAccessAsync(offeredCourseId, teacherUserId);
        if (access.Error != null)
        {
            return access.Error;
        }

        var sheet = await BuildSheetAsync(access.OfferedCourse!);
        return Ok(sheet);
    }

    [HttpPost("offered-course/{offeredCourseId:int}/import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AttendanceImportResultDto>> ImportSheet(
        int offeredCourseId,
        [FromForm] int teacherUserId,
        [FromForm] IFormFile file)
    {
        var access = await EnsureAccessAsync(offeredCourseId, teacherUserId);
        if (access.Error != null)
        {
            return access.Error;
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Excel file is required." });
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only .xlsx files are supported." });
        }

        var roster = await LoadRosterAsync(offeredCourseId);
        if (roster.Count == 0)
        {
            return BadRequest(new { message = "No enrolled students found for this offered course." });
        }

        var byArid = roster
            .Where(s => !string.IsNullOrWhiteSpace(s.AridNo))
            .GroupBy(s => NormalizeKey(s.AridNo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var byName = roster
            .Where(s => !string.IsNullOrWhiteSpace(s.FullName))
            .GroupBy(s => NormalizeKey(s.FullName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var existing = await _context.AttendanceRecords
            .Where(r => r.OfferedCourseId == offeredCourseId)
            .ToListAsync();

        var existingLookup = existing.ToDictionary(
            r => (r.StudentId, r.AttendanceDate),
            r => r);

        var upserted = 0;
        var skipped = 0;
        var errors = new List<string>();

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            return BadRequest(new { message = "No worksheet found." });
        }

        var rows = worksheet.RowsUsed().ToList();
        if (rows.Count < 2)
        {
            return BadRequest(new { message = "File must contain a header row and at least one data row." });
        }

        var headerCells = rows[0].CellsUsed()
            .Select(c => (Raw: c.GetString().Trim(), Col: c.Address.ColumnNumber))
            .Where(c => !string.IsNullOrWhiteSpace(c.Raw))
            .ToList();
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (raw, col) in headerCells)
        {
            var key = NormalizeHeader(raw);
            if (!string.IsNullOrWhiteSpace(key) && !headerMap.ContainsKey(key))
            {
                headerMap[key] = col;
            }
        }

        var studentCol = GetColumnIndex(headerMap, ["student", "studentname", "name", "fullname"]);
        var aridCol = GetColumnIndex(headerMap, ["arid", "aridno", "regno", "registration", "roll", "rollno"]);
        var statusCol = GetColumnIndex(headerMap, ["status", "attendance", "attendancestatus"]);
        var dateCol = GetColumnIndex(headerMap, ["date", "attendancedate"]);

        // Wide format: date columns across the sheet (parse original header text).
        var dateColumns = new List<(int Column, DateOnly Date)>();
        foreach (var (raw, col) in headerCells)
        {
            if (col == studentCol || col == aridCol || col == statusCol || col == dateCol)
            {
                continue;
            }

            if (TryParseDate(raw, out var parsed))
            {
                dateColumns.Add((col, parsed));
            }
        }

        if (studentCol == 0 && aridCol == 0)
        {
            return BadRequest(new { message = "Excel must include a Student/Name or ARID/Reg No column." });
        }

        if (dateColumns.Count == 0 && (dateCol == 0 || statusCol == 0))
        {
            return BadRequest(new
            {
                message = "Excel must include date columns, or Status + Date columns."
            });
        }

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var arid = aridCol > 0 ? row.Cell(aridCol).GetString().Trim() : string.Empty;
            var name = studentCol > 0 ? row.Cell(studentCol).GetString().Trim() : string.Empty;

            Student? student = null;
            if (!string.IsNullOrWhiteSpace(arid) &&
                byArid.TryGetValue(NormalizeKey(arid), out var byAridMatch))
            {
                student = byAridMatch;
            }
            else if (!string.IsNullOrWhiteSpace(name) &&
                     byName.TryGetValue(NormalizeKey(name), out var byNameMatch))
            {
                student = byNameMatch;
            }

            if (student == null)
            {
                skipped++;
                errors.Add($"Row {i + 1}: student not found ({arid}/{name}).");
                continue;
            }

            if (dateColumns.Count > 0)
            {
                foreach (var (column, date) in dateColumns)
                {
                    var rawStatus = row.Cell(column).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(rawStatus))
                    {
                        continue;
                    }

                    var status = NormalizeStatus(rawStatus);
                    if (status == null)
                    {
                        skipped++;
                        errors.Add($"Row {i + 1}: invalid status '{rawStatus}' for {date:yyyy-MM-dd}.");
                        continue;
                    }

                    UpsertRecord(existingLookup, offeredCourseId, student.StudentId, date, status, ref upserted);
                }
            }
            else
            {
                var rawDate = row.Cell(dateCol).GetString().Trim();
                if (string.IsNullOrWhiteSpace(rawDate))
                {
                    var cell = row.Cell(dateCol);
                    if (cell.TryGetValue(out DateTime dt))
                    {
                        rawDate = dt.ToString("yyyy-MM-dd");
                    }
                }

                if (!TryParseDate(rawDate, out var date))
                {
                    skipped++;
                    errors.Add($"Row {i + 1}: invalid date '{rawDate}'.");
                    continue;
                }

                var status = NormalizeStatus(row.Cell(statusCol).GetString());
                if (status == null)
                {
                    skipped++;
                    errors.Add($"Row {i + 1}: invalid status.");
                    continue;
                }

                UpsertRecord(existingLookup, offeredCourseId, student.StudentId, date, status, ref upserted);
            }
        }

        if (upserted > 0)
        {
            await _context.SaveChangesAsync();
        }

        var relativePath = await SaveImportedFileAsync(offeredCourseId, file);
        var sheet = await BuildSheetAsync(access.OfferedCourse!, relativePath);

        return Ok(new AttendanceImportResultDto
        {
            Message = $"Imported attendance. Upserted {upserted}, skipped {skipped}.",
            UpsertedCount = upserted,
            SkippedCount = skipped,
            ErrorCount = errors.Count,
            Errors = errors.Take(50).ToList(),
            Sheet = sheet
        });
    }

    [HttpPut("offered-course/{offeredCourseId:int}")]
    public async Task<ActionResult<AttendanceSheetDto>> UpdateSheet(
        int offeredCourseId,
        [FromBody] UpdateAttendanceSheetDto dto)
    {
        var access = await EnsureAccessAsync(offeredCourseId, dto.TeacherUserId);
        if (access.Error != null)
        {
            return access.Error;
        }

        var rosterIds = (await _context.StudentOfferedCourses
            .AsNoTracking()
            .Where(soc => soc.OfferedCourseId == offeredCourseId)
            .Select(soc => soc.StudentId)
            .ToListAsync()).ToHashSet();

        var updates = dto.Updates ?? new List<AttendanceCellUpdateDto>();
        foreach (var update in updates)
        {
            if (!rosterIds.Contains(update.StudentId))
            {
                continue;
            }

            if (!TryParseDate(update.Date, out var date))
            {
                continue;
            }

            var existing = await _context.AttendanceRecords
                .FirstOrDefaultAsync(r =>
                    r.OfferedCourseId == offeredCourseId &&
                    r.StudentId == update.StudentId &&
                    r.AttendanceDate == date);

            var status = NormalizeStatus(update.Status);
            if (status == null)
            {
                if (existing != null)
                {
                    _context.AttendanceRecords.Remove(existing);
                }

                continue;
            }

            if (existing == null)
            {
                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    OfferedCourseId = offeredCourseId,
                    StudentId = update.StudentId,
                    AttendanceDate = date,
                    Status = status
                });
            }
            else
            {
                existing.Status = status;
            }
        }

        await _context.SaveChangesAsync();
        var sheet = await BuildSheetAsync(access.OfferedCourse!);
        return Ok(sheet);
    }

    private void UpsertRecord(
        Dictionary<(int StudentId, DateOnly Date), AttendanceRecord> existingLookup,
        int offeredCourseId,
        int studentId,
        DateOnly date,
        string status,
        ref int upserted)
    {
        var key = (studentId, date);
        if (existingLookup.TryGetValue(key, out var existing))
        {
            if (!string.Equals(existing.Status, status, StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = status;
                upserted++;
            }
        }
        else
        {
            var record = new AttendanceRecord
            {
                OfferedCourseId = offeredCourseId,
                StudentId = studentId,
                AttendanceDate = date,
                Status = status
            };
            _context.AttendanceRecords.Add(record);
            existingLookup[key] = record;
            upserted++;
        }
    }

    private async Task<AttendanceSheetDto> BuildSheetAsync(OfferedCourse offeredCourse, string? sheetPath = null)
    {
        var students = await LoadRosterAsync(offeredCourse.OfferedCourseId);
        var records = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.OfferedCourseId == offeredCourse.OfferedCourseId)
            .ToListAsync();

        var dates = records
            .Select(r => r.AttendanceDate)
            .Distinct()
            .OrderBy(d => d)
            .Select(d => d.ToString("yyyy-MM-dd"))
            .ToList();

        var byStudent = records
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.AttendanceDate, x => x));

        var rows = students.Select(student =>
        {
            byStudent.TryGetValue(student.StudentId, out var studentRecords);
            studentRecords ??= new Dictionary<DateOnly, AttendanceRecord>();

            var cells = dates.Select(dateText =>
            {
                var date = DateOnly.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (studentRecords.TryGetValue(date, out var record))
                {
                    return new AttendanceCellDto
                    {
                        AttendanceId = record.AttendanceId,
                        Date = dateText,
                        Status = record.Status
                    };
                }

                return new AttendanceCellDto
                {
                    AttendanceId = null,
                    Date = dateText,
                    Status = string.Empty
                };
            }).ToList();

            return new AttendanceStudentRowDto
            {
                StudentId = student.StudentId,
                FullName = student.FullName,
                AridNo = student.AridNo,
                Cells = cells
            };
        }).ToList();

        return new AttendanceSheetDto
        {
            OfferedCourseId = offeredCourse.OfferedCourseId,
            CourseTitle = offeredCourse.Course?.CourseTitle ?? string.Empty,
            SectionName = offeredCourse.Section?.SectionName ?? string.Empty,
            AttendanceSheetPath = sheetPath,
            Dates = dates,
            Students = rows
        };
    }

    private async Task<List<Student>> LoadRosterAsync(int offeredCourseId)
    {
        return await (
            from soc in _context.StudentOfferedCourses.AsNoTracking()
            join student in _context.Students.AsNoTracking() on soc.StudentId equals student.StudentId
            where soc.OfferedCourseId == offeredCourseId
            orderby student.AridNo, student.FullName
            select student).ToListAsync();
    }

    private async Task<(OfferedCourse? OfferedCourse, ActionResult? Error)> EnsureAccessAsync(
        int offeredCourseId,
        int? teacherUserId)
    {
        var offeredCourse = await _context.OfferedCourses
            .Include(oc => oc.Course)
            .Include(oc => oc.Section)
            .FirstOrDefaultAsync(oc => oc.OfferedCourseId == offeredCourseId);

        if (offeredCourse == null)
        {
            return (null, NotFound(new { message = "Offered course not found." }));
        }

        if (teacherUserId is null or <= 0)
        {
            return (null, BadRequest(new { message = "teacherUserId is required." }));
        }

        if (offeredCourse.TeacherUserId == teacherUserId.Value)
        {
            if (GraderAccessService.IsGrader(User)
                && !await _graderAccess.CanAccessAsync(User, offeredCourseId, GraderTaskCodes.Attendance))
            {
                return (null, BadRequest(new { message = "Attendance is not assigned to you for this course." }));
            }

            return (offeredCourse, null);
        }

        var isPrivileged = await _context.Users
            .AsNoTracking()
            .AnyAsync(u =>
                u.UserId == teacherUserId.Value &&
                (u.Role.RoleName == "HOD" || u.Role.RoleName == "Admin"));

        if (!isPrivileged)
        {
            return (null, BadRequest(new { message = "You do not have access to this attendance sheet." }));
        }

        return (offeredCourse, null);
    }

    private async Task<string?> SaveImportedFileAsync(int offeredCourseId, IFormFile file)
    {
        try
        {
            var relativeDir = Path.Combine("src", "Attendance", offeredCourseId.ToString());
            var absoluteDir = Path.Combine(_environment.ContentRootPath, relativeDir);
            Directory.CreateDirectory(absoluteDir);

            var fileName = $"attendance_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            var absolutePath = Path.Combine(absoluteDir, fileName);
            await using (var stream = new FileStream(absolutePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Path.Combine(relativeDir, fileName).Replace("\\", "/");
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        return value.ToLowerInvariant() switch
        {
            "p" or "present" or "1" or "yes" or "y" => "Present",
            "a" or "absent" or "0" or "no" or "n" => "Absent",
            "l" or "leave" or "excused" => "Leave",
            _ when value.Equals("Present", StringComparison.OrdinalIgnoreCase) => "Present",
            _ when value.Equals("Absent", StringComparison.OrdinalIgnoreCase) => "Absent",
            _ when value.Equals("Leave", StringComparison.OrdinalIgnoreCase) => "Leave",
            _ => null
        };
    }

    private static bool TryParseDate(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();
        string[] formats =
        [
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "dd-MM-yyyy",
            "d/M/yyyy",
            "M/d/yyyy",
            "yyyy/MM/dd"
        ];

        if (DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        // ClosedXML / Excel serial sometimes arrives as a number string.
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa) &&
            oa > 20000 && oa < 60000)
        {
            try
            {
                date = DateOnly.FromDateTime(DateTime.FromOADate(oa));
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static int GetColumnIndex(Dictionary<string, int> headerMap, string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (headerMap.TryGetValue(NormalizeHeader(candidate), out var index))
            {
                return index;
            }
        }

        return 0;
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace(".", "");

    private static string NormalizeKey(string value) =>
        value.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
}
