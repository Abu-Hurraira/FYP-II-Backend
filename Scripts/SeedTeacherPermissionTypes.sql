USE FolderManagementDB;
GO

/* Seed / expand teacher permission types (separated capabilities). */

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLOs')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Manage CLOs');

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Weekly Content')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Upload Weekly Content');

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Mid Exam Questions')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Upload Mid Exam Questions');

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Final Exam Questions')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Upload Final Exam Questions');

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-PLO Mapping')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Manage CLO-PLO Mapping');

IF NOT EXISTS (SELECT 1 FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-Task Mapping')
    INSERT INTO TeacherPermissionTypes (PermissionType) VALUES (N'Manage CLO-Task Mapping');
GO

/* Migrate legacy Upload Course Content → Upload Weekly Content */
DECLARE @LegacyContent INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Course Content');
DECLARE @Weekly INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Weekly Content');

IF @LegacyContent IS NOT NULL AND @Weekly IS NOT NULL
BEGIN
    INSERT INTO TeacherPermissions (TeacherUserID, ProgramCourseID, PermissionTypeID, HODUserID, IsAllowed)
    SELECT p.TeacherUserID, p.ProgramCourseID, @Weekly, p.HODUserID, p.IsAllowed
    FROM TeacherPermissions p
    WHERE p.PermissionTypeID = @LegacyContent
      AND NOT EXISTS (
          SELECT 1
          FROM TeacherPermissions x
          WHERE x.TeacherUserID = p.TeacherUserID
            AND x.ProgramCourseID = p.ProgramCourseID
            AND x.PermissionTypeID = @Weekly
      );
END
GO

/* Migrate legacy Upload Exam Questions → Mid + Final */
DECLARE @LegacyExam INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Exam Questions');
DECLARE @Mid INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Mid Exam Questions');
DECLARE @Final INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Upload Final Exam Questions');

IF @LegacyExam IS NOT NULL AND @Mid IS NOT NULL AND @Final IS NOT NULL
BEGIN
    INSERT INTO TeacherPermissions (TeacherUserID, ProgramCourseID, PermissionTypeID, HODUserID, IsAllowed)
    SELECT p.TeacherUserID, p.ProgramCourseID, @Mid, p.HODUserID, p.IsAllowed
    FROM TeacherPermissions p
    WHERE p.PermissionTypeID = @LegacyExam
      AND NOT EXISTS (
          SELECT 1 FROM TeacherPermissions x
          WHERE x.TeacherUserID = p.TeacherUserID
            AND x.ProgramCourseID = p.ProgramCourseID
            AND x.PermissionTypeID = @Mid
      );

    INSERT INTO TeacherPermissions (TeacherUserID, ProgramCourseID, PermissionTypeID, HODUserID, IsAllowed)
    SELECT p.TeacherUserID, p.ProgramCourseID, @Final, p.HODUserID, p.IsAllowed
    FROM TeacherPermissions p
    WHERE p.PermissionTypeID = @LegacyExam
      AND NOT EXISTS (
          SELECT 1 FROM TeacherPermissions x
          WHERE x.TeacherUserID = p.TeacherUserID
            AND x.ProgramCourseID = p.ProgramCourseID
            AND x.PermissionTypeID = @Final
      );
END
GO

/* Migrate Manage CLOs → also grant CLO-PLO and CLO-Task when CLOs were allowed */
DECLARE @Clos INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLOs');
DECLARE @CloPlo INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-PLO Mapping');
DECLARE @CloTask INT = (SELECT PermissionTypeID FROM TeacherPermissionTypes WHERE PermissionType = N'Manage CLO-Task Mapping');

IF @Clos IS NOT NULL AND @CloPlo IS NOT NULL AND @CloTask IS NOT NULL
BEGIN
    INSERT INTO TeacherPermissions (TeacherUserID, ProgramCourseID, PermissionTypeID, HODUserID, IsAllowed)
    SELECT p.TeacherUserID, p.ProgramCourseID, @CloPlo, p.HODUserID, p.IsAllowed
    FROM TeacherPermissions p
    WHERE p.PermissionTypeID = @Clos AND p.IsAllowed = 1
      AND NOT EXISTS (
          SELECT 1 FROM TeacherPermissions x
          WHERE x.TeacherUserID = p.TeacherUserID
            AND x.ProgramCourseID = p.ProgramCourseID
            AND x.PermissionTypeID = @CloPlo
      );

    INSERT INTO TeacherPermissions (TeacherUserID, ProgramCourseID, PermissionTypeID, HODUserID, IsAllowed)
    SELECT p.TeacherUserID, p.ProgramCourseID, @CloTask, p.HODUserID, p.IsAllowed
    FROM TeacherPermissions p
    WHERE p.PermissionTypeID = @Clos AND p.IsAllowed = 1
      AND NOT EXISTS (
          SELECT 1 FROM TeacherPermissions x
          WHERE x.TeacherUserID = p.TeacherUserID
            AND x.ProgramCourseID = p.ProgramCourseID
            AND x.PermissionTypeID = @CloTask
      );
END
GO
