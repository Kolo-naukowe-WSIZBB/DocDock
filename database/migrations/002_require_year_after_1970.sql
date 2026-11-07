PRAGMA foreign_keys = ON;

BEGIN IMMEDIATE;

CREATE TABLE files_new (
    Id INTEGER PRIMARY KEY,
    file_name TEXT NOT NULL,
    extension TEXT NOT NULL CHECK (length(extension) > 1),
    file_type TEXT,
    file_tags TEXT CHECK (file_tags IS NULL OR json_valid(file_tags)),
    year INTEGER NOT NULL CHECK (year BETWEEN 1971 AND 9999),
    month INTEGER NOT NULL CHECK (month BETWEEN 1 AND 12),
    datatype TEXT NOT NULL CHECK (datatype IN ('image', 'video', 'audio', 'text', '3d', 'other')),
    UNIQUE (year, month, datatype, file_name, extension)
);

INSERT INTO files_new (Id, file_name, extension, file_type, file_tags, year, month, datatype)
SELECT Id, file_name, extension, file_type, file_tags, year, month, datatype
FROM files;

DROP TABLE files;
ALTER TABLE files_new RENAME TO files;

INSERT OR IGNORE INTO schema_migrations (version)
VALUES ('002_require_year_after_1970');

COMMIT;
