using Microsoft.EntityFrameworkCore;
using PandoraShared.Data;
using PandoraShared.Models;

namespace PandoraBot.Repositories;

public sealed class DbEnemyRepository : IEnemyRepository
{
    private readonly string connectionString;

    public DbEnemyRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<IReadOnlyList<EnemyRow>> GetEnemiesAsync()
    {
        await using var db = CreateDb();
        var enemies = await db.Enemies
            .AsNoTracking()
            .OrderBy(x => x.EncounterTag)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return enemies.Select(MapEnemy).ToList();
    }

    public async Task<EnemySearchResult> GetEnemyByIdOrNameAsync(string idOrName)
    {
        var query = idOrName.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new EnemySearchResult(null, Array.Empty<EnemyRow>(), "에너미 ID 또는 이름을 입력해 주세요.");
        }

        var rows = await GetEnemiesAsync();
        var exactId = rows.Where(row => string.Equals(row.EnemyId, query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exactId.Count == 1)
        {
            return new EnemySearchResult(exactId[0], exactId, null);
        }

        var exactName = rows.Where(row => string.Equals(row.Name, query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exactName.Count == 1)
        {
            return new EnemySearchResult(exactName[0], exactName, null);
        }

        var matches = rows
            .Where(row => row.EnemyId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => new EnemySearchResult(null, matches, "해당 에너미를 찾을 수 없습니다."),
            1 => new EnemySearchResult(matches[0], matches, null),
            _ => new EnemySearchResult(null, matches, "조건에 맞는 에너미가 여러 명입니다.")
        };
    }

    public async Task<EnemyRow> CreateEnemyAsync(EnemyCreateInput input)
    {
        await using var db = CreateDb();
        var entity = new EnemyEntity
        {
            Id = Guid.NewGuid(),
            EnemyCode = await BuildNextEnemyCodeAsync(db),
            Name = input.Name.Trim(),
            NormalizedName = Normalize(input.Name),
            Strength = input.Strength,
            Dexterity = input.Dexterity,
            Constitution = input.Constitution,
            Intelligence = input.Intelligence,
            Wisdom = input.Wisdom,
            Charisma = input.Charisma,
            MaxHp = Math.Max(1, input.MaxHp),
            EncounterTag = input.Region?.Trim() ?? string.Empty,
            Memo = RepositoryMemoParser.ComposeEnemyMemo(input.Category, input.DamageFormula, input.Dp, input.Description),
            IsActive = true
        };

        db.Enemies.Add(entity);
        await db.SaveChangesAsync();
        return MapEnemy(entity);
    }

    public async Task<EnemyRow> UpdateEnemyAsync(string idOrName, EnemyEditInput input)
    {
        await using var db = CreateDb();
        var entity = await FindEnemyEntityAsync(db, idOrName);

        entity.Name = input.Name.Trim();
        entity.NormalizedName = Normalize(entity.Name);
        entity.Strength = input.Strength;
        entity.Dexterity = input.Dexterity;
        entity.Constitution = input.Constitution;
        entity.Intelligence = input.Intelligence;
        entity.Wisdom = input.Wisdom;
        entity.Charisma = input.Charisma;
        entity.MaxHp = Math.Max(1, input.MaxHp);
        entity.EncounterTag = input.Region?.Trim() ?? string.Empty;
        entity.IsActive = input.IsEnabled;
        entity.Memo = RepositoryMemoParser.ComposeEnemyMemo(input.Category, input.DamageFormula, input.Dp, input.Description);

        await db.SaveChangesAsync();
        return MapEnemy(entity);
    }

    public async Task<EnemyRow> UpdateEnemyStatAsync(string idOrName, string statName, int value)
    {
        await using var db = CreateDb();
        var entity = await FindEnemyEntityAsync(db, idOrName);
        var normalized = statName.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "근력":
            case "str":
            case "strength":
                entity.Strength = value;
                break;
            case "민첩":
            case "dex":
            case "dexterity":
                entity.Dexterity = value;
                break;
            case "체력":
            case "con":
            case "constitution":
                entity.Constitution = value;
                break;
            case "지능":
            case "int":
            case "intelligence":
                entity.Intelligence = value;
                break;
            case "지혜":
            case "wis":
            case "wisdom":
                entity.Wisdom = value;
                break;
            case "매력":
            case "cha":
            case "charisma":
                entity.Charisma = value;
                break;
            case "최대hp":
            case "최대체력":
            case "hp":
            case "maxhp":
                entity.MaxHp = Math.Max(1, value);
                break;
            default:
                throw new InvalidOperationException("스탯은 근력, 민첩, 체력, 지능, 지혜, 매력, 최대HP 중 하나로 입력해 주세요.");
        }

        await db.SaveChangesAsync();
        return MapEnemy(entity);
    }

    private PandoraDbContext CreateDb()
        => PandoraDbContextFactory.CreateOrNull(connectionString)
           ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");

    private static async Task<EnemyEntity> FindEnemyEntityAsync(PandoraDbContext db, string idOrName)
    {
        var query = idOrName.Trim();
        var normalized = Normalize(query);
        var matches = await db.Enemies
            .Where(x => x.EnemyCode == query || x.NormalizedName.Contains(normalized))
            .OrderBy(x => x.EnemyCode)
            .ToListAsync();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException("해당 에너미를 찾을 수 없습니다.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException("조건에 맞는 에너미가 여러 개입니다. 에너미 ID로 다시 지정해 주세요.");
        }

        return matches[0];
    }

    private static async Task<string> BuildNextEnemyCodeAsync(PandoraDbContext db)
    {
        var lastCodes = await db.Enemies
            .AsNoTracking()
            .OrderByDescending(x => x.EnemyCode)
            .Select(x => x.EnemyCode)
            .Take(50)
            .ToListAsync();

        var maxNumber = 0;
        foreach (var code in lastCodes)
        {
            var digits = new string(code.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var number) && number > maxNumber)
            {
                maxNumber = number;
            }
        }

        return $"ENEMY-{maxNumber + 1:000}";
    }

    private static string Normalize(string value)
        => value.Trim().ToLowerInvariant();

    private static EnemyRow MapEnemy(EnemyEntity entity)
    {
        var memo = RepositoryMemoParser.ParseEnemyMemo(entity.Memo);
        return new EnemyRow(
            RowNumber: 0,
            EnemyId: entity.EnemyCode,
            Region: entity.EncounterTag ?? string.Empty,
            Name: entity.Name,
            Category: string.IsNullOrWhiteSpace(memo.Category) ? (entity.EncounterTag ?? string.Empty) : memo.Category,
            Strength: entity.Strength,
            Dexterity: entity.Dexterity,
            Constitution: entity.Constitution,
            Intelligence: entity.Intelligence,
            Wisdom: entity.Wisdom,
            Charisma: entity.Charisma,
            DamageFormula: memo.DamageFormula,
            Dp: memo.Dp,
            CurrentHp: entity.MaxHp,
            MaxHp: entity.MaxHp,
            Description: memo.Description,
            IsEnabled: entity.IsActive ? "TRUE" : "FALSE");
    }
}
