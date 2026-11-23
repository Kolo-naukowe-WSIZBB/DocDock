using Microsoft.Data.Sqlite;
using Xunit;

namespace DocDock.Database.Tests;

public sealed class MigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MigrationTests()
    {
        _connection = OpenDatabase();
        ApplyMigration(_connection, "001_initial_schema.sql");
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void MigrationsSeedDefaultTagsAndFiletypeMappings()
    {
        using var tagsCommand = _connection.CreateCommand();
        tagsCommand.CommandText = "SELECT tag_name FROM tags WHERE is_default = 1 ORDER BY tag_name";
        using var tagsReader = tagsCommand.ExecuteReader();
        var tags = new List<string>();
        while (tagsReader.Read())
            tags.Add(tagsReader.GetString(0));
        Assert.Equal(
            new[] { "3d", "audio", "document", "image", "other", "script", "spreadsheet", "video" },
            tags);

        using var mappingsCommand = _connection.CreateCommand();
        mappingsCommand.CommandText = """
            SELECT tags.tag_name, filetypes.extension
            FROM filetypes JOIN tags ON tags.Id = filetypes.tag_id
            """;
        using var mappingsReader = mappingsCommand.ExecuteReader();
        var mappings = new HashSet<(string Tag, string Extension)>();
        while (mappingsReader.Read())
            mappings.Add((mappingsReader.GetString(0), mappingsReader.GetString(1)));
        Assert.Contains(("image", "png"), mappings);
        Assert.Contains(("document", "pdf"), mappings);
        Assert.Contains(("spreadsheet", "csv"), mappings);
        Assert.Contains(("script", "py"), mappings);
        Assert.Contains(("script", "r"), mappings);
        Assert.Contains(("3d", "obj"), mappings);
        Assert.All(mappings, mapping => Assert.DoesNotContain(".", mapping.Extension));

        using var versionsCommand = _connection.CreateCommand();
        versionsCommand.CommandText = "SELECT version FROM schema_migrations ORDER BY version";
        using var versionsReader = versionsCommand.ExecuteReader();
        var versions = new List<string>();
        while (versionsReader.Read())
            versions.Add(versionsReader.GetString(0));
        Assert.Equal(new[] { "001_initial_schema" }, versions);
    }

    [Fact]
    public void InitialMigrationCanBeReappliedWithoutDuplicateSeedData()
    {
        const string query = "SELECT (SELECT COUNT(*) FROM tags), (SELECT COUNT(*) FROM filetypes)";
        var before = GetCounts(_connection, query);
        using var tagIdCommand = _connection.CreateCommand();
        tagIdCommand.CommandText = "SELECT Id FROM tags WHERE tag_name = 'image'";
        var imageId = tagIdCommand.ExecuteScalar();
        ApplyMigration(_connection, "001_initial_schema.sql");
        Assert.Equal(before, GetCounts(_connection, query));
        Assert.Equal(imageId, tagIdCommand.ExecuteScalar());
    }

    [Fact]
    public void ForeignKeysAreEnforcedOnAFreshConnection()
    {
        using var connection = new SqliteConnection(_connection.ConnectionString);
        connection.Open();
        AssertConstraint(() => Execute(
            connection, "INSERT INTO filetypes (tag_id, extension) VALUES (99999, 'custom')"));
    }

    [Theory]
    [InlineData("", null, 2025, 1, "document")]
    [InlineData("pdf", "not-json", 2025, 1, "document")]
    [InlineData("pdf", null, 1970, 1, "document")]
    [InlineData("pdf", null, 10000, 1, "document")]
    [InlineData("pdf", null, 2025, 0, "document")]
    [InlineData("pdf", null, 2025, 13, "document")]
    [InlineData("pdf", null, 2025, 1, "unknown")]
    public void FileConstraintsRejectInvalidValues(
        string extension, string? tags, int year, int month, string fileType)
    {
        AssertConstraint(() => InsertFile(_connection, extension, tags, year, month, fileType));
    }

    [Fact]
    public void FileConstraintsRejectDuplicateStoragePaths()
    {
        InsertFile(_connection, "pdf", "[\"work\"]", 2025, 1, "document");
        AssertConstraint(() => InsertFile(_connection, "pdf", null, 2025, 1, "document"));
    }

    [Fact]
    public void FilesAcceptSingleCharacterExtensions()
    {
        InsertFile(_connection, "r", null, 2025, 1, "script");
    }

    [Fact]
    public void FileAndFiletypeExtensionsAreCaseInsensitive()
    {
        InsertFile(_connection, "pdf", null, 2025, 1, "document");
        AssertConstraint(() => Execute(_connection, """
            INSERT INTO files (file_name, extension, year, month, file_type)
            VALUES ('REPORT', 'pdf', 2025, 1, 'document')
            """));
        AssertConstraint(() => Execute(_connection, """
            INSERT INTO files (file_name, extension, year, month, file_type)
            VALUES ('report', 'PDF', 2025, 1, 'document')
            """));
        AssertConstraint(() => Execute(_connection, """
            INSERT INTO filetypes (tag_id, extension)
            SELECT Id, 'PDF' FROM tags WHERE tag_name = 'document'
            """));
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM filetypes WHERE extension = 'PDF'";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Theory]
    [InlineData("")]
    [InlineData("../report")]
    [InlineData("..\\report")]
    [InlineData("report/child")]
    [InlineData("report\\child")]
    [InlineData("report..pdf")]
    [InlineData("report\0hidden")]
    public void FileNamesCannotEscapeTheStorageDirectory(string name)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO files (file_name, extension, year, month, file_type)
            VALUES ($name, 'pdf', 2025, 1, 'document')
            """;
        command.Parameters.AddWithValue("$name", name);
        AssertConstraint(() => command.ExecuteNonQuery());
    }

    [Theory]
    [InlineData("")]
    [InlineData(".pdf")]
    [InlineData("tar.gz")]
    [InlineData("../escape")]
    [InlineData("pdf\\escape")]
    [InlineData("pdf/escape")]
    [InlineData("pdf\0hidden")]
    public void ExtensionsCannotEscapeTheStorageDirectory(string extension)
    {
        AssertConstraint(() => InsertFile(_connection, extension, null, 2025, 1, "document"));
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO filetypes (tag_id, extension)
            SELECT Id, $extension FROM tags WHERE tag_name = 'document'
            """;
        command.Parameters.AddWithValue("$extension", extension);
        AssertConstraint(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void DefaultTagsCannotBeDeletedOrReset()
    {
        AssertConstraint(() => Execute(_connection, "DELETE FROM tags WHERE tag_name = 'image'"));
        AssertConstraint(() => Execute(_connection, "UPDATE tags SET is_default = 0 WHERE tag_name = 'image'"));
    }

    [Fact]
    public void DefaultTagsCannotBeRenamedOrReplaced()
    {
        Execute(_connection, "PRAGMA foreign_keys = OFF");
        AssertTrigger(() => Execute(_connection, "UPDATE tags SET tag_name = 'pictures' WHERE tag_name = 'image'"));
        AssertTrigger(() => Execute(_connection, "UPDATE tags SET Id = 99999 WHERE tag_name = 'image'"));
        AssertTrigger(() => Execute(_connection, "INSERT OR REPLACE INTO tags (tag_name, is_default) VALUES ('image', 0)"));
        AssertTrigger(() => Execute(_connection, "INSERT OR REPLACE INTO tags (tag_name, is_default) VALUES ('image', 1)"));
        AssertTrigger(() => Execute(_connection, """
            INSERT OR REPLACE INTO tags (Id, tag_name)
            SELECT Id, 'pictures' FROM tags WHERE tag_name = 'image'
            """));
        Execute(_connection, "INSERT INTO tags (tag_name) VALUES ('custom')");
        AssertTrigger(() => Execute(_connection, "UPDATE OR REPLACE tags SET tag_name = 'image' WHERE tag_name = 'custom'"));
    }

    [Fact]
    public void FiletypesRequireAnExistingTagAndUniqueExtension()
    {
        AssertConstraint(() => Execute(
            _connection, "INSERT INTO filetypes (tag_id, extension) VALUES (99999, 'custom')"));
        AssertConstraint(() => Execute(_connection, """
            INSERT INTO filetypes (tag_id, extension)
            SELECT Id, 'pdf' FROM tags WHERE tag_name = 'document'
            """));
    }

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Guid.NewGuid().ToString(),
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void ApplyMigration(SqliteConnection connection, string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "migrations", name);
        Execute(connection, File.ReadAllText(path));
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static (long Tags, long Filetypes) GetCounts(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static void InsertFile(
        SqliteConnection connection, string extension, string? tags, int year, int month, string fileType)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO files (file_name, extension, file_tags, year, month, file_type)
            VALUES ('report', $extension, $tags, $year, $month, $fileType)
            """;
        command.Parameters.AddWithValue("$extension", extension);
        command.Parameters.AddWithValue("$tags", (object?)tags ?? DBNull.Value);
        command.Parameters.AddWithValue("$year", year);
        command.Parameters.AddWithValue("$month", month);
        command.Parameters.AddWithValue("$fileType", fileType);
        command.ExecuteNonQuery();
    }

    private static void AssertConstraint(Action action)
    {
        var exception = Assert.Throws<SqliteException>(action);
        Assert.Equal(19, exception.SqliteErrorCode);
    }

    private static void AssertTrigger(Action action)
    {
        var exception = Assert.Throws<SqliteException>(action);
        Assert.Equal(1811, exception.SqliteExtendedErrorCode);
    }
}
