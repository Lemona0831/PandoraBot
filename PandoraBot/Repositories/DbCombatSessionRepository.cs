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
            throw new InvalidOperationException("현재 채널에는 이미 진행 중인 활성 전투 세션이 있습니다.");
        }

        var safeTitle = string.IsNullOrWhiteSpace(title)
            ? $"전투 {DateTimeOffset.UtcNow:MM-dd HH:mm}"
            : title.Trim();

        var entity = new CombatSessionEntity
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = channelId,
            Title = safeTitle,
            Status = "active",
            CreatedByDiscordId = createdByDiscordId,
            CreatedAt = DateTimeOffset.UtcNow,
            Memo = memo ?? string.Empty
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

    public async Task<CombatSessionEndResult> EndCombatSessionAsync(string guildId, string channelId, string endedByDiscordId)
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
            throw new InvalidOperationException("현재 채널에는 종료할 활성 전투 세션이 없습니다.");
        }

        var endedAt = DateTimeOffset.UtcNow;
        var participants = await db.CombatParticipants
            .Where(x =>
                x.CombatSessionId == entity.Id &&
                x.Status != "left" &&
                x.Status != "removed")
            .ToListAsync();

        var cleanedPlayerCount = participants.Count(x => !string.Equals(x.Type, "enemy", StringComparison.OrdinalIgnoreCase));
        var cleanedEnemyCount = participants.Count(x => string.Equals(x.Type, "enemy", StringComparison.OrdinalIgnoreCase));

        foreach (var participant in participants)
        {
            participant.Status = string.Equals(participant.Type, "enemy", StringComparison.OrdinalIgnoreCase)
                ? "removed"
                : "left";
            participant.UpdatedAt = endedAt;
        }

        entity.Status = "ended";
        entity.EndedAt = endedAt;

        db.CombatLogs.Add(new CombatLogEntity
        {
            Id = Guid.NewGuid(),
            CombatSessionId = entity.Id,
            ActorDiscordId = endedByDiscordId,
            ActionType = "combat_session_end",
            TargetName = entity.Title,
            BeforeValue = $"open_participants={participants.Count}",
            AfterValue = "ended",
            Message = $"세션 종료와 함께 참가자 {participants.Count}명을 정리했습니다.",
            CreatedAt = endedAt
        });

        await db.SaveChangesAsync();
        return new CombatSessionEndResult(
            Map(entity),
            participants.Count,
            cleanedPlayerCount,
            cleanedEnemyCount);
    }

    public async Task<bool> AppendLogIfActiveAsync(
        string guildId,
        string channelId,
        string actorDiscordId,
        string actionType,
        string targetName,
        string beforeValue,
        string afterValue,
        string message)
    {
        await using var db = CreateDb();
        var session = await db.CombatSessions
            .AsNoTracking()
            .Where(x =>
                x.GuildId == guildId &&
                x.ChannelId == channelId &&
                x.Status == "active")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (session is null)
        {
            return false;
        }

        db.CombatLogs.Add(new CombatLogEntity
        {
            Id = Guid.NewGuid(),
            CombatSessionId = session.Id,
            ActorDiscordId = actorDiscordId,
            ActionType = actionType,
            TargetName = targetName,
            BeforeValue = beforeValue,
            AfterValue = afterValue,
            Message = message,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        return true;
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
