SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Per-section Lab solid-mark division (must total 20 when course has lab).
IF OBJECT_ID(N'dbo.OfferedCourseLabMarkSchemes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OfferedCourseLabMarkSchemes
    (
        OfferedCourseID INT NOT NULL
            CONSTRAINT PK_OfferedCourseLabMarkSchemes PRIMARY KEY,
        QuizMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_LabMarkSchemes_Quiz DEFAULT (0),
        AssignmentMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_LabMarkSchemes_Assignment DEFAULT (0),
        LabTaskMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_LabMarkSchemes_LabTask DEFAULT (0),
        ProjectMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_LabMarkSchemes_Project DEFAULT (0),
        AttendanceMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_LabMarkSchemes_Attendance DEFAULT (0),
        ExamMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_LabMarkSchemes_Exam DEFAULT (0),
        UpdatedAt DATETIME NULL,
        UpdatedByUserID INT NULL,
        CONSTRAINT FK_OfferedCourseLabMarkSchemes_OfferedCourse
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses (OfferedCourseID)
    );
END
GO

-- Per-section Homework solid-mark division (must total fixed homework marks).
IF OBJECT_ID(N'dbo.OfferedCourseHomeworkMarkSchemes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OfferedCourseHomeworkMarkSchemes
    (
        OfferedCourseID INT NOT NULL
            CONSTRAINT PK_OfferedCourseHomeworkMarkSchemes PRIMARY KEY,
        AttendanceMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_HwMarkSchemes_Attendance DEFAULT (0),
        QuizMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_HwMarkSchemes_Quiz DEFAULT (0),
        AssignmentMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_HwMarkSchemes_Assignment DEFAULT (0),
        UpdatedAt DATETIME NULL,
        UpdatedByUserID INT NULL,
        CONSTRAINT FK_OfferedCourseHomeworkMarkSchemes_OfferedCourse
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses (OfferedCourseID)
    );
END
GO

-- Ensure best-of settings exist on the scheme table (totals become derived/fixed).
IF COL_LENGTH('dbo.OfferedCourseResultSchemes', 'AssignmentBestOf') IS NULL
BEGIN
    ALTER TABLE dbo.OfferedCourseResultSchemes
        ADD AssignmentBestOf INT NOT NULL
            CONSTRAINT DF_OfferedCourseResultSchemes_AssignmentBestOf DEFAULT (3);
END
GO

IF COL_LENGTH('dbo.OfferedCourseResultSchemes', 'QuizBestOf') IS NULL
BEGIN
    ALTER TABLE dbo.OfferedCourseResultSchemes
        ADD QuizBestOf INT NOT NULL
            CONSTRAINT DF_OfferedCourseResultSchemes_QuizBestOf DEFAULT (6);
END
GO
