/*
  SeedCS3AssignmentsQuizzes.sql
  Computer Science 3 (ProgramID 9) — Adeel offerings 666/667/668:
    - Remove existing Assignment/Quiz questions (including completed Assignment 1)
    - Ensure 4 Assignments + 8 Quizzes per course
    - 2 topic-related questions each, CLO coverage 100% per design share
    - Setup complete → ready to mark
*/
USE FolderManagementDB;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @TeacherId INT = 9; -- adeel@biit.edu.pk
DECLARE @SessionId INT = 8;
DECLARE @TypeAssign INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Assignment');
DECLARE @TypeQuiz INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Quiz');

DECLARE @OCs TABLE (
    OfferedCourseID INT PRIMARY KEY,
    ProgramCourseID INT NOT NULL,
    CourseCode NVARCHAR(50) NOT NULL
);
INSERT INTO @OCs (OfferedCourseID, ProgramCourseID, CourseCode) VALUES
(666, 472, N'CAI-262'),
(667, 470, N'MTH-502'),
(668, 471, N'CAI-261');

/* ---------- Wipe Assignment + Quiz content on these offerings ---------- */
DECLARE @A TABLE (AssessmentID INT PRIMARY KEY);
INSERT INTO @A (AssessmentID)
SELECT a.AssessmentID
FROM Assessments a
JOIN @OCs o ON o.OfferedCourseID = a.OfferedCourseID
WHERE a.AssessmentTypeID IN (@TypeAssign, @TypeQuiz);

DECLARE @Q TABLE (QuestionID INT PRIMARY KEY);
INSERT INTO @Q (QuestionID)
SELECT q.QuestionID
FROM AssessmentQuestions q
WHERE q.AssessmentID IN (SELECT AssessmentID FROM @A);

DELETE FROM StudentQuestionMarks WHERE QuestionID IN (SELECT QuestionID FROM @Q);
DELETE FROM AssessmentQuestionCLOMappings WHERE QuestionID IN (SELECT QuestionID FROM @Q);
DELETE FROM AssessmentQuestions WHERE QuestionID IN (SELECT QuestionID FROM @Q);
DELETE FROM StudentAssessmentMarks WHERE AssessmentID IN (SELECT AssessmentID FROM @A);

DECLARE @ClearedCount INT = (SELECT COUNT(*) FROM @A);
PRINT CONCAT('Cleared questions/marks on ', @ClearedCount, ' Assignment/Quiz assessments.');

/* ---------- Ensure 4 Assignment + 8 Quiz shells per OC ---------- */
DECLARE @OcId INT, @PcId INT, @Code NVARCHAR(50);
DECLARE oc_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT OfferedCourseID, ProgramCourseID, CourseCode FROM @OCs;
OPEN oc_cur;
FETCH NEXT FROM oc_cur INTO @OcId, @PcId, @Code;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @n INT = 1;
    WHILE @n <= 4
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM Assessments
            WHERE OfferedCourseID = @OcId AND AssessmentTypeID = @TypeAssign AND AssessmentNo = @n)
        BEGIN
            INSERT INTO Assessments
                (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks,
                 CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
            VALUES
                (@OcId, @TypeAssign, @n, N'', NULL, NULL, @TeacherId, GETDATE(), 0, NULL);
        END
        SET @n = @n + 1;
    END

    SET @n = 1;
    WHILE @n <= 8
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM Assessments
            WHERE OfferedCourseID = @OcId AND AssessmentTypeID = @TypeQuiz AND AssessmentNo = @n)
        BEGIN
            INSERT INTO Assessments
                (OfferedCourseID, AssessmentTypeID, AssessmentNo, Title, DueDate, TotalMarks,
                 CreatedByUserID, CreatedAt, IsSetupComplete, SetupCompletedAt)
            VALUES
                (@OcId, @TypeQuiz, @n, N'', NULL, NULL, @TeacherId, GETDATE(), 0, NULL);
        END
        SET @n = @n + 1;
    END

    FETCH NEXT FROM oc_cur INTO @OcId, @PcId, @Code;
END
CLOSE oc_cur;
DEALLOCATE oc_cur;

/* ---------- Topics (weekly roots + first child) per program course ---------- */
DECLARE @Topics TABLE (
    ProgramCourseID INT,
    Slot INT, -- 1..4
    TopicTitle NVARCHAR(300)
);

