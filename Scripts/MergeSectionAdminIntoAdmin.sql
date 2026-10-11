/*
  Merge SectionAdmin into Admin.

  - Reassigns every SectionAdmin user to the Admin role
  - Leaves the SectionAdmin role row in place (unused) so older DBs stay compatible
  - Optionally upgrades the demo sectionadmin account display name

  Run against FolderManagementDB after deploying the unified Admin app.
*/

SET NOCOUNT ON;

DECLARE @AdminRoleId INT = (SELECT TOP 1 RoleID FROM dbo.Roles WHERE RoleName = N'Admin');
DECLARE @SectionAdminRoleId INT = (SELECT TOP 1 RoleID FROM dbo.Roles WHERE RoleName = N'SectionAdmin');

IF @AdminRoleId IS NULL
BEGIN
    RAISERROR(N'Admin role not found. Aborting merge.', 16, 1);
    RETURN;
END;

IF @SectionAdminRoleId IS NULL
BEGIN
    PRINT 'No SectionAdmin role found. Nothing to migrate.';
    RETURN;
END;

DECLARE @Moved INT;

UPDATE dbo.Users
SET RoleID = @AdminRoleId,
    Rank = CASE
        WHEN Rank IS NULL OR LTRIM(RTRIM(Rank)) = N'' THEN N'Administrator'
        WHEN Rank = N'Demo' THEN N'Administrator'
        ELSE Rank
    END
WHERE RoleID = @SectionAdminRoleId;

SET @Moved = @@ROWCOUNT;

UPDATE dbo.Users
SET FullName = N'Administrator (merged)',
    Rank = N'Administrator'
WHERE Email = N'sectionadmin@biit.edu.pk'
  AND RoleID = @AdminRoleId;

PRINT CONCAT('Merged SectionAdmin users into Admin: ', @Moved);
PRINT 'Legacy SectionAdmin logins now use the unified Administration workspace.';
PRINT 'Create additional admins from Admin → Admin Users in the app.';
