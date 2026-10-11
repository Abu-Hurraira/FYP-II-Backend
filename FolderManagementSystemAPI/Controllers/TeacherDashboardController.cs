using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TeacherDashboardController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly string _connectionString;
    private readonly TeacherPermissionService _permissionService;

    // Constructor
    // Initializes database context and extracts connection string for SQL operations
    public TeacherDashboardController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    // Courses
    // Retrieves all courses assigned to a specific teacher along with progress information
    [HttpGet("{teacherUserId:int}/courses")]
    public async Task<ActionResult<IEnumerable<TeacherCourseCardDto>>> GetTeacherCourses(int teacherUserId)
    {
        // Check if teacher exists in database
        var teacherExists = await _context.Users.AnyAsync(u => u.UserId == teacherUserId);

        if (!teacherExists)
        {
            return NotFound(new { message = "Teacher not found." });
        }

        // Fetch all offered courses assigned to this teacher with related data
        var offeredCourses = await _context.OfferedCourses
            .Include(oc => oc.Course)
            .Include(oc => oc.Section)
                .ThenInclude(s => s.Session)
                    .ThenInclude(sess => sess.Program)
            .Include(oc => oc.TeacherUser)
            .Where(oc => oc.TeacherUserId == teacherUserId)
            .OrderBy(oc => oc.Course.CourseTitle)
            .ThenBy(oc => oc.Section.SectionName)
            .ToListAsync();

        var result = new List<TeacherCourseCardDto>();

        // Process each course and calculate progress
        foreach (var oc in offeredCourses)
        {
            var programId = oc.Section.Session.ProgramId;

            // Ensure mapping exists between program and course
            var programCourseId = await EnsureProgramCourseAsync(programId, oc.CourseId);

            // Calculate weekly progress for course
            var progress = await GetWeeklyProgressAsync(oc.OfferedCourseId, programCourseId);

            // Build response DTO
            result.Add(new TeacherCourseCardDto
            {
                OfferedCourseId = oc.OfferedCourseId,
                CourseId = oc.CourseId,
                ProgramCourseId = programCourseId,
                ProgramId = programId,
                ProgramName = oc.Section.Session.Program?.ProgramName ?? string.Empty,
                ProgramCode = oc.Section.Session.Program?.ProgramCode ?? string.Empty,
                SessionTitle = oc.Section.Session.SessionTitle ?? string.Empty,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode,
                SectionName = oc.Section.SectionName,
                TeacherName = oc.TeacherUser.FullName,
                HasWeeklyContent = progress.HasContent,
                WeeklyProgressPercentage = progress.HasContent ? progress.Percentage : null
            });
        }

        return Ok(result);
    }

    // CourseMenu
    // Retrieves detailed course menu information for a specific offered course
    [HttpGet("course-menu/{offeredCourseId:int}")]
    public async Task<ActionResult<TeacherCourseMenuDto>> GetCourseMenu(
        int offeredCourseId,
        [FromQuery] int? teacherUserId = null)
    {
        // Fetch offered course with related entities
        var oc = await _context.OfferedCourses
            .Include(x => x.Course)
            .Include(x => x.Section)
                .ThenInclude(s => s.Session)
            .Include(x => x.TeacherUser)
            .FirstOrDefaultAsync(x => x.OfferedCourseId == offeredCourseId);

        if (oc == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var programId = oc.Section.Session.ProgramId;
        var effectiveTeacherUserId = teacherUserId ?? oc.TeacherUserId;

        // Ensure program-course mapping exists
        var programCourseId = await EnsureProgramCourseAsync(programId, oc.CourseId);

        // Return course menu details
        var permissions = await _permissionService.GetTeacherFlagsAsync(
            effectiveTeacherUserId,
            programCourseId);

        return Ok(new TeacherCourseMenuDto
        {
            OfferedCourseId = oc.OfferedCourseId,
            CourseId = oc.CourseId,
            ProgramCourseId = programCourseId,
            CourseTitle = oc.Course.CourseTitle,
            CourseCode = oc.Course.CourseCode,
            SectionName = oc.Section.SectionName,
            TeacherName = oc.TeacherUser.FullName,
            Permissions = permissions
        });
    }

    // EnsureProgramCourse
    // Ensures a ProgramCourse relationship exists, creates it if missing, and returns its ID
    private async Task<int> EnsureProgramCourseAsync(int programId, int courseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Check if mapping already exists
        var selectCommand = new SqlCommand(@"
            SELECT ProgramCourseID
            FROM AcademicProgramCourses
            WHERE ProgramID = @ProgramID AND CourseID = @CourseID;
        ", connection);

        selectCommand.Parameters.AddWithValue("@ProgramID", programId);
        selectCommand.Parameters.AddWithValue("@CourseID", courseId);

        var existing = await selectCommand.ExecuteScalarAsync();

        // Return existing mapping if found
        if (existing != null && existing != DBNull.Value)
        {
            return Convert.ToInt32(existing);
        }

        // Insert new mapping if not found
        var insertCommand = new SqlCommand(@"
            INSERT INTO AcademicProgramCourses (ProgramID, CourseID)
            OUTPUT INSERTED.ProgramCourseID
            VALUES (@ProgramID, @CourseID);
        ", connection);

        insertCommand.Parameters.AddWithValue("@ProgramID", programId);
        insertCommand.Parameters.AddWithValue("@CourseID", courseId);

        return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
    }

    // WeeklyProgress
    // Calculates weekly topic completion progress for a course
    private async Task<(bool HasContent, decimal Percentage)> GetWeeklyProgressAsync(
        int offeredCourseId,
        int programCourseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Query to calculate leaf topics and completed topics
        var command = new SqlCommand(@"
            ;WITH LeafTopics AS
            (
                SELECT t.TopicID
                FROM CourseTopics t
                WHERE t.ProgramCourseID = @ProgramCourseID
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM CourseTopics child
                    WHERE child.ParentTopicID = t.TopicID
                )
            )
            SELECT
                COUNT(lt.TopicID) AS TotalTopics,
                SUM(CASE WHEN ISNULL(p.IsCompleted, 0) = 1 THEN 1 ELSE 0 END) AS CompletedTopics
            FROM LeafTopics lt
            LEFT JOIN OfferedCourseTopicProgress p
                ON p.TopicID = lt.TopicID
                AND p.OfferedCourseID = @OfferedCourseID;
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);
        command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return (false, 0);
        }

        var totalTopics = reader["TotalTopics"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalTopics"]);
        var completedTopics = reader["CompletedTopics"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CompletedTopics"]);

        // If no topics exist, return empty progress
        if (totalTopics == 0)
        {
            return (false, 0);
        }

        // Calculate completion percentage
        var percentage = Math.Round((decimal)completedTopics / totalTopics * 100, 2);
        return (true, percentage);
    }
}