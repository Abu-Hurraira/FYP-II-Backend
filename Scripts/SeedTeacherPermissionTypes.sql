USE FolderManagementDB;
GO

IF OBJECT_ID(N'dbo.TeacherPermissionTypes', N'U') IS NULL
BEGIN
    CREATE TABLE TeacherPermissionTypes
    (
        PermissionTypeID INT IDENTITY(1,1) PRIMARY KEY,
        PermissionType NVARCHAR(100) NOT NULL UNIQUE
    );
END
GO

IF OBJECT_ID(N'dbo.TeacherPermissions', N'U') IS NULL
BEGIN
    CREATE TABLE TeacherPermissions
    (
        PermissionID INT IDENTITY(1,1) PRIMARY KEY,
        TeacherUserID INT NOT NULL,
        ProgramCourseID INT NOT NULL,
        PermissionTypeID INT NOT NULL,
        HODUserID INT NULL,
        IsAllowed BIT NOT NULL DEFAULT 0,
        CONSTRAINT UQ_TeacherPermissions UNIQUE (TeacherUserID, ProgramCourseID, PermissionTypeID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLOs')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Manage CLOs');

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Course Content')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Upload Course Content');

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Exam Questions')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Upload Exam Questions');
GO
