USE FolderManagementDB;
GO

IF OBJECT_ID(N'dbo.FcarReportInputs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FcarReportInputs
    (
        FcarReportInputID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_FcarReportInputs PRIMARY KEY,
        OfferedCourseID INT NOT NULL,
        CatalogDescription NVARCHAR(MAX) NOT NULL,
        CourseModifications NVARCHAR(MAX) NOT NULL
            CONSTRAINT DF_FcarReportInputs_Modifications DEFAULT (N'No modification'),
        StudentFeedbackText NVARCHAR(MAX) NULL,
        ReflectionText NVARCHAR(MAX) NULL,
        ProposedActionsText NVARCHAR(MAX) NULL,
        UpdatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_FcarReportInputs_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserID INT NOT NULL,
        CONSTRAINT UQ_FcarReportInputs_OfferedCourse UNIQUE (OfferedCourseID),
        CONSTRAINT FK_FcarReportInputs_OfferedCourse
            FOREIGN KEY (OfferedCourseID) REFERENCES dbo.OfferedCourses(OfferedCourseID)
            ON DELETE CASCADE,
        CONSTRAINT FK_FcarReportInputs_UpdatedBy
            FOREIGN KEY (UpdatedByUserID) REFERENCES dbo.Users(UserID)
    );
END
GO

PRINT 'FCAR report input table ready.';
GO
