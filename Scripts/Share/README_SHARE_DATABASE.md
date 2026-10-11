# Share FolderManagementDB

Generated share package for recreating the same SQL Server database on another PC.

## What to send friends

Send this folder (or at least these two files):

| File | Purpose |
|------|---------|
| `FolderManagementDB_Full.sql` | **Main file** — full schema + all current data |
| `README_SHARE_DATABASE.md` | These instructions |

Optional: `export_db.py` (only needed if *you* want to regenerate the dump later).

> A `.bak` backup may also exist on the SQL Server machine, but Express often locks that file so it cannot be copied easily. Prefer the `.sql` script for sharing.

## Friend setup (about 5 minutes)

1. Install **SQL Server Express** (or Developer) and **SSMS**.
2. Open `FolderManagementDB_Full.sql` in SSMS.
3. Connect as an admin / `sa`.
4. Press **F5** to run the whole script.
5. Confirm database **FolderManagementDB** appears in Object Explorer.

### Or from command line

```bat
sqlcmd -S YOUR_SERVER_NAME -E -i FolderManagementDB_Full.sql
```

SQL auth:

```bat
sqlcmd -S YOUR_SERVER_NAME -U sa -P YourPassword -C -i FolderManagementDB_Full.sql
```

## Point the API at their database

Edit `FolderManagementSystemAPI/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=FolderManagementDB;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
}
```

Windows auth:

```json
"DefaultConnection": "Server=YOUR_SERVER;Database=FolderManagementDB;Trusted_Connection=True;TrustServerCertificate=True;"
```

Do **not** keep `DESKTOP-JPM26B8\\FOLDERMANAGEMENT` — that is only this machine’s instance name.

## Notes

- Re-running the script drops and recreates user tables inside `FolderManagementDB`, then reloads data.
- SQL logins are not copied; friends use their own `sa` / Windows login.
- Uploaded files (`Uploads/`, local course folders) are **not** inside the database. Share those separately only if demos need the actual files.
- To regenerate after more DB changes (on the original machine):

```bat
cd "Project\FolderManagementSystemAPI\Scripts\Share"
python export_db.py
```
