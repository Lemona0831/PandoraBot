using Google.Apis.Sheets.v4;
using PandoraShared.Models;

namespace PandoraShared.Services;

public sealed class DropService : SheetServiceBase
{
    private readonly EnemyService enemyService;

    public DropService(SheetsService service, string spreadsheetId, EnemyService? enemyService = null)
        : base(service, spreadsheetId)
    {
        this.enemyService = enemyService ?? new EnemyService(service, spreadsheetId);
    }

    public async Task<IReadOnlyList<EnemyDropRow>> GetEnemyDropsAsync()
    {
        await EnsureEnemySheetsAsync();

        var response = await Service.Spreadsheets.Values.Get(
            SpreadsheetId,
            $"{RangeSheet(SheetNames.EnemyDrop)}!A2:I").ExecuteAsync();
        var values = response.Values ?? new List<IList<object>>();
        var rows = new List<EnemyDropRow>();

        for (var index = 0; index < values.Count; index++)
        {
            var row = values[index];
            var enemyId = GetString(row, 0);
            var itemName = GetString(row, 1);

            if (string.IsNullOrWhiteSpace(enemyId) && string.IsNullOrWhiteSpace(itemName))
            {
                continue;
            }

            rows.Add(new EnemyDropRow(
                RowNumber: index + 2,
                EnemyId: enemyId,
                ItemName: itemName,
                Chance: GetInt(row, 2),
                MinCount: GetInt(row, 3),
                MaxCount: GetInt(row, 4),
                Weight: GetInt(row, 5),
                Rarity: GetString(row, 6),
                Tag: GetString(row, 7),
                Memo: GetString(row, 8)));
        }

        return rows
            .OrderBy(row => row.EnemyId)
            .ThenBy(row => row.ItemName)
            .ThenBy(row => row.RowNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<EnemyDropSettingRow>> GetEnemyDropSettingsAsync()
    {
        await EnsureEnemySheetsAsync();

        var response = await Service.Spreadsheets.Values.Get(
            SpreadsheetId,
            $"{RangeSheet(SheetNames.EnemyDropSetting)}!A2:E").ExecuteAsync();
        var values = response.Values ?? new List<IList<object>>();
        var rows = new List<EnemyDropSettingRow>();

        for (var index = 0; index < values.Count; index++)
        {
            var row = values[index];
            var enemyId = GetString(row, 0);
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                continue;
            }

            rows.Add(new EnemyDropSettingRow(
                RowNumber: index + 2,
                EnemyId: enemyId,
                DropRate: Math.Clamp(GetInt(row, 1), 0, 100),
                DropCount: Math.Max(GetInt(row, 2), 1),
                AllowDuplicate: false,
                Memo: GetString(row, 4)));
        }

        return rows
            .OrderBy(row => row.EnemyId)
            .ThenBy(row => row.RowNumber)
            .ToList();
    }

    public async Task AddEnemyDropAsync(EnemyDropCreateInput input)
    {
        var enemies = await enemyService.GetEnemiesAsync();
        if (!enemies.Any(row => string.Equals(row.EnemyId, input.EnemyId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("선택한 에너미를 찾을 수 없습니다.");
        }

        var minCount = Math.Max(1, input.MinCount);
        var maxCount = Math.Max(input.MaxCount, minCount);
        var chance = Math.Clamp(input.Chance, 1, 100);
        var existingDrops = await GetEnemyDropsAsync();
        var duplicated = existingDrops.Any(row =>
            string.Equals(row.EnemyId, input.EnemyId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Normalize(row.ItemName), Normalize(input.ItemName), StringComparison.Ordinal));

        if (duplicated)
        {
            throw new InvalidOperationException("이미 같은 에너미에 같은 전리품이 연결되어 있습니다.");
        }

        await AppendRowAsync(SheetNames.EnemyDrop, new List<object>
        {
            input.EnemyId,
            input.ItemName,
            chance,
            minCount,
            maxCount,
            input.Weight,
            input.Rarity,
            input.Tag,
            input.Memo
        });

        await AppendAdminLogRowAsync("전리품추가", "", input.ItemName, $"{input.EnemyId} / {chance}%");
    }

    public async Task SetEnemyDropSettingAsync(EnemyDropSettingInput input)
    {
        var enemies = await enemyService.GetEnemiesAsync();
        if (!enemies.Any(row => string.Equals(row.EnemyId, input.EnemyId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("선택한 에너미를 찾을 수 없습니다.");
        }

        var dropRate = Math.Clamp(input.DropRate, 0, 100);
        var dropCount = Math.Max(input.DropCount, 1);
        var values = new List<object> { input.EnemyId, dropRate, dropCount, "FALSE", input.Memo };
        var settings = await GetEnemyDropSettingsAsync();
        var existing = settings.FirstOrDefault(row => string.Equals(row.EnemyId, input.EnemyId, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            await AppendRowAsync(SheetNames.EnemyDropSetting, values);
        }
        else
        {
            await UpdateRowAsync(SheetNames.EnemyDropSetting, $"A{existing.RowNumber}:E{existing.RowNumber}", values);
        }

        await AppendAdminLogRowAsync("드롭설정", "", input.EnemyId, $"발생률 {dropRate}% / {dropCount}회 / 중복 FALSE");
    }

    public async Task<DropRollResult> RollDropAsync(string enemyId, bool writeLog = true)
    {
        var result = await RollDropCoreAsync(enemyId);
        if (writeLog)
        {
            await AppendAdminLogRowAsync("드롭굴림", "", result.EnemyName, result.Message);
        }

        return result;
    }

    public async Task<DropTestResult> TestDropAsync(string enemyId)
    {
        var result = await RollDropCoreAsync(enemyId);
        await AppendAdminLogRowAsync("드롭테스트", "", result.EnemyName, result.Message);
        return new DropTestResult(result.Message, result);
    }

    public async Task DeleteEnemyDropDataAsync(string enemyId)
    {
        var drops = await GetEnemyDropsAsync();
        foreach (var drop in drops.Where(row => string.Equals(row.EnemyId, enemyId, StringComparison.OrdinalIgnoreCase)))
        {
            await ClearRangeAsync(SheetNames.EnemyDrop, $"A{drop.RowNumber}:I{drop.RowNumber}");
        }

        var settings = await GetEnemyDropSettingsAsync();
        foreach (var setting in settings.Where(row => string.Equals(row.EnemyId, enemyId, StringComparison.OrdinalIgnoreCase)))
        {
            await ClearRangeAsync(SheetNames.EnemyDropSetting, $"A{setting.RowNumber}:E{setting.RowNumber}");
        }
    }

    private async Task<DropRollResult> RollDropCoreAsync(string enemyId)
    {
        var enemies = await enemyService.GetEnemiesAsync();
        var enemy = enemies.FirstOrDefault(row => string.Equals(row.EnemyId, enemyId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("선택한 에너미를 찾을 수 없습니다.");
        var drops = (await GetEnemyDropsAsync())
            .Where(row => string.Equals(row.EnemyId, enemyId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var setting = (await GetEnemyDropSettingsAsync())
            .FirstOrDefault(row => string.Equals(row.EnemyId, enemyId, StringComparison.OrdinalIgnoreCase))
            ?? new EnemyDropSettingRow(0, enemyId, 100, 1, false, "");

        if (drops.Count == 0)
        {
            var noDropMessage = $"{enemy.Name}: 연결된 전리품이 없습니다.";
            return new DropRollResult(enemy.EnemyId, enemy.Name, false, 0, setting.DropRate, Array.Empty<DropRollItem>(), noDropMessage);
        }

        var occurRoll = Random.Shared.Next(1, 101);
        if (occurRoll > setting.DropRate)
        {
            var failMessage = $"{enemy.Name}: 전리품 없음 (발생 {setting.DropRate}%, 굴림 {occurRoll})";
            return new DropRollResult(enemy.EnemyId, enemy.Name, false, occurRoll, setting.DropRate, Array.Empty<DropRollItem>(), failMessage);
        }

        var remaining = drops.ToList();
        var results = new List<DropRollItem>();
        for (var i = 0; i < setting.DropCount && remaining.Count > 0; i++)
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
            remaining.RemoveAll(drop => drop.RowNumber == selected.RowNumber);
        }

        var message = results.Count == 0
            ? $"{enemy.Name}: 전리품 발생은 성공했지만 개별 전리품 확률을 통과하지 못했습니다."
            : $"{enemy.Name}: {string.Join(", ", results.Select(item => $"{item.ItemName} x{item.Count}"))}";

        return new DropRollResult(enemy.EnemyId, enemy.Name, true, occurRoll, setting.DropRate, results, message);
    }
}
