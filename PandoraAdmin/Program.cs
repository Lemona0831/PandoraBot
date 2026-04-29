using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.Net;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PandoraSheetAdminService>();

var app = builder.Build();
app.UseStaticFiles();

app.MapGet("/", async (PandoraSheetAdminService sheets, string? q, string? status) =>
{
    var rows = await sheets.GetCharactersAsync();
    var query = q?.Trim() ?? "";
    var filtered = string.IsNullOrWhiteSpace(query)
        ? rows
        : rows.Where(row =>
            row.UserId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.CharacterName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

    var duplicateKeys = rows
        .GroupBy(row => $"{row.UserId.Trim()}::{row.CharacterName.Trim().ToUpperInvariant()}")
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var totalUsers = rows.Select(row => row.UserId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Count();
    var selectedCount = rows.Count(row => row.IsSelected);
    var duplicateCount = rows.Count(row => duplicateKeys.Contains(row.DuplicateKey));

    return Results.Content(RenderPage(filtered, query, status, rows.Count, totalUsers, selectedCount, duplicateCount, duplicateKeys), "text/html; charset=utf-8");
});

app.MapPost("/characters/{rowNumber:int}/select", async (PandoraSheetAdminService sheets, int rowNumber) =>
{
    await sheets.SelectCharacterAsync(rowNumber);
    return Results.Redirect("/?status=" + Uri.EscapeDataString("선택 캐릭터를 변경했습니다."));
});

app.MapPost("/characters/{rowNumber:int}/clear-selection", async (PandoraSheetAdminService sheets, int rowNumber) =>
{
    await sheets.ClearSelectionAsync(rowNumber);
    return Results.Redirect("/?status=" + Uri.EscapeDataString("선택 상태를 해제했습니다."));
});

app.MapPost("/characters/{rowNumber:int}/delete", async (PandoraSheetAdminService sheets, int rowNumber) =>
{
    await sheets.DeleteCharacterAsync(rowNumber);
    return Results.Redirect("/?status=" + Uri.EscapeDataString("캐릭터 행을 비웠습니다."));
});

app.MapPost("/maintenance/clean-duplicates", async (PandoraSheetAdminService sheets) =>
{
    var removed = await sheets.CleanDuplicatesAsync();
    return Results.Redirect("/?status=" + Uri.EscapeDataString($"중복 데이터 {removed}개를 정리했습니다."));
});

app.Run();

static string RenderPage(
    IReadOnlyList<CharacterRow> rows,
    string query,
    string? status,
    int totalCharacters,
    int totalUsers,
    int selectedCount,
    int duplicateCount,
    HashSet<string> duplicateKeys)
{
    var html = new StringBuilder();
    html.Append("""
<!doctype html>
<html lang="ko">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PANDORA Admin</title>
  <link rel="stylesheet" href="/styles.css">
</head>
<body>
  <div class="shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">PANDORA NETWORK</p>
        <h1>캐릭터 관리 대시보드</h1>
      </div>
      <div class="operator-card">
        <span>MODE</span>
        <strong>ADMIN</strong>
      </div>
    </header>
""");

    if (!string.IsNullOrWhiteSpace(status))
    {
        html.Append($"""<div class="notice">{H(status)}</div>""");
    }

    html.Append($"""
    <section class="stats-grid" aria-label="요약">
      <article class="stat-card">
        <span>Characters</span>
        <strong>{totalCharacters}</strong>
        <small>등록된 캐릭터</small>
      </article>
      <article class="stat-card">
        <span>Players</span>
        <strong>{totalUsers}</strong>
        <small>등록 유저 수</small>
      </article>
      <article class="stat-card">
        <span>Selected</span>
        <strong>{selectedCount}</strong>
        <small>선택된 캐릭터</small>
      </article>
      <article class="stat-card warn">
        <span>Duplicates</span>
        <strong>{duplicateCount}</strong>
        <small>중복 의심 행</small>
      </article>
    </section>

    <section class="toolbar">
      <form method="get" class="search-form">
        <label for="q">캐릭터명 또는 유저ID 검색</label>
        <div class="search-row">
          <input id="q" name="q" value="{H(query)}" placeholder="예: 렘모낭 또는 402314..." autocomplete="off">
          <button type="submit">검색</button>
          <a class="ghost-button" href="/">초기화</a>
        </div>
      </form>
      <form method="post" action="/maintenance/clean-duplicates" class="maintenance-form">
        <button type="submit" class="danger-outline">중복 데이터 정리</button>
      </form>
    </section>

    <main class="table-panel">
      <div class="panel-heading">
        <div>
          <h2>저장소 캐릭터</h2>
          <p>Google Sheets의 캐릭터 저장소 A:K 범위를 기준으로 표시합니다.</p>
        </div>
        <span>{rows.Count} rows</span>
      </div>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Row</th>
              <th>캐릭터</th>
              <th>유저ID</th>
              <th>HP</th>
              <th>Physical</th>
              <th>Mental</th>
              <th>상태</th>
              <th>관리</th>
            </tr>
          </thead>
          <tbody>
""");

    if (rows.Count == 0)
    {
        html.Append("""<tr><td colspan="8" class="empty">표시할 캐릭터가 없습니다.</td></tr>""");
    }
    else
    {
        foreach (var row in rows)
        {
            var duplicate = duplicateKeys.Contains(row.DuplicateKey);
            var hpPercent = row.MaxHp <= 0 ? 0 : Math.Clamp(row.CurrentHp * 100 / row.MaxHp, 0, 100);
            html.Append($"""
            <tr class="{(duplicate ? "duplicate" : "")}">
              <td class="mono">#{row.RowNumber}</td>
              <td>
                <strong class="character-name">{H(row.CharacterName)}</strong>
                {(duplicate ? """<span class="badge danger">중복 의심</span>""" : "")}
              </td>
              <td class="mono">{H(row.UserId)}</td>
              <td>
                <div class="hp-line">
                  <span>{row.CurrentHp} / {row.MaxHp}</span>
                  <div class="hp-track"><div style="width:{hpPercent}%"></div></div>
                </div>
              </td>
              <td>
                <div class="stat-list">
                  <span>근력 <b>{row.Strength}</b></span>
                  <span>민첩 <b>{row.Dexterity}</b></span>
                  <span>체력 <b>{row.Constitution}</b></span>
                </div>
              </td>
              <td>
                <div class="stat-list">
                  <span>지능 <b>{row.Intelligence}</b></span>
                  <span>지혜 <b>{row.Wisdom}</b></span>
                  <span>매력 <b>{row.Charisma}</b></span>
                </div>
              </td>
              <td>
                {(row.IsSelected ? """<span class="badge active">선택됨</span>""" : """<span class="badge muted">대기</span>""")}
              </td>
              <td>
                <div class="actions">
                  <form method="post" action="/characters/{row.RowNumber}/select"><button type="submit">선택</button></form>
                  <form method="post" action="/characters/{row.RowNumber}/clear-selection"><button type="submit" class="secondary">해제</button></form>
                  <form method="post" action="/characters/{row.RowNumber}/delete"><button type="submit" class="danger">삭제</button></form>
                </div>
              </td>
            </tr>
""");
        }
    }

    html.Append("""
          </tbody>
        </table>
      </div>
    </main>
  </div>
</body>
</html>
""");
    return html.ToString();
}

static string H(string? value)
{
    return WebUtility.HtmlEncode(value ?? "");
}

sealed class PandoraSheetAdminService
{
    private readonly SheetsService service;
    private readonly string spreadsheetId;

    public PandoraSheetAdminService(IHostEnvironment environment)
    {
        var settings = LoadSettings(environment.ContentRootPath);
        spreadsheetId = Environment.GetEnvironmentVariable("PANDORA_SPREADSHEET_ID") ?? AppConstants.DefaultSpreadsheetId;

        var credentialPath = ResolveCredentialPath(settings, environment.ContentRootPath);
        var credential = GoogleCredential.FromFile(credentialPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PandoraAdmin"
        });
    }

    public async Task<IReadOnlyList<CharacterRow>> GetCharactersAsync()
    {
        var response = await service.Spreadsheets.Values.Get(spreadsheetId, $"{RangeSheet(AppConstants.StorageSheetName)}!A2:K").ExecuteAsync();
        var values = response.Values ?? new List<IList<object>>();
        var rows = new List<CharacterRow>();

        for (var index = 0; index < values.Count; index++)
        {
            var row = values[index];
            var userId = GetString(row, 0);
            var characterName = GetString(row, 1);

            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(characterName))
            {
                continue;
            }

            rows.Add(new CharacterRow(
                RowNumber: index + 2,
                UserId: userId,
                CharacterName: characterName,
                Strength: GetInt(row, 2),
                Dexterity: GetInt(row, 3),
                Constitution: GetInt(row, 4),
                Intelligence: GetInt(row, 5),
                Wisdom: GetInt(row, 6),
                Charisma: GetInt(row, 7),
                MaxHp: GetInt(row, 8),
                CurrentHp: GetInt(row, 9),
                Selected: GetString(row, 10)));
        }

        return rows
            .OrderBy(row => row.UserId)
            .ThenBy(row => row.CharacterName)
            .ThenBy(row => row.RowNumber)
            .ToList();
    }

    public async Task SelectCharacterAsync(int rowNumber)
    {
        var rows = await GetCharactersAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber)
            ?? throw new InvalidOperationException("해당 행의 캐릭터를 찾을 수 없습니다.");

        foreach (var row in rows.Where(row => row.UserId == target.UserId))
        {
            await UpdateCellAsync(row.RowNumber, "K", row.RowNumber == rowNumber ? AppConstants.SelectedMarker : "");
        }
    }

    public async Task ClearSelectionAsync(int rowNumber)
    {
        await UpdateCellAsync(rowNumber, "K", "");
    }

    public async Task DeleteCharacterAsync(int rowNumber)
    {
        var clearRequest = service.Spreadsheets.Values.Clear(
            new ClearValuesRequest(),
            spreadsheetId,
            $"{RangeSheet(AppConstants.StorageSheetName)}!A{rowNumber}:K{rowNumber}");

        await clearRequest.ExecuteAsync();
    }

    public async Task<int> CleanDuplicatesAsync()
    {
        var rows = await GetCharactersAsync();
        var duplicateRows = rows
            .GroupBy(row => row.DuplicateKey)
            .Where(group => group.Count() > 1)
            .SelectMany(group =>
            {
                var keeper = group
                    .OrderByDescending(row => row.IsSelected)
                    .ThenBy(row => row.RowNumber)
                    .First();

                return group.Where(row => row.RowNumber != keeper.RowNumber);
            })
            .ToList();

        foreach (var duplicate in duplicateRows)
        {
            await DeleteCharacterAsync(duplicate.RowNumber);
        }

        return duplicateRows.Count;
    }

    private async Task UpdateCellAsync(int rowNumber, string column, string value)
    {
        var valueRange = new ValueRange
        {
            Values = new List<IList<object>> { new List<object> { value } }
        };

        var request = service.Spreadsheets.Values.Update(
            valueRange,
            spreadsheetId,
            $"{RangeSheet(AppConstants.StorageSheetName)}!{column}{rowNumber}");

        request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await request.ExecuteAsync();
    }

    private static BotSettings LoadSettings(string contentRoot)
    {
        var candidates = new[]
        {
            Path.Combine(contentRoot, "BotSettings.json"),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "PandoraBot", "BotSettings.json"))
        };

        var settingsPath = candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("BotSettings.json 파일을 찾을 수 없습니다.");

        var json = File.ReadAllText(settingsPath);
        var settings = JsonSerializer.Deserialize<BotSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new BotSettings();

        settings.SettingsDirectory = Path.GetDirectoryName(settingsPath) ?? contentRoot;
        return settings;
    }

    private static string ResolveCredentialPath(BotSettings settings, string contentRoot)
    {
        var configuredPath = settings.GoogleCredentialPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "Credental.json";
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var relativeToSettings = Path.GetFullPath(Path.Combine(settings.SettingsDirectory, configuredPath));
        if (File.Exists(relativeToSettings))
        {
            return relativeToSettings;
        }

        return Path.GetFullPath(Path.Combine(contentRoot, configuredPath));
    }

    private static string GetString(IList<object> values, int index)
    {
        return values.Count > index ? values[index]?.ToString()?.Trim() ?? "" : "";
    }

    private static int GetInt(IList<object> values, int index)
    {
        return int.TryParse(GetString(values, index), out var value) ? value : 0;
    }

    private static string RangeSheet(string sheetName)
    {
        return $"'{sheetName.Replace("'", "''")}'";
    }
}

sealed record CharacterRow(
    int RowNumber,
    string UserId,
    string CharacterName,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    int MaxHp,
    int CurrentHp,
    string Selected)
{
    public bool IsSelected => string.Equals(Selected, AppConstants.SelectedMarker, StringComparison.OrdinalIgnoreCase);

    public string DuplicateKey => $"{UserId.Trim()}::{CharacterName.Trim().ToUpperInvariant()}";
}

sealed class BotSettings
{
    public string GoogleCredentialPath { get; set; } = "";
    public string SettingsDirectory { get; set; } = "";
}

static class AppConstants
{
    public const string StorageSheetName = "캐릭터 저장소";
    public const string SelectedMarker = "selected";
    public const string DefaultSpreadsheetId = "13DKG_V3TD5GHxQrVpmFGQhFluPvGc3E_M5FXfdvRkqI";
}
