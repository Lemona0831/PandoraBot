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

    public async Task<IReadOnlyList<EnemyDropRow>> GetEnemyDropsAsync()
    {
        await using var db = CreateDb();
        var rows = await db.EnemyDrops
            .AsNoTracking()
            .Include(x => x.Enemy)
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

    private async Task<DropRollResult> RollDropCoreAsync(string enemyCode)
    {
        await using var db = CreateDb();
        var enemy = await db.Enemies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EnemyCode == enemyCode);

        if (enemy is null)
        {
            throw new InvalidOperationException("선택한 에너미를 찾을 수 없습니다.");
        }

        var drops = await db.EnemyDrops
            .AsNoTracking()
            .Where(x => x.EnemyId == enemy.Id)
            .ToListAsync();

        var setting = await db.EnemyDropSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EnemyId == enemy.Id);

        var settingRow = setting is null
            ? new EnemyDropSettingRow(0, enemy.EnemyCode, 100, 1, false, "")
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

        var remaining = drops.Select(MapDrop).ToList();
        var results = new List<DropRollItem>();
        for (var i = 0; i < Math.Max(1, settingRow.DropCount) && remaining.Count > 0; i++)
        {
            var passed = remaining
                .Where(drop => Random.Shared.Next(1, 101) <= Math.Clamp(drop.Chance, 1, 100))
                .ToList();

            if (passed.Count == 0)
            {
                continue;
            }

            var selected = passed[Random.Shared.Next(passed.Count)];
            var minCount = Math.Max(1, selected.MinCount);
            var maxCount = Math.Max(selected.MaxCount, minCount);
            var count = Random.Shared.Next(minCount, maxCount + 1);
            results.Add(new DropRollItem(selected.ItemName, count, selected.Chance, selected.Rarity, selected.Tag));
            remaining.RemoveAll(drop => string.Equals(drop.ItemName, selected.ItemName, StringComparison.OrdinalIgnoreCase));
        }

        var message = results.Count == 0
            ? $"{enemy.Name}: 전리품 발생에는 성공했지만 개별 전리품이 통과하지 못했습니다."
            : $"{enemy.Name}: {string.Join(", ", results.Select(item => $"{item.ItemName} x{item.Count}"))}";

        return new DropRollResult(enemy.EnemyCode, enemy.Name, true, occurRoll, settingRow.DropRate, results, message);
    }

    private PandoraDbContext CreateDb()
        => PandoraDbContextFactory.CreateOrNull(connectionString)
           ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");

    private static EnemyDropRow MapDrop(EnemyDropEntity entity)
    {
        var memo = RepositoryMemoParser.ParseDropMemo(entity.Memo);
        return new EnemyDropRow(
            RowNumber: 0,
            EnemyId: entity.Enemy?.EnemyCode ?? "",
            ItemName: entity.ItemName,
            Chance: ToPercent(entity.Probability),
            MinCount: Math.Max(1, entity.MinQuantity),
            MaxCount: Math.Max(Math.Max(1, entity.MinQuantity), entity.MaxQuantity),
            Weight: memo.Weight,
            Rarity: memo.Rarity,
            Tag: memo.Tag,
            Memo: memo.Memo);
    }

    private static EnemyDropSettingRow MapSetting(EnemyDropSettingEntity entity)
        => MapSetting(entity, entity.Enemy?.EnemyCode ?? "");

    private static EnemyDropSettingRow MapSetting(EnemyDropSettingEntity entity, string enemyCode)
    {
        var allowDuplicate = entity.Memo.Contains("allow_duplicate=true", StringComparison.OrdinalIgnoreCase);
        return new EnemyDropSettingRow(
            RowNumber: 0,
            EnemyId: enemyCode,
            DropRate: ToPercent(entity.DropRate),
            DropCount: Math.Max(1, entity.DropSlots),
            AllowDuplicate: allowDuplicate,
            Memo: entity.Memo);
    }

    private static int ToPercent(decimal probability)
        => (int)Math.Round(Math.Clamp(probability, 0m, 1m) * 100m, MidpointRounding.AwayFromZero);
}
