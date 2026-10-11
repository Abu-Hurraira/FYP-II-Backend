SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.OfferedCourseResultSchemes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OfferedCourseResultSchemes
    (
        SchemeID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_OfferedCourseResultSchemes PRIMARY KEY,
        OfferedCourseID INT NOT NULL,
        TotalMarks DECIMAL(6,2) NOT NULL,
        QuizMarks DECIMAL(6,2) NOT NULL,
        HomeworkMarks DECIMAL(6,2) NOT NULL,
        MidMarks DECIMAL(6,2) NOT NULL,
        LabMarks DECIMAL(6,2) NOT NULL,
        FinalTheoryMarks DECIMAL(6,2) NOT NULL,
        FinalLabMarks DECIMAL(6,2) NOT NULL,
        AssignmentBestOf INT NOT NULL,
        QuizBestOf INT NOT NULL,
        AttendanceHomeworkMarks DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_OfferedCourseResultSchemes_Attendance DEFAULT (0),
        UpdatedAt DATETIME NULL,
        UpdatedByUserID INT NULL,
        LastGeneratedAt DATETIME NULL,
        LastGeneratedByUserID INT NULL,
        CONSTRAINT UQ_OfferedCourseResultSchemes_OfferedCourse UNIQUE (OfferedCourseID),
        CONSTRAINT FK_OfferedCourseResultSchemes_OfferedCourse
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses (OfferedCourseID)
    );
END
GO

IF OBJECT_ID(N'dbo.StudentCourseResults', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentCourseResults
    (
        ResultID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_StudentCourseResults PRIMARY KEY,
        OfferedCourseID INT NOT NULL,
        StudentID INT NOT NULL,
        QuizMarks DECIMAL(6,2) NOT NULL,
        HomeworkMarks DECIMAL(6,2) NOT NULL,
        MidMarks DECIMAL(6,2) NOT NULL,
        LabMarks DECIMAL(6,2) NOT NULL,
        FinalTheoryMarks DECIMAL(6,2) NOT NULL,
        FinalLabMarks DECIMAL(6,2) NOT NULL,
        TotalObtained DECIMAL(6,2) NOT NULL,
        TotalMax DECIMAL(6,2) NOT NULL,
        Percentage DECIMAL(5,2) NOT NULL,
        GeneratedAt DATETIME NOT NULL,
        GeneratedByUserID INT NOT NULL,
        CONSTRAINT UQ_StudentCourseResults_Offering_Student UNIQUE (OfferedCourseID, StudentID),
        CONSTRAINT FK_StudentCourseResults_OfferedCourse
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses (OfferedCourseID),
        CONSTRAINT FK_StudentCourseResults_Student
            FOREIGN KEY (StudentID) REFERENCES dbo.Students (StudentID)
    );
END
GO

IF OBJECT_ID(N'dbo.StudentCourseResultSelections', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentCourseResultSelections
    (
        SelectionID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_StudentCourseResultSelections PRIMARY KEY,
        OfferedCourseID INT NOT NULL,
        StudentID INT NOT NULL,
        AssessmentID INT NOT NULL,
        Category NVARCHAR(20) NOT NULL,
        CONSTRAINT UQ_StudentCourseResultSelections UNIQUE (OfferedCourseID, StudentID, AssessmentID),
        CONSTRAINT FK_StudentCourseResultSelections_OfferedCourse
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses (OfferedCourseID),
        CONSTRAINT FK_StudentCourseResultSelections_Student
            FOREIGN KEY (StudentID) REFERENCES dbo.Students (StudentID),
        CONSTRAINT FK_StudentCourseResultSelections_Assessment
            FOREIGN KEY (AssessmentID) REFERENCES dbo.Assessments (AssessmentID)
    );
END
GO
