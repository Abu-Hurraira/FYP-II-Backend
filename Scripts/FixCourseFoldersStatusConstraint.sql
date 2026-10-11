-- Allow HOD approve/reject statuses on CourseFolders.FolderStatus
-- and FolderSubmissions.SubmissionStatus.
-- (API uses Approved / Rejected; leftover CHECK constraints omitted those values.)

DECLARE @schema SYSNAME;
DECLARE @table SYSNAME;
DECLARE @name SYSNAME;
DECLARE drop_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name, c.name
    FROM sys.check_constraints c
    INNER JOIN sys.tables t ON t.object_id = c.parent_object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.name IN (N'CourseFolders', N'FolderSubmissions')
      AND (
          c.definition LIKE N'%FolderStatus%'
          OR c.definition LIKE N'%SubmissionStatus%'
      );

OPEN drop_cursor;
FETCH NEXT FROM drop_cursor INTO @schema, @table, @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC (
        N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table)
        + N' DROP CONSTRAINT ' + QUOTENAME(@name)
    );
    FETCH NEXT FROM drop_cursor INTO @schema, @table, @name;
END
CLOSE drop_cursor;
DEALLOCATE drop_cursor;
GO

ALTER TABLE dbo.CourseFolders ADD CONSTRAINT CK_CourseFolders_FolderStatus CHECK (
  [FolderStatus] IN (
    N'In Progress',
    N'Generated',
    N'Ready For Submission',
    N'Submitted',
    N'Approved',
    N'Rejected',
    N'Needs Revision',
    N'Failed'
  )
);
GO

ALTER TABLE dbo.FolderSubmissions ADD CONSTRAINT CK_FolderSubmissions_Status CHECK (
  [SubmissionStatus] IN (
    N'Draft',
    N'In Progress',
    N'Generated',
    N'Ready For Submission',
    N'Submitted',
    N'Approved',
    N'Rejected',
    N'Needs Revision',
    N'Failed'
  )
);
GO
