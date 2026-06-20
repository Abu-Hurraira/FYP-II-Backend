# Folder Management System API

ASP.NET Core Web API for the Folder Management mobile app.

## Setup

1. Copy `FolderManagementSystemAPI/appsettings.example.json` to `appsettings.json`.
2. Set your SQL Server connection string in `appsettings.json`.
3. Run from `FolderManagementSystemAPI/FolderManagementSystemAPI`:

```bash
dotnet run
```

Default URL: `http://localhost:5161`

## Scripts

Optional SQL scripts are in `Scripts/` (e.g. HOD progress cache, PEO-PLO mappings).

## Notes

- `appsettings.json` is gitignored (contains credentials).
- Uploaded course folders and HOD submission files are gitignored (runtime data).
