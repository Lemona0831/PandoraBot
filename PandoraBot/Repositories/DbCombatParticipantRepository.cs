using Microsoft.EntityFrameworkCore;
using PandoraShared.Data;

namespace PandoraBot.Repositories;

public sealed class DbCombatParticipantRepository : ICombatParticipantRepository
{
    private readonly string connectionString;

    public DbCombatParticipantRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<CombatParticipantSummary> AddPlayerAsync(
        string guildId,
        string channelId,
        string characterName,
        string createdByDiscordId,
        string memo = "")
    {
        await using var db = CreateDb();
        var session = await GetActiveSessionAsync(db, guildId, channelId);

        var query = characterName.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("참가시킬 캐릭터 이름을 입력해 주세요.");
        }

        var character = await FindCharacterAsync(db, query);

        var duplicate = await db.CombatParticipants.AnyAsync(x =>
            x.CombatSessionId == session.Id &&
            x.Type == "player" &&
            x.SourceId == character.Id.ToString() &&
            x.Status != "left" &&
            x.Status != "removed");

        if (duplicate)
        {
            throw new InvalidOperationException("해당 캐릭터는 이미 현재 전투 세션에 참가해 있습니다.");
        }

        var now = DateTimeOffset.UtcNow;
        var participant = new CombatParticipantEntity
        {
            Id = Guid.NewGuid(),
            CombatSessionId = session.Id,
            Type = "player",
            SourceId = character.Id.ToString(),
            DisplayName = character.DisplayName,
            NormalizedDisplayName = NormalizeSearch(character.DisplayName),
            CurrentHp = Math.Clamp(character.CurrentHp, 0, Math.Max(1, character.MaxHp)),
            MaxHp = Math.Max(1, character.MaxHp),
            Status = "active",
            CreatedByDiscordId = createdByDiscordId,
            Memo = memo ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.CombatParticipants.Add(participant);
        db.CombatLogs.Add(new CombatLogEntity
        {
            Id = Guid.NewGuid(),
            CombatSessionId = session.Id,
            ActorDiscordId = createdByDiscordId,
            ActionType = "combat_join",
            TargetParticipantId = participant.Id,
            TargetName = participant.DisplayName,
            BeforeValue = string.Empty,
            AfterValue = $"player/{participant.CurrentHp}/{participant.MaxHp}",
            Message = BuildJoinLogMessage(participant, memo),
            CreatedAt = now
        });

        await db.SaveChangesAsync();
        return Map(participant);
    }

