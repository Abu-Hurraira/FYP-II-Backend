/*
  SeedCS3MidFinalAndMarks.sql
  Computer Science 3 (666/667/668):
    - Mid + Final (Theory; Lab if template exists) with full CLO coverage, setup complete
    - Random student marks on Assignment, Quiz, Mid, Final (all 3 students per course)
*/
USE FolderManagementDB;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @TeacherId INT = 9;
DECLARE @SessionId INT = 8;
DECLARE @TypeAssign INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Assignment');
DECLARE @TypeQuiz INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Quiz');
DECLARE @TypeMid INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Mid');
DECLARE @TypeFinal INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Final');

DECLARE @OCs TABLE (
    OfferedCourseID INT PRIMARY KEY,
    ProgramCourseID INT NOT NULL,
    CourseCode NVARCHAR(50) NOT NULL,
    HasLab BIT NOT NULL
);
INSERT INTO @OCs (OfferedCourseID, ProgramCourseID, CourseCode, HasLab)
SELECT o.OfferedCourseID, o.ProgramCourseID, o.CourseCode,
       CASE WHEN ISNULL(c.LabCreditHours, 0) > 0 THEN 1 ELSE 0 END
FROM (VALUES
    (666, 472, N'CAI-262'),
    (667, 470, N'MTH-502'),
    (668, 471, N'CAI-261')
) o(OfferedCourseID, ProgramCourseID, CourseCode)
JOIN OfferedCourses oc ON oc.OfferedCourseID = o.OfferedCourseID
JOIN Courses c ON c.CourseID = oc.CourseID;

/* ---------- Wipe Mid/Final questions on these OCs ---------- */
DECLARE @ExamA TABLE (AssessmentID INT PRIMARY KEY);
INSERT INTO @ExamA (AssessmentID)
SELECT a.AssessmentID
FROM Assessments a
JOIN @OCs o ON o.OfferedCourseID = a.OfferedCourseID
WHERE a.AssessmentTypeID IN (@TypeMid, @TypeFinal);

DECLARE @ExamQ TABLE (QuestionID INT PRIMARY KEY);
INSERT INTO @ExamQ (QuestionID)
SELECT q.QuestionID FROM AssessmentQuestions q WHERE q.AssessmentID IN (SELECT AssessmentID FROM @ExamA);

DELETE FROM StudentQuestionMarks WHERE QuestionID IN (SELECT QuestionID FROM @ExamQ);
DELETE FROM AssessmentQuestionCLOMappings WHERE QuestionID IN (SELECT QuestionID FROM @ExamQ);
DELETE FROM AssessmentQuestions WHERE QuestionID IN (SELECT QuestionID FROM @ExamQ);
DELETE FROM StudentAssessmentMarks WHERE AssessmentID IN (SELECT AssessmentID FROM @ExamA);

DECLARE @Cleared INT = (SELECT COUNT(*) FROM @ExamA);
PRINT CONCAT('Cleared Mid/Final content on ', @Cleared, ' assessments.');

