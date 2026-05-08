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
                await FollowupAsync($"{target} 대상의 로그가 아직 없습니다.", ephemeral: true);
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

            await FollowupAsync(embed: BuildDropCreatedEmbed(created), ephemeral: true);
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

        if (message.Contains("찾을 수", StringComparison.OrdinalIgnoreCase))
        {
            return $"{message}\n필요하면 목록 명령으로 현재 등록 상태를 먼저 확인해 주세요.";
        }

        return $"처리 중 문제가 발생했습니다: {message}";
    }

    private static Embed BuildUnifiedLogEmbed(string target, IReadOnlyList<UnifiedLogEntry> logs)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"PANDORA ADMIN | LOGS: {target}")
            .WithColor(new Color(120, 180, 255))
            .WithDescription($"최근 {logs.Count}개의 로그를 표시합니다.")
            .WithFooter("PANDORA NETWORK / UNIFIED LOG VIEW")
            .WithCurrentTimestamp();

        foreach (var log in logs.Take(15))
        {
            var targetText = string.IsNullOrWhiteSpace(log.Target) ? "-" : log.Target;
            var summaryText = string.IsNullOrWhiteSpace(log.Summary) ? "-" : log.Summary;
            embed.AddField(
                $"[{log.Category}] {log.Action}",
                $"대상: {targetText}\n내용: {summaryText}\n시각: <t:{log.CreatedAt.ToUnixTimeSeconds()}:f>",
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
            .AddField("HP", $"{enemy.CurrentHp} / {enemy.MaxHp}", inline: true)
            .AddField("능력치", $"STR {enemy.Strength} / DEX {enemy.Dexterity} / CON {enemy.Constitution}\nINT {enemy.Intelligence} / WIS {enemy.Wisdom} / CHA {enemy.Charisma}", inline: false)
            .AddField("설명", FormatEmpty(enemy.Description), inline: false)
            .WithFooter("PANDORA NETWORK / ENEMY STORAGE")
            .WithCurrentTimestamp()
            .Build();
    }

    private static Embed BuildDropCreatedEmbed(EnemyDropRow drop)
    {
        return new EmbedBuilder()
            .WithTitle("PANDORA ADMIN | DROP CREATED")
            .WithColor(new Color(130, 220, 160))
            .WithDescription("드롭 아이템을 추가했습니다.")
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

    private static string FormatEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
