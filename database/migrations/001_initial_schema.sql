PRAGMA foreign_keys = ON;

BEGIN IMMEDIATE;

CREATE TABLE IF NOT EXISTS schema_migrations (
    version TEXT PRIMARY KEY,
    applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS tags (
    Id INTEGER PRIMARY KEY,
    tag_name TEXT NOT NULL UNIQUE,
    tag_description TEXT,
    is_default INTEGER NOT NULL DEFAULT 0 CHECK (is_default IN (0, 1))
);

CREATE TABLE IF NOT EXISTS files (
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

CREATE TABLE IF NOT EXISTS filetypes (
    Id INTEGER PRIMARY KEY,
    tag_id INTEGER NOT NULL REFERENCES tags (Id),
    extension TEXT NOT NULL UNIQUE CHECK (length(extension) > 1)
);

CREATE TRIGGER IF NOT EXISTS prevent_default_tag_deletion
BEFORE DELETE ON tags
WHEN OLD.is_default = 1
BEGIN
    SELECT RAISE(ABORT, 'Default tags cannot be deleted');
END;

CREATE TRIGGER IF NOT EXISTS prevent_default_tag_flag_reset
BEFORE UPDATE OF is_default ON tags
WHEN OLD.is_default = 1 AND NEW.is_default = 0
BEGIN
    SELECT RAISE(ABORT, 'Default tags must remain default');
END;

INSERT INTO tags (tag_name, is_default) VALUES
    ('image', 1),
    ('video', 1),
    ('audio', 1),
    ('text', 1),
    ('3d', 1),
    ('other', 1)
ON CONFLICT (tag_name) DO UPDATE SET
    is_default = 1;

WITH datatype_extensions (tag_name, extension) AS (
    VALUES
        ('image', '.jpg'),
        ('image', '.jpeg'),
        ('image', '.png'),
        ('image', '.gif'),
        ('image', '.webp'),
        ('image', '.svg'),
        ('image', '.bmp'),
        ('video', '.mp4'),
        ('video', '.mov'),
        ('video', '.avi'),
        ('video', '.mkv'),
        ('video', '.webm'),
        ('video', '.wmv'),
        ('video', '.flv'),
        ('audio', '.mp3'),
        ('audio', '.wav'),
        ('audio', '.flac'),
        ('audio', '.aac'),
        ('audio', '.ogg'),
        ('audio', '.m4a'),
        ('audio', '.wma'),
        ('text', '.txt'),
        ('text', '.md'),
        ('text', '.csv'),
        ('text', '.json'),
        ('text', '.xml'),
        ('text', '.html'),
        ('text', '.css'),
        ('text', '.js'),
        ('text', '.ts'),
        ('text', '.sql'),
        ('3d', '.obj'),
        ('3d', '.fbx'),
        ('3d', '.stl'),
        ('3d', '.dae'),
        ('3d', '.glb'),
        ('3d', '.gltf'),
        ('3d', '.blend')
)
INSERT OR IGNORE INTO filetypes (tag_id, extension)
SELECT tags.Id, datatype_extensions.extension
FROM datatype_extensions
JOIN tags ON tags.tag_name = datatype_extensions.tag_name;

INSERT OR IGNORE INTO schema_migrations (version)
VALUES ('001_initial_schema');

COMMIT;
