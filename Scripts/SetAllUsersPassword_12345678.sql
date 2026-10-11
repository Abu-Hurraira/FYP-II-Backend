-- Dev only: set every Users.PasswordHash to ASP.NET Identity hash for password 12345678
-- (same hasher as AuthController / PasswordHasher<User>)
USE FolderManagementDB;
GO

UPDATE dbo.Users
SET PasswordHash = N'AQAAAAEAACcQAAAAEJMuS1qMk9+iocI8U4N0+YzuqqeONfCkaEAUEGsjqBRnAfzpIUDgdcQ0koFuRABw5Q==';

SELECT COUNT(*) AS UserCount FROM dbo.Users;
GO
