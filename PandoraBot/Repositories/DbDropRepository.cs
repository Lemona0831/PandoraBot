using Microsoft.EntityFrameworkCore;
using PandoraShared.Data;
using PandoraShared.Models;

namespace PandoraBot.Repositories;

public sealed class DbDropRepository : IDropRepository
{
    private readonly string connectionString;

    public DbDropRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<IReadOnlyList<EnemyDropRow>> GetEnemyDropsAsync(bool includeDeleted = false)
    {
        await using var db = CreateDb();
        var query = db.EnemyDrops
            .AsNoTracking()
            .Include(x => x.Enemy)
            .AsQueryable();

        if (!includeDeleted)
        {
            query = query.Where(x => x.IsActive);
        }

        var rows = await query
            .OrderBy(x => x.Enemy!.EnemyCode)
            .ThenBy(x => x.ItemName)
            .ToListAsync();

        return rows.Select(MapDrop).ToList();
    }

    public async Task<IReadOnlyList<EnemyDropSettingRow>> GetEnemyDropSettingsAsync()
    {
        await using var db = CreateDb();
        var rows = await db.EnemyDropSettings
            .AsNoTracking()
            .Include(x => x.Enemy)
            .OrderBy(x => x.Enemy!.EnemyCode)
            .ToListAsync();

        return rows.Select(MapSetting).ToList();
    }

    public Task<DropRollResult> RollDropAsync(string enemyId, bool writeLog = true)
        => RollDropCoreAsync(enemyId);

    public async Task<DropTestResult> TestDropAsync(string enemyId)
    {
        var result = await RollDropCoreAsync(enemyId);
        return new DropTestResult(result.Message, result);
    }

    public async Task<EnemyDropRow> CreateDropAsync(EnemyDropCreateInput input)
    {
        await using var db = CreateDb();
        var enemy = await FindEnemyAsync(db, input.EnemyId);
        var existing = await FindDropEntityOrNullAsync(db, enemy.Id, input.ItemName);

        if (existing is not null)
        {
            if (existing.IsActive)
            {
                throw new InvalidOperationException("같은 에너미에 동일한 드롭 아이템이 이미 등록되어 있습니다.");
            }

            ApplyDropValues(existing, input, isActive: true);
            await db.SaveChangesAsync();
            existing.Enemy = enemy;
            return MapDrop(existing);
        }

        var entity = new EnemyDropEntity
        {
            Id = Guid.NewGuid(),
            EnemyId = enemy.Id,
            ItemName = input.ItemName.Trim(),
            Probability = ToProbability(input.Chance),
            MinQuantity = Math.Max(1, input.MinCount),
            MaxQuantity = Math.Max(Math.Max(1, input.MinCount), input.MaxCount),
            Weight = Math.Max(0, input.Weight),
            Rarity = input.Rarity?.Trim() ?? string.Empty,
            Tag = input.Tag?.Trim() ?? string.Empty,
            IsActive = true,
            Memo = input.Memo?.Trim() ?? string.Empty
        };

        db.EnemyDrops.Add(entity);
        await db.SaveChangesAsync();
        entity.Enemy = enemy;
        return MapDrop(entity);
    }

    public async Task<EnemyDropRow> UpdateDropAsync(string enemyId, string itemName, EnemyDropCreateInput input)
    {
        await using var db = CreateDb();
        var enemy = await FindEnemyAsync(db, enemyId);
        var entity = await FindDropEntityAsync(db, enemy.Id, itemName);
        ApplyDropValues(entity, input, isActive: true);
        await db.SaveChangesAsync();
        entity.Enemy = enemy;
        return MapDrop(entity);
    }

    public async Task<EnemyDropRow> DeleteDropAsync(string enemyId, string itemName)
    {
        await using var db = CreateDb();
        var enemy = await FindEnemyAsync(db, enemyId);
        var entity = await FindDropEntityAsync(db, enemy.Id, itemName);
        entity.IsActive = false;
        await db.SaveChangesAsync();
        entity.Enemy = enemy;
        return MapDrop(entity);
    }

    public async Task<EnemyDropSettingRow> UpsertDropSettingAsync(EnemyDropSettingInput input, bool allowDuplicate)
    {
        await using var db = CreateDb();
        var enemy = await FindEnemyAsync(db, input.EnemyId);
        var entity = await db.EnemyDropSettings.FirstOrDefaultAsync(x => x.EnemyId == enemy.Id);
        if (entity is null)
        {
            entity = new EnemyDropSettingEntity
            {
                Id = Guid.NewGuid(),
                EnemyId = enemy.Id
            };
            db.EnemyDropSettings.Add(entity);
        }

        entity.DropRate = ToProbability(input.DropRate);
        entity.DropSlots = Math.Max(1, input.DropCount);
        entity.AllowDuplicate = allowDuplicate;
        entity.Memo = input.Memo?.Trim() ?? string.Empty;

        await db.SaveChangesAsync();
        entity.Enemy = enemy;
        return MapSetting(entity);
    }