INSERT INTO @Topics (ProgramCourseID, Slot, TopicTitle)
SELECT pc.ProgramCourseID, 1, ISNULL(t.TopicTitle, N'Topic 1 - Foundations')
FROM @OCs pc
OUTER APPLY (
    SELECT TOP 1 TopicTitle FROM CourseTopics
    WHERE ProgramCourseID = pc.ProgramCourseID AND SessionID = @SessionId AND ParentTopicID IS NULL
    ORDER BY DisplayOrder, TopicID
) t;

INSERT INTO @Topics (ProgramCourseID, Slot, TopicTitle)
SELECT pc.ProgramCourseID, 2, ISNULL(t.TopicTitle, N'Topic 1.1 - Core concepts')
FROM @OCs pc
OUTER APPLY (
    SELECT TOP 1 TopicTitle FROM CourseTopics
    WHERE ProgramCourseID = pc.ProgramCourseID AND SessionID = @SessionId AND ParentTopicID IS NOT NULL
    ORDER BY DisplayOrder, TopicID
) t;

INSERT INTO @Topics (ProgramCourseID, Slot, TopicTitle)
SELECT pc.ProgramCourseID, 3, ISNULL(t.TopicTitle, N'Topic 2 - Practice & applications')
FROM @OCs pc
OUTER APPLY (
    SELECT TopicTitle FROM (
        SELECT TopicTitle, ROW_NUMBER() OVER (ORDER BY DisplayOrder, TopicID) rn
        FROM CourseTopics
        WHERE ProgramCourseID = pc.ProgramCourseID AND SessionID = @SessionId AND ParentTopicID IS NULL
    ) x WHERE rn = 2
) t;

INSERT INTO @Topics (ProgramCourseID, Slot, TopicTitle)
SELECT pc.ProgramCourseID, 4, ISNULL(t.TopicTitle, N'Topic 3 - Assessment readiness')
FROM @OCs pc
OUTER APPLY (
    SELECT TopicTitle FROM (
        SELECT TopicTitle, ROW_NUMBER() OVER (ORDER BY DisplayOrder, TopicID) rn
        FROM CourseTopics
        WHERE ProgramCourseID = pc.ProgramCourseID AND SessionID = @SessionId AND ParentTopicID IS NULL
    ) x WHERE rn = 3
) t;

/* ---------- Build question plan: each assessment gets 2 questions ---------- */
DECLARE @Plan TABLE (
    RowNum INT IDENTITY(1,1) PRIMARY KEY,
    OfferedCourseID INT,
    ProgramCourseID INT,
    CourseCode NVARCHAR(50),
    AssessmentTypeID INT,
    AssessmentNo INT,
    AssessmentID INT NULL,
    TopicTitle NVARCHAR(300),
    QIndex INT, -- 1 or 2
    QuestionNo NVARCHAR(20),
    QuestionText NVARCHAR(MAX),
    Marks DECIMAL(6,2)
);

-- Assignments: slot = AssessmentNo (1..4)
INSERT INTO @Plan
    (OfferedCourseID, ProgramCourseID, CourseCode, AssessmentTypeID, AssessmentNo, TopicTitle, QIndex, QuestionNo, QuestionText, Marks)
SELECT
    o.OfferedCourseID, o.ProgramCourseID, o.CourseCode, @TypeAssign, a.AssessmentNo,
    ISNULL(tp.TopicTitle, CONCAT(N'Topic ', a.AssessmentNo)),
    q.QIndex,
    CONCAT(N'Q', q.QIndex),
    CASE q.QIndex
        WHEN 1 THEN CONCAT(N'Explain the main idea from "', ISNULL(tp.TopicTitle, N'Topic'), N'" in your own words and give one example for ', o.CourseCode, N'.')
        ELSE CONCAT(N'Apply concepts from "', ISNULL(tp.TopicTitle, N'Topic'), N'" to solve a short problem. Show steps clearly (', o.CourseCode, N').')
    END,
    10.00
FROM @OCs o
CROSS JOIN (VALUES (1),(2),(3),(4)) a(AssessmentNo)
CROSS JOIN (VALUES (1),(2)) q(QIndex)
LEFT JOIN @Topics tp ON tp.ProgramCourseID = o.ProgramCourseID AND tp.Slot = a.AssessmentNo;

