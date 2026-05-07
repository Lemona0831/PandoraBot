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
            return new EnemySearchResult(null, Array.Empty<EnemyRow>(), "에너미 ID 또는 이름을 입력해야 합니다.");
        }

        var rows = await GetEnemiesAsync();
        var exactId = rows
            .Where(row => string.Equals(row.EnemyId, query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exactId.Count == 1)
        {
            return new EnemySearchResult(exactId[0], exactId, null);
        }

        var exactName = rows
            .Where(row => string.Equals(row.Name, query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exactName.Count == 1)
        {
            return new EnemySearchResult(exactName[0], exactName, null);
        }

        var matches = rows
            .Where(row =>
                row.EnemyId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => new EnemySearchResult(null, matches, "해당 에너미를 찾을 수 없습니다."),
            1 => new EnemySearchResult(matches[0], matches, null),
            _ => new EnemySearchResult(null, matches, "조건에 맞는 에너미가 여러 명입니다.")
        };
    }

    private PandoraDbContext CreateDb()
        => PandoraDbContextFactory.CreateOrNull(connectionString)
           ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");

    private static EnemyRow MapEnemy(EnemyEntity entity)
    {
        var memo = RepositoryMemoParser.ParseEnemyMemo(entity.Memo);
        return new EnemyRow(
            RowNumber: 0,
            EnemyId: entity.EnemyCode,
            Region: entity.EncounterTag ?? "",
            Name: entity.Name,
            Category: string.IsNullOrWhiteSpace(memo.Category) ? (entity.EncounterTag ?? "") : memo.Category,
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
