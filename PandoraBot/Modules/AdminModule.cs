using Discord;
using Discord.Interactions;
using PandoraBot.Models;
using PandoraBot.Services;
using PandoraShared.Models;
using System.Text;

namespace PandoraBot.Modules
{

    [DefaultMemberPermissions(GuildPermission.ManageGuild)]
    public class AdminModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("관리목록", "관리자용: 등록된 캐릭터 목록을 조회합니다.")]
        public async Task ListAllCharacters(
            [Summary("개수", "1~50 사이 조회 개수")] int limit = 25)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await GoogleSheetService.Instance.ListAllCharactersAsync(limit);
                if (characters.Count == 0)
                {
                    await FollowupAsync("등록된 캐릭터가 없습니다.", ephemeral: true);
                    return;
                }

                await FollowupAsync(embed: BuildCharacterListEmbed(characters, "PANDORA ADMIN | 캐릭터 목록"), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리정보", "관리자용: 특정 캐릭터 정보를 조회합니다.")]
        public async Task ShowCharacterForAdmin(
            [Summary("캐릭터", "캐릭터 이름")] string characterName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var hunter = await GoogleSheetService.Instance.GetCharacterForAdminAsync(characterName);
                await FollowupAsync(embed: BuildHunterEmbed(hunter), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리체력", "관리자용: 특정 캐릭터의 현재 HP를 설정합니다.")]
        public async Task SetCharacterHp(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("현재HP", "변경할 현재 HP")] int currentHp)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.SetCharacterHpAsync(characterName, currentHp);
                await GoogleSheetService.Instance.AppendAdminLogAsync(
                    "HP설정",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    result.UserId,
                    result.CharacterName,
                    $"{result.OldHp} -> {result.CurrentHp}");

                await FollowupAsync(
                    $"`{result.CharacterName}` HP를 `{result.OldHp} -> {result.CurrentHp} / {result.MaxHp}`로 변경했습니다. (row {result.RowNumber})",
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("피해", "관리자용: 특정 캐릭터에게 피해를 적용합니다.")]
        public async Task DamageCharacter(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("수치", "적용할 피해량")] int amount,
            [Summary("메모", "선택 사항: 피해 사유")] string? memo = null)
        {
            await AdjustHpAsync(characterName, amount, "damage", memo);
        }

        [SlashCommand("회복", "관리자용: 특정 캐릭터를 회복합니다.")]
        public async Task HealCharacter(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("수치", "적용할 회복량")] int amount,
            [Summary("메모", "선택 사항: 회복 사유")] string? memo = null)
        {
            await AdjustHpAsync(characterName, amount, "heal", memo);
        }

        [SlashCommand("관리선택해제", "관리자용: 특정 캐릭터 소유자의 선택 상태를 해제합니다.")]
        public async Task ClearUserSelection(
            [Summary("캐릭터", "캐릭터 이름")] string characterName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.ClearSelectedCharacterForAdminAsync(characterName);
                await GoogleSheetService.Instance.AppendAdminLogAsync(
                    "선택해제",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    "",
                    characterName,
                    $"{result.ClearedCount} row(s) cleared");

                await FollowupAsync($"`{characterName}` 소유자의 선택 상태 {result.ClearedCount}개를 해제했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리삭제", "관리자용: 특정 캐릭터를 삭제합니다.")]
        public async Task DeleteCharacterForAdmin(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("확인", "True로 설정하면 캐릭터를 삭제합니다.")] bool confirm = false)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (!confirm)
                {
                    await FollowupAsync(
                        $"`{characterName}` 삭제를 진행하려면 `/관리삭제 캐릭터:{characterName} 확인:True`로 다시 실행해주세요.",
                        ephemeral: true);
                    return;
                }

                var result = await GoogleSheetService.Instance.DeleteCharacterForAdminAsync(characterName);
                await GoogleSheetService.Instance.AppendAdminLogAsync(
                    "삭제",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    "",
                    result.CharacterName,
                    $"row {result.RowNumber}");

                await FollowupAsync($"`{result.CharacterName}` 캐릭터 등록 정보를 삭제했습니다. (row {result.RowNumber})", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리승인", "관리자용: 캐릭터를 승인합니다.")]
        public async Task ApproveCharacter(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("메모", "선택 사항: 검수 메모")] string? memo = null)
        {
            await SetReviewStatusAsync(characterName, "approved", memo);
        }

        [SlashCommand("관리반려", "관리자용: 캐릭터를 반려합니다.")]
        public async Task RejectCharacter(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("메모", "선택 사항: 반려 사유")] string? memo = null)
        {
            await SetReviewStatusAsync(characterName, "rejected", memo);
        }

        [SlashCommand("관리검수목록", "관리자용: 검수 상태별 캐릭터 목록을 조회합니다.")]
        public async Task ListReviewCharacters(
            [Summary("상태", "pending, approved, rejected")] string status = "pending",
            [Summary("개수", "1~50 사이 조회 개수")] int limit = 25)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await GoogleSheetService.Instance.ListReviewCharactersAsync(status, limit);
                if (characters.Count == 0)
                {
                    await FollowupAsync($"`{status}` 상태의 캐릭터가 없습니다.", ephemeral: true);
                    return;
                }

                await FollowupAsync(embed: BuildCharacterListEmbed(characters, $"PANDORA ADMIN | 검수 목록: {status}"), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리판정로그", "관리자용: 최근 판정 로그를 조회합니다.")]
        public async Task ListJudgementLogs(
            [Summary("개수", "1~30 사이 조회 개수")] int limit = 10)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var logs = await GoogleSheetService.Instance.ListRecentJudgementLogsAsync(limit);
                if (logs.Count == 0)
                {
                    await FollowupAsync("판정 로그가 없습니다.", ephemeral: true);
                    return;
                }

                var builder = new StringBuilder();
                builder.AppendLine("```text");
                builder.AppendLine("PANDORA ADMIN / RECENT CHECKS");
                builder.AppendLine("--------------------------------");

                foreach (var log in logs)
                {
                    builder.AppendLine($"{log.CreatedAt} | {log.Username} / {log.CharacterName} | {log.StatCode} {log.Total} | {log.Outcome}");
                }

                builder.Append("```");
                await FollowupAsync(builder.ToString(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("공지", "관리자용: 공지 템플릿을 발송하고 기록합니다.")]
        public async Task SendNotice(
            [Summary("종류", "세션, 모집, 점검, 안내 등")] string noticeType,
            [Summary("제목", "공지 제목")] string title,
            [Summary("내용", "공지 내용")] string content,
            [Summary("역할멘션", "선택 사항: 함께 호출할 역할")] IRole? role = null,
            [Summary("유저멘션", "선택 사항: 함께 호출할 유저")] IUser? user = null,
            [Summary("전체멘션", "True면 @everyone을 함께 호출합니다.")] bool everyone = false)
        {
            await DeferAsync();

            try
            {
                var mentions = new List<string>();
                if (everyone)
                {
                    mentions.Add("@everyone");
                }

                if (role != null)
                {
                    mentions.Add(role.Mention);
                }

                if (user != null)
                {
                    mentions.Add(user.Mention);
                }

                var embed = new EmbedBuilder()
                    .WithTitle($"PROJECT:PANDORA | {title}")
                    .WithColor(new Color(60, 190, 255))
                    .WithDescription(content)
                    .AddField("분류", noticeType, inline: true)
                    .AddField("작성", Context.User.Mention, inline: true)
                    .AddField("호출", mentions.Count == 0 ? "없음" : string.Join(" ", mentions), inline: false)
                    .WithFooter("PANDORA NETWORK / OPERATOR NOTICE")
                    .WithCurrentTimestamp()
                    .Build();

                await GoogleSheetService.Instance.AppendNoticeLogAsync(
                    noticeType,
                    title,
                    $"{content}\nMENTION: {(mentions.Count == 0 ? "none" : string.Join(" ", mentions))}",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    Context.Channel.Id.ToString());

                var mentionText = mentions.Count == 0 ? null : string.Join(" ", mentions);
                await FollowupAsync(mentionText, embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("\uC5D0\uB108\uBBF8\uBAA9\uB85D", "\uAD00\uB9AC\uC790\uC6A9: \uB4F1\uB85D\uB41C \uC5D0\uB108\uBBF8 \uBAA9\uB85D\uC744 \uC870\uD68C\uD569\uB2C8\uB2E4.")]
        public async Task ListEnemies(
            [Summary("\uCD9C\uD604\uAD6C\uBD84", "\uC120\uD0DD \uC0AC\uD56D: \uCD9C\uD604\uAD6C\uBD84\uC73C\uB85C \uD544\uD130\uB9C1\uD569\uB2C8\uB2E4.")] string? category = null,
            [Summary("\uAC1C\uC218", "1~25 \uC0AC\uC774 \uD45C\uC2DC \uAC1C\uC218")] int limit = 20)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var enemies = await GoogleSheetService.Instance.Enemies.GetEnemiesAsync();
                var filter = category?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    enemies = enemies
                        .Where(enemy => enemy.Category.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (enemies.Count == 0)
                {
                    var message = string.IsNullOrWhiteSpace(filter)
                        ? "\uB4F1\uB85D\uB41C \uC5D0\uB108\uBBF8\uAC00 \uC544\uC9C1 \uC5C6\uC2B5\uB2C8\uB2E4. \uAD00\uB9AC\uC790 \uC6F9\uC5D0\uC11C \uC5D0\uB108\uBBF8\uB97C \uBA3C\uC800 \uCD94\uAC00\uD574\uC8FC\uC138\uC694."
                        : $"`{filter}` \uCD9C\uD604\uAD6C\uBD84\uC5D0 \uD574\uB2F9\uD558\uB294 \uC5D0\uB108\uBBF8\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
                    await FollowupAsync(message, ephemeral: true);
                    return;
                }

                await FollowupAsync(
                    embed: BuildEnemyListEmbed(enemies, filter, Math.Clamp(limit, 1, 25)),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("\uC5D0\uB108\uBBF8\uC870\uD68C", "\uAD00\uB9AC\uC790\uC6A9: \uD2B9\uC815 \uC5D0\uB108\uBBF8\uC758 \uC0C1\uC138 \uC815\uBCF4\uB97C \uC870\uD68C\uD569\uB2C8\uB2E4.")]
        public async Task ShowEnemy(
            [Summary("\uC5D0\uB108\uBBF8", "\uC5D0\uB108\uBBF8 ID \uB610\uB294 \uC774\uB984 \uC77C\uBD80")] string enemy)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.Enemies.GetEnemyByIdOrNameAsync(enemy);
                if (!result.Found)
                {
                    if (result.HasMultipleMatches)
                    {
                        await FollowupAsync(embed: BuildEnemyCandidateEmbed(result.Matches, enemy), ephemeral: true);
                        return;
                    }

                    await FollowupAsync($"\uC5D0\uB108\uBBF8 `{enemy}`\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. `/\uC5D0\uB108\uBBF8\uBAA9\uB85D`\uC73C\uB85C \uB4F1\uB85D \uBAA9\uB85D\uC744 \uBA3C\uC800 \uD655\uC778\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                var found = result.Enemy!;
                var drops = await GoogleSheetService.Instance.Drops.GetEnemyDropsAsync();
                var settings = await GoogleSheetService.Instance.Drops.GetEnemyDropSettingsAsync();
                var hasDrops = drops.Any(drop => string.Equals(drop.EnemyId, found.EnemyId, StringComparison.OrdinalIgnoreCase));
                var hasSetting = settings.Any(setting => string.Equals(setting.EnemyId, found.EnemyId, StringComparison.OrdinalIgnoreCase));

                await FollowupAsync(embed: BuildEnemyDetailEmbed(found, hasDrops, hasSetting), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("\uC5D0\uB108\uBBF8\uD310\uC815", "\uAD00\uB9AC\uC790\uC6A9: \uC5D0\uB108\uBBF8 \uB2A5\uB825\uCE58\uB85C 2d6 \uD310\uC815\uC744 \uAD74\uB9BD\uB2C8\uB2E4.")]
        public async Task RollEnemy(
            [Summary("\uC5D0\uB108\uBBF8", "\uC5D0\uB108\uBBF8 ID \uB610\uB294 \uC774\uB984 \uC77C\uBD80")] string enemy,
            [Summary("\uB2A5\uB825\uCE58", "\uADFC\uB825, \uBBFC\uCCA9, \uCCB4\uB825, \uC9C0\uB2A5, \uC9C0\uD61C, \uB9E4\uB825 \uC911 \uD558\uB098")] string ability)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.Enemies.GetEnemyByIdOrNameAsync(enemy);
                if (!result.Found)
                {
                    if (result.HasMultipleMatches)
                    {
                        await FollowupAsync(embed: BuildEnemyCandidateEmbed(result.Matches, enemy), ephemeral: true);
                        return;
                    }

                    await FollowupAsync($"\uC5D0\uB108\uBBF8 `{enemy}`\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. `/\uC5D0\uB108\uBBF8\uBAA9\uB85D`\uC73C\uB85C \uB4F1\uB85D \uBAA9\uB85D\uC744 \uBA3C\uC800 \uD655\uC778\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                var found = result.Enemy!;
                var stat = ResolveEnemyStat(found, ability);
                if (stat is null)
                {
                    await FollowupAsync("\uB2A5\uB825\uCE58\uB294 `\uADFC\uB825`, `\uBBFC\uCCA9`, `\uCCB4\uB825`, `\uC9C0\uB2A5`, `\uC9C0\uD61C`, `\uB9E4\uB825` \uB610\uB294 `str`, `dex`, `con`, `int`, `wis`, `cha` \uC911 \uD558\uB098\uB85C \uC785\uB825\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                var die1 = Random.Shared.Next(1, 7);
                var die2 = Random.Shared.Next(1, 7);
                var diceTotal = die1 + die2;
                var total = diceTotal + stat.Modifier;
                var outcome = ResolveEnemyOutcome(total);

                await GoogleSheetService.Instance.AppendAdminLogAsync(
                    "\uC5D0\uB108\uBBF8 \uD310\uC815",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    "",
                    found.Name,
                    $"{found.EnemyId} / {stat.Code} {stat.Value} ({FormatModifier(stat.Modifier)}) / {die1}+{die2} => {total} / {outcome.Label}");

                await FollowupAsync(
                    embed: BuildEnemyRollEmbed(found, stat, die1, die2, diceTotal, total, outcome),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("\uB4DC\uB86D", "\uAD00\uB9AC\uC790\uC6A9: \uC5D0\uB108\uBBF8 \uB4DC\uB86D \uD14C\uC774\uBE14\uB85C \uC804\uB9AC\uD488\uC744 \uAD74\uB9BD\uB2C8\uB2E4.")]
        public async Task RollEnemyDrops(
            [Summary("\uC5D0\uB108\uBBF8", "\uC5D0\uB108\uBBF8 ID \uB610\uB294 \uC774\uB984 \uC77C\uBD80")] string enemy,
            [Summary("\uC218\uB7C9", "\uCC98\uCE58\uD55C \uC5D0\uB108\uBBF8 \uC218\uB7C9")] int quantity = 1)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (quantity <= 0)
                {
                    await FollowupAsync("\uC218\uB7C9\uC740 1 \uC774\uC0C1\uC73C\uB85C \uC785\uB825\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                var result = await GoogleSheetService.Instance.Enemies.GetEnemyByIdOrNameAsync(enemy);
                if (!result.Found)
                {
                    if (result.HasMultipleMatches)
                    {
                        await FollowupAsync(embed: BuildEnemyCandidateEmbed(result.Matches, enemy), ephemeral: true);
                        return;
                    }

                    await FollowupAsync($"\uC5D0\uB108\uBBF8 `{enemy}`\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. `/\uC5D0\uB108\uBBF8\uBAA9\uB85D`\uC73C\uB85C \uB4F1\uB85D \uBAA9\uB85D\uC744 \uBA3C\uC800 \uD655\uC778\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                var found = result.Enemy!;
                var rollCount = Math.Clamp(quantity, 1, 50);
                var rolls = new List<DropRollResult>();
                for (var i = 0; i < rollCount; i++)
                {
                    rolls.Add(await GoogleSheetService.Instance.Drops.RollDropAsync(found.EnemyId, writeLog: false));
                }

                var items = rolls
                    .SelectMany(roll => roll.Items)
                    .GroupBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new DropSummaryItem(group.First().ItemName, group.Sum(item => item.Count)))
                    .OrderBy(item => item.ItemName)
                    .ToList();

                var summary = items.Count == 0
                    ? "\uC804\uB9AC\uD488 \uC5C6\uC74C"
                    : string.Join(", ", items.Select(item => $"{item.ItemName} x{item.Count}"));

                await GoogleSheetService.Instance.AppendAdminLogAsync(
                    "\uB4DC\uB86D \uAD74\uB9BC",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    "",
                    found.Name,
                    $"{found.EnemyId} x{rollCount}: {summary}");

                await FollowupAsync(
                    embed: BuildDropResultEmbed(found, rollCount, quantity, rolls, items),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }
        private async Task AdjustHpAsync(string characterName, int amount, string action, string? memo)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (amount <= 0)
                {
                    await FollowupAsync("수치는 1 이상이어야 합니다.", ephemeral: true);
                    return;
                }

                var result = await GoogleSheetService.Instance.AdjustCharacterHpAsync(
                    characterName,
                    amount,
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    action,
                    memo);

                var label = action == "heal" ? "회복" : "피해";
                await FollowupAsync(
                    $"`{result.CharacterName}` {label} 처리: HP `{result.OldHp} -> {result.CurrentHp} / {result.MaxHp}` (row {result.RowNumber})",
                    ephemeral: true);

                var publicEmbed = new EmbedBuilder()
                    .WithTitle($"PROJECT:PANDORA | {label} 알림")
                    .WithColor(action == "heal" ? Color.Green : Color.Red)
                    .WithDescription($"<@{result.UserId}> 님의 **{result.CharacterName}** 상태가 변경되었습니다.")
                    .AddField("HP", $"`{result.OldHp} -> {result.CurrentHp} / {result.MaxHp}`", inline: true)
                    .AddField("처리", label, inline: true)
                    .WithFooter("PANDORA NETWORK / STATUS UPDATE")
                    .WithCurrentTimestamp()
                    .Build();

                try
                {
                    await FollowupAsync(embed: publicEmbed);
                }
                catch (Exception notifyEx)
                {
                    await FollowupAsync(
                        $"처리는 완료됐지만 공개 알림 전송에 실패했습니다: {notifyEx.Message}",
                        ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        private async Task SetReviewStatusAsync(string characterName, string status, string? memo)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.SetCharacterReviewStatusAsync(
                    characterName,
                    status,
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    memo);

                await FollowupAsync(
                    $"`{result.CharacterName}` 검수 상태를 `{result.ReviewStatus}`로 변경했습니다. (row {result.RowNumber})",
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        private static Embed BuildHunterEmbed(Hunter hunter)
        {
            return new EmbedBuilder()
                .WithTitle("PANDORA ADMIN | CHARACTER DATA")
                .WithColor(new Color(70, 160, 255))
                .WithDescription($"**{hunter.CharacterName}**")
                .AddField("Discord User ID", hunter.UserId, inline: false)
                .AddField("HP", $"{hunter.CurrentHp} / {hunter.MaxHp}", inline: true)
                .AddField("Physical", $"STR {hunter.Strength} ({FormatModifier(hunter.GetModifier(hunter.Strength))})\nDEX {hunter.Dexterity} ({FormatModifier(hunter.GetModifier(hunter.Dexterity))})\nCON {hunter.Constitution} ({FormatModifier(hunter.GetModifier(hunter.Constitution))})", inline: true)
                .AddField("Mental", $"INT {hunter.Intelligence} ({FormatModifier(hunter.GetModifier(hunter.Intelligence))})\nWIS {hunter.Wisdom} ({FormatModifier(hunter.GetModifier(hunter.Wisdom))})\nCHA {hunter.Charisma} ({FormatModifier(hunter.GetModifier(hunter.Charisma))})", inline: true)
                .WithCurrentTimestamp()
                .Build();
        }

        private static Embed BuildCharacterListEmbed(IReadOnlyList<GoogleSheetService.AdminCharacterSummary> characters, string title)
        {
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(new Color(70, 160, 255))
                .WithDescription($"총 {characters.Count}개의 캐릭터를 표시합니다.")
                .WithFooter("선택 상태는 별도 선택 상태 시트 기준입니다.")
                .WithCurrentTimestamp();

            foreach (var character in characters.Take(20))
            {
                var selectedText = character.IsSelected
                    ? $"선택 중: <@{character.SelectedByUserId}>"
                    : "선택 안 됨";
                var ownerText = string.IsNullOrWhiteSpace(character.UserId)
                    ? "소유자 미확인"
                    : $"소유자: <@{character.UserId}>";

                embed.AddField(
                    $"{character.CharacterName} | HP {character.CurrentHp}/{character.MaxHp}",
                    $"{ownerText}\n검수: `{character.ReviewStatus}`\n{selectedText}\nrow: `{character.RowNumber}`",
                    inline: false);
            }

            if (characters.Count > 20)
            {
                embed.AddField("표시 제한", "Discord Embed 가독성을 위해 최대 20개까지만 표시했습니다. `/관리목록 개수:20`처럼 나누어 확인해주세요.", inline: false);
            }

            return embed.Build();
        }

        private static Embed BuildEnemyListEmbed(IReadOnlyList<EnemyRow> enemies, string categoryFilter, int limit)
        {
            var shown = enemies.Take(limit).ToList();
            var title = string.IsNullOrWhiteSpace(categoryFilter)
                ? "PANDORA ADMIN | ENEMY LIST"
                : $"PANDORA ADMIN | ENEMY LIST: {categoryFilter}";

            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(new Color(255, 110, 90))
                .WithDescription($"\uCD1D {enemies.Count}\uAC1C \uC911 {shown.Count}\uAC1C\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.")
                .WithFooter("\uC5D0\uB108\uBBF8 ID\uB294 \uD5A5\uD6C4 \uC870\uD68C/\uD310\uC815/\uB4DC\uB86D \uBA85\uB839\uC5D0\uC11C \uAE30\uC900\uAC12\uC73C\uB85C \uC0AC\uC6A9\uB429\uB2C8\uB2E4.")
                .WithCurrentTimestamp();

            foreach (var enemy in shown)
            {
                var category = string.IsNullOrWhiteSpace(enemy.Category) ? "\uBBF8\uC9C0\uC815" : enemy.Category;
                var hp = enemy.MaxHp > 0 ? $"{enemy.CurrentHp} / {enemy.MaxHp}" : "\uBBF8\uC9C0\uC815";

                embed.AddField(
                    $"{enemy.EnemyId} | {enemy.Name}",
                    $"HP `{hp}`\n\uCD9C\uD604\uAD6C\uBD84 `{category}`",
                    inline: false);
            }

            if (enemies.Count > shown.Count)
            {
                embed.AddField(
                    "\uD45C\uC2DC \uC81C\uD55C",
                    $"\uBAA9\uB85D\uC774 \uAE38\uC5B4 {shown.Count}\uAC1C\uB9CC \uD45C\uC2DC\uD588\uC2B5\uB2C8\uB2E4. \uB098\uBA38\uC9C0 {enemies.Count - shown.Count}\uAC1C\uAC00 \uB354 \uC788\uC2B5\uB2C8\uB2E4.",
                    inline: false);
            }

            return embed.Build();
        }

        private static Embed BuildEnemyCandidateEmbed(IReadOnlyList<EnemyRow> enemies, string query)
        {
            var shown = enemies.Take(10).ToList();
            var embed = new EmbedBuilder()
                .WithTitle("PANDORA ADMIN | ENEMY CANDIDATES")
                .WithColor(new Color(255, 185, 80))
                .WithDescription($"`{query}`\uC5D0 \uD574\uB2F9\uD558\uB294 \uC5D0\uB108\uBBF8\uAC00 \uC5EC\uB7EC \uBA85\uC785\uB2C8\uB2E4. \uC5D0\uB108\uBBF8ID\uB098 \uB354 \uC815\uD655\uD55C \uC774\uB984\uC73C\uB85C \uB2E4\uC2DC \uC870\uD68C\uD574\uC8FC\uC138\uC694.")
                .WithCurrentTimestamp();

            foreach (var enemy in shown)
            {
                var category = string.IsNullOrWhiteSpace(enemy.Category) ? "\uBBF8\uC9C0\uC815" : enemy.Category;
                embed.AddField(
                    $"{enemy.EnemyId} | {enemy.Name}",
                    $"\uCD9C\uD604\uAD6C\uBD84 `{category}` / HP `{enemy.CurrentHp} / {enemy.MaxHp}`",
                    inline: false);
            }

            if (enemies.Count > shown.Count)
            {
                embed.AddField(
                    "\uD45C\uC2DC \uC81C\uD55C",
                    $"\uD6C4\uBCF4\uAC00 {enemies.Count}\uAC1C\uB77C \uC0C1\uC704 {shown.Count}\uAC1C\uB9CC \uD45C\uC2DC\uD588\uC2B5\uB2C8\uB2E4.",
                    inline: false);
            }

            return embed.Build();
        }

        private static Embed BuildEnemyDetailEmbed(EnemyRow enemy, bool hasDrops, bool hasSetting)
        {
            var category = string.IsNullOrWhiteSpace(enemy.Category) ? "\uBBF8\uC9C0\uC815" : enemy.Category;
            var hp = enemy.MaxHp > 0 ? $"{enemy.CurrentHp} / {enemy.MaxHp}" : "\uBBF8\uC9C0\uC815";
            var enabled = string.Equals(enemy.IsEnabled, "TRUE", StringComparison.OrdinalIgnoreCase)
                ? "\uC0AC\uC6A9"
                : "\uBE44\uD65C\uC131";

            var embed = new EmbedBuilder()
                .WithTitle($"PANDORA ADMIN | {enemy.Name}")
                .WithColor(new Color(255, 110, 90))
                .WithDescription($"`{enemy.EnemyId}`")
                .AddField("HP", hp, inline: true)
                .AddField("\uCD9C\uD604\uAD6C\uBD84", category, inline: true)
                .AddField("\uC0C1\uD0DC", enabled, inline: true)
                .AddField("PHYSICAL", $"\uADFC\uB825 `{enemy.Strength}`\n\uBBFC\uCCA9 `{enemy.Dexterity}`\n\uCCB4\uB825 `{enemy.Constitution}`", inline: true)
                .AddField("MENTAL", $"\uC9C0\uB2A5 `{enemy.Intelligence}`\n\uC9C0\uD61C `{enemy.Wisdom}`\n\uB9E4\uB825 `{enemy.Charisma}`", inline: true)
                .AddField("\uC804\uD22C \uC815\uBCF4", $"\uD53C\uD574\uC2DD `{FormatEmpty(enemy.DamageFormula)}`\nDP `{enemy.Dp}`", inline: true)
                .AddField("\uB4DC\uB86D \uD14C\uC774\uBE14", hasDrops ? "\uC5F0\uACB0\uB428" : "\uC5C6\uC74C", inline: true)
                .AddField("\uB4DC\uB86D \uC124\uC815", hasSetting ? "\uC124\uC815\uB428" : "\uC5C6\uC74C", inline: true)
                .WithFooter("PANDORA NETWORK / ENEMY RECORD")
                .WithCurrentTimestamp();

            if (!string.IsNullOrWhiteSpace(enemy.Description))
            {
                embed.AddField("\uC124\uBA85 / \uBA54\uBAA8", enemy.Description, inline: false);
            }

            return embed.Build();
        }

        private static Embed BuildEnemyRollEmbed(EnemyRow enemy, EnemyStatInfo stat, int die1, int die2, int diceTotal, int total, EnemyJudgementOutcome outcome)
        {
            return new EmbedBuilder()
                .WithTitle("PROJECT:PANDORA | ENEMY ROLL")
                .WithColor(outcome.Color)
                .WithDescription($"**{enemy.Name}** \uC5D0\uB108\uBBF8\uC758 **{stat.KoreanName}({stat.Code})** \uD310\uC815\uC785\uB2C8\uB2E4.")
                .AddField("\uB2A5\uB825\uCE58", $"{stat.KoreanName} `{stat.Value}`", inline: true)
                .AddField("\uC218\uC815\uCE58", FormatModifier(stat.Modifier), inline: true)
                .AddField("\uC8FC\uC0AC\uC704", $"`{die1}` + `{die2}` = **{diceTotal}**", inline: true)
                .AddField("\uCD5C\uC885 \uD569\uACC4", $"**{total}**", inline: true)
                .AddField("\uACB0\uACFC", outcome.Text, inline: true)
                .AddField("\uAE30\uC900", "`10+` \uC131\uACF5 / `7-9` \uBD80\uBD84 \uC131\uACF5 / `6-` \uC2E4\uD328", inline: false)
                .WithFooter("PANDORA NETWORK / ENEMY OPERATION")
                .WithCurrentTimestamp()
                .Build();
        }

        private static Embed BuildDropResultEmbed(EnemyRow enemy, int rollCount, int requestedQuantity, IReadOnlyList<DropRollResult> rolls, IReadOnlyList<DropSummaryItem> items)
        {
            var title = $"[\uB4DC\uB86D \uACB0\uACFC] {enemy.Name} x{rollCount}";
            var description = items.Count == 0
                ? "- \uC804\uB9AC\uD488 \uC5C6\uC74C"
                : string.Join("\n", items.Select(item => $"- {item.ItemName} x{item.Count}"));
            var occurred = rolls.Count(roll => roll.Occurred);

            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(items.Count == 0 ? new Color(120, 130, 145) : new Color(120, 220, 160))
                .WithDescription(description)
                .AddField("\uB300\uC0C1", $"{enemy.EnemyId} | {enemy.Name}", inline: true)
                .AddField("\uAD74\uB9BC \uC218\uB7C9", $"{rollCount}", inline: true)
                .AddField("\uBC1C\uC0DD", $"{occurred} / {rollCount}", inline: true)
                .WithFooter("PANDORA NETWORK / DROP RESULT")
                .WithCurrentTimestamp();

            if (requestedQuantity != rollCount)
            {
                embed.AddField(
                    "\uC218\uB7C9 \uC81C\uD55C",
                    $"\uC548\uC815\uC801\uC778 \uCC98\uB9AC\uB97C \uC704\uD574 \uC694\uCCAD \uC218\uB7C9 {requestedQuantity}\uAC1C \uC911 {rollCount}\uAC1C\uB9CC \uAD74\uB838\uC2B5\uB2C8\uB2E4.",
                    inline: false);
            }

            return embed.Build();
        }

        private static EnemyStatInfo? ResolveEnemyStat(EnemyRow enemy, string ability)
        {
            var normalized = ability.Trim().ToLowerInvariant();
            return normalized switch
            {
                "\uADFC\uB825" or "str" or "strength" => CreateEnemyStat("STR", "\uADFC\uB825", enemy.Strength),
                "\uBBFC\uCCA9" or "\uBBFC\uCCA9\uC131" or "dex" or "dexterity" => CreateEnemyStat("DEX", "\uBBFC\uCCA9", enemy.Dexterity),
                "\uCCB4\uB825" or "con" or "constitution" => CreateEnemyStat("CON", "\uCCB4\uB825", enemy.Constitution),
                "\uC9C0\uB2A5" or "int" or "intelligence" => CreateEnemyStat("INT", "\uC9C0\uB2A5", enemy.Intelligence),
                "\uC9C0\uD61C" or "wis" or "wisdom" => CreateEnemyStat("WIS", "\uC9C0\uD61C", enemy.Wisdom),
                "\uB9E4\uB825" or "cha" or "charisma" => CreateEnemyStat("CHA", "\uB9E4\uB825", enemy.Charisma),
                _ => null
            };
        }

        private static EnemyStatInfo CreateEnemyStat(string code, string koreanName, int value)
        {
            return new EnemyStatInfo(code, koreanName, value, GetModifier(value));
        }

        private static int GetModifier(int statValue)
        {
            return statValue switch
            {
                >= 18 => 3,
                >= 16 => 2,
                >= 13 => 1,
                >= 9 => 0,
                >= 6 => -1,
                >= 4 => -2,
                _ => -3
            };
        }

        private static EnemyJudgementOutcome ResolveEnemyOutcome(int total)
        {
            if (total >= 10)
            {
                return new EnemyJudgementOutcome("\uC131\uACF5", "**\uC131\uACF5** - \uC758\uB3C4\uD55C \uD589\uB3D9\uC744 \uC548\uC815\uC801\uC73C\uB85C \uD574\uB0C5\uB2C8\uB2E4.", Color.Green);
            }

            if (total >= 7)
            {
                return new EnemyJudgementOutcome("\uBD80\uBD84 \uC131\uACF5", "**\uBD80\uBD84 \uC131\uACF5** - \uC131\uACF5\uD558\uC9C0\uB9CC \uB300\uAC00, \uC120\uD0DD, \uC704\uD5D8\uC774 \uB530\uB77C\uBD99\uC2B5\uB2C8\uB2E4.", Color.Gold);
            }

            return new EnemyJudgementOutcome("\uC2E4\uD328", "**\uC2E4\uD328** - \uC9C4\uD589\uC790\uAC00 \uC2E4\uD328\uC5D0 \uB530\uB978 \uC0C1\uD669 \uBCC0\uD654\uB97C \uC81C\uC2DC\uD569\uB2C8\uB2E4.", Color.Red);
        }

        private static string FormatEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
        private static string FormatModifier(int modifier)
        {
            return modifier >= 0 ? $"+{modifier}" : modifier.ToString();
        }

        private sealed record EnemyStatInfo(string Code, string KoreanName, int Value, int Modifier);

        private sealed record EnemyJudgementOutcome(string Label, string Text, Color Color);

        private sealed record DropSummaryItem(string ItemName, int Count);
    }
}
