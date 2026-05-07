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
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(context =>
    {
        var message = Uri.EscapeDataString("처리 중 문제가 발생했습니다. 시트 권한, 봇 실행 상태, 설정값을 확인해주세요.");
        var referer = context.Request.Headers.Referer.ToString();
        var targetPath = Uri.TryCreate(referer, UriKind.Absolute, out var refererUri)
            ? refererUri.AbsolutePath
            : context.Request.Path.Value ?? "/";
        targetPath = targetPath.StartsWith("/enemies", StringComparison.OrdinalIgnoreCase) ||
            targetPath.StartsWith("/enemy-", StringComparison.OrdinalIgnoreCase)
            ? "/enemies"
            : "/";
        context.Response.Redirect($"{targetPath}?status={message}");
        return Task.CompletedTask;
    });
});

app.MapGet("/", async (PandoraSheetAdminService sheets, string? q, string? review, string? status) =>
{
    var rows = await sheets.GetCharactersAsync();
    var query = q?.Trim() ?? "";
    var reviewFilter = NormalizeReviewFilter(review);
    var filtered = string.IsNullOrWhiteSpace(query)
        ? rows
        : rows.Where(row =>
            row.UserId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.CharacterName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

    filtered = reviewFilter == "all"
        ? filtered
        : filtered.Where(row => row.ReviewStatus == reviewFilter).ToList();

    var duplicateKeys = rows
        .GroupBy(row => $"{row.UserId.Trim()}::{row.CharacterName.Trim().ToUpperInvariant()}")
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var totalUsers = rows.Select(row => row.UserId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Count();
    var selectedCount = rows.Count(row => row.IsSelected);
    var duplicateCount = rows.Count(row => duplicateKeys.Contains(row.DuplicateKey));

    return Results.Content(RenderPage(filtered, query, reviewFilter, status, rows.Count, totalUsers, selectedCount, duplicateCount, duplicateKeys), "text/html; charset=utf-8");
});

app.MapGet("/enemies", async (PandoraSheetAdminService sheets, string? q, string? category, string? status) =>
{
    var rows = await sheets.GetEnemiesAsync();
    var drops = await sheets.GetEnemyDropsAsync();
    var dropSettings = await sheets.GetEnemyDropSettingsAsync();
    var query = q?.Trim() ?? "";
    var categoryFilter = category?.Trim() ?? "all";
    var filtered = string.IsNullOrWhiteSpace(query)
        ? rows
        : rows.Where(row =>
            row.EnemyId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.Region.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

    filtered = categoryFilter == "all"
        ? filtered
        : filtered.Where(row => string.Equals(row.Category, categoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();

    return Results.Content(RenderEnemyPage(filtered, rows, drops, dropSettings, query, categoryFilter, status, rows.Count), "text/html; charset=utf-8");
});

app.MapPost("/characters/{rowNumber:int}/hp/set", async (PandoraSheetAdminService sheets, int rowNumber, HttpRequest request) =>
{
    var hp = await ReadPositiveFormIntAsync(request, "hp", allowZero: true);
    await sheets.SetHpAsync(rowNumber, hp);
    return RedirectWithStatus("HP를 설정했습니다.");
});

app.MapPost("/characters/{rowNumber:int}/hp/damage", async (PandoraSheetAdminService sheets, int rowNumber, HttpRequest request) =>
{
    var amount = await ReadPositiveFormIntAsync(request, "amount");
    await sheets.AdjustHpAsync(rowNumber, -amount);
    return RedirectWithStatus("피해를 적용했습니다.");
});

app.MapPost("/characters/{rowNumber:int}/hp/heal", async (PandoraSheetAdminService sheets, int rowNumber, HttpRequest request) =>
{
    var amount = await ReadPositiveFormIntAsync(request, "amount");
    await sheets.AdjustHpAsync(rowNumber, amount);
    return RedirectWithStatus("회복을 적용했습니다.");
});

app.MapPost("/characters/{rowNumber:int}/stats", async (PandoraSheetAdminService sheets, int rowNumber, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var input = new CharacterStatsInput(
        Strength: ReadRequiredFormInt(form, "strength", "근력"),
        Dexterity: ReadRequiredFormInt(form, "dexterity", "민첩"),
        Constitution: ReadRequiredFormInt(form, "constitution", "체력"),
        Intelligence: ReadRequiredFormInt(form, "intelligence", "지능"),
        Wisdom: ReadRequiredFormInt(form, "wisdom", "지혜"),
        Charisma: ReadRequiredFormInt(form, "charisma", "매력"));

    var syncedSource = await sheets.SetCharacterStatsAsync(rowNumber, input);
    return RedirectWithStatus(syncedSource
        ? "캐릭터 능력치와 원본 시트를 함께 저장했습니다."
        : "캐릭터 능력치를 저장했습니다. 원본 시트 정보가 없어 원본에는 반영하지 못했습니다.");
});

app.MapPost("/characters/{rowNumber:int}/review/{reviewStatus}", async (PandoraSheetAdminService sheets, int rowNumber, string reviewStatus) =>
{
    await sheets.SetReviewStatusAsync(rowNumber, reviewStatus);
    return RedirectWithStatus("검수 상태를 변경했습니다.");
});

app.MapPost("/characters/{rowNumber:int}/delete", async (PandoraSheetAdminService sheets, int rowNumber) =>
{
    await sheets.DeleteCharacterAsync(rowNumber);
    return RedirectWithStatus("캐릭터 정보를 삭제했습니다.");
});

app.MapPost("/maintenance/clean-duplicates", async (PandoraSheetAdminService sheets) =>
{
    var removed = await sheets.CleanDuplicatesAsync();
    return RedirectWithStatus($"중복 데이터 {removed}개를 정리했습니다.");
});

app.MapPost("/enemies/create", async (PandoraSheetAdminService sheets, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var input = new EnemyCreateInput(
        Region: ReadRequiredFormString(form, "region", "지역"),
        Name: ReadRequiredFormString(form, "name", "에너미 이름"),
        Category: form["category"].ToString(),
        Strength: ReadRequiredFormInt(form, "strength", "근력"),
        Dexterity: ReadRequiredFormInt(form, "dexterity", "민첩"),
        Constitution: ReadRequiredFormInt(form, "constitution", "체력"),
        Intelligence: ReadRequiredFormInt(form, "intelligence", "지능"),
        Wisdom: ReadRequiredFormInt(form, "wisdom", "지혜"),
        Charisma: ReadRequiredFormInt(form, "charisma", "매력"),
        DamageFormula: ReadRequiredFormString(form, "damageFormula", "피해식"),
        Dp: ReadRequiredFormInt(form, "dp", "DP", allowZero: true),
        MaxHp: ReadRequiredFormInt(form, "maxHp", "최대 HP"),
        Description: form["description"].ToString().Trim());

    var enemyId = await sheets.AddEnemyAsync(input);
    var status = Uri.EscapeDataString($"{input.Name} 에너미를 추가했습니다. ID: {enemyId}");
    var updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return Results.Redirect($"/enemies?status={status}&updated={updated}");
});

app.MapPost("/enemy-drops/create", async (PandoraSheetAdminService sheets, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var input = new EnemyDropCreateInput(
        EnemyId: ReadRequiredFormString(form, "enemyId", "에너미"),
        ItemName: ReadRequiredFormString(form, "itemName", "아이템명"),
        Chance: ReadRequiredFormInt(form, "chance", "확률"),
        MinCount: ReadRequiredFormInt(form, "minCount", "최소개수"),
        MaxCount: ReadRequiredFormInt(form, "maxCount", "최대개수"),
        Weight: ReadRequiredFormInt(form, "weight", "무게", allowZero: true),
        Rarity: form["rarity"].ToString().Trim(),
        Tag: form["tag"].ToString().Trim(),
        Memo: form["memo"].ToString().Trim());

    await sheets.AddEnemyDropAsync(input);
    var status = Uri.EscapeDataString($"{input.ItemName} 전리품을 추가했습니다.");
    var updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return Results.Redirect($"/enemies?status={status}&updated={updated}");
});

app.MapPost("/enemy-drop-settings/{enemyId}", async (PandoraSheetAdminService sheets, string enemyId, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var input = new EnemyDropSettingInput(
        EnemyId: enemyId,
        DropRate: ReadRequiredFormInt(form, "dropRate", "전리품 발생률", allowZero: true),
        DropCount: ReadRequiredFormInt(form, "dropCount", "드롭횟수"),
        Memo: form["memo"].ToString().Trim());

    await sheets.SetEnemyDropSettingAsync(input);
    var status = Uri.EscapeDataString($"{enemyId} 드롭 설정을 저장했습니다.");
    var updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return Results.Redirect($"/enemies?status={status}&updated={updated}");
});

app.MapPost("/enemies/{rowNumber:int}/edit", async (PandoraSheetAdminService sheets, int rowNumber, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var input = new EnemyEditInput(
        Region: ReadRequiredFormString(form, "region", "지역"),
        Name: ReadRequiredFormString(form, "name", "에너미 이름"),
        Category: form["category"].ToString(),
        Strength: ReadRequiredFormInt(form, "strength", "근력"),
        Dexterity: ReadRequiredFormInt(form, "dexterity", "민첩"),
        Constitution: ReadRequiredFormInt(form, "constitution", "체력"),
        Intelligence: ReadRequiredFormInt(form, "intelligence", "지능"),
        Wisdom: ReadRequiredFormInt(form, "wisdom", "지혜"),
        Charisma: ReadRequiredFormInt(form, "charisma", "매력"),
        DamageFormula: ReadRequiredFormString(form, "damageFormula", "피해식"),
        Dp: ReadRequiredFormInt(form, "dp", "DP", allowZero: true),
        MaxHp: ReadRequiredFormInt(form, "maxHp", "최대 HP"),
        Description: form["description"].ToString().Trim(),
        IsEnabled: form["isEnabled"].ToString() == "on");

    await sheets.UpdateEnemyAsync(rowNumber, input);
    var status = Uri.EscapeDataString($"{input.Name} 에너미 정보를 수정했습니다.");
    var updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return Results.Redirect($"/enemies?status={status}&updated={updated}");
});

app.MapPost("/enemies/{rowNumber:int}/delete", async (PandoraSheetAdminService sheets, int rowNumber) =>
{
    var deletedName = await sheets.DeleteEnemyAsync(rowNumber);
    var status = Uri.EscapeDataString($"{deletedName} 에너미와 연결 드롭 정보를 삭제했습니다.");
    var updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return Results.Redirect($"/enemies?status={status}&updated={updated}");
});

app.MapPost("/enemies/{enemyId}/drops/test", async (PandoraSheetAdminService sheets, string enemyId) =>
{
    var result = await sheets.TestEnemyDropAsync(enemyId);
    var status = Uri.EscapeDataString(result.Message);
    var updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return Results.Redirect($"/enemies?status={status}&updated={updated}");
});

app.MapPost("/enemies/{rowNumber:int}/category", async (PandoraSheetAdminService sheets, int rowNumber, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var category = form["category"].ToString();
    await sheets.SetEnemyCategoryAsync(rowNumber, category);
    var status = Uri.EscapeDataString("에너미 출현구분을 변경했습니다.");
    var updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return Results.Redirect($"/enemies?status={status}&updated={updated}");
});

app.Run();

static IResult RedirectWithStatus(string message)
{
    var status = Uri.EscapeDataString(message);
    var updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    return Results.Redirect($"/?status={status}&updated={updated}");
}

static async Task<int> ReadPositiveFormIntAsync(HttpRequest request, string fieldName, bool allowZero = false)
{
    var form = await request.ReadFormAsync();
    var rawValue = form[fieldName].ToString();
    if (!int.TryParse(rawValue, out var value))
    {
        throw new InvalidOperationException("숫자를 입력해주세요.");
    }

    var minimum = allowZero ? 0 : 1;
    if (value < minimum)
    {
        throw new InvalidOperationException(allowZero ? "0 이상의 숫자를 입력해주세요." : "1 이상의 숫자를 입력해주세요.");
    }

    return value;
}

static string ReadRequiredFormString(IFormCollection form, string fieldName, string label)
{
    var value = form[fieldName].ToString().Trim();
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"{label}을 입력해주세요.");
    }

    return value;
}

static int ReadRequiredFormInt(IFormCollection form, string fieldName, string label, bool allowZero = false)
{
    var rawValue = form[fieldName].ToString();
    if (!int.TryParse(rawValue, out var value))
    {
        throw new InvalidOperationException($"{label}에는 숫자를 입력해주세요.");
    }

    var minimum = allowZero ? 0 : 1;
    if (value < minimum)
    {
        throw new InvalidOperationException(allowZero ? $"{label}에는 0 이상의 숫자를 입력해주세요." : $"{label}에는 1 이상의 숫자를 입력해주세요.");
    }

    return value;
}

static string RenderPage(
    IReadOnlyList<CharacterRow> rows,
    string query,
    string reviewFilter,
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
  <link rel="stylesheet" href="/styles.css?v=20260503-admin-ui-2">
</head>
<body>
  <div class="shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">PANDORA NETWORK</p>
        <h1>캐릭터 관리 대시보드</h1>
      </div>
      <nav class="page-tabs" aria-label="관리 화면">
        <a class="active" href="/">캐릭터</a>
        <a href="/enemies">에너미</a>
      </nav>
    </header>
""");

    if (!string.IsNullOrWhiteSpace(status))
    {
        html.Append($"""<div class="notice">{H(status)}</div>""");
    }

    html.Append($"""
    <section class="stats-grid" aria-label="요약">
      <article class="stat-card">
        <span>캐릭터</span>
        <strong>{totalCharacters}</strong>
        <small>등록된 캐릭터</small>
      </article>
      <article class="stat-card">
        <span>플레이어</span>
        <strong>{totalUsers}</strong>
        <small>등록 유저 수</small>
      </article>
      <article class="stat-card">
        <span>사용 중</span>
        <strong>{selectedCount}</strong>
        <small>선택된 캐릭터</small>
      </article>
      <article class="stat-card warn">
        <span>확인 필요</span>
        <strong>{duplicateCount}</strong>
        <small>중복 의심 행</small>
      </article>
    </section>

    <section class="toolbar">
      <form method="get" class="search-form">
        <label for="q">캐릭터 찾기</label>
        <div class="search-row">
          <input id="q" name="q" value="{H(query)}" placeholder="캐릭터 이름이나 플레이어 ID를 입력하세요" autocomplete="off">
          <select name="review" aria-label="검수 상태">
            <option value="all" {Selected(reviewFilter, "all")}>전체</option>
            <option value="pending" {Selected(reviewFilter, AppConstants.ReviewPending)}>검수 대기</option>
            <option value="approved" {Selected(reviewFilter, AppConstants.ReviewApproved)}>승인됨</option>
            <option value="rejected" {Selected(reviewFilter, AppConstants.ReviewRejected)}>반려됨</option>
          </select>
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
          <p>캐릭터 상태를 확인하고 세션 중 필요한 조작을 처리합니다.</p>
        </div>
        <span>{rows.Count}명 표시</span>
      </div>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>캐릭터</th>
              <th>HP</th>
              <th>Physical</th>
              <th>Mental</th>
              <th>검수/선택</th>
              <th>관리</th>
            </tr>
          </thead>
          <tbody>
""");

    if (rows.Count == 0)
    {
        html.Append("""<tr><td colspan="6" class="empty">표시할 캐릭터가 없습니다.</td></tr>""");
    }
    else
    {
        foreach (var row in rows)
        {
            var duplicate = duplicateKeys.Contains(row.DuplicateKey);
            var hpPercent = row.MaxHp <= 0 ? 0 : Math.Clamp(row.CurrentHp * 100 / row.MaxHp, 0, 100);
            var calculatedMaxHp = Math.Max(1, row.Strength + row.Constitution + 2);
            html.Append($"""
            <tr class="{(duplicate ? "duplicate" : "")}">
              <td>
                <strong class="character-name">{H(row.CharacterName)}</strong>
                {(duplicate ? """<span class="badge danger">중복 의심</span>""" : "")}
                <details class="advanced-details">
                  <summary>세부 정보</summary>
                  <div>플레이어 ID: <span class="mono">{H(row.UserId)}</span></div>
                  <div>저장 위치: <span class="mono">#{row.RowNumber}</span></div>
                </details>
                <details class="advanced-details stat-edit-details">
                  <summary>능력치 수정</summary>
                  <form method="post" action="/characters/{row.RowNumber}/stats" class="stat-edit-form">
                    <label><span>근력</span><input type="number" name="strength" min="1" value="{row.Strength}" required></label>
                    <label><span>민첩</span><input type="number" name="dexterity" min="1" value="{row.Dexterity}" required></label>
                    <label><span>체력</span><input type="number" name="constitution" min="1" value="{row.Constitution}" required></label>
                    <label><span>지능</span><input type="number" name="intelligence" min="1" value="{row.Intelligence}" required></label>
                    <label><span>지혜</span><input type="number" name="wisdom" min="1" value="{row.Wisdom}" required></label>
                    <label><span>매력</span><input type="number" name="charisma" min="1" value="{row.Charisma}" required></label>
                    <div class="readonly-stat"><span>최대 HP</span><strong>{calculatedMaxHp}</strong></div>
                    <button type="submit" class="secondary">능력치 저장</button>
                  </form>
                </details>
              </td>
              <td>
                <div class="hp-line">
                  <span>{row.CurrentHp} / {row.MaxHp}</span>
                  <div class="hp-track"><div style="width:{hpPercent}%"></div></div>
                  <div class="hp-controls">
                    <div class="quick-hp-actions" aria-label="{H(row.CharacterName)} 빠른 HP 조작">
                      <form method="post" action="/characters/{row.RowNumber}/hp/damage"><input type="hidden" name="amount" value="1"><button type="submit" class="damage">-1</button></form>
                      <form method="post" action="/characters/{row.RowNumber}/hp/damage"><input type="hidden" name="amount" value="3"><button type="submit" class="damage">-3</button></form>
                      <form method="post" action="/characters/{row.RowNumber}/hp/damage"><input type="hidden" name="amount" value="5"><button type="submit" class="damage">-5</button></form>
                      <form method="post" action="/characters/{row.RowNumber}/hp/heal"><input type="hidden" name="amount" value="1"><button type="submit" class="heal">+1</button></form>
                      <form method="post" action="/characters/{row.RowNumber}/hp/heal"><input type="hidden" name="amount" value="3"><button type="submit" class="heal">+3</button></form>
                      <form method="post" action="/characters/{row.RowNumber}/hp/heal"><input type="hidden" name="amount" value="5"><button type="submit" class="heal">+5</button></form>
                    </div>
                    <form method="post" action="/characters/{row.RowNumber}/hp/set">
                      <input type="number" name="hp" min="0" max="{row.MaxHp}" value="{row.CurrentHp}" aria-label="{H(row.CharacterName)} HP 설정">
                      <button type="submit" class="secondary">설정</button>
                    </form>
                    <form method="post" action="/characters/{row.RowNumber}/hp/damage">
                      <input type="number" name="amount" min="1" value="1" aria-label="{H(row.CharacterName)} 피해량">
                      <button type="submit" class="damage">피해</button>
                    </form>
                    <form method="post" action="/characters/{row.RowNumber}/hp/heal">
                      <input type="number" name="amount" min="1" value="1" aria-label="{H(row.CharacterName)} 회복량">
                      <button type="submit" class="heal">회복</button>
                    </form>
                  </div>
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
                <div class="state-stack">
                  {ReviewBadge(row.ReviewStatus)}
                  {(row.IsSelected ? $"""<span class="badge active">선택됨</span><span class="selection-user">by {H(row.SelectedByUserId)}</span>""" : """<span class="badge muted">미선택</span>""")}
                </div>
              </td>
              <td>
                <div class="actions">
                  <form method="post" action="/characters/{row.RowNumber}/review/approved"><button type="submit" class="approve">승인</button></form>
                  <form method="post" action="/characters/{row.RowNumber}/review/pending"><button type="submit" class="secondary">대기</button></form>
                  <form method="post" action="/characters/{row.RowNumber}/review/rejected" onsubmit="return confirm('{H(row.CharacterName)} 캐릭터를 반려할까요? 반려하면 선택 상태도 해제됩니다.');"><button type="submit" class="reject">반려</button></form>
                  <form method="post" action="/characters/{row.RowNumber}/delete" onsubmit="return confirm('{H(row.CharacterName)} 캐릭터 정보를 삭제할까요? 이 작업은 되돌리기 어렵습니다.');"><button type="submit" class="danger">삭제</button></form>
                </div>
              </td>
            </tr>
""");
        }
    }

    html.Append($"""
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

static string RenderEnemyPage(
    IReadOnlyList<EnemyRow> rows,
    IReadOnlyList<EnemyRow> allEnemies,
    IReadOnlyList<EnemyDropRow> drops,
    IReadOnlyList<EnemyDropSettingRow> dropSettings,
    string query,
    string categoryFilter,
    string? status,
    int totalEnemies)
{
    var categoryOptions = new[]
    {
        "",
        "일반 조우",
        "이벤트",
        "네임드",
        "보스전"
    };
    var enemyOptions = string.Join("", allEnemies.Select(row =>
        $"""<option value="{H(row.EnemyId)}">{H(row.Name)} ({H(row.EnemyId)})</option>"""));
    var enemyNames = allEnemies.ToDictionary(row => row.EnemyId, row => row.Name, StringComparer.OrdinalIgnoreCase);
    var dropSettingsByEnemyId = dropSettings.ToDictionary(row => row.EnemyId, StringComparer.OrdinalIgnoreCase);
    var dropsByEnemyId = drops
        .GroupBy(row => row.EnemyId, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    var html = new StringBuilder();
    html.Append("""
<!doctype html>
<html lang="ko">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PANDORA Enemy Admin</title>
  <link rel="stylesheet" href="/styles.css?v=20260504-enemy-ui-1">
</head>
<body>
  <div class="shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">PANDORA NETWORK</p>
        <h1>에너미 관리 대시보드</h1>
      </div>
      <nav class="page-tabs" aria-label="관리 화면">
        <a href="/">캐릭터</a>
        <a class="active" href="/enemies">에너미</a>
      </nav>
    </header>
""");

    if (!string.IsNullOrWhiteSpace(status))
    {
        html.Append($"""<div class="notice">{H(status)}</div>""");
    }

    html.Append($"""
    <section class="stats-grid compact">
      <article class="stat-card">
        <span>에너미</span>
        <strong>{totalEnemies}</strong>
        <small>저장된 에너미</small>
      </article>
      <article class="stat-card">
        <span>표시</span>
        <strong>{rows.Count}</strong>
        <small>현재 조건과 일치</small>
      </article>
    </section>

    <section class="toolbar">
      <form method="get" class="search-form">
        <label for="q">에너미 찾기</label>
        <div class="search-row">
          <input id="q" name="q" value="{H(query)}" placeholder="이름, 지역, 에너미ID를 입력하세요" autocomplete="off">
          <select name="category" aria-label="출현구분">
            <option value="all" {Selected(categoryFilter, "all")}>전체</option>
            <option value="" {Selected(categoryFilter, "")}>미지정</option>
            <option value="일반 조우" {Selected(categoryFilter, "일반 조우")}>일반 조우</option>
            <option value="이벤트" {Selected(categoryFilter, "이벤트")}>이벤트</option>
            <option value="네임드" {Selected(categoryFilter, "네임드")}>네임드</option>
            <option value="보스전" {Selected(categoryFilter, "보스전")}>보스전</option>
          </select>
          <button type="submit">검색</button>
          <a class="ghost-button" href="/enemies">초기화</a>
        </div>
      </form>
    </section>

    <section class="form-panel">
      <div class="panel-heading compact-heading">
        <div>
          <h2>에너미 추가</h2>
          <p>스포일러가 보이는 시트를 열지 않고, 운영 화면에서만 새 에너미를 등록합니다.</p>
        </div>
      </div>
      <form method="post" action="/enemies/create" class="enemy-create-form">
        <label>
          <span>지역</span>
          <input name="region" placeholder="예: 고북행성" required>
        </label>
        <label>
          <span>에너미 이름</span>
          <input name="name" placeholder="예: 그림자 추적자" required>
        </label>
        <label>
          <span>출현구분</span>
          <select name="category">
            <option value="">미지정</option>
            <option value="일반 조우">일반 조우</option>
            <option value="이벤트">이벤트</option>
            <option value="네임드">네임드</option>
            <option value="보스전">보스전</option>
          </select>
        </label>
        <fieldset>
          <legend>능력치</legend>
          <label><span>근력</span><input type="number" name="strength" min="1" value="1" required></label>
          <label><span>민첩</span><input type="number" name="dexterity" min="1" value="1" required></label>
          <label><span>체력</span><input type="number" name="constitution" min="1" value="1" required></label>
          <label><span>지능</span><input type="number" name="intelligence" min="1" value="1" required></label>
          <label><span>지혜</span><input type="number" name="wisdom" min="1" value="1" required></label>
          <label><span>매력</span><input type="number" name="charisma" min="1" value="1" required></label>
        </fieldset>
        <fieldset>
          <legend>전투값</legend>
          <label><span>피해식</span><input name="damageFormula" placeholder="예: 2D6+1" required></label>
          <label><span>DP</span><input type="number" name="dp" min="0" value="0" required></label>
          <label><span>최대 HP</span><input type="number" name="maxHp" min="1" value="1" required></label>
        </fieldset>
        <label class="wide-field">
          <span>설명</span>
          <input name="description" placeholder="운영자만 확인할 짧은 메모">
        </label>
        <div class="form-actions">
          <button type="submit" class="approve">에너미 추가</button>
        </div>
      </form>
    </section>

    <section class="form-panel">
      <div class="panel-heading compact-heading">
        <div>
          <h2>전리품 추가</h2>
          <p>공유 시트에 직접 적지 않고, 에너미ID 기준으로 드롭 테이블에 연결합니다.</p>
        </div>
      </div>
      <form method="post" action="/enemy-drops/create" class="enemy-create-form drop-create-form">
        <label>
          <span>에너미</span>
          <select name="enemyId" required>
            {enemyOptions}
          </select>
        </label>
        <label>
          <span>아이템명</span>
          <input name="itemName" placeholder="예: 슬라임 조각" required>
        </label>
        <label>
          <span>확률</span>
          <input type="number" name="chance" min="1" max="100" value="100" required>
        </label>
        <label>
          <span>최소개수</span>
          <input type="number" name="minCount" min="1" value="1" required>
        </label>
        <label>
          <span>최대개수</span>
          <input type="number" name="maxCount" min="1" value="1" required>
        </label>
        <label>
          <span>무게</span>
          <input type="number" name="weight" min="0" value="1" required>
        </label>
        <label>
          <span>희귀도</span>
          <select name="rarity">
            <option value="">미지정</option>
            <option value="커먼">커먼</option>
            <option value="언커먼">언커먼</option>
            <option value="레어">레어</option>
            <option value="에픽">에픽</option>
            <option value="전설">전설</option>
          </select>
        </label>
        <label>
          <span>태그</span>
          <input name="tag" placeholder="예: 재료">
        </label>
        <label class="wide-field">
          <span>메모</span>
          <input name="memo" placeholder="운영자용 메모">
        </label>
        <div class="form-actions">
          <button type="submit" class="approve">전리품 추가</button>
        </div>
      </form>
    </section>

    <main class="table-panel">
      <div class="panel-heading">
        <div>
          <h2>에너미 저장소</h2>
          <p>운영자가 직접 출현구분을 선택합니다. 비워두면 아직 분류하지 않은 상태입니다.</p>
        </div>
        <span>{rows.Count}명 표시</span>
      </div>
      <div class="table-wrap">
        <table class="enemy-table">
          <thead>
            <tr>
              <th>에너미</th>
              <th>능력치</th>
              <th>전투값</th>
              <th>출현구분</th>
              <th>상태</th>
              <th>관리</th>
            </tr>
          </thead>
          <tbody>
""");

    if (rows.Count == 0)
    {
        html.Append("""<tr><td colspan="6" class="empty">표시할 에너미가 없습니다.</td></tr>""");
    }
    else
    {
        foreach (var row in rows)
        {
            var options = string.Join("", categoryOptions.Select(option =>
                $"""<option value="{H(option)}" {Selected(row.Category, option)}>{(string.IsNullOrWhiteSpace(option) ? "미지정" : H(option))}</option>"""));
            var rowDrops = dropsByEnemyId.TryGetValue(row.EnemyId, out var linkedDrops) ? linkedDrops : new List<EnemyDropRow>();
            var setting = dropSettingsByEnemyId.TryGetValue(row.EnemyId, out var foundSetting)
                ? foundSetting
                : new EnemyDropSettingRow(0, row.EnemyId, 100, 1, false, "");
            var chanceSum = rowDrops.Sum(drop => Math.Clamp(drop.Chance, 1, 100));
            var chanceBadge = chanceSum > 100
                ? $"""<span class="badge rejected">확률합 {chanceSum}%</span>"""
                : $"""<span class="badge approved">확률합 {chanceSum}%</span>""";
            var linkedDropPreview = rowDrops.Count == 0
                ? """<span class="muted-text">연결 전리품 없음</span>"""
                : string.Join("", rowDrops.Select(drop => $"""<span>{H(drop.ItemName)} <b>{drop.Chance}%</b></span>"""));

            html.Append($"""
            <tr>
              <td>
                <strong class="character-name">{H(row.Name)}</strong>
                <span class="mono">{H(row.EnemyId)}</span>
                <div class="enemy-region">{H(row.Region)}</div>
                <details class="advanced-details enemy-detail">
                  <summary>상세/수정</summary>
                  <div>설명: {H(row.Description)}</div>
                  <form method="post" action="/enemies/{row.RowNumber}/edit" class="enemy-edit-form">
                    <label><span>지역</span><input name="region" value="{H(row.Region)}" required></label>
                    <label><span>이름</span><input name="name" value="{H(row.Name)}" required></label>
                    <label><span>출현구분</span><select name="category">{options}</select></label>
                    <label><span>근력</span><input type="number" name="strength" min="1" value="{row.Strength}" required></label>
                    <label><span>민첩</span><input type="number" name="dexterity" min="1" value="{row.Dexterity}" required></label>
                    <label><span>체력</span><input type="number" name="constitution" min="1" value="{row.Constitution}" required></label>
                    <label><span>지능</span><input type="number" name="intelligence" min="1" value="{row.Intelligence}" required></label>
                    <label><span>지혜</span><input type="number" name="wisdom" min="1" value="{row.Wisdom}" required></label>
                    <label><span>매력</span><input type="number" name="charisma" min="1" value="{row.Charisma}" required></label>
                    <label><span>피해식</span><input name="damageFormula" value="{H(row.DamageFormula)}" required></label>
                    <label><span>DP</span><input type="number" name="dp" min="0" value="{row.Dp}" required></label>
                    <label><span>최대 HP</span><input type="number" name="maxHp" min="1" value="{row.MaxHp}" required></label>
                    <label class="toggle-field"><input type="checkbox" name="isEnabled" {(IsTruthy(row.IsEnabled) ? "checked" : "")}> 사용</label>
                    <label class="wide-field"><span>설명</span><input name="description" value="{H(row.Description)}"></label>
                    <button type="submit" class="secondary">수정 저장</button>
                  </form>
                </details>
              </td>
              <td>
                <div class="stat-list enemy-stats">
                  <span>근력 <b>{row.Strength}</b></span>
                  <span>민첩 <b>{row.Dexterity}</b></span>
                  <span>체력 <b>{row.Constitution}</b></span>
                  <span>지능 <b>{row.Intelligence}</b></span>
                  <span>지혜 <b>{row.Wisdom}</b></span>
                  <span>매력 <b>{row.Charisma}</b></span>
                </div>
              </td>
              <td>
                <div class="state-stack">
                  <span>피해식 <b>{H(row.DamageFormula)}</b></span>
                  <span>DP <b>{row.Dp}</b></span>
                  <span>기준 HP <b>{row.MaxHp}</b></span>
                </div>
              </td>
              <td>
                <form method="post" action="/enemies/{row.RowNumber}/category" class="inline-select-form">
                  <select name="category" aria-label="{H(row.Name)} 출현구분">
                    {options}
                  </select>
                  <button type="submit" class="secondary">저장</button>
                </form>
              </td>
              <td>
                <div class="state-stack">
                  {(string.IsNullOrWhiteSpace(row.Category) ? """<span class="badge muted">미지정</span>""" : $"""<span class="badge pending">{H(row.Category)}</span>""")}
                  {(IsTruthy(row.IsEnabled) ? """<span class="badge approved">사용</span>""" : """<span class="badge rejected">비활성</span>""")}
                  <span class="badge muted">미발생 {100 - setting.DropRate}%</span>
                  {chanceBadge}
                </div>
              </td>
              <td>
                <details class="advanced-details drop-link-details">
                  <summary>드롭 {rowDrops.Count}개</summary>
                  <div class="drop-link-list">{linkedDropPreview}</div>
                </details>
                <form method="post" action="/enemies/{H(row.EnemyId)}/drops/test" class="compact-action-form">
                  <button type="submit" class="secondary">드롭 테스트</button>
                </form>
                <form method="post" action="/enemies/{row.RowNumber}/delete" class="compact-action-form">
                  <button type="submit" class="delete">삭제</button>
                </form>
              </td>
            </tr>
""");
        }
    }

    html.Append($"""
          </tbody>
        </table>
      </div>
    </main>

    <main class="table-panel">
      <div class="panel-heading">
        <div>
          <h2>드롭 설정</h2>
          <p>전리품이 아예 나오지 않을 가능성을 에너미별로 관리합니다. 중복 드롭은 허용하지 않습니다.</p>
        </div>
        <span>중복 없음</span>
      </div>
      <div class="table-wrap">
        <table class="drop-setting-table">
          <thead>
            <tr>
              <th>에너미</th>
              <th>전리품 발생률</th>
              <th>드롭횟수</th>
              <th>중복</th>
              <th>비고</th>
              <th>관리</th>
            </tr>
          </thead>
          <tbody>
""");

    if (rows.Count == 0)
    {
        html.Append("""<tr><td colspan="6" class="empty">표시할 에너미가 없습니다.</td></tr>""");
    }
    else
    {
        foreach (var row in rows)
        {
            var setting = dropSettingsByEnemyId.TryGetValue(row.EnemyId, out var found)
                ? found
                : new EnemyDropSettingRow(0, row.EnemyId, 100, 1, false, "");
            var settingFormId = $"drop-setting-{row.RowNumber}";

            html.Append($"""
            <tr>
              <td>
                <strong class="character-name">{H(row.Name)}</strong>
                <span class="mono">{H(row.EnemyId)}</span>
                <form id="{settingFormId}" method="post" action="/enemy-drop-settings/{H(row.EnemyId)}" class="drop-setting-form"></form>
              </td>
              <td>
                <div class="percent-input">
                  <input form="{settingFormId}" type="number" name="dropRate" min="0" max="100" value="{setting.DropRate}" aria-label="{H(row.Name)} 전리품 발생률">
                  <span>%</span>
                </div>
              </td>
              <td>
                <input form="{settingFormId}" type="number" name="dropCount" min="1" value="{setting.DropCount}" aria-label="{H(row.Name)} 드롭횟수">
              </td>
              <td><span class="badge muted">중복 없음</span></td>
              <td>
                <input form="{settingFormId}" name="memo" value="{H(setting.Memo)}" placeholder="운영자 메모" aria-label="{H(row.Name)} 드롭 설정 메모">
              </td>
              <td>
                <button form="{settingFormId}" type="submit" class="secondary">저장</button>
              </td>
            </tr>
""");
        }
    }

    html.Append($"""
          </tbody>
        </table>
      </div>
    </main>

    <main class="table-panel">
      <div class="panel-heading">
        <div>
          <h2>전리품 테이블</h2>
          <p>시트에는 에너미ID만 저장하고, 화면에서만 이름을 함께 표시합니다.</p>
        </div>
        <span>전리품 {drops.Count}개</span>
      </div>
      <div class="table-wrap">
        <table class="drop-table">
          <thead>
            <tr>
              <th>에너미</th>
              <th>아이템</th>
              <th>확률</th>
              <th>개수</th>
              <th>속성</th>
              <th>메모</th>
            </tr>
          </thead>
          <tbody>
""");

    if (drops.Count == 0)
    {
        html.Append("""<tr><td colspan="6" class="empty">표시할 전리품이 없습니다.</td></tr>""");
    }
    else
    {
        foreach (var drop in drops)
        {
            var enemyName = enemyNames.TryGetValue(drop.EnemyId, out var name) ? name : "알 수 없음";
            html.Append($"""
            <tr>
              <td>
                <strong class="character-name">{H(enemyName)}</strong>
                <span class="mono">{H(drop.EnemyId)}</span>
              </td>
              <td>{H(drop.ItemName)}</td>
              <td><span class="badge pending">{drop.Chance}%</span></td>
              <td>{drop.MinCount} ~ {drop.MaxCount}</td>
              <td>
                <div class="state-stack">
                  <span>무게 <b>{drop.Weight}</b></span>
                  <span>희귀도 <b>{H(drop.Rarity)}</b></span>
                  <span>태그 <b>{H(drop.Tag)}</b></span>
                </div>
              </td>
              <td>{H(drop.Memo)}</td>
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

static bool IsTruthy(string? value)
{
    return value?.Trim().ToUpperInvariant() is "TRUE" or "Y" or "YES" or "1" or "사용";
}

static string Selected(string current, string value)
{
    return string.Equals(current, value, StringComparison.OrdinalIgnoreCase) ? "selected" : "";
}

static string ReviewBadge(string reviewStatus)
{
    var normalized = NormalizeReviewStatus(reviewStatus);
    return normalized switch
    {
        AppConstants.ReviewPending => """<span class="badge pending">검수 대기</span>""",
        AppConstants.ReviewRejected => """<span class="badge rejected">반려</span>""",
        _ => """<span class="badge approved">승인</span>"""
    };
}

static string NormalizeReviewStatus(string value)
{
    var normalized = value.Trim().ToLowerInvariant();
    return normalized switch
    {
        "" => AppConstants.ReviewApproved,
        "승인" or "approve" or "approved" => AppConstants.ReviewApproved,
        "대기" or "검수" or "pending" => AppConstants.ReviewPending,
        "반려" or "reject" or "rejected" => AppConstants.ReviewRejected,
        _ => normalized
    };
}

static string NormalizeReviewFilter(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "all";
    }

    var normalized = NormalizeReviewStatus(value);
    return normalized is AppConstants.ReviewApproved or AppConstants.ReviewPending or AppConstants.ReviewRejected
        ? normalized
        : "all";
}

sealed class PandoraSheetAdminService
{
    private readonly SheetsService service;
    private readonly string spreadsheetId;
    private bool operationalSheetsChecked;

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
        await EnsureOperationalSheetsAsync();

        var response = await service.Spreadsheets.Values.Get(spreadsheetId, $"{RangeSheet(AppConstants.StorageSheetName)}!A2:N").ExecuteAsync();
        var values = response.Values ?? new List<IList<object>>();
        var selections = await ReadSelectionRowsAsync();
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

            var maxHp = GetInt(row, 8);
            var currentHp = ClampHp(GetInt(row, 9), maxHp);

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
                MaxHp: maxHp,
                CurrentHp: currentHp,
                Selected: GetString(row, 10),
                ReviewStatus: NormalizeReviewStatus(GetString(row, 11)),
                SelectedByUserId: GetSelectedBy(userId, characterName, GetString(row, 10), selections),
                SourceSpreadsheetId: GetString(row, 12),
                SourceSheetName: GetString(row, 13)));
        }

        return rows
            .OrderBy(row => row.UserId)
            .ThenBy(row => row.CharacterName)
            .ThenBy(row => row.RowNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<EnemyRow>> GetEnemiesAsync()
    {
        await EnsureOperationalSheetsAsync();

        var response = await service.Spreadsheets.Values.Get(spreadsheetId, $"{RangeSheet(AppConstants.EnemyStorageSheetName)}!A2:P").ExecuteAsync();
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

    public async Task<IReadOnlyList<EnemyDropRow>> GetEnemyDropsAsync()
    {
        await EnsureOperationalSheetsAsync();

        var response = await service.Spreadsheets.Values.Get(spreadsheetId, $"{RangeSheet(AppConstants.EnemyDropSheetName)}!A2:I").ExecuteAsync();
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
        await EnsureOperationalSheetsAsync();

        var response = await service.Spreadsheets.Values.Get(spreadsheetId, $"{RangeSheet(AppConstants.EnemyDropSettingSheetName)}!A2:E").ExecuteAsync();
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

        var enemyId = CreateNextEnemyId(rows);
        await AppendRowAsync(AppConstants.EnemyStorageSheetName, new List<object>
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
            input.MaxHp,
            input.MaxHp,
            input.Description,
            "TRUE"
        });

        await AppendAdminLogRowAsync("에너미추가", "", input.Name, $"{enemyId} / {input.Region}");
        return enemyId;
    }

    public async Task AddEnemyDropAsync(EnemyDropCreateInput input)
    {
        var enemies = await GetEnemiesAsync();
        if (!enemies.Any(row => string.Equals(row.EnemyId, input.EnemyId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("선택한 에너미를 찾을 수 없습니다.");
        }

        var minCount = input.MinCount;
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

        await AppendRowAsync(AppConstants.EnemyDropSheetName, new List<object>
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
        var enemies = await GetEnemiesAsync();
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
            await AppendRowAsync(AppConstants.EnemyDropSettingSheetName, values);
        }
        else
        {
            var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
            var request = service.Spreadsheets.Values.Update(
                valueRange,
                spreadsheetId,
                $"{RangeSheet(AppConstants.EnemyDropSettingSheetName)}!A{existing.RowNumber}:E{existing.RowNumber}");

            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await request.ExecuteAsync();
        }

        await AppendAdminLogRowAsync("드롭설정", "", input.EnemyId, $"발생률 {dropRate}% / {dropCount}회 / 중복 FALSE");
    }

    public async Task SetEnemyCategoryAsync(int rowNumber, string category)
    {
        var normalized = NormalizeEnemyCategory(category);
        await UpdateCellAsync(AppConstants.EnemyStorageSheetName, rowNumber, "D", normalized);
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

        var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
        var request = service.Spreadsheets.Values.Update(
            valueRange,
            spreadsheetId,
            $"{RangeSheet(AppConstants.EnemyStorageSheetName)}!B{rowNumber}:P{rowNumber}");

        request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await request.ExecuteAsync();
        await AppendAdminLogRowAsync("에너미수정", "", target.Name, $"{target.EnemyId}: {target.Name} -> {input.Name}");
    }

    public async Task<string> DeleteEnemyAsync(int rowNumber)
    {
        var rows = await GetEnemiesAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber)
            ?? throw new InvalidOperationException("해당 행의 에너미를 찾을 수 없습니다.");

        await service.Spreadsheets.Values.Clear(
            new ClearValuesRequest(),
            spreadsheetId,
            $"{RangeSheet(AppConstants.EnemyStorageSheetName)}!A{rowNumber}:P{rowNumber}").ExecuteAsync();

        var drops = await GetEnemyDropsAsync();
        foreach (var drop in drops.Where(row => string.Equals(row.EnemyId, target.EnemyId, StringComparison.OrdinalIgnoreCase)))
        {
            await service.Spreadsheets.Values.Clear(
                new ClearValuesRequest(),
                spreadsheetId,
                $"{RangeSheet(AppConstants.EnemyDropSheetName)}!A{drop.RowNumber}:I{drop.RowNumber}").ExecuteAsync();
        }

        var settings = await GetEnemyDropSettingsAsync();
        foreach (var setting in settings.Where(row => string.Equals(row.EnemyId, target.EnemyId, StringComparison.OrdinalIgnoreCase)))
        {
            await service.Spreadsheets.Values.Clear(
                new ClearValuesRequest(),
                spreadsheetId,
                $"{RangeSheet(AppConstants.EnemyDropSettingSheetName)}!A{setting.RowNumber}:E{setting.RowNumber}").ExecuteAsync();
        }

        await AppendAdminLogRowAsync("에너미삭제", "", target.Name, $"{target.EnemyId} / 연결 드롭 삭제");
        return target.Name;
    }

    public async Task<DropTestResult> TestEnemyDropAsync(string enemyId)
    {
        var enemies = await GetEnemiesAsync();
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
            return new DropTestResult($"{enemy.Name}: 연결된 전리품이 없습니다.");
        }

        var occurRoll = Random.Shared.Next(1, 101);
        if (occurRoll > setting.DropRate)
        {
            return new DropTestResult($"{enemy.Name}: 전리품 없음 (발생 {setting.DropRate}%, 굴림 {occurRoll})");
        }

        var remaining = drops.ToList();
        var results = new List<string>();
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
            var count = Random.Shared.Next(selected.MinCount, Math.Max(selected.MaxCount, selected.MinCount) + 1);
            results.Add($"{selected.ItemName} x{count}");
            remaining.RemoveAll(drop => drop.RowNumber == selected.RowNumber);
        }

        var message = results.Count == 0
            ? $"{enemy.Name}: 전리품 발생은 성공했지만 개별 전리품 확률을 통과하지 못했습니다."
            : $"{enemy.Name}: {string.Join(", ", results)}";

        await AppendAdminLogRowAsync("드롭테스트", "", enemy.Name, message);
        return new DropTestResult(message);
    }

    public async Task SetHpAsync(int rowNumber, int hp)
    {
        var rows = await GetCharactersAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber)
            ?? throw new InvalidOperationException("해당 행의 캐릭터를 찾을 수 없습니다.");

        var nextHp = Math.Clamp(hp, 0, target.MaxHp);
        await UpdateStorageCellAsync(rowNumber, "J", nextHp.ToString());
        await AppendAdminLogRowAsync("웹HP설정", target.UserId, target.CharacterName, $"{target.CurrentHp} -> {nextHp}");
    }

    public async Task<bool> SetCharacterStatsAsync(int rowNumber, CharacterStatsInput input)
    {
        var rows = await GetCharactersAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber)
            ?? throw new InvalidOperationException("해당 행의 캐릭터를 찾을 수 없습니다.");

        var nextMaxHp = CalculateMaxHp(input.Strength, input.Constitution);
        var nextCurrentHp = Math.Clamp(target.CurrentHp, 0, nextMaxHp);
        var valueRange = new ValueRange
        {
            Values = new List<IList<object>>
            {
                new List<object>
                {
                    input.Strength,
                    input.Dexterity,
                    input.Constitution,
                    input.Intelligence,
                    input.Wisdom,
                    input.Charisma,
                    nextMaxHp,
                    nextCurrentHp
                }
            }
        };

        var request = service.Spreadsheets.Values.Update(
            valueRange,
            spreadsheetId,
            $"{RangeSheet(AppConstants.StorageSheetName)}!C{rowNumber}:J{rowNumber}");

        request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await request.ExecuteAsync();
        var syncedSource = await TryUpdateSourceCharacterStatsAsync(target, input, nextCurrentHp);

        await AppendAdminLogRowAsync(
            "능력치수정",
            target.UserId,
            target.CharacterName,
            $"STR {target.Strength}->{input.Strength}, DEX {target.Dexterity}->{input.Dexterity}, CON {target.Constitution}->{input.Constitution}, INT {target.Intelligence}->{input.Intelligence}, WIS {target.Wisdom}->{input.Wisdom}, CHA {target.Charisma}->{input.Charisma}, MaxHP {target.MaxHp}->{nextMaxHp}, SourceSynced={syncedSource}");

        return syncedSource;
    }

    private async Task<bool> TryUpdateSourceCharacterStatsAsync(CharacterRow target, CharacterStatsInput input, int currentHp)
    {
        if (string.IsNullOrWhiteSpace(target.SourceSpreadsheetId) || string.IsNullOrWhiteSpace(target.SourceSheetName))
        {
            return false;
        }

        var sheetName = RangeSheet(target.SourceSheetName);
        var updates = new List<ValueRange>
        {
            Cell($"{sheetName}!U10", input.Strength),
            Cell($"{sheetName}!X10", input.Dexterity),
            Cell($"{sheetName}!AA10", input.Constitution),
            Cell($"{sheetName}!AD10", input.Intelligence),
            Cell($"{sheetName}!AG10", input.Wisdom),
            Cell($"{sheetName}!AJ10", input.Charisma),
            Cell($"{sheetName}!AE15", currentHp)
        };

        var request = service.Spreadsheets.Values.BatchUpdate(
            new BatchUpdateValuesRequest
            {
                ValueInputOption = "RAW",
                Data = updates
            },
            target.SourceSpreadsheetId);

        await request.ExecuteAsync();
        return true;
    }

    private static ValueRange Cell(string range, object value)
    {
        return new ValueRange
        {
            Range = range,
            Values = new List<IList<object>> { new List<object> { value } }
        };
    }

    public async Task AdjustHpAsync(int rowNumber, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var rows = await GetCharactersAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber)
            ?? throw new InvalidOperationException("해당 행의 캐릭터를 찾을 수 없습니다.");

        var nextHp = Math.Clamp(target.CurrentHp + delta, 0, target.MaxHp);
        await UpdateStorageCellAsync(rowNumber, "J", nextHp.ToString());
        var action = delta > 0 ? "웹회복" : "웹피해";
        await AppendAdminLogRowAsync(action, target.UserId, target.CharacterName, $"{target.CurrentHp} -> {nextHp} ({delta:+#;-#;0})");
    }

    public async Task SetReviewStatusAsync(int rowNumber, string reviewStatus)
    {
        var normalizedStatus = NormalizeReviewStatus(reviewStatus);
        if (normalizedStatus is not AppConstants.ReviewApproved and not AppConstants.ReviewPending and not AppConstants.ReviewRejected)
        {
            throw new InvalidOperationException("검수 상태는 approved, pending, rejected 중 하나여야 합니다.");
        }

        var rows = await GetCharactersAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber)
            ?? throw new InvalidOperationException("해당 행의 캐릭터를 찾을 수 없습니다.");

        await UpdateStorageCellAsync(rowNumber, "L", normalizedStatus);
        await AppendAdminLogRowAsync("웹검수상태", target.UserId, target.CharacterName, $"{target.ReviewStatus} -> {normalizedStatus}");

        if (normalizedStatus == AppConstants.ReviewRejected)
        {
            await ClearSelectionForCharacterAsync(target.UserId, target.CharacterName);
            await UpdateStorageCellAsync(rowNumber, "K", "");
        }
    }

    public async Task DeleteCharacterAsync(int rowNumber)
    {
        var rows = await GetCharactersAsync();
        var target = rows.FirstOrDefault(row => row.RowNumber == rowNumber);
        if (target != null)
        {
            await ClearSelectionForCharacterAsync(target.UserId, target.CharacterName);
        }

        var clearRequest = service.Spreadsheets.Values.Clear(
            new ClearValuesRequest(),
            spreadsheetId,
            $"{RangeSheet(AppConstants.StorageSheetName)}!A{rowNumber}:L{rowNumber}");

        await clearRequest.ExecuteAsync();

        if (target != null)
        {
            await AppendAdminLogRowAsync("웹삭제", target.UserId, target.CharacterName, $"row {rowNumber}");
        }
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

    private async Task UpdateStorageCellAsync(int rowNumber, string column, string value)
    {
        await UpdateCellAsync(AppConstants.StorageSheetName, rowNumber, column, value);
    }

    private async Task UpdateCellAsync(string sheetName, int rowNumber, string column, string value)
    {
        var valueRange = new ValueRange
        {
            Values = new List<IList<object>> { new List<object> { value } }
        };

        var request = service.Spreadsheets.Values.Update(
            valueRange,
            spreadsheetId,
            $"{RangeSheet(sheetName)}!{column}{rowNumber}");

        request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await request.ExecuteAsync();
    }

    private async Task AppendAdminLogRowAsync(string action, string targetUserId, string characterName, string detail)
    {
        await AppendRowAsync(AppConstants.AdminLogSheetName, new List<object>
        {
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            action,
            "WEB_DASHBOARD",
            "PandoraAdmin",
            targetUserId,
            characterName,
            detail
        });
    }

    private async Task AppendRowAsync(string sheetName, IList<object> values)
    {
        var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
        var request = service.Spreadsheets.Values.Append(valueRange, spreadsheetId, $"{RangeSheet(sheetName)}!A:Z");
        request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
        request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
        await request.ExecuteAsync();
    }

    private async Task<IReadOnlyList<SelectionRow>> ReadSelectionRowsAsync()
    {
        var response = await service.Spreadsheets.Values.Get(spreadsheetId, $"{RangeSheet(AppConstants.SelectionSheetName)}!A2:D").ExecuteAsync();
        var values = response.Values ?? new List<IList<object>>();
        var rows = new List<SelectionRow>();

        for (var index = 0; index < values.Count; index++)
        {
            var row = values[index];
            var userId = GetString(row, 0);
            var characterName = GetString(row, 1);

            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(characterName))
            {
                continue;
            }

            rows.Add(new SelectionRow(index + 2, userId, characterName, GetString(row, 2)));
        }

        return rows;
    }

    private async Task ClearSelectionForCharacterAsync(string userId, string characterName)
    {
        var selections = await ReadSelectionRowsAsync();
        var target = selections.FirstOrDefault(row =>
            row.UserId == userId &&
            string.Equals(Normalize(row.CharacterName), Normalize(characterName), StringComparison.Ordinal));

        if (target == null)
        {
            return;
        }

        var clearRequest = service.Spreadsheets.Values.Clear(
            new ClearValuesRequest(),
            spreadsheetId,
            $"{RangeSheet(AppConstants.SelectionSheetName)}!A{target.RowNumber}:D{target.RowNumber}");

        await clearRequest.ExecuteAsync();
    }

    private async Task EnsureOperationalSheetsAsync()
    {
        if (operationalSheetsChecked)
        {
            return;
        }

        var metadata = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
        var existingSheets = (metadata.Sheets ?? new List<Sheet>())
            .Select(sheet => sheet.Properties?.Title ?? "")
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .ToHashSet(StringComparer.Ordinal);

        var addRequests = new List<Request>();
        foreach (var sheetName in new[] { AppConstants.SelectionSheetName, AppConstants.AdminLogSheetName, AppConstants.EnemyDropSheetName, AppConstants.EnemyDropSettingSheetName })
        {
            if (!existingSheets.Contains(sheetName))
            {
                addRequests.Add(new Request
                {
                    AddSheet = new AddSheetRequest
                    {
                        Properties = new SheetProperties { Title = sheetName }
                    }
                });
            }
        }

        if (addRequests.Count > 0)
        {
            await service.Spreadsheets.BatchUpdate(
                new BatchUpdateSpreadsheetRequest { Requests = addRequests },
                spreadsheetId).ExecuteAsync();
        }

        await WriteHeaderRowAsync(AppConstants.StorageSheetName, "L1", new List<object> { "검수상태" });
        await WriteHeaderRowAsync(AppConstants.SelectionSheetName, "A1:D1", new List<object> { "유저ID", "선택캐릭터", "선택시각", "비고" });
        await WriteHeaderRowAsync(AppConstants.AdminLogSheetName, "A1:G1", new List<object> { "시각", "행동", "관리자ID", "관리자명", "대상유저ID", "캐릭터", "상세" });
        await WriteHeaderRowAsync(AppConstants.EnemyDropSheetName, "A1:I1", new List<object> { "에너미ID", "아이템명", "확률", "최소개수", "최대개수", "무게", "희귀도", "태그", "메모" });
        await WriteHeaderRowAsync(AppConstants.EnemyDropSettingSheetName, "A1:E1", new List<object> { "에너미ID", "전리품발생률", "드롭횟수", "중복허용", "비고" });
        operationalSheetsChecked = true;
    }

    private async Task WriteHeaderRowAsync(string sheetName, string range, IList<object> values)
    {
        var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
        var request = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, $"{RangeSheet(sheetName)}!{range}");
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

    private static string GetSelectedBy(string userId, string characterName, string legacySelected, IReadOnlyList<SelectionRow> selections)
    {
        var selection = selections.FirstOrDefault(row =>
            row.UserId == userId &&
            string.Equals(Normalize(row.CharacterName), Normalize(characterName), StringComparison.Ordinal));

        if (selection != null)
        {
            return selection.UserId;
        }

        return string.Equals(legacySelected, AppConstants.SelectedMarker, StringComparison.OrdinalIgnoreCase)
            ? userId
            : "";
    }

    private static string NormalizeReviewStatus(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "" => AppConstants.ReviewApproved,
            "승인" or "approve" or "approved" => AppConstants.ReviewApproved,
            "대기" or "검수" or "pending" => AppConstants.ReviewPending,
            "반려" or "reject" or "rejected" => AppConstants.ReviewRejected,
            _ => normalized
        };
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

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static int GetInt(IList<object> values, int index)
    {
        return int.TryParse(GetString(values, index), out var value) ? value : 0;
    }

    private static int ClampHp(int currentHp, int maxHp)
    {
        return maxHp > 0 ? Math.Clamp(currentHp, 0, maxHp) : Math.Max(currentHp, 0);
    }

    private static int CalculateMaxHp(int strength, int constitution)
    {
        return Math.Max(1, strength + constitution + 2);
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
    string Selected,
    string ReviewStatus,
    string SelectedByUserId,
    string SourceSpreadsheetId,
    string SourceSheetName)
{
    public bool IsSelected => !string.IsNullOrWhiteSpace(SelectedByUserId) ||
        string.Equals(Selected, AppConstants.SelectedMarker, StringComparison.OrdinalIgnoreCase);

    public string DuplicateKey => $"{UserId.Trim()}::{CharacterName.Trim().ToUpperInvariant()}";
}

sealed record SelectionRow(int RowNumber, string UserId, string CharacterName, string SelectedAt);

sealed record CharacterStatsInput(
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma);

sealed record EnemyRow(
    int RowNumber,
    string EnemyId,
    string Region,
    string Name,
    string Category,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    string DamageFormula,
    int Dp,
    int CurrentHp,
    int MaxHp,
    string Description,
    string IsEnabled);

sealed record EnemyDropRow(
    int RowNumber,
    string EnemyId,
    string ItemName,
    int Chance,
    int MinCount,
    int MaxCount,
    int Weight,
    string Rarity,
    string Tag,
    string Memo);

sealed record EnemyDropSettingRow(
    int RowNumber,
    string EnemyId,
    int DropRate,
    int DropCount,
    bool AllowDuplicate,
    string Memo);

sealed record EnemyCreateInput(
    string Region,
    string Name,
    string Category,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    string DamageFormula,
    int Dp,
    int MaxHp,
    string Description);

sealed record EnemyEditInput(
    string Region,
    string Name,
    string Category,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    string DamageFormula,
    int Dp,
    int MaxHp,
    string Description,
    bool IsEnabled);

sealed record EnemyDropCreateInput(
    string EnemyId,
    string ItemName,
    int Chance,
    int MinCount,
    int MaxCount,
    int Weight,
    string Rarity,
    string Tag,
    string Memo);

sealed record EnemyDropSettingInput(
    string EnemyId,
    int DropRate,
    int DropCount,
    string Memo);

sealed record DropTestResult(string Message);

sealed class BotSettings
{
    public string? GoogleCredentialPath { get; set; } = "";
    public string SettingsDirectory { get; set; } = "";
}

static class AppConstants
{
    public const string StorageSheetName = "캐릭터 저장소";
    public const string EnemyStorageSheetName = "에너미 저장소";
    public const string EnemyDropSheetName = "에너미 드롭 테이블";
    public const string EnemyDropSettingSheetName = "에너미 드롭 설정";
    public const string SelectionSheetName = "선택 상태";
    public const string AdminLogSheetName = "관리 로그";
    public const string SelectedMarker = "selected";
    public const string ReviewPending = "pending";
    public const string ReviewApproved = "approved";
    public const string ReviewRejected = "rejected";
    public const string DefaultSpreadsheetId = "13DKG_V3TD5GHxQrVpmFGQhFluPvGc3E_M5FXfdvRkqI";
}