/* ---------- Ensure Mid + Final shells ---------- */
DECLARE @OcId INT, @PcId INT, @Code NVARCHAR(50), @HasLab BIT;
DECLARE oc_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT OfferedCourseID, ProgramCourseID, CourseCode, HasLab FROM @OCs;
OPEN oc_cur;
FETCH NEXT FROM oc_cur INTO @OcId, @PcId, @Code, @HasLab;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM Assessments
        WHERE OfferedCourseID = @OcId AND AssessmentTypeID = @TypeMid AND AssessmentNo = 1)
    BEGIN
        INSERT INTO Assessments
            (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks,
             CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
        VALUES
            (@OcId, @TypeMid, 1, N'Mid Exams', NULL, 40, @TeacherId, GETDATE(), 0, NULL);
    END

    IF NOT EXISTS (
        SELECT 1 FROM Assessments
        WHERE OfferedCourseID = @OcId AND AssessmentTypeID = @TypeFinal AND AssessmentNo = 1)
    BEGIN
        INSERT INTO Assessments
            (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks,
             CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
        VALUES
            (@OcId, @TypeFinal, 1, N'Final Theory', NULL, 60, @TeacherId, GETDATE(), 0, NULL);
    END

    IF @HasLab = 1 AND NOT EXISTS (
        SELECT 1 FROM Assessments
        WHERE OfferedCourseID = @OcId AND AssessmentTypeID = @TypeFinal AND AssessmentNo = 2)
    BEGIN
        INSERT INTO Assessments
            (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks,
             CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
        VALUES
            (@OcId, @TypeFinal, 2, N'Final Lab', NULL, 40, @TeacherId, GETDATE(), 0, NULL);
    END

    FETCH NEXT FROM oc_cur INTO @OcId, @PcId, @Code, @HasLab;
END
CLOSE oc_cur;
DEALLOCATE oc_cur;

/* ---------- Question plan: Mid 4 Qs; Final Theory 4 Qs; Final Lab 2 Qs ---------- */
DECLARE @Plan TABLE (
    RowNum INT IDENTITY(1,1) PRIMARY KEY,
    OfferedCourseID INT,
    ProgramCourseID INT,
    CourseCode NVARCHAR(50),
    AssessmentTypeID INT,
    AssessmentNo INT,
    AssessmentID INT NULL,
    QIndex INT,
    QuestionNo NVARCHAR(20),
    QuestionText NVARCHAR(MAX),
    Marks DECIMAL(6,2)
);

-- Mid: 4 questions, paper 10 each, assessment total 40
INSERT INTO @Plan (OfferedCourseID, ProgramCourseID, CourseCode, AssessmentTypeID, AssessmentNo, QIndex, QuestionNo, QuestionText, Marks)
SELECT o.OfferedCourseID, o.ProgramCourseID, o.CourseCode, @TypeMid, 1, q.QIndex,
       CONCAT(N'Q', q.QIndex),
       CASE q.QIndex
           WHEN 1 THEN CONCAT(N'Mid Q1 (', o.CourseCode, N'): Explain a core concept from Topic 1 — Foundations.')
           WHEN 2 THEN CONCAT(N'Mid Q2 (', o.CourseCode, N'): Short problem on Topic 1.1 — Core concepts. Show steps.')
           WHEN 3 THEN CONCAT(N'Mid Q3 (', o.CourseCode, N'): Apply Topic 2 — Practice & applications to a scenario.')
           ELSE CONCAT(N'Mid Q4 (', o.CourseCode, N'): Assessment readiness: compare two approaches from the course.')
       END,
       10.00
FROM @OCs o
CROSS JOIN (VALUES (1),(2),(3),(4)) q(QIndex);

-- Final Theory: 4 questions, paper 15 each, assessment total 60
INSERT INTO @Plan (OfferedCourseID, ProgramCourseID, CourseCode, AssessmentTypeID, AssessmentNo, QIndex, QuestionNo, QuestionText, Marks)
SELECT o.OfferedCourseID, o.ProgramCourseID, o.CourseCode, @TypeFinal, 1, q.QIndex,
       CONCAT(N'Q', q.QIndex),
       CASE q.QIndex
           WHEN 1 THEN CONCAT(N'Final Theory Q1 (', o.CourseCode, N'): Comprehensive question on Foundations.')
           WHEN 2 THEN CONCAT(N'Final Theory Q2 (', o.CourseCode, N'): Design/problem on Core concepts.')
           WHEN 3 THEN CONCAT(N'Final Theory Q3 (', o.CourseCode, N'): Application from Practice & applications.')
           ELSE CONCAT(N'Final Theory Q4 (', o.CourseCode, N'): Synthesis / assessment readiness essay.')
       END,
       15.00
FROM @OCs o
CROSS JOIN (VALUES (1),(2),(3),(4)) q(QIndex);

-- Final Lab (lab courses only): 2 questions, paper 20 each, total 40
INSERT INTO @Plan (OfferedCourseID, ProgramCourseID, CourseCode, AssessmentTypeID, AssessmentNo, QIndex, QuestionNo, QuestionText, Marks)
SELECT o.OfferedCourseID, o.ProgramCourseID, o.CourseCode, @TypeFinal, 2, q.QIndex,
       CONCAT(N'Q', q.QIndex),
       CASE q.QIndex
           WHEN 1 THEN CONCAT(N'Final Lab Q1 (', o.CourseCode, N'): Implement / demonstrate a lab task from Topic 2.')
           ELSE CONCAT(N'Final Lab Q2 (', o.CourseCode, N'): Debug and explain results for a lab scenario.')
       END,
       20.00
FROM @OCs o
CROSS JOIN (VALUES (1),(2)) q(QIndex)
WHERE o.HasLab = 1
  AND EXISTS (
      SELECT 1 FROM Assessments a
      WHERE a.OfferedCourseID = o.OfferedCourseID AND a.AssessmentTypeID = @TypeFinal AND a.AssessmentNo = 2);

UPDATE p
SET AssessmentID = a.AssessmentID
FROM @Plan p
JOIN Assessments a ON a.OfferedCourseID = p.OfferedCourseID
    AND a.AssessmentTypeID = p.AssessmentTypeID
    AND a.AssessmentNo = p.AssessmentNo;

IF EXISTS (SELECT 1 FROM @Plan WHERE AssessmentID IS NULL)
BEGIN
    RAISERROR(N'Missing Mid/Final assessment shells.', 16, 1);
    RETURN;
END

/* ---------- Update headers + setup complete ---------- */
UPDATE a
SET
    Title = CASE
        WHEN a.AssessmentTypeID = @TypeMid THEN N'Mid Exams'
        WHEN a.AssessmentNo = 2 THEN N'Final Lab'
        ELSE N'Final Theory'
    END,
    DueDate = NULL,
    TotalMarks = CASE
        WHEN a.AssessmentTypeID = @TypeMid THEN 40.00
        WHEN a.AssessmentNo = 2 THEN 40.00
        ELSE 60.00
    END,
    IsSetupComplete = 1,
    SetupCompletedAt = GETDATE()
FROM Assessments a
WHERE a.AssessmentID IN (SELECT DISTINCT AssessmentID FROM @Plan);

/* ---------- Insert questions ---------- */
DECLARE @NewQ TABLE (PlanRow INT PRIMARY KEY, QuestionID INT NOT NULL);
DECLARE @PlanRow INT, @AsmId INT, @QNo NVARCHAR(20), @QText NVARCHAR(MAX), @QMarks DECIMAL(6,2), @NewQid INT;

DECLARE q_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT RowNum, AssessmentID, QuestionNo, QuestionText, Marks FROM @Plan ORDER BY RowNum;
OPEN q_cur;
FETCH NEXT FROM q_cur INTO @PlanRow, @AsmId, @QNo, @QText, @QMarks;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO AssessmentQuestions (AssessmentID, ParentQuestionID, DisplayOrder, QuestionNo, QuestionText, Marks)
    VALUES (@AsmId, NULL, @PlanRow % 10 + 1, @QNo, @QText, @QMarks);
    SET @NewQid = SCOPE_IDENTITY();
    INSERT INTO @NewQ (PlanRow, QuestionID) VALUES (@PlanRow, @NewQid);
    FETCH NEXT FROM q_cur INTO @PlanRow, @AsmId, @QNo, @QText, @QMarks;
END
CLOSE q_cur;
DEALLOCATE q_cur;

DECLARE @InsertedQ INT;
SELECT @InsertedQ = COUNT(*) FROM @NewQ;
PRINT CONCAT('Inserted Mid/Final questions: ', @InsertedQ);

/* Fix display order properly */
UPDATE q
SET DisplayOrder = p.QIndex
FROM AssessmentQuestions q
JOIN @NewQ n ON n.QuestionID = q.QuestionID
JOIN @Plan p ON p.RowNum = n.PlanRow;

/* ---------- CLO coverage 100% per Mid/Final design share ---------- */
DECLARE @CloShare TABLE (
    ProgramCourseID INT,
    AssessmentTypeID INT,
    CloId INT,
    CloCode NVARCHAR(20)
);
INSERT INTO @CloShare (ProgramCourseID, AssessmentTypeID, CloId, CloCode)
SELECT m.ProgramCourseID, m.AssessmentTypeID, m.CLOID, clo.CLOCode
FROM CourseAssessmentTypeCLOMappings m
JOIN CLOs clo ON clo.CLOID = m.CLOID
JOIN @OCs o ON o.ProgramCourseID = m.ProgramCourseID
WHERE m.SessionID = @SessionId
  AND m.ContentStatus = N'Approved'
  AND m.AssessmentTypeID IN (@TypeMid, @TypeFinal)
  AND m.Percentage IS NOT NULL
  AND m.Percentage > 0;

DECLARE @TypeQuestions TABLE (
    Seq INT IDENTITY(1,1) PRIMARY KEY,
    ProgramCourseID INT,
    AssessmentTypeID INT,
    QuestionID INT,
    Ordinal INT
);
INSERT INTO @TypeQuestions (ProgramCourseID, AssessmentTypeID, QuestionID, Ordinal)
SELECT
    p.ProgramCourseID,
    p.AssessmentTypeID,
    nq.QuestionID,
    ROW_NUMBER() OVER (
        PARTITION BY p.ProgramCourseID, p.AssessmentTypeID
        ORDER BY p.AssessmentNo, p.QIndex, p.RowNum
    )
FROM @Plan p
JOIN @NewQ nq ON nq.PlanRow = p.RowNum;

DECLARE @Pc INT, @TypeId INT, @CloId INT, @QCount INT, @Base INT, @Rem INT, @i INT, @Qid INT, @Pct DECIMAL(6,2);
DECLARE clo_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT ProgramCourseID, AssessmentTypeID, CloId FROM @CloShare ORDER BY 1, 2, CloCode;
OPEN clo_cur;
FETCH NEXT FROM clo_cur INTO @Pc, @TypeId, @CloId;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @QCount = COUNT(*) FROM @TypeQuestions WHERE ProgramCourseID = @Pc AND AssessmentTypeID = @TypeId;
    IF @QCount > 0
    BEGIN
        SET @Base = 100 / @QCount;
        SET @Rem = 100 - (@Base * @QCount);
        SET @i = 1;
        WHILE @i <= @QCount
        BEGIN
            SELECT @Qid = QuestionID
            FROM @TypeQuestions
            WHERE ProgramCourseID = @Pc AND AssessmentTypeID = @TypeId AND Ordinal = @i;

            IF @i <= @Rem SET @Pct = @Base + 1; ELSE SET @Pct = @Base;

            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Qid, @CloId, @Pct);

            SET @i = @i + 1;
        END
    END
    FETCH NEXT FROM clo_cur INTO @Pc, @TypeId, @CloId;
END
CLOSE clo_cur;
DEALLOCATE clo_cur;

PRINT N'Mid/Final CLO coverage applied.';

/* ---------- Random marks for ALL gradeable questions (Assign/Quiz/Mid/Final) ---------- */
-- Clear existing marks on these OCs for these types
DECLARE @AllAsm TABLE (AssessmentID INT PRIMARY KEY);
INSERT INTO @AllAsm (AssessmentID)
SELECT a.AssessmentID
FROM Assessments a
JOIN @OCs o ON o.OfferedCourseID = a.OfferedCourseID
WHERE a.AssessmentTypeID IN (@TypeAssign, @TypeQuiz, @TypeMid, @TypeFinal)
  AND a.IsSetupComplete = 1;

DECLARE @AllQ TABLE (QuestionID INT PRIMARY KEY, AssessmentID INT, MaxMarks DECIMAL(6,2));
INSERT INTO @AllQ (QuestionID, AssessmentID, MaxMarks)
SELECT q.QuestionID, q.AssessmentID, q.Marks
FROM AssessmentQuestions q
WHERE q.AssessmentID IN (SELECT AssessmentID FROM @AllAsm)
  AND q.Marks IS NOT NULL AND q.Marks > 0
  AND NOT EXISTS (
      SELECT 1 FROM AssessmentQuestions c WHERE c.ParentQuestionID = q.QuestionID);

DELETE FROM StudentQuestionMarks WHERE QuestionID IN (SELECT QuestionID FROM @AllQ);
DELETE FROM StudentAssessmentMarks WHERE AssessmentID IN (SELECT AssessmentID FROM @AllAsm);

-- Insert random obtained marks (55%–98% of max, with student/question variation)
INSERT INTO StudentQuestionMarks (StudentID, QuestionID, ObtainedMarks)
SELECT
    soc.StudentID,
    aq.QuestionID,
    CAST(ROUND(
        aq.MaxMarks * (0.55 + (ABS(CHECKSUM(NEWID(), soc.StudentID, aq.QuestionID)) % 44) / 100.0)
    , 2) AS DECIMAL(6,2))
FROM StudentOfferedCourses soc
JOIN @OCs o ON o.OfferedCourseID = soc.OfferedCourseID
JOIN Assessments a ON a.OfferedCourseID = o.OfferedCourseID
JOIN @AllQ aq ON aq.AssessmentID = a.AssessmentID
WHERE a.IsSetupComplete = 1
  AND a.AssessmentTypeID IN (@TypeAssign, @TypeQuiz, @TypeMid, @TypeFinal);

DECLARE @MarkRows INT;
SELECT @MarkRows = COUNT(*) FROM StudentQuestionMarks WHERE QuestionID IN (SELECT QuestionID FROM @AllQ);
PRINT CONCAT('Student question mark rows: ', @MarkRows);

/* ---------- Sync StudentAssessmentMarks with paper→total conversion ---------- */
;WITH Paper AS (
    SELECT
        a.AssessmentID,
        a.TotalMarks,
        PaperMax = SUM(q.MaxMarks)
    FROM Assessments a
    JOIN @AllQ q ON q.AssessmentID = a.AssessmentID
    GROUP BY a.AssessmentID, a.TotalMarks
),
Raw AS (
    SELECT
        m.StudentID,
        q.AssessmentID,
        RawTotal = SUM(m.ObtainedMarks)
    FROM StudentQuestionMarks m
    JOIN @AllQ q ON q.QuestionID = m.QuestionID
    GROUP BY m.StudentID, q.AssessmentID
)
INSERT INTO StudentAssessmentMarks (AssessmentID, StudentID, ObtainedMarks)
SELECT
    r.AssessmentID,
    r.StudentID,
    CAST(ROUND(
        CASE
            WHEN p.PaperMax > 0 AND p.TotalMarks IS NOT NULL AND p.TotalMarks > 0
                THEN r.RawTotal / p.PaperMax * p.TotalMarks
            ELSE r.RawTotal
        END
    , 2) AS DECIMAL(6,2))
FROM Raw r
JOIN Paper p ON p.AssessmentID = r.AssessmentID;

DECLARE @AsmMarkRows INT;
SELECT @AsmMarkRows = COUNT(*) FROM StudentAssessmentMarks WHERE AssessmentID IN (SELECT AssessmentID FROM @AllAsm);
PRINT CONCAT('Student assessment mark rows: ', @AsmMarkRows);

/* ---------- Verify ---------- */
SELECT o.CourseCode, at.AssessmentTypeName, clo.CLOCode, SUM(m.Percentage) AS CoverageTotal,
       CASE WHEN ABS(SUM(m.Percentage) - 100) < 0.01 THEN N'OK' ELSE N'BAD' END AS Status
FROM AssessmentQuestionCLOMappings m
JOIN AssessmentQuestions q ON q.QuestionID = m.QuestionID
JOIN Assessments a ON a.AssessmentID = q.AssessmentID
JOIN AssessmentTypes at ON at.AssessmentTypeID = a.AssessmentTypeID
JOIN CLOs clo ON clo.CLOID = m.CLOID
JOIN @OCs o ON o.OfferedCourseID = a.OfferedCourseID
WHERE a.AssessmentTypeID IN (@TypeMid, @TypeFinal)
GROUP BY o.CourseCode, at.AssessmentTypeName, clo.CLOCode
ORDER BY 1, 2, 3;

SELECT o.CourseCode, at.AssessmentTypeName, a.AssessmentNo, a.Title, a.TotalMarks, a.IsSetupComplete,
       (SELECT COUNT(*) FROM AssessmentQuestions q WHERE q.AssessmentID = a.AssessmentID AND q.ParentQuestionID IS NULL) AS Qs
FROM Assessments a
JOIN @OCs o ON o.OfferedCourseID = a.OfferedCourseID
JOIN AssessmentTypes at ON at.AssessmentTypeID = a.AssessmentTypeID
WHERE a.AssessmentTypeID IN (@TypeMid, @TypeFinal)
ORDER BY o.CourseCode, at.AssessmentTypeName, a.AssessmentNo;

SELECT o.CourseCode, COUNT(DISTINCT soc.StudentID) Students,
       COUNT(DISTINCT m.QuestionID) QuestionsMarked,
       COUNT(*) MarkCells
FROM StudentQuestionMarks m
JOIN @AllQ q ON q.QuestionID = m.QuestionID
JOIN Assessments a ON a.AssessmentID = q.AssessmentID
JOIN @OCs o ON o.OfferedCourseID = a.OfferedCourseID
JOIN StudentOfferedCourses soc ON soc.OfferedCourseID = o.OfferedCourseID AND soc.StudentID = m.StudentID
GROUP BY o.CourseCode
ORDER BY o.CourseCode;

PRINT N'CS3 Mid/Final + random marks seed complete.';
GO
