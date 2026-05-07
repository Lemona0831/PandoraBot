using Microsoft.EntityFrameworkCore;
using PandoraShared.Data;

namespace PandoraBot.Repositories;

public sealed class DbCombatSessionRepository : ICombatSessionRepository
{
    private readonly string connectionString;

    public DbCombatSessionRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<CombatSessionSummary> StartCombatSessionAsync(
        string guildId,
        string channelId,
        string title,
        string createdByDiscordId,
        string memo = "")
    {
        await using var db = CreateDb();

        var existing = await db.CombatSessions
            .AsNoTracking()
            .Where(x =>
                x.GuildId == guildId &&
                x.ChannelId == channelId &&
                x.Status == "active")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            throw new InvalidOperationException("이 채널에는 이미 진행 중인 활성 전투 세션이 있습니다.");
        }

        var entity = new CombatSessionEntity
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = channelId,
            Title = title.Trim(),
            Status = "active",
            CreatedByDiscordId = createdByDiscordId,
            CreatedAt = DateTimeOffset.UtcNow,
            Memo = memo ?? ""
        };

        db.CombatSessions.Add(entity);
        await db.SaveChangesAsync();
        return Map(entity);
    }

    public async Task<CombatSessionSummary?> GetActiveCombatSessionAsync(string guildId, string channelId)
    {
        await using var db = CreateDb();
        var entity = await db.CombatSessions
            .AsNoTracking()
            .Where(x =>
                x.GuildId == guildId &&
                x.ChannelId == channelId &&
                x.Status == "active")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        return entity is null ? null : Map(entity);
    }

    public async Task<CombatSessionSummary> EndCombatSessionAsync(string guildId, string channelId)
    {
        await using var db = CreateDb();
        var entity = await db.CombatSessions
            .Where(x =>
                x.GuildId == guildId &&
                x.ChannelId == channelId &&
                x.Status == "active")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (entity is null)
        {
            throw new InvalidOperationException("이 채널에는 종료할 활성 전투 세션이 없습니다.");
        }

        entity.Status = "ended";
        entity.EndedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Map(entity);
    }

    private PandoraDbContext CreateDb()
        => PandoraDbContextFactory.CreateOrNull(connectionString)
           ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");

    private static CombatSessionSummary Map(CombatSessionEntity entity)
        => new(
            entity.Id,
            entity.GuildId,
            entity.ChannelId,
            entity.Title,
            entity.Status,
            entity.CreatedByDiscordId,
            entity.CreatedAt,
            entity.EndedAt,
            entity.Memo);
}
