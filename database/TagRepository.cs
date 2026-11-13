using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public sealed class Tag
{
    public long Id { get; set; }

    public string TagName { get; set; } = string.Empty;

    public string? TagDescription { get; set; }

    public bool IsDefault { get; set; }
}

public sealed class DocDockDbContext : DbContext
{
    public DocDockDbContext(DbContextOptions<DocDockDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tag = modelBuilder.Entity<Tag>();

        tag.ToTable("tags", table =>
            table.HasCheckConstraint("CK_tags_is_default", "is_default IN (0, 1)"));
        tag.HasKey(entity => entity.Id);
        tag.Property(entity => entity.Id).HasColumnName("Id");
        tag.Property(entity => entity.TagName)
            .HasColumnName("tag_name")
            .IsRequired();
        tag.HasIndex(entity => entity.TagName).IsUnique();
        tag.Property(entity => entity.TagDescription)
            .HasColumnName("tag_description");
        tag.Property(entity => entity.IsDefault)
            .HasColumnName("is_default")
            .HasDefaultValue(false)
            .IsRequired();
    }
}

public sealed class TagRepository
{
    private readonly DocDockDbContext _dbContext;

    public TagRepository(DocDockDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Tag> InsertTagAsync(
        string tagName,
        string? tagDescription,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("Tag name cannot be empty.", nameof(tagName));
        }

        var tag = new Tag
        {
            TagName = tagName,
            TagDescription = tagDescription,
            IsDefault = false
        };

        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return tag;
    }

    public async Task<bool> DeleteTagAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tag = await _dbContext.Tags
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (tag is null || tag.IsDefault)
        {
            return false;
        }

        _dbContext.Tags.Remove(tag);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<List<Tag>> GetTagsAsync(
        bool nonDefault,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Tag> tags = _dbContext.Tags;

        if (nonDefault)
        {
            tags = tags.Where(tag => !tag.IsDefault);
        }

        return await tags
            .OrderBy(tag => tag.TagName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
