/*
  Optional performance table for HOD Courses dashboard.
  Run once, then call sp_RefreshHodOfferingProgressCache after topic/permission changes
  or on a schedule (e.g. every 15 minutes).

  The API endpoint GET /api/HodDashboard/{hodUserId}/courses-dashboard
  uses in-memory batching by default. This table is for environments with
  many offerings where you want faster repeat reads.
*/

IF OBJECT_ID(N'dbo.HodOfferingProgressCache', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HodOfferingProgressCache (
        OfferedCourseID       INT            NOT NULL PRIMARY KEY,
        CourseID              INT            NOT NULL,
        ProgramID             INT            NOT NULL,
        ProgramCourseID       INT            NOT NULL,
        TeacherUserID         INT            NOT NULL,
        TeacherName           NVARCHAR(200)  NOT NULL,
        SectionName           NVARCHAR(100)  NOT NULL,
        ProgramName           NVARCHAR(200)  NOT NULL,
        IsSectionA            BIT            NOT NULL DEFAULT 0,
        TotalTopics           INT            NOT NULL DEFAULT 0,
        CoveredTopics         INT            NOT NULL DEFAULT 0,
        GapTopics             INT            NOT NULL DEFAULT 0,
        SyncScore             DECIMAL(5, 2)  NOT NULL DEFAULT 0,
        ProgressPercentage    DECIMAL(5, 2)  NOT NULL DEFAULT 0,
        HasWeeklyContent      BIT            NOT NULL DEFAULT 0,
        DeviationLabel        NVARCHAR(50)   NOT NULL DEFAULT '',
        FolderStatus          NVARCHAR(50)   NOT NULL DEFAULT 'Open',
        CachedAtUtc           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_HodOfferingProgressCache_CourseID
        ON dbo.HodOfferingProgressCache (CourseID);

    CREATE INDEX IX_HodOfferingProgressCache_ProgramCourseID
        ON dbo.HodOfferingProgressCache (ProgramCourseID);
END
GO

IF OBJECT_ID(N'dbo.sp_RefreshHodOfferingProgressCache', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_RefreshHodOfferingProgressCache;
GO

CREATE PROCEDURE dbo.sp_RefreshHodOfferingProgressCache
AS
BEGIN
    SET NOCOUNT ON;

    TRUNCATE TABLE dbo.HodOfferingProgressCache;

    ;WITH LeafTopics AS (
        SELECT t.ProgramCourseID, t.TopicID
        FROM CourseTopics t
        WHERE NOT EXISTS (
            SELECT 1 FROM CourseTopics child WHERE child.ParentTopicID = t.TopicID
        )
    ),
    OfferingBase AS (
        SELECT
            oc.OfferedCourseID,
            oc.CourseID,
            sess.ProgramID,
            apc.ProgramCourseID,
            oc.TeacherUserID,
            u.FullName AS TeacherName,
            s.SectionName,
            p.ProgramName,
            CASE
                WHEN s.SectionName LIKE '%-A' OR s.SectionName = 'A' THEN 1
                WHEN LEN(s.SectionName) >= 2 AND RIGHT(s.SectionName, 1) = 'A'
                     AND ISNUMERIC(SUBSTRING(s.SectionName, LEN(s.SectionName) - 1, 1)) = 1 THEN 1
                ELSE 0
            END AS IsSectionA,
            ISNULL(cf.FolderStatus, 'Open') AS FolderStatus
        FROM OfferedCourses oc
        INNER JOIN Sections s ON s.SectionID = oc.SectionID
        INNER JOIN AcademicSessions sess ON sess.SessionID = s.SessionID
        INNER JOIN AcademicPrograms p ON p.ProgramID = sess.ProgramID
        INNER JOIN Users u ON u.UserID = oc.TeacherUserID
        LEFT JOIN AcademicProgramCourses apc
            ON apc.ProgramID = sess.ProgramID AND apc.CourseID = oc.CourseID
        LEFT JOIN CourseFolders cf ON cf.OfferedCourseID = oc.OfferedCourseID
        WHERE ISNULL(cf.FolderStatus, 'Open') <> 'Submitted'
    ),
    TopicTotals AS (
        SELECT ProgramCourseID, COUNT(*) AS TotalTopics
        FROM LeafTopics
        GROUP BY ProgramCourseID
    ),
    Covered AS (
        SELECT
            p.OfferedCourseID,
            lt.ProgramCourseID,
            COUNT(*) AS CoveredTopics
        FROM OfferedCourseTopicProgress p
        INNER JOIN LeafTopics lt ON lt.TopicID = p.TopicID
        WHERE p.IsCompleted = 1
        GROUP BY p.OfferedCourseID, lt.ProgramCourseID
    )
    INSERT INTO dbo.HodOfferingProgressCache (
        OfferedCourseID, CourseID, ProgramID, ProgramCourseID,
        TeacherUserID, TeacherName, SectionName, ProgramName, IsSectionA,
        TotalTopics, CoveredTopics, GapTopics, SyncScore, ProgressPercentage,
        HasWeeklyContent, DeviationLabel, FolderStatus, CachedAtUtc
    )
    SELECT
        ob.OfferedCourseID,
        ob.CourseID,
        ob.ProgramID,
        ob.ProgramCourseID,
        ob.TeacherUserID,
        ob.TeacherName,
        ob.SectionName,
        ob.ProgramName,
        ob.IsSectionA,
        ISNULL(tt.TotalTopics, 0),
        ISNULL(c.CoveredTopics, 0),
        0 AS GapTopics,
        CASE
            WHEN ISNULL(tt.TotalTopics, 0) = 0 THEN 0
            ELSE CAST(100.0 AS DECIMAL(5, 2))
        END AS SyncScore,
        CASE
            WHEN ISNULL(tt.TotalTopics, 0) = 0 THEN 0
            ELSE CAST(100.0 * ISNULL(c.CoveredTopics, 0) / tt.TotalTopics AS DECIMAL(5, 2))
        END AS ProgressPercentage,
        CASE WHEN ISNULL(tt.TotalTopics, 0) > 0 THEN 1 ELSE 0 END,
        CASE
            WHEN ISNULL(tt.TotalTopics, 0) = 0 THEN 'No Topics'
            ELSE 'None'
        END,
        ob.FolderStatus,
        SYSUTCDATETIME()
    FROM OfferingBase ob
    LEFT JOIN TopicTotals tt ON tt.ProgramCourseID = ob.ProgramCourseID
    LEFT JOIN Covered c
        ON c.OfferedCourseID = ob.OfferedCourseID
       AND c.ProgramCourseID = ob.ProgramCourseID;
END
GO
