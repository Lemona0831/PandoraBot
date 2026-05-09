using Microsoft.EntityFrameworkCore;
using PandoraShared.Data;

namespace PandoraBot.Repositories;

public sealed class DbLogRepository : ILogRepository
{
    private readonly string connectionString;

    public DbLogRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task AppendRollLogAsync(
        Guid? characterId,
        string characterDisplayName,
        string statName,
        int dice1,
        int dice2,
        int modifier,
        int total,
        string resultTier)
    {
        await using var db = CreateDb();
        db.RollLogs.Add(new RollLogEntity
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            CharacterDisplayName = characterDisplayName,
            StatName = statName,
            Dice1 = dice1,
            Dice2 = dice2,
            Modifier = modifier,
            Total = total,
            ResultTier = resultTier,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<UnifiedLogEntry>> GetLogsAsync(string target, int limit)
    {
        await using var db = CreateDb();

        var normalized = (target ?? "전체").Trim().ToLowerInvariant();
        var take = Math.Clamp(limit, 1, 50);
        var entries = new List<UnifiedLogEntry>();

        if (normalized is "전체" or "all" or "")
        {
            entries.AddRange(await LoadRollLogsAsync(db, take));
            entries.AddRange(await LoadAdminLogsAsync(db, take, includeNotice: true, includeNonNotice: true));
            entries.AddRange(await LoadCombatLogsAsync(db, take));
        }
        else if (normalized is "판정" or "roll")
        {
            entries.AddRange(await LoadRollLogsAsync(db, take));
        }
        else if (normalized is "관리" or "admin")
        {
            entries.AddRange(await LoadAdminLogsAsync(db, take, includeNotice: false, includeNonNotice: true));
        }
        else if (normalized is "공지" or "notice")
        {
            entries.AddRange(await LoadAdminLogsAsync(db, take, includeNotice: true, includeNonNotice: false));
        }
        else if (normalized is "전투" or "combat")
        {
            entries.AddRange(await LoadCombatLogsAsync(db, take));
        }
        else
        {
            throw new InvalidOperationException("로그 대상은 전체, 판정, 관리, 공지, 전투 중 하나로 입력해 주세요.");
        }

        return entries
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToList();
    }

    private static async Task<List<UnifiedLogEntry>> LoadRollLogsAsync(PandoraDbContext db, int take)
    {
        var rows = await db.RollLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        return rows.Select(row => new UnifiedLogEntry(
            Category: "판정",
            Action: row.StatName,
            Target: row.CharacterDisplayName,
            Summary: $"{row.Dice1}+{row.Dice2} {(row.Modifier >= 0 ? "+" : string.Empty)}{row.Modifier} = {row.Total} / {row.ResultTier}",
            CreatedAt: row.CreatedAt)).ToList();
    }

    private static async Task<List<UnifiedLogEntry>> LoadAdminLogsAsync(
        PandoraDbContext db,
        int take,
        bool includeNotice,
        bool includeNonNotice)
    {
        var rows = await db.AdminLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Max(take * 2, 20))
            .ToListAsync();

        return rows
            .Where(row =>
            {
                var isNotice = row.ActionType.StartsWith("notice:", StringComparison.OrdinalIgnoreCase);
                return (includeNotice && isNotice) || (includeNonNotice && !isNotice);
            })
            .Take(take)
            .Select(row => new UnifiedLogEntry(
                Category: row.ActionType.StartsWith("notice:", StringComparison.OrdinalIgnoreCase) ? "공지" : "관리",
                Action: row.ActionType,
                Target: string.IsNullOrWhiteSpace(row.TargetDisplayName) ? (string.IsNullOrWhiteSpace(row.TargetId) ? row.TargetType : row.TargetId) : row.TargetDisplayName,
                Summary: string.IsNullOrWhiteSpace(row.Message) ? row.AfterValue : row.Message,
                CreatedAt: row.CreatedAt))
            .ToList();
    }

    private static async Task<List<UnifiedLogEntry>> LoadCombatLogsAsync(PandoraDbContext db, int take)
    {
        var rows = await db.CombatLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        return rows.Select(row => new UnifiedLogEntry(
            Category: "전투",
            Action: row.ActionType,
            Target: row.TargetName,
            Summary: string.IsNullOrWhiteSpace(row.Message) ? $"{row.BeforeValue} -> {row.AfterValue}" : row.Message,
            CreatedAt: row.CreatedAt)).ToList();
    }

    private PandoraDbContext CreateDb()
        => PandoraDbContextFactory.CreateOrNull(connectionString)
           ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");
}
