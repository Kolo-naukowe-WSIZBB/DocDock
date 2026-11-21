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
        ApplyMigration(_connection, "002_checks.sql");
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
        Assert.Contains(("image", ".png"), mappings);
        Assert.Contains(("document", ".pdf"), mappings);
        Assert.Contains(("spreadsheet", ".csv"), mappings);
        Assert.Contains(("script", ".py"), mappings);
        Assert.Contains(("3d", ".obj"), mappings);

        using var versionsCommand = _connection.CreateCommand();
        versionsCommand.CommandText = "SELECT version FROM schema_migrations ORDER BY version";
        using var versionsReader = versionsCommand.ExecuteReader();
        var versions = new List<string>();
        while (versionsReader.Read())
            versions.Add(versionsReader.GetString(0));
        Assert.Equal(new[] { "001_initial_schema", "002_checks" }, versions);
    }

    [Fact]
    public void InitialMigrationCanBeReappliedWithoutDuplicateSeedData()
    {
        const string query = "SELECT (SELECT COUNT(*) FROM tags), (SELECT COUNT(*) FROM filetypes)";
        var before = GetCounts(_connection, query);
        ApplyMigration(_connection, "001_initial_schema.sql");
        Assert.Equal(before, GetCounts(_connection, query));
    }

    [Fact]
    public void SecondMigrationPreservesExistingFileRecords()
    {
        using var connection = OpenDatabase();
        ApplyMigration(connection, "001_initial_schema.sql");
        Execute(connection, """
            INSERT INTO files (file_name, extension, file_tags, year, month, datatype)
            VALUES ('report', '.pdf', '["work"]', 2025, 6, 'document')
            """);
        ApplyMigration(connection, "002_checks.sql");

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT file_name, extension, file_tags, year, month, file_type FROM files
            """;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("report", reader.GetString(0));
        Assert.Equal(".pdf", reader.GetString(1));
        Assert.Equal("[\"work\"]", reader.GetString(2));
        Assert.Equal(2025, reader.GetInt32(3));
        Assert.Equal(6, reader.GetInt32(4));
        Assert.Equal("document", reader.GetString(5));
        Assert.False(reader.Read());
    }

    [Theory]
    [InlineData(".", null, 2025, 1, "document")]
    [InlineData(".pdf", "not-json", 2025, 1, "document")]
    [InlineData(".pdf", null, 1970, 1, "document")]
    [InlineData(".pdf", null, 10000, 1, "document")]
    [InlineData(".pdf", null, 2025, 0, "document")]
    [InlineData(".pdf", null, 2025, 13, "document")]
    [InlineData(".pdf", null, 2025, 1, "unknown")]
    public void FileConstraintsRejectInvalidValues(
        string extension, string? tags, int year, int month, string fileType)
    {
        AssertConstraint(() => InsertFile(_connection, extension, tags, year, month, fileType));
    }

    [Fact]
    public void FileConstraintsRejectDuplicateStoragePaths()
    {
        InsertFile(_connection, ".pdf", "[\"work\"]", 2025, 1, "document");
        AssertConstraint(() => InsertFile(_connection, ".pdf", null, 2025, 1, "document"));
    }

    [Fact]
    public void DefaultTagsCannotBeDeletedOrReset()
    {
        AssertConstraint(() => Execute(_connection, "DELETE FROM tags WHERE tag_name = 'image'"));
        AssertConstraint(() => Execute(_connection, "UPDATE tags SET is_default = 0 WHERE tag_name = 'image'"));
    }

    [Fact]
    public void FiletypesRequireAnExistingTagAndUniqueExtension()
    {
        AssertConstraint(() => Execute(
            _connection, "INSERT INTO filetypes (tag_id, extension) VALUES (99999, '.custom')"));
        AssertConstraint(() => Execute(_connection, """
            INSERT INTO filetypes (tag_id, extension)
            SELECT Id, '.pdf' FROM tags WHERE tag_name = 'document'
            """));
    }

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
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
}
