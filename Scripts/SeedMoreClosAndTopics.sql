/*
  Additive test data for CAI-261 (Programming for AI):
  - Extra approved CLOs with differentiated CLO-Task shares
  - Extra weekly topics and nested subtopics

  Safe to re-run: skips rows that already exist.
*/
USE FolderManagementDB;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @ProgramId INT = (SELECT TOP 1 ProgramID FROM AcademicPrograms WHERE ProgramCode = N'BSCS' ORDER BY ProgramID);
IF @ProgramId IS NULL
    SET @ProgramId = (SELECT TOP 1 ProgramID FROM AcademicPrograms ORDER BY ProgramID);

DECLARE @SessionId INT = (SELECT TOP 1 SessionID FROM AcademicSessions WHERE ProgramID = @ProgramId ORDER BY SessionID DESC);
DECLARE @HodId INT = (SELECT TOP 1 UserID FROM Users WHERE Email = N'munir@biit.edu.pk');
DECLARE @TeacherA INT = (SELECT TOP 1 UserID FROM Users WHERE Email = N'aftab@biit.edu.pk');
DECLARE @CourseId INT = (SELECT TOP 1 CourseID FROM Courses WHERE CourseCode = N'CAI-261');
DECLARE @ProgramCourseId INT = (
    SELECT TOP 1 ProgramCourseID
    FROM AcademicProgramCourses
    WHERE ProgramID = @ProgramId AND CourseID = @CourseId
);

DECLARE @SectionA INT = (
    SELECT TOP 1 s.SectionID
    FROM Sections s
    JOIN OfferedCourses oc ON oc.SectionID = s.SectionID
    WHERE s.SessionID = @SessionId AND oc.CourseID = @CourseId
      AND (s.SectionName LIKE N'%A' OR s.SectionName LIKE N'%-A' OR s.SectionName LIKE N'% A')
    ORDER BY s.SectionID
);

DECLARE @OfferedA INT = (
    SELECT TOP 1 OfferedCourseID FROM OfferedCourses
    WHERE CourseID = @CourseId AND SectionID = @SectionA
);

IF @ProgramId IS NULL OR @SessionId IS NULL OR @CourseId IS NULL OR @ProgramCourseId IS NULL
   OR @SectionA IS NULL OR @OfferedA IS NULL OR @TeacherA IS NULL OR @HodId IS NULL
BEGIN
    RAISERROR(N'CAI-261 prerequisites missing. Run SeedFullDemoData.sql first.', 16, 1);
    RETURN;
END

DECLARE @CtClos INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CLOs');
DECLARE @CtCloPlo INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CloPloMapping');
DECLARE @CtCloTask INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'CloTaskMapping');
DECLARE @CtWeekly INT = (SELECT ContentTypeID FROM ContentSubmissionTypes WHERE ContentTypeCode = N'WeeklyContent');

DECLARE @TypeAssign INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Assignment');
DECLARE @TypeQuiz INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Quiz');
DECLARE @TypeMid INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Mid');
DECLARE @TypeFinal INT = (SELECT AssessmentTypeID FROM AssessmentTypes WHERE AssessmentTypeName = N'Final');

DECLARE @SubClos INT = (
    SELECT TOP 1 SubmissionID
    FROM ProgramCourseContentSubmissions
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ContentTypeID = @CtClos
      AND SubmissionStatus = N'Approved'
    ORDER BY SubmissionID DESC
);
DECLARE @SubCloPlo INT = (
    SELECT TOP 1 SubmissionID
    FROM ProgramCourseContentSubmissions
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ContentTypeID = @CtCloPlo
      AND SubmissionStatus = N'Approved'
    ORDER BY SubmissionID DESC
);
DECLARE @SubCloTask INT = (
    SELECT TOP 1 SubmissionID
    FROM ProgramCourseContentSubmissions
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ContentTypeID = @CtCloTask
      AND SubmissionStatus = N'Approved'
    ORDER BY SubmissionID DESC
);
DECLARE @SubWeekly INT = (
    SELECT TOP 1 SubmissionID
    FROM ProgramCourseContentSubmissions
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ContentTypeID = @CtWeekly
      AND SubmissionStatus = N'Approved'
    ORDER BY SubmissionID DESC
);

IF @SubClos IS NULL OR @SubCloTask IS NULL OR @SubWeekly IS NULL
BEGIN
    RAISERROR(N'Approved CLO / CLO-Task / Weekly submissions missing. Run SeedFullDemoData.sql first.', 16, 1);
    RETURN;
END

PRINT 'Adding extra approved CLOs (CLO4-CLO6) if missing...';

IF NOT EXISTS (
    SELECT 1 FROM CLOs
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO4'
)
    INSERT INTO CLOs (ProgramCourseID, CLOCode, CLODescription, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, N'CLO4', N'Test: Apply quiz-focused debugging and unit testing skills.', @SessionId, N'Approved', @SubClos);

