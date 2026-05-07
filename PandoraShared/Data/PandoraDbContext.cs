using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;

namespace PandoraShared.Data;

public sealed class PandoraDbContext : DbContext
{
    public PandoraDbContext(DbContextOptions<PandoraDbContext> options)
        : base(options)
    {
    }

    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
    public DbSet<CharacterSelectionEntity> CharacterSelections => Set<CharacterSelectionEntity>();
    public DbSet<RollLogEntity> RollLogs => Set<RollLogEntity>();
    public DbSet<AdminLogEntity> AdminLogs => Set<AdminLogEntity>();
    public DbSet<EnemyEntity> Enemies => Set<EnemyEntity>();
    public DbSet<EnemyDropEntity> EnemyDrops => Set<EnemyDropEntity>();
    public DbSet<EnemyDropSettingEntity> EnemyDropSettings => Set<EnemyDropSettingEntity>();
    public DbSet<CombatSessionEntity> CombatSessions => Set<CombatSessionEntity>();
    public DbSet<CombatParticipantEntity> CombatParticipants => Set<CombatParticipantEntity>();
    public DbSet<CombatLogEntity> CombatLogs => Set<CombatLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        ConfigureCharacters(modelBuilder);
        ConfigureCharacterSelections(modelBuilder);
        ConfigureRollLogs(modelBuilder);
        ConfigureAdminLogs(modelBuilder);
        ConfigureEnemies(modelBuilder);
        ConfigureEnemyDrops(modelBuilder);
        ConfigureEnemyDropSettings(modelBuilder);
        ConfigureCombatSessions(modelBuilder);
        ConfigureCombatParticipants(modelBuilder);
        ConfigureCombatLogs(modelBuilder);

