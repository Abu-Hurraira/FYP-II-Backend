"""Export FolderManagementDB schema + data for sharing."""
import os
from collections import defaultdict, deque
from datetime import datetime, date, time
from decimal import Decimal

import pyodbc

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
SQL_PATH = os.path.join(OUT_DIR, "FolderManagementDB_Full.sql")
BAK_PATH = os.path.join(OUT_DIR, "FolderManagementDB.bak")
README_PATH = os.path.join(OUT_DIR, "README_SHARE_DATABASE.md")

SERVER = r"DESKTOP-JPM26B8\FOLDERMANAGEMENT"
DATABASE = "FolderManagementDB"
UID = "sa"
PWD = "Folder"


def build_conn():
    drivers = [d for d in pyodbc.drivers() if "SQL Server" in d]
    preferred = [
        "ODBC Driver 18 for SQL Server",
        "ODBC Driver 17 for SQL Server",
        "SQL Server",
    ]
    driver = next((d for d in preferred if d in drivers), drivers[-1] if drivers else None)
    if not driver:
        raise RuntimeError("No SQL Server ODBC driver found")
    return (
        f"DRIVER={{{driver}}};"
        f"SERVER={SERVER};"
        f"DATABASE={DATABASE};"
        f"UID={UID};PWD={PWD};"
        "TrustServerCertificate=yes;"
    )


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    cn = pyodbc.connect(build_conn(), autocommit=True)
    cur = cn.cursor()

    def q(sql, params=None):
        cur.execute(sql, params or [])
        cols = [c[0] for c in cur.description] if cur.description else []
        rows = cur.fetchall()
        return cols, rows

    print("Creating BAK...")
    bak_targets = [BAK_PATH]
    _, bak_rows = q(
        "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(512)) AS BackupPath"
    )
    default_bak_dir = (bak_rows[0].BackupPath or "").rstrip("\\/") if bak_rows else ""
    if default_bak_dir:
        bak_targets.append(os.path.join(default_bak_dir, "FolderManagementDB.bak"))

    bak_created = None
    for target in bak_targets:
        bak_sql_path = target.replace("'", "''")
        try:
            cur.execute(
                f"""
BACKUP DATABASE [{DATABASE}]
TO DISK = N'{bak_sql_path}'
WITH FORMAT, INIT, NAME = N'{DATABASE}-full', STATS = 10;
"""
            )
            bak_created = target
            print("BAK_OK", target, os.path.getsize(target))
            break
        except Exception as e:
            print("BAK_TRY_FAILED", target, e)

    if bak_created and os.path.abspath(bak_created) != os.path.abspath(BAK_PATH):
        try:
            import shutil

            shutil.copy2(bak_created, BAK_PATH)
            print("BAK_COPIED", BAK_PATH, os.path.getsize(BAK_PATH))
        except Exception as e:
            print("BAK_COPY_FAILED", e)

    _, tables = q(
        """
SELECT s.name AS schema_name, t.name AS table_name, t.object_id
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name
"""
    )
    print("tables", len(tables))

    _, fks = q(
        """
SELECT
  OBJECT_SCHEMA_NAME(fk.parent_object_id) AS child_schema,
  OBJECT_NAME(fk.parent_object_id) AS child_table,
  OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS parent_schema,
  OBJECT_NAME(fk.referenced_object_id) AS parent_table
FROM sys.foreign_keys fk
"""
    )

    nodes = [(r.schema_name, r.table_name) for r in tables]
    deps = defaultdict(set)
    children = defaultdict(set)
    for r in fks:
        child = (r.child_schema, r.child_table)
        parent = (r.parent_schema, r.parent_table)
        if child in nodes and parent in nodes and child != parent:
            deps[child].add(parent)
            children[parent].add(child)

    indeg = {n: 0 for n in nodes}
    for child, parents in deps.items():
        indeg[child] = len(parents)
    qnodes = deque([n for n, d in indeg.items() if d == 0])
    order = []
    while qnodes:
        n = qnodes.popleft()
        order.append(n)
        for ch in children.get(n, []):
            indeg[ch] -= 1
            if indeg[ch] == 0:
                qnodes.append(ch)
    for n in nodes:
        if n not in order:
            order.append(n)
    print("order", len(order))

    def sql_ident(name):
        return "[" + name.replace("]", "]]") + "]"

    def sql_literal(val):
        if val is None:
            return "NULL"
        if isinstance(val, bool):
            return "1" if val else "0"
        if isinstance(val, int) and not isinstance(val, bool):
            return str(val)
        if isinstance(val, float):
            return repr(val)
        if isinstance(val, Decimal):
            return format(val, "f")
        if isinstance(val, datetime):
            return "'" + val.strftime("%Y-%m-%d %H:%M:%S.%f")[:-3] + "'"
        if isinstance(val, date):
            return "'" + val.isoformat() + "'"
        if isinstance(val, time):
            return "'" + val.isoformat() + "'"
        if isinstance(val, bytes):
            return "0x" + val.hex()
        s = str(val).replace("'", "''")
        return "N'" + s + "'"

    def column_defs(schema, table):
        _, cols = q(
            """
SELECT
  c.name AS col_name,
  t.name AS type_name,
  c.max_length,
  c.precision,
  c.scale,
  c.is_nullable,
  c.is_identity,
  CAST(ic.seed_value AS BIGINT) AS seed_value,
  CAST(ic.increment_value AS BIGINT) AS increment_value,
  CAST(dc.definition AS NVARCHAR(MAX)) AS default_definition
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.identity_columns ic
  ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.default_constraints dc
  ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID(?)
ORDER BY c.column_id
""",
            [f"{schema}.{table}"],
        )
        parts = []
        for c in cols:
            typ = c.type_name
            if typ in ("varchar", "nvarchar", "char", "nchar", "varbinary", "binary"):
                ln = c.max_length
                if typ in ("nvarchar", "nchar") and ln > 0:
                    ln = ln // 2
                size = "MAX" if ln < 0 else str(ln)
                typ = f"{typ}({size})"
            elif typ in ("decimal", "numeric"):
                typ = f"{typ}({c.precision},{c.scale})"
            elif typ in ("datetime2", "time", "datetimeoffset"):
                typ = f"{typ}({c.scale})"
            line = f"    {sql_ident(c.col_name)} {typ}"
            if c.is_identity:
                seed = int(c.seed_value) if c.seed_value is not None else 1
                inc = int(c.increment_value) if c.increment_value is not None else 1
                line += f" IDENTITY({seed},{inc})"
            if c.default_definition:
                line += f" DEFAULT {c.default_definition}"
            line += " NULL" if c.is_nullable else " NOT NULL"
            parts.append(line)
        return parts, cols

    def pk_clause(schema, table):
        _, rows = q(
            """
SELECT i.name AS index_name, c.name AS col_name, ic.key_ordinal
FROM sys.indexes i
JOIN sys.index_columns ic
  ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c
  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(?) AND i.is_primary_key = 1
ORDER BY ic.key_ordinal
""",
            [f"{schema}.{table}"],
        )
        if not rows:
            return None, []
        return rows[0].index_name, [r.col_name for r in rows]

    def unique_indexes(schema, table):
        _, rows = q(
            """
SELECT i.name AS index_name, i.is_unique_constraint, c.name AS col_name, ic.key_ordinal, i.index_id
FROM sys.indexes i
JOIN sys.index_columns ic
  ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
JOIN sys.columns c
  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(?)
  AND i.is_primary_key = 0
  AND i.is_unique = 1
  AND i.type_desc <> 'HEAP'
ORDER BY i.index_id, ic.key_ordinal
""",
            [f"{schema}.{table}"],
        )
        groups = {}
        for r in rows:
            groups.setdefault(
                r.index_name, {"is_constraint": r.is_unique_constraint, "cols": []}
            )
            groups[r.index_name]["cols"].append(r.col_name)
        return groups

    def foreign_keys(schema, table):
        _, rows = q(
            """
SELECT
  fk.name AS fk_name,
  COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS col_name,
  OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ref_schema,
  OBJECT_NAME(fk.referenced_object_id) AS ref_table,
  COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ref_col,
  fkc.constraint_column_id
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID(?)
ORDER BY fk.name, fkc.constraint_column_id
""",
            [f"{schema}.{table}"],
        )
        groups = {}
        for r in rows:
            g = groups.setdefault(
                r.fk_name,
                {
                    "ref_schema": r.ref_schema,
                    "ref_table": r.ref_table,
                    "cols": [],
                    "refs": [],
                },
            )
            g["cols"].append(r.col_name)
            g["refs"].append(r.ref_col)
        return groups

    lines = []
    lines.append(
        f"""/*
  FolderManagementDB - schema + data export
  Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
  Source: {SERVER} / {DATABASE}

  HOW FRIENDS RESTORE (SQL script)
  1. Install SQL Server Express/Developer.
  2. Open this file in SSMS and Execute, or:
       sqlcmd -S YOUR_SERVER -E -i FolderManagementDB_Full.sql
  3. Update API appsettings.json connection string to their server.

  ALTERNATIVE (faster): restore FolderManagementDB.bak — see README_SHARE_DATABASE.md
*/
IF DB_ID(N'{DATABASE}') IS NULL
BEGIN
    CREATE DATABASE [{DATABASE}];
END
GO
USE [{DATABASE}];
GO
SET NOCOUNT ON;
GO
-- Drop FKs then tables for clean re-run
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
  + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
FROM sys.foreign_keys;
EXEC sp_executesql @sql;
GO
DECLARE @sql2 NVARCHAR(MAX) = N'';
SELECT @sql2 += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_ms_shipped = 0;
EXEC sp_executesql @sql2;
GO
"""
    )

    for schema, table in order:
        col_parts, _ = column_defs(schema, table)
        pk_name, pk_cols = pk_clause(schema, table)
        create = (
            f"CREATE TABLE {sql_ident(schema)}.{sql_ident(table)} (\n"
            + ",\n".join(col_parts)
        )
        if pk_cols:
            create += (
                ",\n    CONSTRAINT "
                + sql_ident(pk_name)
                + " PRIMARY KEY ("
                + ", ".join(sql_ident(c) for c in pk_cols)
                + ")"
            )
        uqs = unique_indexes(schema, table)
        for uname, info in uqs.items():
            if info["is_constraint"]:
                create += (
                    ",\n    CONSTRAINT "
                    + sql_ident(uname)
                    + " UNIQUE ("
                    + ", ".join(sql_ident(c) for c in info["cols"])
                    + ")"
                )
        create += "\n);\nGO\n"
        lines.append(create)
        for uname, info in uqs.items():
            if not info["is_constraint"]:
                lines.append(
                    f"CREATE UNIQUE INDEX {sql_ident(uname)} ON {sql_ident(schema)}.{sql_ident(table)} ("
                    + ", ".join(sql_ident(c) for c in info["cols"])
                    + ");\nGO\n"
                )

    for schema, table in order:
        _, col_meta = column_defs(schema, table)
        col_names = [c.col_name for c in col_meta]
        has_identity = any(c.is_identity for c in col_meta)
        full = f"{sql_ident(schema)}.{sql_ident(table)}"
        cur.execute(f"SELECT * FROM {full}")
        rows = cur.fetchall()
        if not rows:
            lines.append(f"-- No rows: {schema}.{table}\n")
            continue
        print(f"data {schema}.{table}: {len(rows)}")
        if has_identity:
            lines.append(f"SET IDENTITY_INSERT {full} ON;\nGO\n")
        colnames_sql = ", ".join(sql_ident(c) for c in col_names)
        batch = []
        for row in rows:
            vals = ", ".join(sql_literal(v) for v in row)
            batch.append(f"INSERT INTO {full} ({colnames_sql}) VALUES ({vals});")
            if len(batch) >= 200:
                lines.append("\n".join(batch) + "\nGO\n")
                batch = []
        if batch:
            lines.append("\n".join(batch) + "\nGO\n")
        if has_identity:
            lines.append(f"SET IDENTITY_INSERT {full} OFF;\nGO\n")

    for schema, table in order:
        fkd = foreign_keys(schema, table)
        for fk_name, info in fkd.items():
            lines.append(
                f"ALTER TABLE {sql_ident(schema)}.{sql_ident(table)} ADD CONSTRAINT {sql_ident(fk_name)} "
                f"FOREIGN KEY ({', '.join(sql_ident(c) for c in info['cols'])}) "
                f"REFERENCES {sql_ident(info['ref_schema'])}.{sql_ident(info['ref_table'])} "
                f"({', '.join(sql_ident(c) for c in info['refs'])});\nGO\n"
            )

    lines.append(f"PRINT '{DATABASE} restore complete.';\nGO\n")

    with open(SQL_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write("".join(lines))

    size_mb = round(os.path.getsize(SQL_PATH) / 1024 / 1024, 2)
    print("SQL_OK", SQL_PATH, size_mb, "MB")

    bak_note = (
        "present"
        if os.path.exists(BAK_PATH) and os.path.getsize(BAK_PATH) > 0
        else "missing or empty — use the .sql file"
    )
    readme = f"""# Share FolderManagementDB

Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

This folder contains everything needed to recreate the same SQL Server database on another PC.

## Files

| File | Purpose |
|------|---------|
| `FolderManagementDB_Full.sql` | Full schema + data script (tables, keys, indexes, inserts, FKs) |
| `FolderManagementDB.bak` | Native SQL Server backup ({bak_note}) |
| `README_SHARE_DATABASE.md` | These instructions |
| `export_db.py` | Re-run this exporter anytime from your machine |

## Option A — Restore from SQL script (recommended for friends)

1. Install **SQL Server** (Express or Developer) and optionally **SSMS**.
2. Open `FolderManagementDB_Full.sql` in SSMS.
3. Connect with an account that can create databases (Windows admin or `sa`).
4. Execute the whole script (F5).
5. Confirm database `FolderManagementDB` exists and tables have data.

Command line:

```bat
sqlcmd -S YOUR_SERVER_NAME -E -i FolderManagementDB_Full.sql
```

Or with SQL auth:

```bat
sqlcmd -S YOUR_SERVER_NAME -U sa -P YourPassword -C -i FolderManagementDB_Full.sql
```

## Option B — Restore from .bak (if file exists and size > 0)

```sql
RESTORE DATABASE FolderManagementDB
FROM DISK = N'C:\\path\\to\\FolderManagementDB.bak'
WITH REPLACE,
MOVE N'FolderManagementDB' TO N'C:\\Program Files\\Microsoft SQL Server\\MSSQL16.SQLEXPRESS\\MSSQL\\DATA\\FolderManagementDB.mdf',
MOVE N'FolderManagementDB_log' TO N'C:\\Program Files\\Microsoft SQL Server\\MSSQL16.SQLEXPRESS\\MSSQL\\DATA\\FolderManagementDB_log.ldf';
```

Adjust `MOVE` paths to their SQL Server data folder.

## Point the API at the restored database

Edit:

`FolderManagementSystemAPI/appsettings.json`

```json
"ConnectionStrings": {{
  "DefaultConnection": "Server=YOUR_SERVER;Database=FolderManagementDB;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
}}
```

For Windows auth:

```json
"DefaultConnection": "Server=YOUR_SERVER;Database=FolderManagementDB;Trusted_Connection=True;TrustServerCertificate=True;"
```

## Notes

- Script recreates `{DATABASE}` objects cleanly (drops existing user tables in that DB first).
- Does **not** copy SQL logins; friends use their own `sa` / Windows account.
- Uploaded files under `Uploads/` and app folder content are **not** in the database — share those separately if needed for demos.
- To regenerate after more data changes: `python export_db.py` (from this folder, with your SQL Server reachable).
"""
    with open(README_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write(readme)
    print("README_OK", README_PATH)
    cn.close()


if __name__ == "__main__":
    main()
