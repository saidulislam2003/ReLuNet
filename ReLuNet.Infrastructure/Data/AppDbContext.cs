using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReLuNet.Core.Entities;

namespace ReLuNet.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<ArticleEmbedding> ArticleEmbeddings => Set<ArticleEmbedding>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Composite keys for junction tables
        builder.Entity<ArticleTag>()
            .HasKey(at => new { at.ArticleId, at.TagId });

        builder.Entity<Like>()
            .HasKey(l => new { l.ArticleId, l.UserId });

        builder.Entity<Bookmark>()
            .HasKey(b => new { b.ArticleId, b.UserId });

        // Article -> Author
        builder.Entity<Article>()
            .HasOne(a => a.Author)
            .WithMany(u => u.Articles)
            .HasForeignKey(a => a.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comment -> Article
        builder.Entity<Comment>()
            .HasOne(c => c.Article)
            .WithMany(a => a.Comments)
            .HasForeignKey(c => c.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comment -> User
        builder.Entity<Comment>()
            .HasOne(c => c.User)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Like
        builder.Entity<Like>()
            .HasOne(l => l.Article)
            .WithMany(a => a.Likes)
            .HasForeignKey(l => l.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Like>()
            .HasOne(l => l.User)
            .WithMany(u => u.Likes)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Bookmark
        builder.Entity<Bookmark>()
            .HasOne(b => b.Article)
            .WithMany(a => a.Bookmarks)
            .HasForeignKey(b => b.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Bookmark>()
            .HasOne(b => b.User)
            .WithMany(u => u.Bookmarks)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Slug should be unique
        builder.Entity<Article>()
            .HasIndex(a => a.Slug)
            .IsUnique();

        builder.Entity<ArticleEmbedding>()
            .HasOne(e => e.Article)
            .WithOne()
            .HasForeignKey<ArticleEmbedding>(e => e.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Tag>()
            .HasIndex(t => t.Slug)
            .IsUnique();
    }
}