    private async Task<DropRollResult> RollDropCoreAsync(string enemyIdOrName)
    {
        await using var db = CreateDb();
        var enemy = await FindEnemyAsync(db, enemyIdOrName, requireActive: false);

        var drops = await db.EnemyDrops.AsNoTracking()
            .Where(x => x.EnemyId == enemy.Id && x.IsActive)
            .ToListAsync();
        var setting = await db.EnemyDropSettings.AsNoTracking().FirstOrDefaultAsync(x => x.EnemyId == enemy.Id);

        var settingRow = setting is null
            ? new EnemyDropSettingRow(0, enemy.EnemyCode, 100, 1, false, string.Empty)
            : MapSetting(setting, enemy.EnemyCode);

        if (drops.Count == 0)
        {
            var noDropMessage = $"{enemy.Name}: 연결된 전리품이 없습니다.";
            return new DropRollResult(enemy.EnemyCode, enemy.Name, false, 0, settingRow.DropRate, Array.Empty<DropRollItem>(), noDropMessage);
        }

        var occurRoll = Random.Shared.Next(1, 101);
        if (occurRoll > settingRow.DropRate)
        {
            var failMessage = $"{enemy.Name}: 전리품 없음 (발생 {settingRow.DropRate}%, 굴림 {occurRoll})";
            return new DropRollResult(enemy.EnemyCode, enemy.Name, false, occurRoll, settingRow.DropRate, Array.Empty<DropRollItem>(), failMessage);
        }

        var sourceDrops = drops.Select(MapDrop).ToList();
        var pool = sourceDrops.ToList();
        var results = new List<DropRollItem>();
        for (var i = 0; i < Math.Max(1, settingRow.DropCount) && pool.Count > 0; i++)
        {
            var passed = pool
                .Where(drop => Random.Shared.Next(1, 101) <= Math.Clamp(drop.Chance, 1, 100))
                .ToList();

            if (passed.Count == 0)
            {
                continue;
            }

            var selected = SelectWeightedDrop(passed);
            var minCount = Math.Max(1, selected.MinCount);
            var maxCount = Math.Max(selected.MaxCount, minCount);
            var count = Random.Shared.Next(minCount, maxCount + 1);
            results.Add(new DropRollItem(selected.ItemName, count, selected.Chance, selected.Rarity, selected.Tag));

            if (!settingRow.AllowDuplicate)
            {
                pool.RemoveAll(drop => string.Equals(drop.ItemName, selected.ItemName, StringComparison.OrdinalIgnoreCase));
            }
        }

        var message = results.Count == 0
            ? $"{enemy.Name}: 전리품 발생에는 성공했지만 개별 아이템이 통과하지 못했습니다."
            : $"{enemy.Name}: {string.Join(", ", results.Select(item => $"{item.ItemName} x{item.Count}"))}";

        return new DropRollResult(enemy.EnemyCode, enemy.Name, true, occurRoll, settingRow.DropRate, results, message);
    }

    private PandoraDbContext CreateDb()
        => PandoraDbContextFactory.CreateOrNull(connectionString)
           ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");

    private static async Task<EnemyEntity> FindEnemyAsync(PandoraDbContext db, string enemyIdOrName, bool requireActive = false)
    {
        var query = enemyIdOrName.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("에너미 ID 또는 이름을 입력해 주세요.");
        }

        var normalized = Normalize(query);

        var exactMatches = await db.Enemies
            .Where(x => x.EnemyCode == query || x.Name == query || x.NormalizedName == normalized)
            .OrderBy(x => x.EnemyCode)
            .ToListAsync();

        if (exactMatches.Count == 1)
        {
            return EnsureEnemyAvailable(exactMatches[0], requireActive);
        }

        if (exactMatches.Count > 1)
        {
            throw new InvalidOperationException("조건에 맞는 에너미가 여러 개입니다. 에너미 ID로 다시 지정해 주세요.");
        }

        var partialMatches = await db.Enemies
            .Where(x => x.EnemyCode.Contains(query) || x.Name.Contains(query) || x.NormalizedName.Contains(normalized))
            .OrderBy(x => x.EnemyCode)
            .Take(5)
            .ToListAsync();

