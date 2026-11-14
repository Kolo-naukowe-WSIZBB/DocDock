# Database table structures

The database uses SQLite. Stored file paths are derived from the file record:

```text
y{year}/m{month}/{file_type}/{file_name}{extension}
```

The initial schema and default datatype-extension records are defined in
[`migrations/001_initial_schema.sql`](./migrations/001_initial_schema.sql).
Subsequent schema changes are applied in order; migration
[`migrations/002_checks.sql`](./migrations/002_checks.sql) applies the current
file table constraints and stores the datatype in `file_type`.
During that migration, `files_new` is a temporary replacement table used to
copy existing rows into the updated schema; it is renamed to `files` before
the migration completes.

## `files`

Stores metadata and the storage location components for each file.

| Column | SQLite type | Constraints |
| --- | --- | --- |
| `Id` | INTEGER | Primary key |
| `file_name` | TEXT | Required |
| `extension` | TEXT | Required; length greater than 1 |
| `file_tags` | TEXT | Optional; must contain valid JSON |
| `year` | INTEGER | Required; greater than 1970 and no greater than 9999 |
| `month` | INTEGER | Required; between 1 and 12 |
| `file_type` | TEXT | Required; one of `image`, `video`, `audio`, `document`, `spreadsheet`, `script`, `3d`, or `other` |

The combination of `year`, `month`, `file_type`, `file_name`, and `extension`
must be unique.

```sql
CREATE TABLE files (
    Id INTEGER PRIMARY KEY,
    file_name TEXT NOT NULL,
    extension TEXT NOT NULL CHECK (length(extension) > 1),
    file_tags TEXT CHECK (json_valid(file_tags)),
    year INTEGER NOT NULL CHECK (year BETWEEN 1971 AND 9999),
    month INTEGER NOT NULL CHECK (month BETWEEN 1 AND 12),
    file_type TEXT NOT NULL
        CHECK (file_type IN ('image', 'video', 'audio', 'document', 'spreadsheet', 'script', '3d', 'other')),
    UNIQUE (year, month, file_type, file_name, extension)
);
```

## `tags`

Stores built-in and user-created tags. `is_default` is `1` for built-in tags
and `0` for user-created tags.

| Column | SQLite type | Constraints |
| --- | --- | --- |
| `Id` | INTEGER | Primary key |
| `tag_name` | TEXT | Required; unique |
| `tag_description` | TEXT | Optional |
| `is_default` | INTEGER | Required; defaults to `0`; must be `0` or `1` |

```sql
CREATE TABLE tags (
    Id INTEGER PRIMARY KEY,
    tag_name TEXT NOT NULL UNIQUE,
    tag_description TEXT,
    is_default INTEGER NOT NULL DEFAULT 0 CHECK (is_default IN (0, 1))
);
```

The default datatype tags are `image`, `video`, `audio`, `document`,
`spreadsheet`, `script`, `3d`, and `other`. Default tags should not be
deletable or changeable to user-created tags.

### Common document, spreadsheet, and script formats

| Datatype | Extensions |
| --- | --- |
| `document` | `.txt`, `.md`, `.pdf`, `.doc`, `.docx`, `.docm`, `.odt`, `.ott`, `.rtf`, `.epub`, `.tex`, `.html` |
| `spreadsheet` | `.ods`, `.ots`, `.fods`, `.xls`, `.xlsx`, `.xlsm`, `.xlsb`, `.xlt`, `.xltx`, `.xltm`, `.csv`, `.tsv`, `.numbers` |
| `script` | `.py`, `.js`, `.mjs`, `.cjs`, `.ts`, `.jsx`, `.tsx`, `.sh`, `.bash`, `.zsh`, `.fish`, `.ps1`, `.bat`, `.cmd`, `.vbs`, `.wsf`, `.rb`, `.php`, `.pl`, `.lua`, `.r`, `.sql` |

## `filetypes`

Maps a file extension to its datatype tag. Each extension is associated with
one tag.

| Column | SQLite type | Constraints |
| --- | --- | --- |
| `Id` | INTEGER | Primary key |
| `tag_id` | INTEGER | Required; foreign key to `tags.Id` |
| `extension` | TEXT | Required; unique; length greater than 1 |

```sql
CREATE TABLE filetypes (
    Id INTEGER PRIMARY KEY,
    tag_id INTEGER NOT NULL REFERENCES tags (Id),
    extension TEXT NOT NULL UNIQUE CHECK (length(extension) > 1)
);
```
