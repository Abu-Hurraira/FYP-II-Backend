IF OBJECT_ID(N'dbo.GraderCourseTasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GraderCourseTasks
    (
        GraderCourseTaskID INT IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_GraderCourseTasks PRIMARY KEY,
        GraderAllocationID INT NOT NULL,
        OfferedCourseID INT NOT NULL,
        TaskCode NVARCHAR(50) NOT NULL,
        AssignedByUserID INT NOT NULL,
        AssignedAt DATETIME NOT NULL
            CONSTRAINT DF_GraderCourseTasks_AssignedAt DEFAULT (GETDATE()),
        CONSTRAINT UQ_GraderCourseTasks UNIQUE (GraderAllocationID, OfferedCourseID, TaskCode),
        CONSTRAINT FK_GraderCourseTasks_Alloc FOREIGN KEY (GraderAllocationID)
            REFERENCES dbo.GraderAllocations (GraderAllocationID) ON DELETE CASCADE,
        CONSTRAINT FK_GraderCourseTasks_OC FOREIGN KEY (OfferedCourseID)
            REFERENCES dbo.OfferedCourses (OfferedCourseID)
    );
END
GO
