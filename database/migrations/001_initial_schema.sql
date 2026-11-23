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
    file_name TEXT NOT NULL COLLATE NOCASE CHECK (
        length(file_name) > 0 AND instr(file_name, '/') = 0
        AND instr(file_name, '\') = 0 AND instr(file_name, '..') = 0
        AND instr(file_name, char(0)) = 0
    ),
    extension TEXT NOT NULL COLLATE NOCASE CHECK (
        length(extension) > 0 AND instr(extension, '/') = 0
        AND instr(extension, '\') = 0 AND instr(extension, '.') = 0
        AND instr(extension, char(0)) = 0
    ),
    file_tags TEXT CHECK (file_tags IS NULL OR json_valid(file_tags)),
    year INTEGER NOT NULL CHECK (year BETWEEN 1971 AND 9999),
    month INTEGER NOT NULL CHECK (month BETWEEN 1 AND 12),
    file_type TEXT NOT NULL CHECK (file_type IN ('image', 'video', 'audio', 'document', 'spreadsheet', 'script', '3d', 'other')),
    UNIQUE (year, month, file_type, file_name, extension)
);

CREATE TABLE IF NOT EXISTS filetypes (
    Id INTEGER PRIMARY KEY,
    tag_id INTEGER NOT NULL REFERENCES tags (Id),
    extension TEXT NOT NULL COLLATE NOCASE UNIQUE CHECK (
        length(extension) > 0 AND instr(extension, '/') = 0
        AND instr(extension, '\') = 0 AND instr(extension, '.') = 0
        AND instr(extension, char(0)) = 0
    )
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

CREATE TRIGGER IF NOT EXISTS prevent_default_tag_replace
BEFORE INSERT ON tags
WHEN EXISTS (
    SELECT 1 FROM tags
    WHERE is_default = 1 AND (tag_name = NEW.tag_name OR Id = NEW.Id)
)
BEGIN
    SELECT RAISE(ABORT, 'Default tags cannot be replaced');
END;

CREATE TRIGGER IF NOT EXISTS prevent_default_tag_rename
BEFORE UPDATE OF tag_name, Id ON tags
WHEN (OLD.is_default = 1 AND (NEW.tag_name <> OLD.tag_name OR NEW.Id <> OLD.Id))
    OR EXISTS (
        SELECT 1 FROM tags
        WHERE is_default = 1 AND Id <> OLD.Id
            AND (tag_name = NEW.tag_name OR Id = NEW.Id)
    )
BEGIN
    SELECT RAISE(ABORT, 'Default tags cannot be renamed or replaced');
END;


WITH default_tags (tag_name) AS (VALUES
    ('image'),
    ('video'),
    ('audio'),
    ('document'),
    ('spreadsheet'),
    ('script'),
    ('3d'),
    ('other')
)
INSERT INTO tags (tag_name, is_default)
SELECT default_tags.tag_name, 1 FROM default_tags
WHERE NOT EXISTS (SELECT 1 FROM tags WHERE tags.tag_name = default_tags.tag_name);

UPDATE tags SET is_default = 1 WHERE tag_name IN (
    'image', 'video', 'audio', 'document', 'spreadsheet', 'script', '3d', 'other'
);

WITH datatype_extensions (tag_name, extension) AS (
    VALUES
        ('image', 'jpg'),
        ('image', 'jpeg'),
        ('image', 'png'),
        ('image', 'gif'),
        ('image', 'webp'),
        ('image', 'svg'),
        ('image', 'bmp'),
        ('video', 'mp4'),
        ('video', 'mov'),
        ('video', 'avi'),
        ('video', 'mkv'),
        ('video', 'webm'),
        ('video', 'wmv'),
        ('video', 'flv'),
        ('audio', 'mp3'),
        ('audio', 'wav'),
        ('audio', 'flac'),
        ('audio', 'aac'),
        ('audio', 'ogg'),
        ('audio', 'm4a'),
        ('audio', 'wma'),
        ('document', 'txt'),
        ('document', 'md'),
        ('document', 'pdf'),
        ('document', 'doc'),
        ('document', 'docx'),
        ('document', 'docm'),
        ('document', 'odt'),
        ('document', 'ott'),
        ('document', 'rtf'),
        ('document', 'epub'),
        ('document', 'tex'),
        ('document', 'html'),
        ('spreadsheet', 'ods'),
        ('spreadsheet', 'ots'),
        ('spreadsheet', 'fods'),
        ('spreadsheet', 'xls'),
        ('spreadsheet', 'xlsx'),
        ('spreadsheet', 'xlsm'),
        ('spreadsheet', 'xlsb'),
        ('spreadsheet', 'xlt'),
        ('spreadsheet', 'xltx'),
        ('spreadsheet', 'xltm'),
        ('spreadsheet', 'csv'),
        ('spreadsheet', 'tsv'),
        ('spreadsheet', 'numbers'),
        ('script', 'py'),
        ('script', 'js'),
        ('script', 'mjs'),
        ('script', 'cjs'),
        ('script', 'ts'),
        ('script', 'jsx'),
        ('script', 'tsx'),
        ('script', 'sh'),
        ('script', 'bash'),
        ('script', 'zsh'),
        ('script', 'fish'),
        ('script', 'ps1'),
        ('script', 'bat'),
        ('script', 'cmd'),
        ('script', 'vbs'),
        ('script', 'wsf'),
        ('script', 'rb'),
        ('script', 'php'),
        ('script', 'pl'),
        ('script', 'lua'),
        ('script', 'r'),
        ('script', 'sql'),
        ('3d', 'obj'),
        ('3d', 'fbx'),
        ('3d', 'stl'),
        ('3d', 'dae'),
        ('3d', 'glb'),
        ('3d', 'gltf'),
        ('3d', 'blend')
)
INSERT OR IGNORE INTO filetypes (tag_id, extension)
SELECT tags.Id, datatype_extensions.extension
FROM datatype_extensions
JOIN tags ON tags.tag_name = datatype_extensions.tag_name;

INSERT OR IGNORE INTO schema_migrations (version)
VALUES ('001_initial_schema');

COMMIT;