        if (partialMatches.Count == 1)
        {
            return EnsureEnemyAvailable(partialMatches[0], requireActive);
        }

        if (partialMatches.Count > 1)
        {
            throw new InvalidOperationException("조건에 맞는 에너미가 여러 개입니다. 에너미 ID 또는 더 정확한 이름으로 다시 지정해 주세요.");
        }

        throw new InvalidOperationException("드롭을 연결할 에너미를 찾을 수 없습니다.");
    }

    private static EnemyEntity EnsureEnemyAvailable(EnemyEntity entity, bool requireActive)
    {
        if (requireActive && !entity.IsActive)
        {
            throw new InvalidOperationException("비활성화된 에너미입니다. 먼저 `/에너미활성화`로 다시 활성화해 주세요.");
        }

        return entity;
    }

    private static async Task<EnemyDropEntity?> FindDropEntityOrNullAsync(PandoraDbContext db, Guid enemyId, string itemName)
    {
        var trimmed = itemName.Trim();
        return await db.EnemyDrops.FirstOrDefaultAsync(x => x.EnemyId == enemyId && EF.Functions.ILike(x.ItemName, trimmed));
    }

    private static async Task<EnemyDropEntity> FindDropEntityAsync(PandoraDbContext db, Guid enemyId, string itemName)
    {
        var entity = await FindDropEntityOrNullAsync(db, enemyId, itemName);
        if (entity is null)
        {
            throw new InvalidOperationException("해당 에너미에 연결된 드롭 아이템을 찾을 수 없습니다.");
        }

        return entity;
    }

    private static void ApplyDropValues(EnemyDropEntity entity, EnemyDropCreateInput input, bool isActive)
    {
        entity.ItemName = input.ItemName.Trim();
        entity.Probability = ToProbability(input.Chance);
        entity.MinQuantity = Math.Max(1, input.MinCount);
        entity.MaxQuantity = Math.Max(Math.Max(1, input.MinCount), input.MaxCount);
        entity.Weight = Math.Max(0, input.Weight);
        entity.Rarity = input.Rarity?.Trim() ?? string.Empty;
        entity.Tag = input.Tag?.Trim() ?? string.Empty;
        entity.IsActive = isActive;
        entity.Memo = input.Memo?.Trim() ?? string.Empty;
    }

    private static decimal ToProbability(int chance)
        => Math.Clamp(chance, 0, 100) / 100m;

    private static int ToPercent(decimal probability)
        => (int)Math.Round(Math.Clamp(probability, 0m, 1m) * 100m, MidpointRounding.AwayFromZero);

    private static string Normalize(string value)
        => value.Trim().ToLowerInvariant();

    private static EnemyDropRow SelectWeightedDrop(IReadOnlyList<EnemyDropRow> drops)
    {
        var weights = drops.Select(drop => Math.Max(1, drop.Weight)).ToArray();
        var totalWeight = weights.Sum();
        var roll = Random.Shared.Next(1, totalWeight + 1);
        var cumulative = 0;

        for (var i = 0; i < drops.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return drops[i];
            }
        }

        return drops[^1];
    }

    private static EnemyDropRow MapDrop(EnemyDropEntity entity)
    {
        return new EnemyDropRow(
            RowNumber: 0,
            EnemyId: entity.Enemy?.EnemyCode ?? string.Empty,
            ItemName: entity.ItemName,
            Chance: ToPercent(entity.Probability),
            MinCount: Math.Max(1, entity.MinQuantity),
            MaxCount: Math.Max(Math.Max(1, entity.MinQuantity), entity.MaxQuantity),
            Weight: Math.Max(0, entity.Weight),
            Rarity: entity.Rarity ?? string.Empty,
            Tag: entity.Tag ?? string.Empty,
            Memo: entity.Memo ?? string.Empty);
    }

    private static EnemyDropSettingRow MapSetting(EnemyDropSettingEntity entity)
        => MapSetting(entity, entity.Enemy?.EnemyCode ?? string.Empty);

    private static EnemyDropSettingRow MapSetting(EnemyDropSettingEntity entity, string enemyCode)
    {
        return new EnemyDropSettingRow(
            RowNumber: 0,
            EnemyId: enemyCode,
            DropRate: ToPercent(entity.DropRate),
            DropCount: Math.Max(1, entity.DropSlots),
            AllowDuplicate: entity.AllowDuplicate,
            Memo: entity.Memo ?? string.Empty);
    }
}
