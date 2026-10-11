USE FolderManagementDB;
GO

/* =========================================================
   CLO attainment support
   - Session scope for CourseAssessmentTypeCLOMappings
   - Matches session-scoped CLOs used by the app
   ========================================================= */

IF COL_LENGTH('dbo.CourseAssessmentTypeCLOMappings', 'SessionID') IS NULL
BEGIN
    ALTER TABLE dbo.CourseAssessmentTypeCLOMappings
    ADD SessionID INT NULL;
END
GO

-- Backfill SessionID from the program's academic sessions (first session as fallback).
-- Prefer session linked through offered courses of the same course when possible.
UPDATE m
SET m.SessionID = x.SessionID
FROM dbo.CourseAssessmentTypeCLOMappings m
INNER JOIN
(
    SELECT
        pc.ProgramCourseID,
        MIN(s.SessionID) AS SessionID
    FROM dbo.AcademicProgramCourses pc
    INNER JOIN dbo.AcademicSessions s ON s.ProgramID = pc.ProgramID
    GROUP BY pc.ProgramCourseID
) x ON x.ProgramCourseID = m.ProgramCourseID
WHERE m.SessionID IS NULL;
GO

-- If any rows remain without a session, they cannot participate in session-scoped design.
-- Leave them NULL until cleaned; new writes will always set SessionID.

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UQ__CourseAs__19B7B721430939FC'
      AND object_id = OBJECT_ID('dbo.CourseAssessmentTypeCLOMappings')
)
BEGIN
    ALTER TABLE dbo.CourseAssessmentTypeCLOMappings
    DROP CONSTRAINT UQ__CourseAs__19B7B721430939FC;
END
GO

-- Drop any other unique on (ProgramCourseID, AssessmentTypeID, CLOID) without SessionID
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + N'ALTER TABLE dbo.CourseAssessmentTypeCLOMappings DROP CONSTRAINT [' + kc.name + N'];'
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID('dbo.CourseAssessmentTypeCLOMappings')
  AND kc.type = 'UQ'
GROUP BY kc.name
HAVING SUM(CASE WHEN c.name IN ('ProgramCourseID','AssessmentTypeID','CLOID') THEN 1 ELSE 0 END) = 3
   AND SUM(CASE WHEN c.name = 'SessionID' THEN 1 ELSE 0 END) = 0;

IF LEN(@sql) > 0 EXEC sp_executesql @sql;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UQ_CourseAssessmentTypeCLOMappings_Session'
      AND object_id = OBJECT_ID('dbo.CourseAssessmentTypeCLOMappings')
)
BEGIN
    CREATE UNIQUE INDEX UQ_CourseAssessmentTypeCLOMappings_Session
    ON dbo.CourseAssessmentTypeCLOMappings (ProgramCourseID, SessionID, AssessmentTypeID, CLOID)
    WHERE SessionID IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_CourseAssessmentTypeCLOMappings_Session'
)
BEGIN
    ALTER TABLE dbo.CourseAssessmentTypeCLOMappings
    ADD CONSTRAINT FK_CourseAssessmentTypeCLOMappings_Session
    FOREIGN KEY (SessionID) REFERENCES dbo.AcademicSessions(SessionID);
END
GO

PRINT 'CourseAssessmentTypeCLOMappings SessionID migration complete.';
GO