-- Quizzes: map AssessmentNo 1-2→slot1, 3-4→slot2, 5-6→slot3, 7-8→slot4
INSERT INTO @Plan
    (OfferedCourseID, ProgramCourseID, CourseCode, AssessmentTypeID, AssessmentNo, TopicTitle, QIndex, QuestionNo, QuestionText, Marks)
SELECT
    o.OfferedCourseID, o.ProgramCourseID, o.CourseCode, @TypeQuiz, a.AssessmentNo,
    ISNULL(tp.TopicTitle, CONCAT(N'Topic ', ((a.AssessmentNo - 1) / 2) + 1)),
    q.QIndex,
    CONCAT(N'Q', q.QIndex),
    CASE q.QIndex
        WHEN 1 THEN CONCAT(N'Quick check: define one key term from "', ISNULL(tp.TopicTitle, N'Topic'), N'" (', o.CourseCode, N').')
        ELSE CONCAT(N'Short application from "', ISNULL(tp.TopicTitle, N'Topic'), N'": answer in 3–5 lines (', o.CourseCode, N').')
    END,
    5.00
FROM @OCs o
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) a(AssessmentNo)
CROSS JOIN (VALUES (1),(2)) q(QIndex)
LEFT JOIN @Topics tp ON tp.ProgramCourseID = o.ProgramCourseID
    AND tp.Slot = ((a.AssessmentNo - 1) / 2) + 1;

UPDATE p
SET AssessmentID = a.AssessmentID
FROM @Plan p
JOIN Assessments a ON a.OfferedCourseID = p.OfferedCourseID
    AND a.AssessmentTypeID = p.AssessmentTypeID
    AND a.AssessmentNo = p.AssessmentNo;

IF EXISTS (SELECT 1 FROM @Plan WHERE AssessmentID IS NULL)
BEGIN
    RAISERROR(N'Some Assignment/Quiz templates missing after ensure step.', 16, 1);
    RETURN;
END

/* ---------- Update assessment headers (titles, totals, setup complete) ---------- */
UPDATE a
SET
    Title = CASE
        WHEN a.AssessmentTypeID = @TypeAssign THEN CONCAT(N'Assignment ', a.AssessmentNo, N' — ', LEFT(p.TopicTitle, 80))
        ELSE CONCAT(N'Quiz ', a.AssessmentNo, N' — ', LEFT(p.TopicTitle, 80))
    END,
    DueDate = DATEADD(DAY, 7 + a.AssessmentNo, CAST(GETDATE() AS DATE)),
    TotalMarks = CASE WHEN a.AssessmentTypeID = @TypeAssign THEN 10.00 ELSE 5.00 END,
    IsSetupComplete = 1,
    SetupCompletedAt = GETDATE()
FROM Assessments a
JOIN (
    SELECT DISTINCT AssessmentID, AssessmentTypeID, AssessmentNo, TopicTitle
    FROM @Plan
) p ON p.AssessmentID = a.AssessmentID;

/* ---------- Insert questions ---------- */
DECLARE @NewQ TABLE (
    PlanRow INT PRIMARY KEY,
    QuestionID INT NOT NULL
);

DECLARE @PlanRow INT, @AsmId INT, @QNo NVARCHAR(20), @QText NVARCHAR(MAX), @QMarks DECIMAL(6,2), @NewQid INT;
DECLARE q_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT RowNum, AssessmentID, QuestionNo, QuestionText, Marks
    FROM @Plan
    ORDER BY RowNum;
OPEN q_cur;
FETCH NEXT FROM q_cur INTO @PlanRow, @AsmId, @QNo, @QText, @QMarks;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO AssessmentQuestions
        (AssessmentID, ParentQuestionID, DisplayOrder, QuestionNo, QuestionText, Marks)
    VALUES
        (@AsmId, NULL, CASE WHEN @QNo = N'Q1' THEN 1 ELSE 2 END, @QNo, @QText, @QMarks);
    SET @NewQid = SCOPE_IDENTITY();
    INSERT INTO @NewQ (PlanRow, QuestionID) VALUES (@PlanRow, @NewQid);
    FETCH NEXT FROM q_cur INTO @PlanRow, @AsmId, @QNo, @QText, @QMarks;