IF NOT EXISTS (
    SELECT 1 FROM CLOs
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO5'
)
    INSERT INTO CLOs (ProgramCourseID, CLOCode, CLODescription, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, N'CLO5', N'Test: Build assignment-style AI mini-projects with documentation.', @SessionId, N'Approved', @SubClos);

IF NOT EXISTS (
    SELECT 1 FROM CLOs
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO6'
)
    INSERT INTO CLOs (ProgramCourseID, CLOCode, CLODescription, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, N'CLO6', N'Test: Analyze exam scenarios for ethics, limits, and evaluation.', @SessionId, N'Approved', @SubClos);

DECLARE @Clo4 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO4');
DECLARE @Clo5 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO5');
DECLARE @Clo6 INT = (SELECT CLOID FROM CLOs WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOCode = N'CLO6');

DECLARE @Plo1 INT = (SELECT TOP 1 PLOID FROM PLOs WHERE ProgramID = @ProgramId ORDER BY PLOID);
DECLARE @Plo2 INT = (SELECT PLOID FROM (SELECT PLOID, ROW_NUMBER() OVER (ORDER BY PLOID) rn FROM PLOs WHERE ProgramID = @ProgramId) x WHERE rn = 2);
DECLARE @Plo3 INT = (SELECT PLOID FROM (SELECT PLOID, ROW_NUMBER() OVER (ORDER BY PLOID) rn FROM PLOs WHERE ProgramID = @ProgramId) x WHERE rn = 3);

PRINT 'Adding CLO-PLO cells for new CLOs if missing...';
IF @SubCloPlo IS NOT NULL AND @Plo1 IS NOT NULL AND @Clo4 IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM PLOCLOMappings WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOID = @Clo4 AND PLOID = @Plo1)
    INSERT INTO PLOCLOMappings (ProgramCourseID, SessionID, PLOID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @SessionId, @Plo1, @Clo4, 20, @TeacherA, N'Approved', @SubCloPlo);

IF @SubCloPlo IS NOT NULL AND @Plo2 IS NOT NULL AND @Clo5 IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM PLOCLOMappings WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOID = @Clo5 AND PLOID = @Plo2)
    INSERT INTO PLOCLOMappings (ProgramCourseID, SessionID, PLOID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @SessionId, @Plo2, @Clo5, 25, @TeacherA, N'Approved', @SubCloPlo);

IF @SubCloPlo IS NOT NULL AND @Plo3 IS NOT NULL AND @Clo6 IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM PLOCLOMappings WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOID = @Clo6 AND PLOID = @Plo3)
    INSERT INTO PLOCLOMappings (ProgramCourseID, SessionID, PLOID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @SessionId, @Plo3, @Clo6, 30, @TeacherA, N'Approved', @SubCloPlo);

PRINT 'Adding differentiated CLO-Task shares for CLO4-CLO6...';
-- CLO4: Quiz only | CLO5: Assignment only | CLO6: Mid+Final
IF @Clo4 IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM CourseAssessmentTypeCLOMappings
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOID = @Clo4 AND AssessmentTypeID = @TypeQuiz
)
    INSERT INTO CourseAssessmentTypeCLOMappings
        (ProgramCourseID, SessionID, AssessmentTypeID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @SessionId, @TypeQuiz, @Clo4, 100, @TeacherA, N'Approved', @SubCloTask);

IF @Clo5 IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM CourseAssessmentTypeCLOMappings
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOID = @Clo5 AND AssessmentTypeID = @TypeAssign
)
    INSERT INTO CourseAssessmentTypeCLOMappings
        (ProgramCourseID, SessionID, AssessmentTypeID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @SessionId, @TypeAssign, @Clo5, 100, @TeacherA, N'Approved', @SubCloTask);

IF @Clo6 IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM CourseAssessmentTypeCLOMappings
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND CLOID = @Clo6 AND AssessmentTypeID = @TypeMid
)
    INSERT INTO CourseAssessmentTypeCLOMappings
        (ProgramCourseID, SessionID, AssessmentTypeID, CLOID, Percentage, UpdatedByUserID, ContentStatus, SubmissionID)
    VALUES
    (@ProgramCourseId, @SessionId, @TypeMid, @Clo6, 40, @TeacherA, N'Approved', @SubCloTask),
    (@ProgramCourseId, @SessionId, @TypeFinal, @Clo6, 60, @TeacherA, N'Approved', @SubCloTask);

PRINT 'Adding more weekly topics and subtopics...';

DECLARE @Week4 INT = (
    SELECT TOP 1 TopicID FROM CourseTopics
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND WeekNo = 4 AND ParentTopicID IS NULL
);
IF @Week4 IS NULL
BEGIN
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, NULL, 4, N'Neural network basics', N'Test week 4 root topic', 4, @SessionId, N'Approved', @SubWeekly);
    SET @Week4 = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ParentTopicID = @Week4 AND TopicTitle = N'Perceptron model')
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @Week4, 4, N'Perceptron model', N'Subtopic', 1, @SessionId, N'Approved', @SubWeekly);

