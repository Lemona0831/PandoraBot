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
            throw new InvalidOperationException("참가시킬 캐릭터 이름을 입력해주세요.");
        }

        var exactMatches = await db.Characters
            .Where(x => x.DisplayName == query || x.ImportedCharacterName == query)
            .OrderBy(x => x.DisplayName)
            .ToListAsync();

        CharacterEntity? character = null;
        if (exactMatches.Count == 1)
        {
            character = exactMatches[0];
        }
        else if (exactMatches.Count > 1)
        {
            throw new InvalidOperationException("같은 이름의 캐릭터가 여러 개 있습니다. 더 정확한 이름으로 다시 시도해주세요.");
        }
        else
        {
            var partialMatches = await db.Characters
                .Where(x => x.DisplayName.Contains(query) || x.ImportedCharacterName.Contains(query))
                .OrderBy(x => x.DisplayName)
                .Take(5)
                .ToListAsync();

            if (partialMatches.Count == 1)
            {
                character = partialMatches[0];
            }
            else if (partialMatches.Count > 1)
            {
                throw new InvalidOperationException("조건에 맞는 캐릭터가 여러 명입니다. 더 정확한 이름으로 다시 시도해주세요.");
            }
        }

        if (character is null)
        {
            throw new InvalidOperationException("해당 캐릭터를 찾을 수 없습니다.");
        }

        var duplicate = await db.CombatParticipants.AnyAsync(x =>
            x.CombatSessionId == session.Id &&
            x.Type == "player" &&
            x.SourceId == character.Id.ToString());

        if (duplicate)
        {
            throw new InvalidOperationException("해당 캐릭터는 이미 현재 전투 세션에 참가해 있습니다.");
        }

        var participant = new CombatParticipantEntity
        {
            Id = Guid.NewGuid(),
            CombatSessionId = session.Id,
            Type = "player",
            SourceId = character.Id.ToString(),
            DisplayName = character.DisplayName,
            CurrentHp = Math.Clamp(character.CurrentHp, 0, Math.Max(1, character.MaxHp)),
            MaxHp = Math.Max(1, character.MaxHp),
            Status = "active",
            Memo = memo ?? "",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.CombatParticipants.Add(participant);
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
        var query = enemyIdOrName.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("소환할 에너미 이름 또는 ID를 입력해주세요.");
        }

        var exactIdMatches = await db.Enemies
            .Where(x => x.EnemyCode == query)
            .OrderBy(x => x.Name)
            .ToListAsync();

        EnemyEntity? enemy = null;
        if (exactIdMatches.Count == 1)
        {
            enemy = exactIdMatches[0];
        }
        else
        {
            var exactNameMatches = await db.Enemies
                .Where(x => x.Name == query)
                .OrderBy(x => x.Name)
                .ToListAsync();

            if (exactNameMatches.Count == 1)
            {
                enemy = exactNameMatches[0];
            }
            else if (exactNameMatches.Count > 1)
            {
                throw new InvalidOperationException("같은 이름의 에너미가 여러 개 있습니다. ID 또는 더 정확한 이름으로 다시 시도해주세요.");
            }
            else
            {
                var partialMatches = await db.Enemies
                    .Where(x => x.EnemyCode.Contains(query) || x.Name.Contains(query))
                    .OrderBy(x => x.Name)
                    .Take(5)
                    .ToListAsync();

                if (partialMatches.Count == 1)
                {
                    enemy = partialMatches[0];
                }
                else if (partialMatches.Count > 1)
                {
                    throw new InvalidOperationException("조건에 맞는 에너미가 여러 개입니다. ID 또는 더 정확한 이름으로 다시 시도해주세요.");
                }
            }
        }

        if (enemy is null)
        {
            throw new InvalidOperationException("해당 에너미를 찾을 수 없습니다.");
        }

        var existingNames = await db.CombatParticipants
            .Where(x => x.CombatSessionId == session.Id)
            .Select(x => x.DisplayName)
            .ToListAsync();

        var usedNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var participants = new List<CombatParticipantEntity>();

        for (var i = 0; i < quantity; i++)
        {
            var displayName = quantity == 1
                ? CreateUniqueName(enemy.Name, usedNames)
                : CreateUniqueName($"{enemy.Name} {ToSpawnSuffix(i)}", usedNames);

            usedNames.Add(displayName);
            participants.Add(new CombatParticipantEntity
            {
                Id = Guid.NewGuid(),
                CombatSessionId = session.Id,
                Type = "enemy",
                SourceId = enemy.Id.ToString(),
                DisplayName = displayName,
                CurrentHp = Math.Max(1, enemy.MaxHp),
                MaxHp = Math.Max(1, enemy.MaxHp),
                Status = "active",
                Memo = memo ?? "",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        db.CombatParticipants.AddRange(participants);
        await db.SaveChangesAsync();
        return participants.Select(Map).ToList();
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
}
