using Discord;
using Discord.Interactions;
using PandoraBot.Repositories;
using PandoraShared.Models;

namespace PandoraBot.Modules;

[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class OperationsModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("로그", "관리자용: 판정, 관리, 공지, 전투 로그를 한곳에서 확인합니다.")]
    public async Task ShowLogs(
        [Summary("로그대상", "전체, 판정, 관리, 공지, 전투 중 하나")] string target = "전체",
        [Summary("개수", "1~50 사이 조회 개수")] int limit = 15)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var logs = await PandoraRepositoryProvider.Logs.GetLogsAsync(target, limit);
            if (logs.Count == 0)
            {
                await FollowupAsync($"아직 확인할 `{target}` 로그가 없습니다. 조금 뒤에 다시 보거나 다른 로그 구분으로 확인해 주세요.", ephemeral: true);
                return;
            }

            await FollowupAsync(embed: BuildUnifiedLogEmbed(target, logs), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("에너미추가", "관리자용: 에너미를 PostgreSQL 저장소에 추가합니다.")]
    public async Task CreateEnemy(
        [Summary("이름", "에너미 이름")] string name,
        [Summary("출현구분", "층수, 구역, 챕터 등 운영용 분류")] string category,
        [Summary("최대HP", "최대 체력")] int maxHp,
        [Summary("근력", "근력 수치")] int strength,
        [Summary("민첩", "민첩 수치")] int dexterity,
        [Summary("체력", "체력 수치")] int constitution,
        [Summary("지능", "지능 수치")] int intelligence,
        [Summary("지혜", "지혜 수치")] int wisdom,
        [Summary("매력", "매력 수치")] int charisma,
        [Summary("피해식", "선택 사항: 기본 피해식")] string? damageFormula = null,
        [Summary("DP", "선택 사항: 방어 계수")] int dp = 0,
        [Summary("설명", "선택 사항: 설명 또는 메모")] string? description = null)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var created = await PandoraRepositoryProvider.Enemies.CreateEnemyAsync(new EnemyCreateInput(
                Region: category,
                Name: name,
                Category: category,
                Strength: strength,
                Dexterity: dexterity,
                Constitution: constitution,
                Intelligence: intelligence,
                Wisdom: wisdom,
                Charisma: charisma,
                DamageFormula: damageFormula ?? string.Empty,
                Dp: dp,
                MaxHp: maxHp,
                Description: description ?? string.Empty));

            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "에너미추가",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                created.Name,
                $"{created.EnemyId} / HP {created.MaxHp} / {created.Category}");

            await FollowupAsync(embed: BuildEnemyMutationEmbed("PANDORA ADMIN | ENEMY CREATED", created, "에너미를 추가했습니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("에너미수정", "관리자용: 에너미 기본 정보를 수정합니다.")]
    public async Task UpdateEnemy(
        [Summary("에너미", "에너미 ID 또는 이름 일부")] string enemy,
        [Summary("이름", "수정할 이름")] string name,
        [Summary("출현구분", "층수, 구역, 챕터 등 운영용 분류")] string category,
        [Summary("최대HP", "최대 체력")] int maxHp,
        [Summary("근력", "근력 수치")] int strength,
        [Summary("민첩", "민첩 수치")] int dexterity,
        [Summary("체력", "체력 수치")] int constitution,
        [Summary("지능", "지능 수치")] int intelligence,
        [Summary("지혜", "지혜 수치")] int wisdom,
        [Summary("매력", "매력 수치")] int charisma,
        [Summary("피해식", "선택 사항: 기본 피해식")] string? damageFormula = null,
        [Summary("DP", "선택 사항: 방어 계수")] int dp = 0,
        [Summary("설명", "선택 사항: 설명 또는 메모")] string? description = null,
        [Summary("활성", "False면 비활성 상태로 둡니다.")] bool enabled = true)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var updated = await PandoraRepositoryProvider.Enemies.UpdateEnemyAsync(enemy, new EnemyEditInput(
                Region: category,
                Name: name,
                Category: category,
                Strength: strength,
                Dexterity: dexterity,
                Constitution: constitution,
                Intelligence: intelligence,
                Wisdom: wisdom,
                Charisma: charisma,
                DamageFormula: damageFormula ?? string.Empty,
                Dp: dp,
                MaxHp: maxHp,
                Description: description ?? string.Empty,
                IsEnabled: enabled));

            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "에너미수정",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                updated.Name,
                $"{updated.EnemyId} / HP {updated.MaxHp} / {updated.Category} / enabled={enabled}");

            await FollowupAsync(embed: BuildEnemyMutationEmbed("PANDORA ADMIN | ENEMY UPDATED", updated, "에너미 정보를 갱신했습니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("에너미능력치", "관리자용: 에너미 능력치 하나를 바로 조정합니다.")]
    public async Task UpdateEnemyStat(
        [Summary("에너미", "에너미 ID 또는 이름 일부")] string enemy,
        [Summary("능력치", "근력, 민첩, 체력, 지능, 지혜, 매력, 최대HP 중 하나")] string statName,
        [Summary("값", "새로 적용할 수치")] int value)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var updated = await PandoraRepositoryProvider.Enemies.UpdateEnemyStatAsync(enemy, statName, value);

            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "에너미능력치",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                updated.Name,
                $"{updated.EnemyId} / {statName}={value}");

            await FollowupAsync(embed: BuildEnemyMutationEmbed("PANDORA ADMIN | ENEMY STAT UPDATED", updated, $"{statName} 값을 {value}로 반영했습니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("에너미비활성화", "관리자용: 에너미를 soft delete 방식으로 비활성화합니다.")]
    public async Task DisableEnemy(
        [Summary("에너미", "에너미 ID 또는 이름 일부")] string enemy)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var updated = await PandoraRepositoryProvider.Enemies.SetEnemyActiveAsync(enemy, false);
            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "에너미비활성화",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                updated.Name,
                $"{updated.EnemyId} / active=false");

            await FollowupAsync(embed: BuildEnemyMutationEmbed("PANDORA ADMIN | ENEMY DISABLED", updated, "에너미를 비활성화했습니다. 전투 기록을 위해 데이터는 보관됩니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("에너미활성화", "관리자용: 비활성화된 에너미를 다시 활성화합니다.")]
    public async Task EnableEnemy(
        [Summary("에너미", "에너미 ID 또는 이름 일부")] string enemy)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var updated = await PandoraRepositoryProvider.Enemies.SetEnemyActiveAsync(enemy, true);
            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "에너미활성화",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                updated.Name,
                $"{updated.EnemyId} / active=true");

            await FollowupAsync(embed: BuildEnemyMutationEmbed("PANDORA ADMIN | ENEMY ENABLED", updated, "에너미를 다시 활성화했습니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("드롭추가", "관리자용: 드롭 아이템을 PostgreSQL 저장소에 추가합니다.")]
    public async Task CreateDropItem(
        [Summary("에너미ID", "드롭을 연결할 에너미 ID")] string enemyId,
        [Summary("아이템", "전리품 이름")] string itemName,
        [Summary("확률", "0~100 사이 확률")] int chance,
        [Summary("최소수량", "최소 수량")] int minCount = 1,
        [Summary("최대수량", "최대 수량")] int maxCount = 1,
        [Summary("가중치", "선택 사항: 운영용 가중치 메모")] int weight = 0,
        [Summary("희귀도", "선택 사항: common, rare 등")] string? rarity = null,
        [Summary("태그", "선택 사항: 재료, 증표 등")] string? tag = null,
        [Summary("메모", "선택 사항: 추가 메모")] string? memo = null)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var created = await PandoraRepositoryProvider.Drops.CreateDropAsync(new EnemyDropCreateInput(
                EnemyId: enemyId.Trim(),
                ItemName: itemName,
                Chance: chance,
                MinCount: minCount,
                MaxCount: maxCount,
                Weight: weight,
                Rarity: rarity ?? string.Empty,
                Tag: tag ?? string.Empty,
                Memo: memo ?? string.Empty));

            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "드롭추가",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                created.ItemName,
                $"{created.EnemyId} / {created.ItemName} / {created.Chance}% / {created.MinCount}-{created.MaxCount}");

            await FollowupAsync(embed: BuildDropMutationEmbed("PANDORA ADMIN | DROP CREATED", created, "드롭 아이템을 추가했습니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("드롭목록", "관리자용: 드롭 아이템 목록을 조회합니다.")]
    public async Task ShowDropItems(
        [Summary("에너미", "선택 사항: 에너미 ID 또는 이름 일부")] string? enemy = null)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var drops = await PandoraRepositoryProvider.Drops.GetEnemyDropsAsync();
            if (!string.IsNullOrWhiteSpace(enemy))
            {
                var enemyResult = await PandoraRepositoryProvider.Enemies.GetEnemyByIdOrNameAsync(enemy);
                if (!enemyResult.Found)
                {
                    if (enemyResult.HasMultipleMatches)
                    {
                        await FollowupAsync("조건에 맞는 에너미가 여러 개입니다. 에너미 ID로 다시 지정해 주세요.", ephemeral: true);
                        return;
                    }

                    await FollowupAsync("드롭 목록을 볼 에너미를 찾을 수 없습니다.", ephemeral: true);
                    return;
                }

                drops = drops.Where(drop => string.Equals(drop.EnemyId, enemyResult.Enemy!.EnemyId, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (drops.Count == 0)
            {
                await FollowupAsync("표시할 드롭 아이템이 없습니다.", ephemeral: true);
                return;
            }

            await FollowupAsync(embed: BuildDropListEmbed(enemy ?? "전체", drops), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("드롭수정", "관리자용: 등록된 드롭 아이템 정보를 수정합니다.")]
    public async Task UpdateDropItem(
        [Summary("에너미ID", "드롭이 연결된 에너미 ID")] string enemyId,
        [Summary("기존아이템", "수정할 기존 전리품 이름")] string currentItemName,
        [Summary("아이템", "수정할 전리품 이름")] string itemName,
        [Summary("확률", "0~100 사이 확률")] int chance,
        [Summary("최소수량", "최소 수량")] int minCount = 1,
        [Summary("최대수량", "최대 수량")] int maxCount = 1,
        [Summary("가중치", "선택 사항: 운영용 가중치 메모")] int weight = 0,
        [Summary("희귀도", "선택 사항: common, rare 등")] string? rarity = null,
        [Summary("태그", "선택 사항: 재료, 증표 등")] string? tag = null,
        [Summary("메모", "선택 사항: 추가 메모")] string? memo = null)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var updated = await PandoraRepositoryProvider.Drops.UpdateDropAsync(
                enemyId.Trim(),
                currentItemName,
                new EnemyDropCreateInput(
                    EnemyId: enemyId.Trim(),
                    ItemName: itemName,
                    Chance: chance,
                    MinCount: minCount,
                    MaxCount: maxCount,
                    Weight: weight,
                    Rarity: rarity ?? string.Empty,
                    Tag: tag ?? string.Empty,
                    Memo: memo ?? string.Empty));

            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "드롭수정",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                updated.ItemName,
                $"{updated.EnemyId} / {currentItemName} -> {updated.ItemName} / {updated.Chance}% / {updated.MinCount}-{updated.MaxCount}");

            await FollowupAsync(embed: BuildDropMutationEmbed("PANDORA ADMIN | DROP UPDATED", updated, "드롭 아이템 정보를 갱신했습니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("드롭삭제", "관리자용: 드롭 아이템을 soft delete 방식으로 비활성화합니다.")]
    public async Task DeleteDropItem(
        [Summary("에너미ID", "드롭이 연결된 에너미 ID")] string enemyId,
        [Summary("아이템", "비활성화할 전리품 이름")] string itemName)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var deleted = await PandoraRepositoryProvider.Drops.DeleteDropAsync(enemyId.Trim(), itemName);

            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "드롭삭제",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                deleted.ItemName,
                $"{deleted.EnemyId} / soft-delete");

            await FollowupAsync(embed: BuildDropMutationEmbed("PANDORA ADMIN | DROP DISABLED", deleted, "드롭 아이템을 비활성화했습니다. 기록 보존을 위해 데이터는 남겨둡니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("드롭설정", "관리자용: 에너미별 드롭 발생 설정을 저장합니다.")]
    public async Task ConfigureDropSetting(
        [Summary("에너미ID", "설정을 적용할 에너미 ID")] string enemyId,
        [Summary("발생률", "0~100 사이 전리품 발생률")] int dropRate,
        [Summary("슬롯수", "한 번에 굴릴 드롭 슬롯 수")] int dropCount = 1,
        [Summary("중복허용", "True면 같은 아이템이 여러 번 나올 수 있습니다.")] bool allowDuplicate = false,
        [Summary("메모", "선택 사항: 운영 메모")] string? memo = null)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var setting = await PandoraRepositoryProvider.Drops.UpsertDropSettingAsync(
                new EnemyDropSettingInput(enemyId.Trim(), dropRate, dropCount, memo ?? string.Empty),
                allowDuplicate);

            await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                "드롭설정",
                Context.User.Id.ToString(),
                Context.User.Username,
                string.Empty,
                setting.EnemyId,
                $"rate={setting.DropRate}% / count={setting.DropCount} / allowDuplicate={setting.AllowDuplicate}");

            await FollowupAsync(embed: BuildDropSettingEmbed("PANDORA ADMIN | DROP SETTING SAVED", setting, "드롭 설정을 저장했습니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    [SlashCommand("드롭설정보기", "관리자용: 에너미별 드롭 설정을 조회합니다.")]
    public async Task ShowDropSetting(
        [Summary("에너미", "선택 사항: 에너미 ID 또는 이름 일부")] string? enemy = null)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var settings = await PandoraRepositoryProvider.Drops.GetEnemyDropSettingsAsync();
            if (string.IsNullOrWhiteSpace(enemy))
            {
                if (settings.Count == 0)
                {
                    await FollowupAsync("아직 저장된 드롭 설정이 없습니다. `/드롭설정`으로 먼저 기본값을 만들어 주세요.", ephemeral: true);
                    return;
                }

                await FollowupAsync(embed: BuildDropSettingListEmbed(settings), ephemeral: true);
                return;
            }

            var enemyResult = await PandoraRepositoryProvider.Enemies.GetEnemyByIdOrNameAsync(enemy);
            if (!enemyResult.Found)
            {
                if (enemyResult.HasMultipleMatches)
                {
                    await FollowupAsync("조건에 맞는 에너미가 여러 개입니다. 에너미 ID로 다시 지정해 주세요.", ephemeral: true);
                    return;
                }

                await FollowupAsync("드롭 설정을 볼 에너미를 찾을 수 없습니다.", ephemeral: true);
                return;
            }

            var setting = settings.FirstOrDefault(x => string.Equals(x.EnemyId, enemyResult.Enemy!.EnemyId, StringComparison.OrdinalIgnoreCase));
            if (setting is null)
            {
                await FollowupAsync($"`{enemyResult.Enemy!.EnemyId}`에는 아직 저장된 드롭 설정이 없습니다. 필요하면 `/드롭설정`으로 바로 추가해 주세요.", ephemeral: true);
                return;
            }

            await FollowupAsync(embed: BuildDropSettingEmbed("PANDORA 드롭 설정", setting, "현재 저장된 설정을 보여 드립니다."), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
        }
    }

    private static string ToFriendlyAdminError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "처리 중 문제가 발생했습니다. 잠시 후 다시 시도해 주세요.";
        }

        if (message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("429", StringComparison.OrdinalIgnoreCase))
        {
            return "요청이 잠시 몰려 처리 속도가 느립니다. 몇 초 뒤 다시 시도해 주세요.";
        }

        if (message.Contains("PandoraDb", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("ConnectionStrings:PandoraDb", StringComparison.OrdinalIgnoreCase))
        {
            return "PostgreSQL 연결이 아직 준비되지 않았습니다. appsettings.Development.json의 ConnectionStrings:PandoraDb 값을 먼저 확인해 주세요.";
        }

        if (message.Contains("여러", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("multiple", StringComparison.OrdinalIgnoreCase))
        {
            return $"{message}\n더 정확한 ID나 이름으로 다시 지정해 주세요.";
        }

        if (message.Contains("찾을 수", StringComparison.OrdinalIgnoreCase))
        {
            return $"{message}\n필요하면 목록 명령으로 현재 등록 상태를 먼저 확인해 주세요.";
        }

        return $"처리 중 문제가 발생했습니다: {message}";
    }

    private static Embed BuildUnifiedLogEmbed(string target, IReadOnlyList<UnifiedLogEntry> logs)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"PANDORA 로그 확인 | {target}")
            .WithColor(new Color(120, 180, 255))
            .WithDescription($"최근 기록 {logs.Count}개를 보여 드립니다. 필요한 흐름을 빠르게 다시 확인할 때 쓰면 좋습니다.")
            .WithFooter("판정, 관리, 공지, 전투 로그를 한곳에서 확인합니다.")
            .WithCurrentTimestamp();

        foreach (var log in logs.Take(15))
        {
            var targetText = string.IsNullOrWhiteSpace(log.Target) ? "-" : log.Target;
            var summaryText = string.IsNullOrWhiteSpace(log.Summary) ? "-" : log.Summary;
            embed.AddField(
                $"[{log.Category}] {log.Action}",
                $"대상 `{targetText}`\n내용 {summaryText}\n기록 시각 <t:{log.CreatedAt.ToUnixTimeSeconds()}:f>",
                inline: false);
        }

        return embed.Build();
    }

    private static Embed BuildEnemyMutationEmbed(string title, EnemyRow enemy, string message)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithColor(new Color(60, 190, 255))
            .WithDescription(message)
            .AddField("에너미ID", enemy.EnemyId, inline: true)
            .AddField("이름", enemy.Name, inline: true)
            .AddField("출현구분", string.IsNullOrWhiteSpace(enemy.Category) ? "-" : enemy.Category, inline: true)
            .AddField("활성", string.Equals(enemy.IsEnabled, "TRUE", StringComparison.OrdinalIgnoreCase) ? "활성" : "비활성", inline: true)
            .AddField("HP", $"{enemy.CurrentHp} / {enemy.MaxHp}", inline: true)
            .AddField("능력치", $"STR {enemy.Strength} / DEX {enemy.Dexterity} / CON {enemy.Constitution}\nINT {enemy.Intelligence} / WIS {enemy.Wisdom} / CHA {enemy.Charisma}", inline: false)
            .AddField("설명", FormatEmpty(enemy.Description), inline: false)
            .WithFooter("PANDORA NETWORK / ENEMY STORAGE")
            .WithCurrentTimestamp()
            .Build();
    }

    private static Embed BuildDropMutationEmbed(string title, EnemyDropRow drop, string message)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithColor(new Color(130, 220, 160))
            .WithDescription(message)
            .AddField("에너미ID", drop.EnemyId, inline: true)
            .AddField("아이템", drop.ItemName, inline: true)
            .AddField("확률", $"{drop.Chance}%", inline: true)
            .AddField("수량", $"{drop.MinCount} ~ {drop.MaxCount}", inline: true)
            .AddField("희귀도", FormatEmpty(drop.Rarity), inline: true)
            .AddField("태그", FormatEmpty(drop.Tag), inline: true)
            .AddField("메모", FormatEmpty(drop.Memo), inline: false)
            .WithFooter("PANDORA NETWORK / DROP STORAGE")
            .WithCurrentTimestamp()
            .Build();
    }

    private static Embed BuildDropListEmbed(string target, IReadOnlyList<EnemyDropRow> drops)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"PANDORA 드롭 목록 | {target}")
            .WithColor(new Color(130, 220, 160))
            .WithDescription($"지금 확인할 수 있는 드롭 아이템은 총 {drops.Count}개입니다.")
            .WithFooter("비활성화된 드롭은 기본 목록에서 숨깁니다.")
            .WithCurrentTimestamp();

        foreach (var drop in drops.Take(20))
        {
            embed.AddField(
                $"{drop.EnemyId} | {drop.ItemName}",
                $"확률 `{drop.Chance}%`\n수량 `{drop.MinCount}~{drop.MaxCount}`\n희귀도 `{FormatEmpty(drop.Rarity)}` / 태그 `{FormatEmpty(drop.Tag)}`",
                inline: false);
        }

        if (drops.Count > 20)
        {
            embed.AddField("표시 제한", $"가독성을 위해 20개까지만 표시했습니다. 나머지 {drops.Count - 20}개가 더 있습니다.", inline: false);
        }

        return embed.Build();
    }

    private static Embed BuildDropSettingEmbed(string title, EnemyDropSettingRow setting, string message)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithColor(new Color(255, 205, 120))
            .WithDescription(message)
            .AddField("에너미ID", setting.EnemyId, inline: true)
            .AddField("발생률", $"{setting.DropRate}%", inline: true)
            .AddField("슬롯수", setting.DropCount.ToString(), inline: true)
            .AddField("중복허용", setting.AllowDuplicate ? "허용" : "비허용", inline: true)
            .AddField("메모", FormatEmpty(setting.Memo), inline: false)
            .WithFooter("PANDORA NETWORK / DROP SETTING")
            .WithCurrentTimestamp()
            .Build();
    }

    private static Embed BuildDropSettingListEmbed(IReadOnlyList<EnemyDropSettingRow> settings)
    {
        var embed = new EmbedBuilder()
            .WithTitle("PANDORA 드롭 설정 목록")
            .WithColor(new Color(255, 205, 120))
            .WithDescription($"현재 저장된 드롭 설정은 총 {settings.Count}개입니다.")
            .WithCurrentTimestamp();

        foreach (var setting in settings.Take(15))
        {
            embed.AddField(
                setting.EnemyId,
                $"발생률 `{setting.DropRate}%` / 슬롯 `{setting.DropCount}` / 중복 `{(setting.AllowDuplicate ? "허용" : "비허용")}`\n메모: {FormatEmpty(setting.Memo)}",
                inline: false);
        }

        if (settings.Count > 15)
        {
            embed.AddField("표시 제한", $"가독성을 위해 15개까지만 표시했습니다. 나머지 {settings.Count - 15}개가 더 있습니다.", inline: false);
        }

        return embed.Build();
    }

    private static string FormatEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