END
CLOSE q_cur;
DEALLOCATE q_cur;

DECLARE @InsertedQ INT = (SELECT COUNT(*) FROM @NewQ);
PRINT CONCAT('Inserted ', @InsertedQ, ' questions.');

/* ---------- CLO coverage: 100% per CLO with design share for that type ---------- */
DECLARE @CloShare TABLE (
    ProgramCourseID INT,
    AssessmentTypeID INT,
    CloId INT,
    CloCode NVARCHAR(20),
    DesignShare DECIMAL(6,2)
);
INSERT INTO @CloShare (ProgramCourseID, AssessmentTypeID, CloId, CloCode, DesignShare)
SELECT m.ProgramCourseID, m.AssessmentTypeID, m.CLOID, clo.CLOCode, m.Percentage
FROM CourseAssessmentTypeCLOMappings m
JOIN CLOs clo ON clo.CLOID = m.CLOID
JOIN @OCs o ON o.ProgramCourseID = m.ProgramCourseID
WHERE m.SessionID = @SessionId
  AND m.ContentStatus = N'Approved'
  AND m.AssessmentTypeID IN (@TypeAssign, @TypeQuiz)
  AND m.Percentage IS NOT NULL
  AND m.Percentage > 0;

-- Question list per (PC, type) in stable order
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
    SELECT ProgramCourseID, AssessmentTypeID, CloId FROM @CloShare ORDER BY ProgramCourseID, AssessmentTypeID, CloCode;
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

            IF @i <= @Rem
                SET @Pct = @Base + 1;
            ELSE
                SET @Pct = @Base;

            INSERT INTO AssessmentQuestionCLOMappings (QuestionID, CLOID, Percentage)
            VALUES (@Qid, @CloId, @Pct);

            SET @i = @i + 1;
        END
    END

    FETCH NEXT FROM clo_cur INTO @Pc, @TypeId, @CloId;
END
CLOSE clo_cur;
DEALLOCATE clo_cur;

DECLARE @MapCount INT;
SELECT @MapCount = COUNT(*)
FROM AssessmentQuestionCLOMappings
WHERE QuestionID IN (SELECT QuestionID FROM @NewQ);
PRINT CONCAT('CLO mappings inserted: ', @MapCount);

/* ---------- Verify coverage totals ---------- */
SELECT
    o.CourseCode,
    at.AssessmentTypeName,
    clo.CLOCode,
    SUM(m.Percentage) AS CoverageTotal,
    CASE WHEN ABS(SUM(m.Percentage) - 100) < 0.01 THEN N'OK' ELSE N'BAD' END AS Status
FROM AssessmentQuestionCLOMappings m
JOIN AssessmentQuestions q ON q.QuestionID = m.QuestionID
JOIN Assessments a ON a.AssessmentID = q.AssessmentID
JOIN AssessmentTypes at ON at.AssessmentTypeID = a.AssessmentTypeID
JOIN CLOs clo ON clo.CLOID = m.CLOID
JOIN @OCs o ON o.OfferedCourseID = a.OfferedCourseID
WHERE a.AssessmentTypeID IN (@TypeAssign, @TypeQuiz)
GROUP BY o.CourseCode, at.AssessmentTypeName, clo.CLOCode
ORDER BY o.CourseCode, at.AssessmentTypeName, clo.CLOCode;

SELECT
    o.CourseCode,
    at.AssessmentTypeName,
    COUNT(DISTINCT a.AssessmentID) AS Assessments,
    SUM(CASE WHEN a.IsSetupComplete = 1 THEN 1 ELSE 0 END) AS SetupComplete,
    COUNT(q.QuestionID) AS Questions
FROM Assessments a
JOIN @OCs o ON o.OfferedCourseID = a.OfferedCourseID
JOIN AssessmentTypes at ON at.AssessmentTypeID = a.AssessmentTypeID
LEFT JOIN AssessmentQuestions q ON q.AssessmentID = a.AssessmentID AND q.ParentQuestionID IS NULL
WHERE a.AssessmentTypeID IN (@TypeAssign, @TypeQuiz)
GROUP BY o.CourseCode, at.AssessmentTypeName
ORDER BY o.CourseCode, at.AssessmentTypeName;

PRINT 'CS3 Assignment/Quiz seed complete — ready to mark.';
GO
