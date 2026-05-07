using Google.Apis.Sheets.v4;
using PandoraShared.Models;

namespace PandoraShared.Services;

public sealed class EnemyService : SheetServiceBase
{
    public EnemyService(SheetsService service, string spreadsheetId)
        : base(service, spreadsheetId)
    {
    }

    public async Task<IReadOnlyList<EnemyRow>> GetEnemiesAsync()
    {
        await EnsureEnemySheetsAsync();

        var response = await Service.Spreadsheets.Values.Get(
            SpreadsheetId,
            $"{RangeSheet(SheetNames.EnemyStorage)}!A2:P").ExecuteAsync();
        var values = response.Values ?? new List<IList<object>>();
        var rows = new List<EnemyRow>();

        for (var index = 0; index < values.Count; index++)
        {
            var row = values[index];
            var enemyId = GetString(row, 0);
            var name = GetString(row, 2);

            if (string.IsNullOrWhiteSpace(enemyId) && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            rows.Add(new EnemyRow(
                RowNumber: index + 2,
                EnemyId: enemyId,
                Region: GetString(row, 1),
                Name: name,
                Category: GetString(row, 3),
                Strength: GetInt(row, 4),
                Dexterity: GetInt(row, 5),
                Constitution: GetInt(row, 6),
                Intelligence: GetInt(row, 7),
                Wisdom: GetInt(row, 8),
                Charisma: GetInt(row, 9),
                DamageFormula: GetString(row, 10),
                Dp: GetInt(row, 11),
                CurrentHp: ClampHp(GetInt(row, 12), GetInt(row, 13)),
                MaxHp: GetInt(row, 13),
                Description: GetString(row, 14),
                IsEnabled: GetString(row, 15)));
        }

        return rows
            .OrderBy(row => row.Region)
            .ThenBy(row => row.Name)
            .ThenBy(row => row.RowNumber)
            .ToList();
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

    public async Task<string> AddEnemyAsync(EnemyCreateInput input)
    {
        var rows = await GetEnemiesAsync();
        var duplicated = rows.Any(row =>
            string.Equals(Normalize(row.Region), Normalize(input.Region), StringComparison.Ordinal) &&
            string.Equals(Normalize(row.Name), Normalize(input.Name), StringComparison.Ordinal));

        if (duplicated)
        {
            throw new InvalidOperationException("같은 지역에 같은 이름의 에너미가 이미 있습니다.");
        }

        var maxHp = Math.Max(1, input.MaxHp);
        var enemyId = CreateNextEnemyId(rows);
        await AppendRowAsync(SheetNames.EnemyStorage, new List<object>
        {
            enemyId,
            input.Region,
            input.Name,
            NormalizeEnemyCategory(input.Category),
            input.Strength,
            input.Dexterity,
            input.Constitution,
            input.Intelligence,
            input.Wisdom,
            input.Charisma,
            input.DamageFormula,
            input.Dp,
            maxHp,
            maxHp,
            input.Description,
            "TRUE"
        });

        await AppendAdminLogRowAsync("에너미추가", "", input.Name, $"{enemyId} / {input.Region}");
        return enemyId;
    }

    public async Task SetEnemyCategoryAsync(int rowNumber, string category)
    {
        var normalized = NormalizeEnemyCategory(category);
        await UpdateRowAsync(SheetNames.EnemyStorage, $"D{rowNumber}:D{rowNumber}", new List<object> { normalized });
        await AppendAdminLogRowAsync("에너미출현구분", "", "", $"row {rowNumber}: {normalized}");
    }

    public async Task UpdateEnemyAsync(int rowNumber, EnemyEditInput input)
    {
        var rows = await GetEnemiesAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber)
            ?? throw new InvalidOperationException("해당 행의 에너미를 찾을 수 없습니다.");

        var duplicated = rows.Any(row =>
            row.RowNumber != rowNumber &&
            string.Equals(Normalize(row.Region), Normalize(input.Region), StringComparison.Ordinal) &&
            string.Equals(Normalize(row.Name), Normalize(input.Name), StringComparison.Ordinal));

        if (duplicated)
        {
            throw new InvalidOperationException("같은 지역에 같은 이름의 에너미가 이미 있습니다.");
        }

        var nextMaxHp = Math.Max(1, input.MaxHp);
        var nextCurrentHp = Math.Clamp(target.CurrentHp, 0, nextMaxHp);
        var values = new List<object>
        {
            input.Region,
            input.Name,
            NormalizeEnemyCategory(input.Category),
            input.Strength,
            input.Dexterity,
            input.Constitution,
            input.Intelligence,
            input.Wisdom,
            input.Charisma,
            input.DamageFormula,
            input.Dp,
            nextCurrentHp,
            nextMaxHp,
            input.Description,
            input.IsEnabled ? "TRUE" : "FALSE"
        };

        await UpdateRowAsync(SheetNames.EnemyStorage, $"B{rowNumber}:P{rowNumber}", values);
        await AppendAdminLogRowAsync("에너미수정", "", target.Name, $"{target.EnemyId}: {target.Name} -> {input.Name}");
    }

    public async Task<string> DeleteEnemyAsync(int rowNumber, DropService? dropService = null)
    {
        var rows = await GetEnemiesAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber)
            ?? throw new InvalidOperationException("해당 행의 에너미를 찾을 수 없습니다.");

        await ClearRangeAsync(SheetNames.EnemyStorage, $"A{rowNumber}:P{rowNumber}");

        if (dropService is not null)
        {
            await dropService.DeleteEnemyDropDataAsync(target.EnemyId);
        }

        await AppendAdminLogRowAsync("에너미삭제", "", target.Name, $"{target.EnemyId} / 연결 드롭 삭제");
        return target.Name;
    }

    private static string CreateNextEnemyId(IReadOnlyList<EnemyRow> rows)
    {
        var maxNumber = rows
            .Select(row => row.EnemyId.StartsWith("ENEMY-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(row.EnemyId["ENEMY-".Length..], out var number)
                    ? number
                    : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"ENEMY-{maxNumber + 1:000}";
    }

    private static string NormalizeEnemyCategory(string? value)
    {
        var normalized = value?.Trim() ?? "";
        return normalized switch
        {
            "" => "",
            "일반 조우" => "일반 조우",
            "이벤트" => "이벤트",
            "네임드" => "네임드",
            "보스전" => "보스전",
            _ => ""
        };
    }
}