    public async Task<IReadOnlyList<CombatParticipantSummary>> AddEnemiesAsync(
        string guildId,
        string channelId,
        string enemyIdOrName,
        int quantity,
        string createdByDiscordId,
        string memo = "")
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("수량은 1 이상이어야 합니다.");
        }

        await using var db = CreateDb();
        var session = await GetActiveSessionAsync(db, guildId, channelId);
        var enemy = await FindEnemyAsync(db, enemyIdOrName);

        var existingNames = await db.CombatParticipants
            .Where(x =>
                x.CombatSessionId == session.Id &&
                x.Status != "left" &&
                x.Status != "removed")
            .Select(x => x.DisplayName)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var usedNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var participants = new List<CombatParticipantEntity>();

        for (var i = 0; i < quantity; i++)
        {
            var baseName = quantity == 1 ? enemy.Name : $"{enemy.Name} {ToSpawnSuffix(i)}";
            var displayName = CreateUniqueName(baseName, usedNames);
            usedNames.Add(displayName);

            participants.Add(new CombatParticipantEntity
            {
                Id = Guid.NewGuid(),
                CombatSessionId = session.Id,
                Type = "enemy",
                SourceId = enemy.Id.ToString(),
                DisplayName = displayName,
                NormalizedDisplayName = NormalizeSearch(displayName),
                CurrentHp = Math.Max(1, enemy.MaxHp),
                MaxHp = Math.Max(1, enemy.MaxHp),
                Status = "active",
                CreatedByDiscordId = createdByDiscordId,
                Memo = memo ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        db.CombatParticipants.AddRange(participants);
        foreach (var participant in participants)
        {
            db.CombatLogs.Add(new CombatLogEntity
            {
                Id = Guid.NewGuid(),
                CombatSessionId = session.Id,
                ActorDiscordId = createdByDiscordId,
                ActionType = "combat_spawn",
                TargetParticipantId = participant.Id,
                TargetName = participant.DisplayName,
                BeforeValue = string.Empty,
                AfterValue = $"enemy/{participant.CurrentHp}/{participant.MaxHp}",
                Message = BuildJoinLogMessage(participant, memo),
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync();
        return participants.Select(Map).ToList();
    }

    public async Task<CombatParticipantHpResult> AdjustHpAsync(
        string guildId,
        string channelId,
        string participantIdOrName,
        int amount,
        string action,
        string actorDiscordId,
        string memo = "")
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("수치는 1 이상이어야 합니다.");
        }

        var normalizedAction = action.Trim().ToLowerInvariant();
        if (normalizedAction is not ("damage" or "heal"))
        {
            throw new InvalidOperationException("action은 damage 또는 heal이어야 합니다.");
        }

        await using var db = CreateDb();
        var session = await GetActiveSessionAsync(db, guildId, channelId);
        var participant = await FindParticipantAsync(db, session.Id, participantIdOrName);

        var oldHp = participant.CurrentHp;
        var nextHp = normalizedAction == "heal"
            ? Math.Clamp(participant.CurrentHp + amount, 0, participant.MaxHp)
            : Math.Clamp(participant.CurrentHp - amount, 0, participant.MaxHp);
        var nextStatus = nextHp <= 0 ? "defeated" : "active";
        var now = DateTimeOffset.UtcNow;

        participant.CurrentHp = nextHp;
        participant.Status = nextStatus;
        participant.UpdatedAt = now;

        var characterSynced = false;
        if (string.Equals(participant.Type, "player", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(participant.SourceId, out var characterId))
        {
            var character = await db.Characters.FirstOrDefaultAsync(x => x.Id == characterId);
            if (character is not null)
            {
                character.CurrentHp = nextHp;
                character.UpdatedAt = now;
                characterSynced = true;
            }
        }

        db.CombatLogs.Add(new CombatLogEntity
        {
            Id = Guid.NewGuid(),
            CombatSessionId = session.Id,
            ActorDiscordId = actorDiscordId,
            ActionType = normalizedAction == "heal" ? "combat_heal" : "combat_damage",
            TargetParticipantId = participant.Id,
            TargetName = participant.DisplayName,
            BeforeValue = oldHp.ToString(),
            AfterValue = nextHp.ToString(),
            Message = BuildCombatLogMessage(participant, amount, oldHp, nextHp, memo),
            CreatedAt = now
        });

        await db.SaveChangesAsync();

        return new CombatParticipantHpResult(
            session.Id,
            participant.Id.ToString(),
            participant.Type,
            participant.SourceId,
            participant.DisplayName,
            oldHp,
            nextHp,
            participant.MaxHp,
            nextStatus,
            characterSynced);
    }

    public async Task<IReadOnlyList<CombatParticipantSummary>> GetParticipantsAsync(string guildId, string channelId)
    {
        await using var db = CreateDb();
        var session = await GetActiveSessionAsync(db, guildId, channelId);
        var participants = await db.CombatParticipants
            .AsNoTracking()
            .Where(x =>
                x.CombatSessionId == session.Id &&
                x.Status != "left" &&
                x.Status != "removed")
            .OrderBy(x => x.Type)
            .ThenBy(x => x.DisplayName)
            .ToListAsync();

        return participants.Select(Map).ToList();
    }

    public async Task<CombatParticipantSummary> RemoveParticipantAsync(
        string guildId,
        string channelId,
        string participantIdOrName,
        string actorDiscordId,
        string memo = "")
    {
        await using var db = CreateDb();
        var session = await GetActiveSessionAsync(db, guildId, channelId);
        var participant = await FindParticipantAsync(db, session.Id, participantIdOrName);
        var beforeStatus = participant.Status;
        var now = DateTimeOffset.UtcNow;

        participant.Status = string.Equals(participant.Type, "enemy", StringComparison.OrdinalIgnoreCase)
            ? "removed"
            : "left";
        participant.UpdatedAt = now;

        db.CombatLogs.Add(new CombatLogEntity
        {
            Id = Guid.NewGuid(),
            CombatSessionId = session.Id,
            ActorDiscordId = actorDiscordId,
            ActionType = string.Equals(participant.Status, "left", StringComparison.OrdinalIgnoreCase)
                ? "combat_leave"
                : "combat_remove",
            TargetParticipantId = participant.Id,
            TargetName = participant.DisplayName,
            BeforeValue = beforeStatus,
            AfterValue = participant.Status,
            Message = BuildRemovalLogMessage(participant, memo),
            CreatedAt = now
        });

        await db.SaveChangesAsync();
        return Map(participant);
    }

    public async Task<IReadOnlyList<CombatParticipantSummary>> CleanupDefeatedEnemiesAsync(
        string guildId,
        string channelId,
        string actorDiscordId,
        string memo = "")
    {
        await using var db = CreateDb();
        var session = await GetActiveSessionAsync(db, guildId, channelId);
        var targets = await db.CombatParticipants
            .Where(x =>
                x.CombatSessionId == session.Id &&
                x.Type == "enemy" &&
                x.Status != "removed" &&
                x.Status != "left" &&
                (x.CurrentHp <= 0 || x.Status == "defeated"))
            .OrderBy(x => x.DisplayName)
            .ToListAsync();

        if (targets.Count == 0)
        {
            return Array.Empty<CombatParticipantSummary>();
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var participant in targets)
        {
            var beforeStatus = participant.Status;
            participant.Status = "removed";
            participant.UpdatedAt = now;

            db.CombatLogs.Add(new CombatLogEntity
            {
                Id = Guid.NewGuid(),
                CombatSessionId = session.Id,
                ActorDiscordId = actorDiscordId,
                ActionType = "combat_cleanup",
                TargetParticipantId = participant.Id,
                TargetName = participant.DisplayName,
                BeforeValue = beforeStatus,
                AfterValue = "removed",
                Message = BuildRemovalLogMessage(participant, memo),
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync();
        return targets.Select(Map).ToList();
    }

    private PandoraDbContext CreateDb()
        => PandoraDbContextFactory.CreateOrNull(connectionString)
           ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");

    private static async Task<CombatSessionEntity> GetActiveSessionAsync(PandoraDbContext db, string guildId, string channelId)
    {
        var session = await db.CombatSessions
            .Where(x => x.GuildId == guildId && x.ChannelId == channelId && x.Status == "active")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (session is null)
        {
            throw new InvalidOperationException("현재 채널에 활성 전투 세션이 없습니다. 먼저 /전투시작으로 세션을 열어주세요.");
        }

        return session;
    }

    private static async Task<CharacterEntity> FindCharacterAsync(PandoraDbContext db, string query)
    {
        var normalizedQuery = NormalizeSearch(query);
        var exactMatches = await db.Characters
            .Where(x =>
                x.SourceSheetId == query ||
                x.DisplayName == query ||
                x.ImportedCharacterName == query ||
                x.SourceDocumentTitle == query ||
                x.NormalizedDisplayName == normalizedQuery)
            .OrderBy(x => x.DisplayName)
            .ToListAsync();

        if (exactMatches.Count == 1)
        {
            return exactMatches[0];
        }

        if (exactMatches.Count > 1)
        {
            throw new InvalidOperationException("같은 이름의 캐릭터가 여러 개 있습니다. 더 정확한 이름이나 source_sheet_id로 다시 시도해 주세요.");
        }

        var partialMatches = await db.Characters
            .Where(x =>
                x.DisplayName.Contains(query) ||
                x.ImportedCharacterName.Contains(query) ||
                x.SourceDocumentTitle.Contains(query) ||
                x.NormalizedDisplayName.Contains(normalizedQuery))
            .OrderBy(x => x.DisplayName)
            .Take(5)
            .ToListAsync();

        if (partialMatches.Count == 1)
        {
            return partialMatches[0];
        }

        if (partialMatches.Count > 1)
        {
            throw new InvalidOperationException("조건에 맞는 캐릭터가 여러 명입니다. 더 정확한 이름이나 source_sheet_id로 다시 시도해 주세요.");
        }

        throw new InvalidOperationException("해당 캐릭터를 찾을 수 없습니다.");
    }

    private static async Task<EnemyEntity> FindEnemyAsync(PandoraDbContext db, string enemyIdOrName)
    {
        var query = enemyIdOrName.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("소환할 에너미 이름 또는 ID를 입력해 주세요.");
        }

        var normalizedQuery = NormalizeSearch(query).ToLowerInvariant();
        var exactMatches = await db.Enemies
            .Where(x => x.EnemyCode == query || x.Name == query || x.NormalizedName == normalizedQuery)
            .OrderBy(x => x.EnemyCode)
            .ToListAsync();

        if (exactMatches.Count == 1)
        {
            return EnsureEnemyUsable(exactMatches[0]);
        }

        if (exactMatches.Count > 1)
        {
            throw new InvalidOperationException("같은 이름의 에너미가 여러 개 있습니다. 에너미 ID로 다시 지정해 주세요.");
        }

        var partialMatches = await db.Enemies
            .Where(x => x.EnemyCode.Contains(query) || x.Name.Contains(query) || x.NormalizedName.Contains(normalizedQuery))
            .OrderBy(x => x.EnemyCode)
            .Take(5)
            .ToListAsync();

        if (partialMatches.Count == 1)
        {
            return EnsureEnemyUsable(partialMatches[0]);
        }

        if (partialMatches.Count > 1)
        {
            throw new InvalidOperationException("조건에 맞는 에너미가 여러 개입니다. 에너미 ID 또는 더 정확한 이름으로 다시 시도해 주세요.");
        }

        throw new InvalidOperationException("해당 에너미를 찾을 수 없습니다.");
    }

    private static EnemyEntity EnsureEnemyUsable(EnemyEntity entity)
    {
        if (!entity.IsActive)
        {
            throw new InvalidOperationException("비활성화된 에너미입니다. `/에너미활성화`로 다시 활성화한 뒤 사용해 주세요.");
        }

        return entity;
    }

    private static async Task<CombatParticipantEntity> FindParticipantAsync(PandoraDbContext db, Guid combatSessionId, string participantIdOrName)
    {
        var query = participantIdOrName.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("대상 참가자를 입력해 주세요.");
        }

        if (Guid.TryParse(query, out var participantGuid))
        {
            var byId = await db.CombatParticipants.FirstOrDefaultAsync(x =>
                x.CombatSessionId == combatSessionId &&
                x.Id == participantGuid &&
                x.Status != "left" &&
                x.Status != "removed");
            if (byId is not null)
            {
                return byId;
            }
        }

        var normalizedQuery = NormalizeSearch(query);

        var exactMatches = await db.CombatParticipants
            .Where(x =>
                x.CombatSessionId == combatSessionId &&
                x.Status != "left" &&
                x.Status != "removed" &&
                (x.DisplayName == query || x.NormalizedDisplayName == normalizedQuery))
            .OrderBy(x => x.DisplayName)
            .ToListAsync();

        if (exactMatches.Count == 1)
        {
            return exactMatches[0];
        }

        if (exactMatches.Count > 1)
        {
            throw new InvalidOperationException("같은 이름의 전투 참가자가 여러 명입니다. 참가자 ID로 다시 시도해 주세요.");
        }

        var partialMatches = await db.CombatParticipants
            .Where(x =>
                x.CombatSessionId == combatSessionId &&
                x.Status != "left" &&
                x.Status != "removed" &&
                (x.DisplayName.Contains(query) || x.NormalizedDisplayName.Contains(normalizedQuery)))
            .OrderBy(x => x.DisplayName)
            .Take(5)
            .ToListAsync();

        if (partialMatches.Count == 1)
        {
            return partialMatches[0];
        }

        if (partialMatches.Count > 1)
        {
            throw new InvalidOperationException("조건에 맞는 전투 참가자가 여러 명입니다. 참가자 ID 또는 더 정확한 이름으로 다시 시도해 주세요.");
        }

        throw new InvalidOperationException("현재 활성 전투 세션에서 해당 참가자를 찾을 수 없습니다. 먼저 /전투상태로 참가자를 확인해 주세요.");
    }

    private static string NormalizeSearch(string value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string CreateUniqueName(string baseName, HashSet<string> usedNames)
    {
        if (!usedNames.Contains(baseName))
        {
            return baseName;
        }

        var index = 2;
        while (true)
        {
            var candidate = $"{baseName} ({index})";
            if (!usedNames.Contains(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static string ToSpawnSuffix(int index)
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        if (index < letters.Length)
        {
            return letters[index].ToString();
        }

        var loop = index / letters.Length;
        var remainder = index % letters.Length;
        return $"{letters[remainder]}{loop + 1}";
    }

    private static CombatParticipantSummary Map(CombatParticipantEntity entity)
        => new(
            entity.Id,
            entity.CombatSessionId,
            entity.Type,
            entity.SourceId,
            entity.DisplayName,
            entity.CurrentHp,
            entity.MaxHp,
            entity.Status,
            entity.Memo,
            entity.CreatedAt);

    private static string BuildCombatLogMessage(CombatParticipantEntity participant, int amount, int oldHp, int nextHp, string memo)
    {
        var memoText = string.IsNullOrWhiteSpace(memo) ? string.Empty : $" / {memo.Trim()}";
        return $"{participant.DisplayName} / {participant.Type} / {oldHp} -> {nextHp} / amount {amount}{memoText}";
    }

    private static string BuildRemovalLogMessage(CombatParticipantEntity participant, string memo)
    {
        var memoText = string.IsNullOrWhiteSpace(memo) ? string.Empty : $" / {memo.Trim()}";
        return $"{participant.DisplayName} / {participant.Type} / {participant.Status}{memoText}";
    }

    private static string BuildJoinLogMessage(CombatParticipantEntity participant, string? memo)
    {
        var memoText = string.IsNullOrWhiteSpace(memo) ? string.Empty : $" / {memo.Trim()}";
        return $"{participant.DisplayName} / {participant.Type} / hp {participant.CurrentHp}/{participant.MaxHp}{memoText}";
    }
}
