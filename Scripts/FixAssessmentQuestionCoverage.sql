USE FolderManagementDB;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =========================================================
   FixAssessmentQuestionCoverage.sql
   Aligns existing test data with question CLO coverage %:
     - Converts student scores to 0–100% of each question
     - Clears question Marks / assessment TotalMarks
     - Rebuilds AssessmentQuestionCLOMappings.Percentage for OC1
   Safe to re-run.
   ========================================================= */

PRINT 'Converting student marks to question % where max marks exist...';
UPDATE sqm
SET sqm.ObtainedMarks = ROUND((sqm.ObtainedMarks / q.Marks) * 100.0, 2)
FROM StudentQuestionMarks sqm
INNER JOIN AssessmentQuestions q ON q.QuestionID = sqm.QuestionID
WHERE q.Marks IS NOT NULL AND q.Marks > 0;

PRINT 'Clearing question max marks...';
UPDATE AssessmentQuestions SET Marks = NULL WHERE Marks IS NOT NULL;

PRINT 'Clearing assessment TotalMarks...';
UPDATE Assessments
SET TotalMarks = NULL
WHERE TotalMarks IS NOT NULL
  AND AssessmentTypeID IN (
      SELECT AssessmentTypeID FROM AssessmentTypes
      WHERE AssessmentTypeName IN (N'Quiz', N'Assignment', N'Mid', N'Final')
  );

DECLARE @OcA INT = 1;
DECLARE @SessionId INT = 1;
DECLARE @Pc261 INT = 114;

DECLARE @TypeAssign INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Assignment');
DECLARE @TypeQuiz INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Quiz');
DECLARE @TypeMid INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Mid');
DECLARE @TypeFinal INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Final');

DECLARE @Clo1 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO1');
DECLARE @Clo2 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO2');
DECLARE @Clo3 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO3');
DECLARE @Clo4 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO4');
DECLARE @Clo5 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO5');
DECLARE @Clo6 INT = (SELECT TOP 1 CLOID FROM CLOs WHERE ProgramCourseID = @Pc261 AND SessionID = @SessionId AND CLOCode = N'CLO6');

PRINT 'Rebuilding OC1 question CLO coverage mappings...';

-- Remove existing mappings for OC1 assessment questions, then re-insert with percentages
DELETE m
FROM AssessmentQuestionCLOMappings m
INNER JOIN AssessmentQuestions q ON q.QuestionID = m.QuestionID
INNER JOIN Assessments a ON a.AssessmentID = q.AssessmentID
WHERE a.OfferedCourseID = @OcA;

INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
SELECT q.QuestionID, map.CLOID, map.Percentage
FROM AssessmentQuestions q
INNER JOIN Assessments a ON a.AssessmentID = q.AssessmentID
CROSS APPLY (
    SELECT *
    FROM (VALUES
        -- Quiz 1: Clo1 full quiz bucket
        (@TypeQuiz, 1, N'Q1', @Clo1, CAST(50 AS decimal(5,2))),
        (@TypeQuiz, 1, N'Q2', @Clo1, CAST(50 AS decimal(5,2))),
        -- Quizzes 2–8: Clo4 + Clo2
        (@TypeQuiz, 2, N'Q1', @Clo4, CAST(14 AS decimal(5,2))),
        (@TypeQuiz, 2, N'Q2', @Clo2, CAST(12.5 AS decimal(5,2))),
        (@TypeQuiz, 3, N'Q1', @Clo4, CAST(14 AS decimal(5,2))),
        (@TypeQuiz, 3, N'Q2', @Clo2, CAST(12.5 AS decimal(5,2))),
        (@TypeQuiz, 4, N'Q1', @Clo4, CAST(14 AS decimal(5,2))),
        (@TypeQuiz, 4, N'Q2', @Clo2, CAST(12.5 AS decimal(5,2))),
        (@TypeQuiz, 5, N'Q1', @Clo4, CAST(14 AS decimal(5,2))),
        (@TypeQuiz, 5, N'Q2', @Clo2, CAST(12.5 AS decimal(5,2))),
        (@TypeQuiz, 6, N'Q1', @Clo4, CAST(14 AS decimal(5,2))),
        (@TypeQuiz, 6, N'Q2', @Clo2, CAST(12.5 AS decimal(5,2))),
        (@TypeQuiz, 7, N'Q1', @Clo4, CAST(14 AS decimal(5,2))),
        (@TypeQuiz, 7, N'Q2', @Clo2, CAST(12.5 AS decimal(5,2))),
        (@TypeQuiz, 8, N'Q1', @Clo4, CAST(16 AS decimal(5,2))),
        (@TypeQuiz, 8, N'Q2', @Clo2, CAST(12.5 AS decimal(5,2))),
        -- Assignment 1
        (@TypeAssign, 1, N'Q1', @Clo2, CAST(60 AS decimal(5,2))),
        (@TypeAssign, 1, N'Q2', @Clo3, CAST(100 AS decimal(5,2))),
        -- Assignments 2–4
        (@TypeAssign, 2, N'Q1', @Clo5, CAST(33 AS decimal(5,2))),
        (@TypeAssign, 2, N'Q2', @Clo2, CAST(13 AS decimal(5,2))),
        (@TypeAssign, 3, N'Q1', @Clo5, CAST(33 AS decimal(5,2))),
        (@TypeAssign, 3, N'Q2', @Clo2, CAST(13 AS decimal(5,2))),
        (@TypeAssign, 4, N'Q1', @Clo5, CAST(34 AS decimal(5,2))),
        (@TypeAssign, 4, N'Q2', @Clo2, CAST(14 AS decimal(5,2))),
        -- Mid
        (@TypeMid, 1, N'Q1', @Clo1, CAST(50 AS decimal(5,2))),
        (@TypeMid, 1, N'Q1', @Clo2, CAST(40 AS decimal(5,2))),
        (@TypeMid, 1, N'Q2', @Clo2, CAST(60 AS decimal(5,2))),
        (@TypeMid, 1, N'Q2', @Clo6, CAST(100 AS decimal(5,2))),
        -- Final
        (@TypeFinal, 1, N'Q1', @Clo2, CAST(50 AS decimal(5,2))),
        (@TypeFinal, 1, N'Q1', @Clo6, CAST(50 AS decimal(5,2))),
        (@TypeFinal, 1, N'Q2', @Clo3, CAST(50 AS decimal(5,2))),
        (@TypeFinal, 1, N'Q2', @Clo6, CAST(50 AS decimal(5,2)))
    ) AS v(AssessmentTypeID, AssessmentNo, QuestionNo, CLOID, Percentage)
    WHERE v.AssessmentTypeID = a.AssessmentTypeID
      AND v.AssessmentNo = a.AssessmentNo
      AND v.QuestionNo = q.QuestionNo
      AND v.CLOID IS NOT NULL
) AS map
WHERE a.OfferedCourseID = @OcA;