IF NOT EXISTS (SELECT 1 FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ParentTopicID = @Week4 AND TopicTitle = N'Activation functions')
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @Week4, 4, N'Activation functions', N'Subtopic', 2, @SessionId, N'Approved', @SubWeekly);

DECLARE @Week5 INT = (
    SELECT TOP 1 TopicID FROM CourseTopics
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND WeekNo = 5 AND ParentTopicID IS NULL
);
IF @Week5 IS NULL
BEGIN
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, NULL, 5, N'Computer vision intro', N'Test week 5 root topic', 5, @SessionId, N'Approved', @SubWeekly);
    SET @Week5 = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ParentTopicID = @Week5 AND TopicTitle = N'Image tensors')
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @Week5, 5, N'Image tensors', N'Subtopic', 1, @SessionId, N'Approved', @SubWeekly);

IF NOT EXISTS (SELECT 1 FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ParentTopicID = @Week5 AND TopicTitle = N'Convolution intuition')
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @Week5, 5, N'Convolution intuition', N'Subtopic', 2, @SessionId, N'Approved', @SubWeekly);

DECLARE @Week5SubConv INT = (
    SELECT TOP 1 TopicID FROM CourseTopics
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ParentTopicID = @Week5 AND TopicTitle = N'Convolution intuition'
);

IF @Week5SubConv IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM CourseTopics
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ParentTopicID = @Week5SubConv AND TopicTitle = N'Kernel size and stride'
)
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @Week5SubConv, 5, N'Kernel size and stride', N'Nested subtopic', 1, @SessionId, N'Approved', @SubWeekly);

DECLARE @Week6 INT = (
    SELECT TOP 1 TopicID FROM CourseTopics
    WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND WeekNo = 6 AND ParentTopicID IS NULL
);
IF @Week6 IS NULL
BEGIN
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, NULL, 6, N'NLP fundamentals', N'Test week 6 root topic', 6, @SessionId, N'Approved', @SubWeekly);
    SET @Week6 = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ParentTopicID = @Week6 AND TopicTitle = N'Tokenization')
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @Week6, 6, N'Tokenization', N'Subtopic', 1, @SessionId, N'Approved', @SubWeekly);

IF NOT EXISTS (SELECT 1 FROM CourseTopics WHERE ProgramCourseID = @ProgramCourseId AND SessionID = @SessionId AND ParentTopicID = @Week6 AND TopicTitle = N'Embeddings overview')
    INSERT INTO CourseTopics (ProgramCourseID, ParentTopicID, WeekNo, TopicTitle, TopicDescription, DisplayOrder, SessionID, ContentStatus, SubmissionID)
    VALUES (@ProgramCourseId, @Week6, 6, N'Embeddings overview', N'Subtopic', 2, @SessionId, N'Approved', @SubWeekly);

PRINT 'Mapping new topics to CLOs and initializing progress...';

;WITH NewTopics AS (
    SELECT TopicID
    FROM CourseTopics
    WHERE ProgramCourseID = @ProgramCourseId
      AND SessionID = @SessionId
      AND WeekNo IN (4, 5, 6)
)
INSERT INTO CourseTopicCLOMappings (TopicID, CLOID)
SELECT t.TopicID, c.CLOID
FROM NewTopics t
CROSS JOIN (SELECT @Clo4 AS CLOID UNION ALL SELECT @Clo5 UNION ALL SELECT @Clo6) c
WHERE c.CLOID IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM CourseTopicCLOMappings m WHERE m.TopicID = t.TopicID AND m.CLOID = c.CLOID
);

;WITH NewTopics AS (
    SELECT TopicID
    FROM CourseTopics
    WHERE ProgramCourseID = @ProgramCourseId
      AND SessionID = @SessionId
      AND WeekNo IN (4, 5, 6)
)
INSERT INTO OfferedCourseTopicProgress (OfferedCourseID, TopicID, IsCompleted, CompletedAt, CompletedByUserID)
SELECT @OfferedA, t.TopicID, 0, NULL, NULL
FROM NewTopics t
WHERE NOT EXISTS (
    SELECT 1 FROM OfferedCourseTopicProgress p
    WHERE p.OfferedCourseID = @OfferedA AND p.TopicID = t.TopicID
);

PRINT '=========================================================';
PRINT 'Extra test data added for CAI-261.';
PRINT 'CLO4 → Quiz only | CLO5 → Assignment only | CLO6 → Mid+Final';
PRINT 'Also CLO1-CLO3 remain on all types (from demo seed).';
PRINT 'Weekly content weeks 4-6 with nested subtopics.';
PRINT '=========================================================';
GO