        ApplySnakeCaseNames(modelBuilder);
    }

    private static void ConfigureCharacters(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CharacterEntity>();
        entity.ToTable("characters");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.DiscordUserId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.SourceSheetId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.SourceSheetUrl).HasMaxLength(512);
        entity.Property(x => x.SourceDocumentTitle).HasMaxLength(256);
        entity.Property(x => x.ImportedCharacterName).HasMaxLength(128);
        entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        entity.Property(x => x.NormalizedDisplayName).HasMaxLength(128).IsRequired();
        entity.Property(x => x.ReviewStatus).HasMaxLength(32).IsRequired();
        entity.HasIndex(x => new { x.DiscordUserId, x.SourceSheetId }).IsUnique();
        entity.HasIndex(x => x.NormalizedDisplayName);
    }

    private static void ConfigureCharacterSelections(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CharacterSelectionEntity>();
        entity.ToTable("character_selections");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.DiscordUserId).HasMaxLength(64).IsRequired();
        entity.HasIndex(x => x.DiscordUserId).IsUnique();
        entity.HasIndex(x => x.CharacterId);
        entity.HasOne(x => x.Character)
            .WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureRollLogs(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RollLogEntity>();
        entity.ToTable("roll_logs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.CharacterDisplayName).HasMaxLength(128).IsRequired();
        entity.Property(x => x.StatName).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ResultTier).HasMaxLength(32).IsRequired();
        entity.HasIndex(x => x.CharacterId);
        entity.HasIndex(x => x.CreatedAt);
        entity.HasOne(x => x.Character)
            .WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureAdminLogs(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AdminLogEntity>();
        entity.ToTable("admin_logs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.AdminDiscordId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ActionType).HasMaxLength(64).IsRequired();
        entity.Property(x => x.TargetType).HasMaxLength(64).IsRequired();
        entity.Property(x => x.TargetId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.BeforeValue).HasMaxLength(2048);
        entity.Property(x => x.AfterValue).HasMaxLength(2048);
        entity.Property(x => x.Message).HasMaxLength(4096);
        entity.HasIndex(x => x.CreatedAt);
        entity.HasIndex(x => new { x.TargetType, x.TargetId });
    }

    private static void ConfigureEnemies(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EnemyEntity>();
        entity.ToTable("enemies");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.EnemyCode).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
        entity.Property(x => x.NormalizedName).HasMaxLength(128).IsRequired();
        entity.Property(x => x.EncounterTag).HasMaxLength(128);
        entity.Property(x => x.Memo).HasMaxLength(4096);
        entity.HasIndex(x => x.EnemyCode).IsUnique();
        entity.HasIndex(x => x.NormalizedName);
    }

    private static void ConfigureEnemyDrops(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EnemyDropEntity>();
        entity.ToTable("enemy_drops");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.ItemName).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Probability).HasPrecision(5, 4);
        entity.Property(x => x.Memo).HasMaxLength(2048);
        entity.HasIndex(x => x.EnemyId);
        entity.HasOne(x => x.Enemy)
            .WithMany(x => x.Drops)
            .HasForeignKey(x => x.EnemyId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureEnemyDropSettings(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EnemyDropSettingEntity>();
        entity.ToTable("enemy_drop_settings");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.DropRate).HasPrecision(5, 4);
        entity.Property(x => x.Memo).HasMaxLength(2048);
        entity.HasIndex(x => x.EnemyId).IsUnique();
        entity.HasOne(x => x.Enemy)
            .WithOne(x => x.DropSetting)
            .HasForeignKey<EnemyDropSettingEntity>(x => x.EnemyId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureCombatSessions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CombatSessionEntity>();
        entity.ToTable("combat_sessions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.GuildId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ChannelId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        entity.Property(x => x.CreatedByDiscordId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Memo).HasMaxLength(4096);
        entity.HasIndex(x => new { x.GuildId, x.ChannelId, x.Status });
        entity.HasIndex(x => x.CreatedAt);
    }

    private static void ConfigureCombatParticipants(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CombatParticipantEntity>();
        entity.ToTable("combat_participants");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Type).HasMaxLength(32).IsRequired();
        entity.Property(x => x.SourceId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        entity.Property(x => x.Memo).HasMaxLength(4096);
        entity.HasIndex(x => x.CombatSessionId);
        entity.HasIndex(x => new { x.CombatSessionId, x.DisplayName });
        entity.HasOne(x => x.CombatSession)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.CombatSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureCombatLogs(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CombatLogEntity>();
        entity.ToTable("combat_logs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.ActorDiscordId).HasMaxLength(64);
        entity.Property(x => x.ActionType).HasMaxLength(64).IsRequired();
        entity.Property(x => x.TargetName).HasMaxLength(128);
        entity.Property(x => x.BeforeValue).HasMaxLength(2048);
        entity.Property(x => x.AfterValue).HasMaxLength(2048);
        entity.Property(x => x.Message).HasMaxLength(4096);
        entity.HasIndex(x => x.CombatSessionId);
        entity.HasIndex(x => x.CreatedAt);
        entity.HasOne(x => x.CombatSession)
            .WithMany(x => x.Logs)
            .HasForeignKey(x => x.CombatSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current))
            {
                var needsUnderscore = i > 0 &&
                    (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]) ||
                     (i + 1 < value.Length && char.IsLower(value[i + 1])));

                if (needsUnderscore)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else if (current == '-')
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
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

        var options = BuildOptions(connectionString);
        return new PandoraDbContext(options);
    }

    internal static DbContextOptions<PandoraDbContext> BuildOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<PandoraDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .Options;
    }
}

public sealed class PandoraDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PandoraDbContext>
{
    public PandoraDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile(Path.Combine("PandoraBot", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine("PandoraBot", "appsettings.Development.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("PandoraDb") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__PandoraDb") ??
            "Host=localhost;Port=5432;Database=pandora;Username=postgres;Password=postgres";

        return new PandoraDbContext(PandoraDbContextFactory.BuildOptions(connectionString));
    }

    private static string ResolveBasePath()
    {
        var current = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(current, "PandoraBot.slnx")))
        {
            return current;
        }

        var candidate = Path.GetFullPath(Path.Combine(current, ".."));
        if (File.Exists(Path.Combine(candidate, "PandoraBot.slnx")))
        {
            return candidate;
        }

        return current;
    }
}