-- Any other offerings: equal-split missing percentages
;WITH NeedPct AS (
    SELECT
        m.QuestionCLOMappingID,
        COUNT(*) OVER (
            PARTITION BY a.OfferedCourseID, a.AssessmentTypeID, m.CLOID
        ) AS Cnt
    FROM AssessmentQuestionCLOMappings m
    INNER JOIN AssessmentQuestions q ON q.QuestionID = m.QuestionID
    INNER JOIN Assessments a ON a.AssessmentID = q.AssessmentID
    WHERE (m.Percentage IS NULL OR m.Percentage <= 0)
)
UPDATE m
SET m.Percentage = ROUND(100.0 / NULLIF(n.Cnt, 0), 2)
FROM AssessmentQuestionCLOMappings m
INNER JOIN NeedPct n ON n.QuestionCLOMappingID = m.QuestionCLOMappingID;

PRINT 'Summary by type:';
SELECT
    at.AssessmentTypeName,
    COUNT(*) AS MappingCount,
    SUM(CASE WHEN m.Percentage IS NULL OR m.Percentage <= 0 THEN 1 ELSE 0 END) AS MissingPct,
    CAST(MIN(m.Percentage) AS decimal(5,2)) AS MinPct,
    CAST(MAX(m.Percentage) AS decimal(5,2)) AS MaxPct
FROM AssessmentQuestionCLOMappings m
INNER JOIN AssessmentQuestions q ON q.QuestionID = m.QuestionID
INNER JOIN Assessments a ON a.AssessmentID = q.AssessmentID
INNER JOIN AssessmentTypes at ON at.AssessmentTypeID = a.AssessmentTypeID
GROUP BY at.AssessmentTypeName
ORDER BY at.AssessmentTypeName;

PRINT 'OC1 coverage totals (should be ≤ 100 per CLO per type):';
SELECT
    at.AssessmentTypeName,
    c.CLOCode,
    CAST(SUM(m.Percentage) AS decimal(6,2)) AS TotalCoveragePct
FROM AssessmentQuestionCLOMappings m
INNER JOIN AssessmentQuestions q ON q.QuestionID = m.QuestionID
INNER JOIN Assessments a ON a.AssessmentID = q.AssessmentID
INNER JOIN AssessmentTypes at ON at.AssessmentTypeID = a.AssessmentTypeID
INNER JOIN CLOs c ON c.CLOID = m.CLOID
WHERE a.OfferedCourseID = @OcA
GROUP BY at.AssessmentTypeName, c.CLOCode
ORDER BY at.AssessmentTypeName, c.CLOCode;

PRINT 'FixAssessmentQuestionCoverage complete.';
GO
