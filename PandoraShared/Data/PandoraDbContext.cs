using Microsoft.EntityFrameworkCore;

namespace PandoraShared.Data;

public sealed class PandoraDbContext : DbContext
{
    public PandoraDbContext(DbContextOptions<PandoraDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
    }
}

public static class PandoraDbContextFactory
{
    public static PandoraDbContext? CreateOrNull(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var options = new DbContextOptionsBuilder<PandoraDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .Options;

        return new PandoraDbContext(options);
    }
}
