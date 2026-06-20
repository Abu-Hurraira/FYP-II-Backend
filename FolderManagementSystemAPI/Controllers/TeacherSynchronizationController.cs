using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]

public class TeacherSynchronizationController : ControllerBase
{
    // Initializes database context and retrieves connection string for raw SQL operations
    private readonly FolderManagementDbContext _context;
    private readonly string _connectionString;

    // Constructor that sets up database context and connection string
    public TeacherSynchronizationController(FolderManagementDbContext context)
    {
        _context = context;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    // API endpoint that returns synchronization data for a specific offered course
    [HttpGet("offered-course/{offeredCourseId:int}")]
    public async Task<ActionResult<TeacherSynchronizationDto>> GetSynchronizationData(
        int offeredCourseId)
    {
        var current = await GetCurrentOfferedCourseAsync(offeredCourseId);

        if (current == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        var programCourseId = await EnsureProgramCourseAsync(
            current.ProgramId,
            current.CourseId
        );

        var teachers = await GetComparableOfferedCoursesAsync(
            current.ProgramId,
            current.CourseId
        );

        if (teachers.Count == 0)
        {
            return Ok(new TeacherSynchronizationDto
            {
                CurrentOfferedCourseId = offeredCourseId,
                ProgramCourseId = programCourseId,
                CourseTitle = current.CourseTitle,
                CurrentTeacherName = current.TeacherName,
                CurrentSectionName = current.SectionName
            });
        }

        var offeredCourseIds = teachers
            .Select(x => x.OfferedCourseId)
            .ToList();

        await EnsureProgressRowsForAllAsync(programCourseId, offeredCourseIds);

        var topics = await GetLeafTopicsAsync(programCourseId);
        var progressMap = await GetProgressMapAsync(offeredCourseIds);

        var matrix = BuildMatrix(topics, teachers, progressMap);
        var commonTopics = BuildCommonTopics(matrix);
        var gapTopics = BuildGapTopics(matrix, teachers, offeredCourseId);

        var result = new TeacherSynchronizationDto
        {
            CurrentOfferedCourseId = offeredCourseId,
            ProgramCourseId = programCourseId,
            CourseTitle = current.CourseTitle,
            CurrentTeacherName = current.TeacherName,
            CurrentSectionName = current.SectionName,
            Teachers = teachers,
            Matrix = matrix,
            CommonTopics = commonTopics,
            GapTopics = gapTopics
        };

        return Ok(result);
    }

    // Retrieves details of the currently selected offered course from the database
    private async Task<CurrentOfferedCourseInfo?> GetCurrentOfferedCourseAsync(int offeredCourseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                oc.OfferedCourseID,
                oc.CourseID,
                oc.TeacherUserID,
                c.CourseTitle,
                c.CourseCode,
                s.SectionID,
                s.SectionName,
                sess.ProgramID,
                u.FullName AS TeacherName
            FROM OfferedCourses oc
            INNER JOIN Courses c
                ON c.CourseID = oc.CourseID
            INNER JOIN Sections s
                ON s.SectionID = oc.SectionID
            INNER JOIN AcademicSessions sess
                ON sess.SessionID = s.SessionID
            INNER JOIN Users u
                ON u.UserID = oc.TeacherUserID
            WHERE oc.OfferedCourseID = @OfferedCourseID;
        ", connection);

        command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new CurrentOfferedCourseInfo
        {
            OfferedCourseId = Convert.ToInt32(reader["OfferedCourseID"]),
            CourseId = Convert.ToInt32(reader["CourseID"]),
            TeacherUserId = Convert.ToInt32(reader["TeacherUserID"]),
            CourseTitle = Convert.ToString(reader["CourseTitle"]) ?? "",
            CourseCode = Convert.ToString(reader["CourseCode"]) ?? "",
            SectionId = Convert.ToInt32(reader["SectionID"]),
            SectionName = Convert.ToString(reader["SectionName"]) ?? "",
            ProgramId = Convert.ToInt32(reader["ProgramID"]),
            TeacherName = Convert.ToString(reader["TeacherName"]) ?? ""
        };
    }

    // Ensures that a ProgramCourse entry exists and creates it if missing
    private async Task<int> EnsureProgramCourseAsync(int programId, int courseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var selectCommand = new SqlCommand(@"
            SELECT ProgramCourseID
            FROM AcademicProgramCourses
            WHERE ProgramID = @ProgramID
            AND CourseID = @CourseID;
        ", connection);

        selectCommand.Parameters.AddWithValue("@ProgramID", programId);
        selectCommand.Parameters.AddWithValue("@CourseID", courseId);

        var existing = await selectCommand.ExecuteScalarAsync();

        if (existing != null && existing != DBNull.Value)
        {
            return Convert.ToInt32(existing);
        }

        var insertCommand = new SqlCommand(@"
            INSERT INTO AcademicProgramCourses
            (
                ProgramID,
                CourseID
            )
            OUTPUT INSERTED.ProgramCourseID
            VALUES
            (
                @ProgramID,
                @CourseID
            );
        ", connection);

        insertCommand.Parameters.AddWithValue("@ProgramID", programId);
        insertCommand.Parameters.AddWithValue("@CourseID", courseId);

        return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
    }

    // Retrieves all comparable offered courses (teachers/sections) for same program and course
    private async Task<List<SyncTeacherColumnDto>> GetComparableOfferedCoursesAsync(
        int programId,
        int courseId)
    {
        var teachers = new List<SyncTeacherColumnDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                oc.OfferedCourseID,
                oc.TeacherUserID,
                u.FullName AS TeacherName,
                s.SectionName
            FROM OfferedCourses oc
            INNER JOIN Sections s
                ON s.SectionID = oc.SectionID
            INNER JOIN AcademicSessions sess
                ON sess.SessionID = s.SessionID
            INNER JOIN Users u
                ON u.UserID = oc.TeacherUserID
            WHERE oc.CourseID = @CourseID
            AND sess.ProgramID = @ProgramID
            AND NOT EXISTS (
                SELECT 1
                FROM CourseFolders cf
                WHERE cf.OfferedCourseID = oc.OfferedCourseID
                AND cf.FolderStatus = 'Submitted'
            )
            ORDER BY s.SectionName, u.FullName;
        ", connection);

        command.Parameters.AddWithValue("@CourseID", courseId);
        command.Parameters.AddWithValue("@ProgramID", programId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var teacherName = Convert.ToString(reader["TeacherName"]) ?? "";
            var sectionName = Convert.ToString(reader["SectionName"]) ?? "";

            teachers.Add(new SyncTeacherColumnDto
            {
                OfferedCourseId = Convert.ToInt32(reader["OfferedCourseID"]),
                TeacherUserId = Convert.ToInt32(reader["TeacherUserID"]),
                TeacherName = teacherName,
                SectionName = sectionName,
                ColumnLabel = $"{GetShortName(teacherName)}\n{sectionName}"
            });
        }

        return teachers;
    }

    // Ensures progress tracking rows exist for all offered courses and topics
    private async Task EnsureProgressRowsForAllAsync(
        int programCourseId,
        List<int> offeredCourseIds)
    {
        if (offeredCourseIds.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var offeredCourseId in offeredCourseIds)
        {
            var command = new SqlCommand(@"
                INSERT INTO OfferedCourseTopicProgress
                (
                    OfferedCourseID,
                    TopicID,
                    IsCompleted
                )
                SELECT
                    @OfferedCourseID,
                    t.TopicID,
                    0
                FROM CourseTopics t
                WHERE t.ProgramCourseID = @ProgramCourseID
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM OfferedCourseTopicProgress p
                    WHERE p.OfferedCourseID = @OfferedCourseID
                    AND p.TopicID = t.TopicID
                );
            ", connection);

            command.Parameters.AddWithValue("@OfferedCourseID", offeredCourseId);
            command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);

            await command.ExecuteNonQueryAsync();
        }
    }

    // Retrieves leaf-level topics for a given program course
    private async Task<List<TopicInfo>> GetLeafTopicsAsync(int programCourseId)
    {
        var topics = new List<TopicInfo>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT
                t.TopicID,
                t.WeekNo,
                t.TopicTitle,
                t.DisplayOrder
            FROM CourseTopics t
            WHERE t.ProgramCourseID = @ProgramCourseID
            AND NOT EXISTS
            (
                SELECT 1
                FROM CourseTopics child
                WHERE child.ParentTopicID = t.TopicID
            )
            ORDER BY
                ISNULL(t.WeekNo, 999),
                ISNULL(t.DisplayOrder, 999),
                t.TopicID;
        ", connection);

        command.Parameters.AddWithValue("@ProgramCourseID", programCourseId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            topics.Add(new TopicInfo
            {
                TopicId = Convert.ToInt32(reader["TopicID"]),
                WeekNo = reader["WeekNo"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["WeekNo"]),
                TopicName = Convert.ToString(reader["TopicTitle"]) ?? "",
                DisplayOrder = reader["DisplayOrder"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["DisplayOrder"])
            });
        }

        return topics;
    }

    // Builds a dictionary mapping (offeredCourseId + topicId) to completion status
    private async Task<Dictionary<string, bool>> GetProgressMapAsync(
        List<int> offeredCourseIds)
    {
        var map = new Dictionary<string, bool>();

        if (offeredCourseIds.Count == 0)
        {
            return map;
        }

        var ids = string.Join(",", offeredCourseIds);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand($@"
            SELECT
                OfferedCourseID,
                TopicID,
                IsCompleted
            FROM OfferedCourseTopicProgress
            WHERE OfferedCourseID IN ({ids});
        ", connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var offeredCourseId = Convert.ToInt32(reader["OfferedCourseID"]);
            var topicId = Convert.ToInt32(reader["TopicID"]);
            var completed = Convert.ToBoolean(reader["IsCompleted"]);

            map[$"{offeredCourseId}|{topicId}"] = completed;
        }

        return map;
    }

    // Builds matrix representation of topic completion across all teachers
    private static List<SyncMatrixRowDto> BuildMatrix(
        List<TopicInfo> topics,
        List<SyncTeacherColumnDto> teachers,
        Dictionary<string, bool> progressMap)
    {
        var matrix = new List<SyncMatrixRowDto>();

        foreach (var topic in topics)
        {
            var row = new SyncMatrixRowDto
            {
                TopicId = topic.TopicId,
                WeekNo = topic.WeekNo,
                TopicName = topic.TopicName
            };

            foreach (var teacher in teachers)
            {
                var key = $"{teacher.OfferedCourseId}|{topic.TopicId}";
                var completed = progressMap.TryGetValue(key, out var value) && value;

                row.Statuses.Add(new SyncTopicStatusDto
                {
                    OfferedCourseId = teacher.OfferedCourseId,
                    TeacherUserId = teacher.TeacherUserId,
                    TeacherName = teacher.TeacherName,
                    SectionName = teacher.SectionName,
                    IsCompleted = completed
                });
            }

            matrix.Add(row);
        }

        return matrix;
    }

    // Extracts topics that are common (completed by all teachers)
    private static List<SyncCommonTopicDto> BuildCommonTopics(
        List<SyncMatrixRowDto> matrix)
    {
        return matrix
            .Where(row =>
                row.Statuses.Count > 0 &&
                row.Statuses.All(status => status.IsCompleted))
            .Select(row => new SyncCommonTopicDto
            {
                TopicId = row.TopicId,
                WeekNo = row.WeekNo,
                TopicName = row.TopicName,
                StatusLabel = "COMMON"
            })
            .ToList();
    }

    // Identifies topics missing in current teacher but covered by others (gap analysis)
    private static List<SyncGapTopicDto> BuildGapTopics(
        List<SyncMatrixRowDto> matrix,
        List<SyncTeacherColumnDto> teachers,
        int currentOfferedCourseId)
    {
        var currentTeacher = teachers
            .FirstOrDefault(x => x.OfferedCourseId == currentOfferedCourseId);

        if (currentTeacher == null)
        {
            return new List<SyncGapTopicDto>();
        }

        var gapTopics = new List<SyncGapTopicDto>();

        foreach (var row in matrix)
        {
            var currentStatus = row.Statuses
                .FirstOrDefault(x => x.OfferedCourseId == currentOfferedCourseId);

            if (currentStatus == null || currentStatus.IsCompleted)
            {
                continue;
            }

            var coveredByOthers = row.Statuses
                .Where(x =>
                    x.OfferedCourseId != currentOfferedCourseId &&
                    x.IsCompleted)
                .ToList();

            if (coveredByOthers.Count == 0)
            {
                continue;
            }

            var totalOtherTeachers = Math.Max(row.Statuses.Count - 1, 1);

            gapTopics.Add(new SyncGapTopicDto
            {
                TopicId = row.TopicId,
                WeekNo = row.WeekNo,
                TopicName = row.TopicName,
                MissingInTeacher = currentTeacher.TeacherName,
                MissingInSection = currentTeacher.SectionName,
                CoveredByCount = coveredByOthers.Count,
                GapLevel = coveredByOthers.Count == totalOtherTeachers
                    ? "HIGH GAP"
                    : "GAP",
                CoveredByTeachers = coveredByOthers
                    .Select(x => $"{x.TeacherName} - {x.SectionName}")
                    .ToList()
            });
        }

        return gapTopics;
    }

    // Returns a shortened uppercase version of a full name (usually last name)
    private static string GetShortName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "";
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
        {
            return parts[0].ToUpper();
        }

        return parts[^1].ToUpper();
    }

    // Holds details of the currently selected offered course
    private class CurrentOfferedCourseInfo
    {
        public int OfferedCourseId { get; set; }
        public int CourseId { get; set; }
        public int TeacherUserId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public int SectionId { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public int ProgramId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
    }

    // Represents topic information used in synchronization matrix
    private class TopicInfo
    {
        public int TopicId { get; set; }
        public int? WeekNo { get; set; }
        public string TopicName { get; set; } = string.Empty;
        public int? DisplayOrder { get; set; }
    }
}