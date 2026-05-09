using Discord;
using Discord.Interactions;
using PandoraBot.Models;
using PandoraBot.Repositories;
using PandoraBot.Services;
using PandoraShared.Models;
using System.Text;

namespace PandoraBot.Modules
{

    [DefaultMemberPermissions(GuildPermission.ManageGuild)]
    public class AdminModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("관리목록", "등록된 캐릭터 목록을 조회합니다.")]
        public async Task ListAllCharacters(
            [Summary("개수", "1~50 사이 조회 개수")] int limit = 25)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await PandoraRepositoryProvider.Characters.ListAllCharactersAsync(limit);
                if (characters.Count == 0)
                {
                    await FollowupAsync("아직 등록된 캐릭터가 없습니다. 먼저 플레이어가 `/등록`을 마쳤는지 확인해 주세요.", ephemeral: true);
                    return;
                }

                await FollowupAsync(embed: BuildCharacterListEmbed(characters, "PANDORA 캐릭터 목록"), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }


        [SlashCommand("정보조회", "특정 캐릭터 정보를 조회합니다.")]
        public async Task ShowCharacterForAdmin(
            [Summary("캐릭터", "캐릭터 이름, 시트 제목 또는 source_sheet_id")] string characterName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var character = await PandoraRepositoryProvider.Characters.GetCharacterForAdminAsync(characterName);
                await FollowupAsync(embed: BuildHunterEmbed(ToHunter(character)), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("체력설정", "특정 캐릭터의 현재 HP를 설정합니다.")]
        public async Task SetCharacterHp(
            [Summary("캐릭터", "캐릭터 이름, 시트 제목 또는 source_sheet_id")] string characterName,
            [Summary("현재HP", "변경할 현재 HP")] int currentHp)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await PandoraRepositoryProvider.Characters.SetCharacterHpAsync(characterName, currentHp);
                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "HP설정",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    result.UserId,
                    result.CharacterName,
                    $"{result.OldHp} -> {result.CurrentHp}");

                await FollowupAsync(
                    $"`{result.CharacterName}` HP를 `{result.OldHp} -> {result.CurrentHp} / {result.MaxHp}`로 변경했습니다.",
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("피해", "특정 캐릭터에게 피해를 적용합니다.")]
        public async Task DamageCharacter(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("수치", "적용할 피해량")] int amount,
            [Summary("메모", "선택 사항: 피해 사유")] string? memo = null)
        {
            await AdjustHpAsync(characterName, amount, "damage", memo);
        }

        [SlashCommand("회복", "특정 캐릭터를 회복합니다.")]
        public async Task HealCharacter(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("수치", "적용할 회복량")] int amount,
            [Summary("메모", "선택 사항: 회복 사유")] string? memo = null)
        {
            await AdjustHpAsync(characterName, amount, "heal", memo);
        }
        [SlashCommand("관리선택해제", "특정 캐릭터 소유자의 선택 상태를 해제합니다.")]
        public async Task ClearUserSelection(
            [Summary("캐릭터", "캐릭터 이름")] string characterName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var clearedCount = await PandoraRepositoryProvider.Characters.ClearSelectedCharacterForAdminAsync(characterName);
                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "선택해제",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    "",
                    characterName,
                    $"{clearedCount} selection(s) cleared");

                await FollowupAsync($"`{characterName}` 기준으로 선택 상태 {clearedCount}개를 해제했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("관리삭제", "특정 캐릭터를 삭제합니다.")]
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

                var result = await PandoraRepositoryProvider.Characters.DeleteCharacterForAdminAsync(characterName);
                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "삭제",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    result.UserId,
                    result.CharacterName,
                    $"character_id={result.CharacterId}");

                await FollowupAsync($"`{result.CharacterName}` 캐릭터 등록 정보를 삭제했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("관리승인", "캐릭터를 승인합니다.")]
        public async Task ApproveCharacter(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("메모", "선택 사항: 검수 메모")] string? memo = null)
        {
            await SetReviewStatusAsync(characterName, "approved", memo);
        }

        [SlashCommand("관리반려", "캐릭터를 반려합니다.")]
        public async Task RejectCharacter(
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("메모", "선택 사항: 반려 사유")] string? memo = null)
        {
            await SetReviewStatusAsync(characterName, "rejected", memo);
        }

        [SlashCommand("관리검수목록", "검수 상태별 캐릭터 목록을 조회합니다.")]
        public async Task ListReviewCharacters(
            [Summary("상태", "pending, approved, rejected")] string status = "pending",
            [Summary("개수", "1~50 사이 조회 개수")] int limit = 25)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await PandoraRepositoryProvider.Characters.ListReviewCharactersAsync(status, limit);
                if (characters.Count == 0)
                {
                    await FollowupAsync($"`{status}` 상태의 캐릭터가 아직 없습니다. 다른 상태로 바꿔 보거나, 먼저 `/등록`이 들어왔는지 확인해 주세요.", ephemeral: true);
                    return;
                }

                await FollowupAsync(embed: BuildCharacterListEmbed(characters, $"PANDORA 검수 목록 | {status}"), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }


        [SlashCommand("공지", "공지 템플릿을 발송하고 기록합니다.")]
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

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    $"notice:{noticeType}",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    Context.Channel.Id.ToString(),
                    title,
                    $"{content}\nMENTION: {(mentions.Count == 0 ? "none" : string.Join(" ", mentions))}");

                var mentionText = mentions.Count == 0 ? null : string.Join(" ", mentions);
                await FollowupAsync(mentionText, embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
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
                var enemies = await PandoraRepositoryProvider.Enemies.GetEnemiesAsync();
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
                        ? "아직 등록된 에너미가 없습니다. 먼저 에너미를 추가한 뒤 다시 확인해 주세요."
                        : $"`{filter}` 분류에 맞는 에너미가 아직 없습니다.";
                    await FollowupAsync(message, ephemeral: true);
                    return;
                }

                await FollowupAsync(
                    embed: BuildEnemyListEmbed(enemies, filter, Math.Clamp(limit, 1, 25)),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("\uC5D0\uB108\uBBF8\uC870\uD68C", "\uAD00\uB9AC\uC790\uC6A9: \uD2B9\uC815 \uC5D0\uB108\uBBF8\uC758 \uC0C1\uC138 \uC815\uBCF4\uB97C \uC870\uD68C\uD569\uB2C8\uB2E4.")]
        public async Task ShowEnemy(
            [Summary("\uC5D0\uB108\uBBF8", "\uC5D0\uB108\uBBF8 ID \uB610\uB294 \uC774\uB984 \uC77C\uBD80")] string enemy)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await PandoraRepositoryProvider.Enemies.GetEnemyByIdOrNameAsync(enemy);
                if (!result.Found)
                {
                    if (result.HasMultipleMatches)
                    {
                        await FollowupAsync(embed: BuildEnemyCandidateEmbed(result.Matches, enemy), ephemeral: true);
                        return;
                    }

                    await FollowupAsync($"`{enemy}`에 해당하는 에너미를 찾지 못했습니다. 먼저 `/에너미목록`으로 등록 상태를 확인해 주세요.", ephemeral: true);
                    return;
                }

                var found = result.Enemy!;
                var drops = await PandoraRepositoryProvider.Drops.GetEnemyDropsAsync();
                var settings = await PandoraRepositoryProvider.Drops.GetEnemyDropSettingsAsync();
                var hasDrops = drops.Any(drop => string.Equals(drop.EnemyId, found.EnemyId, StringComparison.OrdinalIgnoreCase));
                var hasSetting = settings.Any(setting => string.Equals(setting.EnemyId, found.EnemyId, StringComparison.OrdinalIgnoreCase));

                await FollowupAsync(embed: BuildEnemyDetailEmbed(found, hasDrops, hasSetting), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
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
                var result = await PandoraRepositoryProvider.Enemies.GetEnemyByIdOrNameAsync(enemy);
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

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
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
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
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

                var result = await PandoraRepositoryProvider.Enemies.GetEnemyByIdOrNameAsync(enemy);
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
                    rolls.Add(await PandoraRepositoryProvider.Drops.RollDropAsync(found.EnemyId, writeLog: false));
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

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "\uB4DC\uB86D \uAD74\uB9BC",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    "",
                    found.Name,
                    $"{found.EnemyId} x{rollCount}: {summary}");

                if (Context.Guild is not null)
                {
                    await PandoraRepositoryProvider.CombatSessions.AppendLogIfActiveAsync(
                        Context.Guild.Id.ToString(),
                        Context.Channel.Id.ToString(),
                        Context.User.Id.ToString(),
                        "combat_drop",
                        found.Name,
                        "",
                        summary,
                        $"{found.EnemyId} x{rollCount}: {summary}");
                }

                await FollowupAsync(
                    embed: BuildDropResultEmbed(found, rollCount, quantity, rolls, items),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("\uB4DC\uB86D\uD14C\uC2A4\uD2B8", "\uAD00\uB9AC\uC790\uC6A9: \uC5D0\uB108\uBBF8 \uB4DC\uB86D \uD655\uB960\uC744 \uC2DC\uBBAC\uB808\uC774\uC158\uD569\uB2C8\uB2E4.")]
        public async Task TestEnemyDrops(
            [Summary("\uC5D0\uB108\uBBF8", "\uC5D0\uB108\uBBF8 ID \uB610\uB294 \uC774\uB984 \uC77C\uBD80")] string enemy,
            [Summary("\uBC18\uBCF5\uD69F\uC218", "\uC2DC\uBBAC\uB808\uC774\uC158 \uBC18\uBCF5 \uD69F\uC218, \uCD5C\uB300 1000\uD68C")] int iterations = 100)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (iterations <= 0)
                {
                    await FollowupAsync("\uBC18\uBCF5\uD69F\uC218\uB294 1 \uC774\uC0C1\uC73C\uB85C \uC785\uB825\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                var result = await PandoraRepositoryProvider.Enemies.GetEnemyByIdOrNameAsync(enemy);
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
                var testCount = Math.Clamp(iterations, 1, 1000);
                var rolls = new List<DropRollResult>();
                for (var i = 0; i < testCount; i++)
                {
                    rolls.Add(await PandoraRepositoryProvider.Drops.RollDropAsync(found.EnemyId, writeLog: false));
                }

                var items = rolls
                    .SelectMany(roll => roll.Items)
                    .GroupBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new DropSummaryItem(group.First().ItemName, group.Sum(item => item.Count)))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.ItemName)
                    .ToList();

                var occurred = rolls.Count(roll => roll.Occurred);
                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "\uB4DC\uB86D\uD14C\uC2A4\uD2B8",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    "",
                    found.Name,
                    $"{found.EnemyId} / {testCount}\uD68C / \uBC1C\uC0DD {occurred}\uD68C / \uD56D\uBAA9 {items.Count}\uAC1C");

                await FollowupAsync(
                    embed: BuildDropTestEmbed(found, testCount, iterations, rolls, items),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("\uC804\uD22C\uBCF4\uC870", "\uAD00\uB9AC\uC790\uC6A9: \uC218\uB3D9 \uC804\uD22C \uC9C4\uD589\uC5D0 \uD544\uC694\uD55C \uBA85\uB839 \uD750\uB984\uC744 \uC548\uB0B4\uD569\uB2C8\uB2E4.")]
        public async Task ShowCombatHelper()
        {
            var embed = new EmbedBuilder()
                .WithTitle("PANDORA 전투보조")
                .WithColor(new Color(90, 190, 255))
                .WithDescription("지금 구현된 전투 세션 흐름에 맞춰, 진행자가 실제로 많이 쓰는 순서대로 정리한 안내입니다.")
                .AddField("1. 세션 열기", "`/전투시작`으로 현재 채널 전투를 엽니다.\n제목을 정해 두면 로그를 다시 찾을 때 편합니다.", inline: false)
                .AddField("2. 참가자 올리기", "`/전투참가`로 PC/NPC를 올리고, `/에너미소환`으로 에너미를 배치합니다.", inline: false)
                .AddField("3. 현재 판 확인", "`/전투상태`로 지금 전투판에 올라온 대상과 HP를 확인합니다.", inline: false)
                .AddField("4. 판정 진행", "`/판정`은 플레이어용, `/에너미판정`은 에너미용입니다.", inline: false)
                .AddField("5. HP와 정리", "`/전투피해`, `/전투회복`, `/전투퇴장`, `/전투정리`로 전투판을 관리합니다.", inline: false)
                .AddField("6. 전투 종료 후", "`/드롭`으로 실전 보상을 굴리고, 확률 점검이 필요하면 `/드롭테스트`를 사용한 뒤 `/전투종료`로 세션을 닫아 주세요.", inline: false)
                .AddField(
                    "1.0 범위",
                    "전투는 세션 단위로 관리하지만, 턴/라운드/행동완료/이니셔티브는 아직 자동화하지 않습니다.\n진행자가 흐름을 잡고, 봇은 세션/판정/HP/드롭을 보조하는 구조입니다.",
                    inline: false)
                .WithFooter("전투가 이미 열려 있다면 /전투상태, 아직 안 열렸다면 /전투시작부터 시작해 주세요.")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("\uC804\uD22C\uC2DC\uC791", "\uAD00\uB9AC\uC790\uC6A9: \uD604\uC7AC \uCC44\uB110\uC5D0 \uD65C\uC131 \uC804\uD22C \uC138\uC158\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4.")]
        public async Task StartCombatSession(
            [Summary("\uC81C\uBAA9", "\uC804\uD22C \uC138\uC158 \uC81C\uBAA9")] string title)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (Context.Guild is null)
                {
                    await FollowupAsync("\uC804\uD22C \uC138\uC158 \uBA85\uB839\uC740 \uC11C\uBC84 \uCC44\uB110\uC5D0\uC11C\uB9CC \uC0AC\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", ephemeral: true);
                    return;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    await FollowupAsync("\uC804\uD22C \uC138\uC158 \uC81C\uBAA9\uC744 \uC785\uB825\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                var session = await PandoraRepositoryProvider.CombatSessions.StartCombatSessionAsync(
                    Context.Guild.Id.ToString(),
                    Context.Channel.Id.ToString(),
                    title.Trim(),
                    Context.User.Id.ToString());

                var warnings = new List<string>();
                try
                {
                    await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                        "\uC804\uD22C\uC2DC\uC791",
                        Context.User.Id.ToString(),
                        Context.User.Username,
                        Context.Channel.Id.ToString(),
                        session.Title,
                        $"{session.Id} / guild={session.GuildId} / channel={session.ChannelId}");
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"[COMBAT START][ADMIN LOG WARNING] {logEx.GetType().FullName}: {logEx.Message}");
                    warnings.Add("관리 로그 기록에는 실패했지만 전투 세션 자체는 시작되었습니다.");
                }

                var combatStartEmbed = new EmbedBuilder()
                    .WithTitle("⚔️ 전투 개시")
                    .WithDescription(
                        $"# 「{session.Title}」\n\n" +
                        "공기가 무겁게 가라앉고,\n" +
                        "긴장감이 전장을 가로지릅니다.\n\n" +
                        "**!전투를 시작합니다!**"
                    )
                    .WithColor(new Color(180, 32, 48))
                    .WithCurrentTimestamp()
                    .WithFooter("PANDORA Combat Session")
                    .Build();

                var gmMention = new EmbedBuilder()
                    .WithTitle("진행자용 전투 운영 안내")
                    .WithDescription(
                        $"전투 세션 **「{title}」** 이 개시되었습니다.\n\n" +
                        "아래 명령어로 전투판을 구성하세요."
                    )
                    .AddField(
                        "전투판 구성",
                        "`/전투참가` - PC/NPC/관리자 캐릭터 참가\n" +
                        "`/에너미소환` - 에너미 배치\n" +
                        "`/전투상태` - 현재 전투 참가자 상태 확인"
                    )
                    .AddField(
                        "전투 중 조작",
                        "`/전투피해` - 참가자에게 피해 적용\n" +
                        "`/전투회복` - 참가자의 HP 회복\n" +
                        "`/전투퇴장` - 특정 참가자 퇴장\n" +
                        "`/전투정리` - 쓰러진 에너미 정리\n" +
                        "`/드롭` - 전리품 굴림\n" +
                        "`/전투종료` - 전투 세션 종료"
                    );

                try
                {
                    await Context.Channel.SendMessageAsync(embed: combatStartEmbed);
                }
                catch (Exception announceEx)
                {
                    Console.WriteLine($"[COMBAT START][ANNOUNCE WARNING] {announceEx.GetType().FullName}: {announceEx.Message}");
                    warnings.Add("채널 공지 메시지 전송에는 실패했지만 전투 세션 자체는 시작되었습니다.");
                }

                if (warnings.Count > 0)
                {
                    gmMention.AddField("확인 필요", string.Join("\n", warnings), inline: false);
                }

                await FollowupAsync(embed: gmMention.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("\uC804\uD22C\uC885\uB8CC", "\uAD00\uB9AC\uC790\uC6A9: \uD604\uC7AC \uCC44\uB110\uC758 \uD65C\uC131 \uC804\uD22C \uC138\uC158\uC744 \uC885\uB8CC\uD569\uB2C8\uB2E4.")]
        public async Task EndCombatSession()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (Context.Guild is null)
                {
                    await FollowupAsync("전투 세션 명령은 서버 채널에서만 사용할 수 있습니다.", ephemeral: true);
                    return;
                }

                var result = await PandoraRepositoryProvider.CombatSessions.EndCombatSessionAsync(
                    Context.Guild.Id.ToString(),
                    Context.Channel.Id.ToString(),
                    Context.User.Id.ToString());

                var warnings = new List<string>();
                try
                {
                    await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                        "전투종료",
                        Context.User.Id.ToString(),
                        Context.User.Username,
                        Context.Channel.Id.ToString(),
                        result.Session.Title,
                        $"{result.Session.Id} / ended_at={result.Session.EndedAt:O} / cleaned={result.CleanedParticipantCount}");
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"[COMBAT END][ADMIN LOG WARNING] {logEx.GetType().FullName}: {logEx.Message}");
                    warnings.Add("관리 로그 기록에는 실패했지만 전투 세션 종료는 반영되었습니다.");
                }

                try
                {
                    await Context.Channel.SendMessageAsync(
                        embed: BuildCombatSessionEndedPlayerEmbed(result.Session));
                }
                catch (Exception announceEx)
                {
                    Console.WriteLine($"[COMBAT END][ANNOUNCE WARNING] {announceEx.GetType().FullName}: {announceEx.Message}");
                    warnings.Add("채널 공지 메시지 전송에는 실패했지만 전투 세션 종료는 반영되었습니다.");
                }

                await FollowupAsync(
                    embed: BuildCombatSessionEndedGmEmbed(result, warnings),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("전투참가", "현재 활성 전투 세션에 캐릭터를 참가시킵니다.")]
        public async Task JoinCombatSession(
            [Summary("캐릭터", "참가시킬 캐릭터 이름")] string characterName,
            [Summary("메모", "선택 사항: 참가 메모")] string? memo = null)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (Context.Guild is null)
                {
                    await FollowupAsync("전투 관련 명령은 서버 채널에서만 사용할 수 있습니다.", ephemeral: true);
                    return;
                }

                var participant = await PandoraRepositoryProvider.CombatParticipants.AddPlayerAsync(
                    Context.Guild.Id.ToString(),
                    Context.Channel.Id.ToString(),
                    characterName,
                    Context.User.Id.ToString(),
                    memo ?? "");

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "전투참가",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    Context.Channel.Id.ToString(),
                    participant.DisplayName,
                    $"{participant.Id} / player / hp={participant.CurrentHp}/{participant.MaxHp}");

                await FollowupAsync(embed: BuildCombatParticipantAddedEmbed(participant, "플레이어 참가"), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("에너미소환", "현재 활성 전투 세션에 에너미를 소환합니다.")]
        public async Task SpawnEnemies(
            [Summary("에너미", "에너미 ID 또는 이름")] string enemy,
            [Summary("수량", "소환할 수량")] int quantity = 1,
            [Summary("메모", "선택 사항: 소환 메모")] string? memo = null)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (Context.Guild is null)
                {
                    await FollowupAsync("전투 관련 명령은 서버 채널에서만 사용할 수 있습니다.", ephemeral: true);
                    return;
                }

                if (quantity <= 0)
                {
                    await FollowupAsync("수량은 1 이상으로 입력해주세요.", ephemeral: true);
                    return;
                }

                var participants = await PandoraRepositoryProvider.CombatParticipants.AddEnemiesAsync(
                    Context.Guild.Id.ToString(),
                    Context.Channel.Id.ToString(),
                    enemy,
                    Math.Clamp(quantity, 1, 26),
                    Context.User.Id.ToString(),
                    memo ?? "");

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "에너미소환",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    Context.Channel.Id.ToString(),
                    enemy,
                    $"{participants.Count}명 / {string.Join(", ", participants.Select(x => x.DisplayName))}");

                await FollowupAsync(embed: BuildCombatEnemySpawnedEmbed(enemy, quantity, participants), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("전투퇴장", "현재 활성 전투 세션에서 특정 참가자를 정리합니다.")]
        public async Task RemoveCombatParticipant(
            [Summary("대상", "참가자 ID 또는 표시명")] string target,
            [Summary("메모", "선택 사항: 제거 메모")] string? memo = null)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (Context.Guild is null)
                {
                    await FollowupAsync("전투 관련 명령은 서버 채널에서만 사용할 수 있습니다.", ephemeral: true);
                    return;
                }

                var participant = await PandoraRepositoryProvider.CombatParticipants.RemoveParticipantAsync(
                    Context.Guild.Id.ToString(),
                    Context.Channel.Id.ToString(),
                    target,
                    Context.User.Id.ToString(),
                    memo ?? "");

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "전투퇴장",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    Context.Channel.Id.ToString(),
                    participant.DisplayName,
                    $"{participant.Id} / {participant.Type} / removed");

                await FollowupAsync(embed: BuildCombatParticipantRemovedEmbed(participant), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("전투정리", "현재 활성 전투 세션에서 HP 0 이하 에너미를 정리합니다.")]
        public async Task CleanupCombatEnemies(
            [Summary("메모", "선택 사항: 정리 메모")] string? memo = null)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (Context.Guild is null)
                {
                    await FollowupAsync("전투 관련 명령은 서버 채널에서만 사용할 수 있습니다.", ephemeral: true);
                    return;
                }

                var removed = await PandoraRepositoryProvider.CombatParticipants.CleanupDefeatedEnemiesAsync(
                    Context.Guild.Id.ToString(),
                    Context.Channel.Id.ToString(),
                    Context.User.Id.ToString(),
                    memo ?? "");

                if (removed.Count == 0)
                {
                    await FollowupAsync("지금 자동으로 정리할 에너미가 없습니다. `/전투정리`는 HP가 0 이하인 에너미만 정리하고, 플레이어는 자동으로 빼지 않습니다.", ephemeral: true);
                    return;
                }

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    "전투정리",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    Context.Channel.Id.ToString(),
                    "enemy-cleanup",
                    $"{removed.Count}명 / {string.Join(", ", removed.Select(x => x.DisplayName))}");

                await FollowupAsync(embed: BuildCombatCleanupEmbed(removed), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("\uC804\uD22C\uD53C\uD574", "\uAD00\uB9AC\uC790\uC6A9: \uD65C\uC131 \uC804\uD22C \uCC38\uAC00\uC790\uC5D0\uAC8C \uD53C\uD574\uB97C \uC801\uC6A9\uD569\uB2C8\uB2E4.")]
        public async Task DamageActiveCombatParticipant(
            [Summary("\uB300\uC0C1", "\uD65C\uC131 \uC804\uD22C \uCC38\uAC00\uC790 ID \uB610\uB294 \uC774\uB984")] string target,
            [Summary("\uD53C\uD574", "\uC801\uC6A9\uD560 \uD53C\uD574\uB7C9")] int damage,
            [Summary("\uBA54\uBAA8", "\uC120\uD0DD \uC0AC\uD56D: \uCC98\uB9AC \uBA54\uBAA8")] string? memo = null)
        {
            await AdjustActiveCombatHpAsync(target, damage, "damage", memo);
        }

        [SlashCommand("\uC804\uD22C\uD68C\uBCF5", "\uAD00\uB9AC\uC790\uC6A9: \uD65C\uC131 \uC804\uD22C \uCC38\uAC00\uC790\uB97C \uD68C\uBCF5\uD569\uB2C8\uB2E4.")]
        public async Task HealActiveCombatParticipant(
            [Summary("\uB300\uC0C1", "\uD65C\uC131 \uC804\uD22C \uCC38\uAC00\uC790 ID \uB610\uB294 \uC774\uB984")] string target,
            [Summary("\uD68C\uBCF5", "\uC801\uC6A9\uD560 \uD68C\uBCF5\uB7C9")] int heal,
            [Summary("\uBA54\uBAA8", "\uC120\uD0DD \uC0AC\uD56D: \uCC98\uB9AC \uBA54\uBAA8")] string? memo = null)
        {
            await AdjustActiveCombatHpAsync(target, heal, "heal", memo);
        }

        private async Task AdjustActiveCombatHpAsync(string target, int amount, string action, string? memo)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (amount <= 0)
                {
                    await FollowupAsync("\uC218\uCE58\uB294 1 \uC774\uC0C1\uC73C\uB85C \uC785\uB825\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                if (Context.Guild is null)
                {
                    await FollowupAsync("전투 관련 명령은 서버 채널에서만 사용할 수 있습니다.", ephemeral: true);
                    return;
                }

                var result = await PandoraRepositoryProvider.CombatParticipants.AdjustHpAsync(
                    Context.Guild.Id.ToString(),
                    Context.Channel.Id.ToString(),
                    target,
                    amount,
                    action,
                    Context.User.Id.ToString(),
                    memo ?? "");

                if (string.Equals(result.Type, "player", StringComparison.OrdinalIgnoreCase))
                {
                    var logAction = string.Equals(action, "heal", StringComparison.OrdinalIgnoreCase) ? "전투회복" : "전투피해";
                    var memoText = string.IsNullOrWhiteSpace(memo) ? "" : $" / {memo.Trim()}";
                    await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                        logAction,
                        Context.User.Id.ToString(),
                        Context.User.Username,
                        Context.Channel.Id.ToString(),
                        result.DisplayName,
                        $"{result.ParticipantId} / {result.Type} / {result.OldHp} -> {result.CurrentHp} / amount {amount}{memoText}");
                }

                await FollowupAsync(
                    embed: BuildActiveCombatHpEmbed(result, action, amount),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("활성 전투 세션", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("/전투상태", StringComparison.OrdinalIgnoreCase))
                {
                    await FollowupAsync("\uB300\uC0C1\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. `/전투상태`\uB85C \uD604\uC7AC \uD65C\uC131 \uC804\uD22C \uCC38\uAC00\uC790\uB97C \uBA3C\uC800 \uD655\uC778\uD574\uC8FC\uC138\uC694.", ephemeral: true);
                    return;
                }

                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
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

                var result = await PandoraRepositoryProvider.Characters.AdjustCharacterHpAsync(characterName, amount, action);
                var label = action == "heal" ? "회복" : "피해";
                var signedAmount = action == "heal" ? amount : -amount;
                var memoText = string.IsNullOrWhiteSpace(memo) ? string.Empty : $" / {memo.Trim()}";

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    label,
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    result.UserId,
                    result.CharacterName,
                    $"{result.OldHp} -> {result.CurrentHp} ({signedAmount:+#;-#;0}){memoText}");

                await FollowupAsync(
                    $"`{result.CharacterName}` {label} 처리: HP `{result.OldHp} -> {result.CurrentHp} / {result.MaxHp}`",
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
                await FollowupAsync(ToFriendlyAdminError(ex.Message), ephemeral: true);
            }
        }
        private async Task SetReviewStatusAsync(string characterName, string status, string? memo)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await PandoraRepositoryProvider.Characters.SetReviewStatusAsync(characterName, status);

                await PandoraRepositoryProvider.AdminLogs.AppendAdminLogAsync(
                    $"검수:{result.ReviewStatus}",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    result.UserId,
                    result.CharacterName,
                    string.IsNullOrWhiteSpace(memo) ? "review status updated" : memo!);

                await FollowupAsync(
                    $"{result.CharacterName} 검수 상태를 {result.ReviewStatus}로 변경했습니다.",
                    ephemeral: true);
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
                return "\uCC98\uB9AC \uC911 \uBB38\uC81C\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574 \uC8FC\uC138\uC694.";
            }

            if (message.Contains("PandoraDb connection string", StringComparison.OrdinalIgnoreCase))
            {
                return "캐릭터 운영용 DB 연결이 아직 준비되지 않았습니다. 설정을 확인한 뒤 다시 시도해 주세요.";
            }

            if (message.Contains("Multiple characters matched", StringComparison.OrdinalIgnoreCase))
            {
                return "조건에 맞는 캐릭터가 여러 개입니다. 더 정확한 캐릭터 이름이나 원본 시트 제목, source_sheet_id로 다시 지정해 주세요.";
            }

            if (message.Contains("No registered character", StringComparison.OrdinalIgnoreCase))
            {
                return "등록된 캐릭터를 찾을 수 없습니다. 필요하면 `/관리목록`으로 현재 등록 상태를 먼저 확인해 주세요.";
            }

            if (message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return "\uC694\uCCAD\uC774 \uC7A0\uC2DC \uB9CE\uC544 \uCC98\uB9AC\uAC00 \uC9C0\uC5F0\uB418\uACE0 \uC788\uC2B5\uB2C8\uB2E4. \uBA87 \uCD08 \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574 \uC8FC\uC138\uC694.";
            }

            if (message.Contains("\uC774\uBBF8 \uC9C4\uD589 \uC911\uC778 \uD65C\uC131 \uC804\uD22C \uC138\uC158", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("\uC774\uBBF8 \uD65C\uC131 \uC804\uD22C \uC138\uC158", StringComparison.OrdinalIgnoreCase))
            {
                return "\uD604\uC7AC \uCC44\uB110\uC5D0\uB294 \uC774\uBBF8 \uC9C4\uD589 \uC911\uC778 \uC804\uD22C \uC138\uC158\uC774 \uC788\uC2B5\uB2C8\uB2E4. `/\uC804\uD22C\uC0C1\uD0DC`\uB85C \uD604\uC7AC \uC0C1\uD0DC\uB97C \uD655\uC778\uD558\uAC70\uB098 `/\uC804\uD22C\uC885\uB8CC`\uB85C \uBA3C\uC800 \uC885\uB8CC\uD574 \uC8FC\uC138\uC694.";
            }

            if (message.Contains("\uD65C\uC131 \uC804\uD22C \uC138\uC158\uC774 \uC5C6", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("/\uC804\uD22C\uC2DC\uC791", StringComparison.OrdinalIgnoreCase))
            {
                return "\uD604\uC7AC \uCC44\uB110\uC5D0 \uD65C\uC131 \uC804\uD22C \uC138\uC158\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uBA3C\uC800 /\uC804\uD22C\uC2DC\uC791\uC73C\uB85C \uC138\uC158\uC744 \uC5F4\uC5B4 \uC8FC\uC138\uC694.";
            }

            if (message.Contains("\uCC3E\uC744 \uC218 \uC5C6", StringComparison.OrdinalIgnoreCase))
            {
                return $"{message}\n\uD544\uC694\uD558\uBA74 \uBAA9\uB85D \uBA85\uB839\uC73C\uB85C \uD604\uC7AC \uB4F1\uB85D \uC0C1\uD0DC\uB97C \uBA3C\uC800 \uD655\uC778\uD574 \uC8FC\uC138\uC694.";
            }

            return $"\uCC98\uB9AC \uC911 \uBB38\uC81C\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4: {message}";
        }
        
        private static Hunter ToHunter(CharacterRecord character)
        {
            return new Hunter
            {
                UserId = character.OwnerDiscordId,
                CharacterName = character.DisplayName,
                Strength = character.Strength,
                Dexterity = character.Dexterity,
                Constitution = character.Constitution,
                Intelligence = character.Intelligence,
                Wisdom = character.Wisdom,
                Charisma = character.Charisma,
                CurrentHp = character.CurrentHp,
                MaxHp = character.MaxHp
            };
        }
        private static Embed BuildHunterEmbed(Hunter hunter)
        {
            return new EmbedBuilder()
                .WithTitle("PANDORA 캐릭터 정보")
                .WithColor(new Color(70, 160, 255))
                .WithDescription($"**{hunter.CharacterName}**")
                .AddField("Discord User ID", hunter.UserId, inline: false)
                .AddField("HP", $"{hunter.CurrentHp} / {hunter.MaxHp}", inline: true)
                .AddField("Physical", $"STR {hunter.Strength} ({FormatModifier(hunter.GetModifier(hunter.Strength))})\nDEX {hunter.Dexterity} ({FormatModifier(hunter.GetModifier(hunter.Dexterity))})\nCON {hunter.Constitution} ({FormatModifier(hunter.GetModifier(hunter.Constitution))})", inline: true)
                .AddField("Mental", $"INT {hunter.Intelligence} ({FormatModifier(hunter.GetModifier(hunter.Intelligence))})\nWIS {hunter.Wisdom} ({FormatModifier(hunter.GetModifier(hunter.Wisdom))})\nCHA {hunter.Charisma} ({FormatModifier(hunter.GetModifier(hunter.Charisma))})", inline: true)
                .WithCurrentTimestamp()
                .Build();
        }

        private static Embed BuildCharacterListEmbed(IReadOnlyList<AdminCharacterListItem> characters, string title)
        {
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(new Color(70, 160, 255))
                .WithDescription($"총 {characters.Count}개의 캐릭터를 표시합니다.")
                .WithFooter("선택 상태는 DB character_selections 기준입니다.")
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
                    $"{ownerText}\n검수: `{character.ReviewStatus}`\n{selectedText}\nsource: `{character.SourceSheetId}`",
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
                ? "PANDORA 에너미 목록"
                : $"PANDORA 에너미 목록 | {categoryFilter}";

            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(new Color(255, 110, 90))
                .WithDescription($"현재 조건에 맞는 에너미는 총 {enemies.Count}개이며, 이 화면에는 {shown.Count}개를 보여 줍니다.")
                .WithFooter("에너미 ID를 함께 기억해 두면 조회, 판정, 드롭 명령에서 다시 찾기 쉽습니다.")
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
                .WithTitle("PANDORA 에너미 후보")
                .WithColor(new Color(255, 185, 80))
                .WithDescription($"`{query}`에 맞는 에너미가 여러 개 있습니다. 아래 후보를 보고 에너미 ID나 더 정확한 이름으로 다시 지정해 주세요.")
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
                .WithTitle($"PANDORA 에너미 정보 | {enemy.Name}")
                .WithColor(new Color(255, 110, 90))
                .WithDescription($"에너미 ID `{enemy.EnemyId}` 기준 상세 정보입니다.")
                .AddField("HP", hp, inline: true)
                .AddField("\uCD9C\uD604\uAD6C\uBD84", category, inline: true)
                .AddField("\uC0C1\uD0DC", enabled, inline: true)
                .AddField("PHYSICAL", $"\uADFC\uB825 `{enemy.Strength}`\n\uBBFC\uCCA9 `{enemy.Dexterity}`\n\uCCB4\uB825 `{enemy.Constitution}`", inline: true)
                .AddField("MENTAL", $"\uC9C0\uB2A5 `{enemy.Intelligence}`\n\uC9C0\uD61C `{enemy.Wisdom}`\n\uB9E4\uB825 `{enemy.Charisma}`", inline: true)
                .AddField("\uC804\uD22C \uC815\uBCF4", $"\uD53C\uD574\uC2DD `{FormatEmpty(enemy.DamageFormula)}`\nDP `{enemy.Dp}`", inline: true)
                .AddField("\uB4DC\uB86D \uD14C\uC774\uBE14", hasDrops ? "연결되어 있습니다" : "아직 없습니다", inline: true)
                .AddField("\uB4DC\uB86D \uC124\uC815", hasSetting ? "저장되어 있습니다" : "아직 없습니다", inline: true)
                .WithFooter("판정이나 드롭을 굴리기 전 상태 확인용으로 바로 사용할 수 있습니다.")
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

        private static Embed BuildDropTestEmbed(EnemyRow enemy, int testCount, int requestedIterations, IReadOnlyList<DropRollResult> rolls, IReadOnlyList<DropSummaryItem> items)
        {
            var shown = items.Take(12).ToList();
            var occurred = rolls.Count(roll => roll.Occurred);
            var totalItemCount = items.Sum(item => item.Count);
            var description = shown.Count == 0
                ? "- \uC804\uB9AC\uD488 \uC5C6\uC74C"
                : string.Join("\n", shown.Select(item =>
                    $"- {item.ItemName} x{item.Count} ({FormatPercent(item.Count, testCount)})"));

            var embed = new EmbedBuilder()
                .WithTitle($"[\uB4DC\uB86D\uD14C\uC2A4\uD2B8] {enemy.Name} x{testCount}")
                .WithColor(new Color(180, 150, 255))
                .WithDescription("\uC774 \uACB0\uACFC\uB294 \uC2E4\uC81C \uBCF4\uC0C1 \uC9C0\uAE09\uC774 \uC544\uB2CC \uD655\uB960 \uD14C\uC2A4\uD2B8\uC785\uB2C8\uB2E4.\n\n" + description)
                .AddField("\uB300\uC0C1", $"{enemy.EnemyId} | {enemy.Name}", inline: true)
                .AddField("\uBC18\uBCF5", $"{testCount}\uD68C", inline: true)
                .AddField("\uB4DC\uB86D \uBC1C\uC0DD", $"{occurred} / {testCount} ({FormatPercent(occurred, testCount)})", inline: true)
                .AddField("\uC804\uB9AC\uD488 \uCD1D\uD569", $"{totalItemCount}\uAC1C", inline: true)
                .WithFooter("PANDORA NETWORK / DROP TEST ONLY")
                .WithCurrentTimestamp();

            if (items.Count > shown.Count)
            {
                embed.AddField(
                    "\uD45C\uC2DC \uC81C\uD55C",
                    $"\uD56D\uBAA9\uC774 \uAE38\uC5B4 \uC0C1\uC704 {shown.Count}\uAC1C\uB9CC \uD45C\uC2DC\uD588\uC2B5\uB2C8\uB2E4. \uB098\uBA38\uC9C0 {items.Count - shown.Count}\uAC1C\uAC00 \uB354 \uC788\uC2B5\uB2C8\uB2E4.",
                    inline: false);
            }

            if (requestedIterations != testCount)
            {
                embed.AddField(
                    "\uBC18\uBCF5\uD69F\uC218 \uC81C\uD55C",
                    $"\uCD5C\uB300 1000\uD68C\uAE4C\uC9C0\uB9CC \uD14C\uC2A4\uD2B8\uD569\uB2C8\uB2E4. \uC694\uCCAD {requestedIterations}\uD68C \uC911 {testCount}\uD68C\uB97C \uC2E4\uD589\uD588\uC2B5\uB2C8\uB2E4.",
                    inline: false);
            }

            return embed.Build();
        }

        private static Embed BuildCombatSessionEndedGmEmbed(CombatSessionEndResult result, IReadOnlyList<string>? warnings = null)
        {
            var session = result.Session;
            var endedAt = session.EndedAt ?? DateTimeOffset.UtcNow;
            var embed = new EmbedBuilder()
                .WithTitle("진행자용 전투 종료 보고")
                .WithDescription(
                    $"전투 세션 **「{session.Title}」** 이 종료되었습니다.\n\n" +
                    "필요하다면 전투 로그, 드롭 결과, 참가자 상태를 확인해 주세요."
                )
                .AddField("세션 ID", session.Id.ToString(), inline: false)
                .AddField("상태", session.Status, inline: true)
                .AddField("채널", $"`{session.ChannelId}`", inline: true)
                .AddField("종료 시각", $"<t:{endedAt.ToUnixTimeSeconds()}:f>", inline: false)
                .AddField("정리된 참가자", $"{result.CleanedParticipantCount}명", inline: true)
                .AddField("플레이어/기타", $"{result.CleanedPlayerCount}명", inline: true)
                .AddField("에너미", $"{result.CleanedEnemyCount}명", inline: true)
                .AddField(
                    "종료 후 확인",
                    "`/전투상태` - 종료 전 참가자 상태 확인이 필요한 경우\n" +
                    "`/드롭` - 전투 보상 굴림이 필요한 경우\n" +
                    "`/정보조회` - PC 상태 확인이 필요한 경우"
                )
                .WithColor(new Color(48, 96, 180))
                .WithCurrentTimestamp()
                .WithFooter("이 메시지는 진행자에게만 보입니다.");

            if (warnings is { Count: > 0 })
            {
                embed.AddField("확인 필요", string.Join("\n", warnings), inline: false);
            }

            return embed.Build();
        }
        private static Embed BuildCombatSessionEndedPlayerEmbed(CombatSessionSummary session)
        {
            return new EmbedBuilder()
                .WithTitle("◆ 전투 종료 ◆")
                .WithDescription(
                    $"# 「{session.Title}」\n\n" +
                    "전장을 짓누르던 긴장감이 천천히 흩어집니다.\n" +
                    "흔들리던 파편의 울림도, 어느새 잦아들었습니다.\n\n" +
                    "**전투가 종료되었습니다.**"
                )
                .WithColor(new Color(80, 80, 96))
                .WithCurrentTimestamp()
                .WithFooter("Project:PANDORA | Combat Session")
                .Build();
        }

        private static Embed BuildCombatParticipantAddedEmbed(CombatParticipantSummary participant, string actionTitle)
        {
            return new EmbedBuilder()
                .WithTitle($"PANDORA {actionTitle}")
                .WithColor(new Color(90, 190, 255))
                .WithDescription($"**{participant.DisplayName}**")
                .AddField("참가자 ID", participant.Id.ToString(), inline: false)
                .AddField("구분", participant.Type, inline: true)
                .AddField("상태", participant.Status, inline: true)
                .AddField("HP", $"{participant.CurrentHp} / {participant.MaxHp}", inline: true)
                .AddField("세션 ID", participant.CombatSessionId.ToString(), inline: false)
                .WithFooter("PANDORA NETWORK / COMBAT PARTICIPANT")
                .Build();
        }

        private static Embed BuildCombatEnemySpawnedEmbed(string requestedEnemy, int requestedQuantity, IReadOnlyList<CombatParticipantSummary> participants)
        {
            var builder = new StringBuilder();
            foreach (var participant in participants)
            {
                builder.AppendLine($"- {participant.DisplayName} (`{participant.CurrentHp}/{participant.MaxHp}`)");
            }

            var embed = new EmbedBuilder()
                .WithTitle("PANDORA 에너미 소환 완료")
                .WithColor(new Color(255, 120, 90))
                .WithDescription($"요청 에너미: **{requestedEnemy}**")
                .AddField("소환 수량", $"{participants.Count} / 요청 {requestedQuantity}", inline: true)
                .AddField("구분", "enemy", inline: true)
                .AddField("참가자", builder.ToString().TrimEnd(), inline: false)
                .WithFooter("PANDORA NETWORK / COMBAT PARTICIPANT")
                .WithCurrentTimestamp();

            if (participants.Count != requestedQuantity)
            {
                embed.AddField("수량 제한", $"현재 요청 {requestedQuantity} 중 {participants.Count}개만 처리했습니다.", inline: false);
            }

            return embed.Build();
        }

        private static Embed BuildCombatParticipantRemovedEmbed(CombatParticipantSummary participant)
        {
            return new EmbedBuilder()
                .WithTitle("PANDORA 전투 참가자 정리")
                .WithColor(new Color(255, 120, 90))
                .WithDescription($"**{participant.DisplayName}**")
                .AddField("참가자 ID", participant.Id.ToString(), inline: false)
                .AddField("구분", participant.Type, inline: true)
                .AddField("마지막 HP", $"{participant.CurrentHp} / {participant.MaxHp}", inline: true)
                .AddField("상태", participant.Status, inline: true)
                .WithFooter("PANDORA NETWORK / COMBAT PARTICIPANT")
                .WithCurrentTimestamp()
                .Build();
        }

        private static Embed BuildCombatCleanupEmbed(IReadOnlyList<CombatParticipantSummary> removed)
        {
            var builder = new StringBuilder();
            foreach (var participant in removed)
            {
                builder.AppendLine($"- {participant.DisplayName} (`{participant.CurrentHp}/{participant.MaxHp}`)");
            }

            return new EmbedBuilder()
                .WithTitle("PANDORA 전투 정리 결과")
                .WithColor(new Color(255, 140, 90))
                .WithDescription($"defeated enemy {removed.Count}명을 정리했습니다.")
                .AddField("정리 대상", builder.ToString().TrimEnd(), inline: false)
                .WithFooter("PANDORA NETWORK / COMBAT CLEANUP")
                .WithCurrentTimestamp()
                .Build();
        }

        private static Embed BuildActiveCombatHpEmbed(CombatParticipantHpResult result, string action, int amount)
        {
            var isHeal = string.Equals(action, "heal", StringComparison.OrdinalIgnoreCase);
            var label = isHeal ? "\uC804\uD22C \uD68C\uBCF5" : "\uC804\uD22C \uD53C\uD574";
            var syncText = string.Equals(result.Type, "player", StringComparison.OrdinalIgnoreCase)
                ? result.CharacterSynced ? "\uCE90\uB9AD\uD130 DB\uC640 \uCC38\uAC00\uC790 HP \uB3D9\uAE30\uD654" : "\uCC38\uAC00\uC790 HP\uB9CC \uBC18\uC601\uB428"
                : "\uD65C\uC131 \uCC38\uAC00\uC790\uB9CC \uBC18\uC601\uB428";

            return new EmbedBuilder()
                .WithTitle($"PROJECT:PANDORA | {label}")
                .WithColor(isHeal ? Color.Green : Color.Red)
                .WithDescription($"**{result.DisplayName}** \uC0C1\uD0DC\uB97C \uBCC0\uACBD\uD588\uC2B5\uB2C8\uB2E4.")
                .AddField("\uB300\uC0C1", $"{result.DisplayName}\n`{result.ParticipantId}`", inline: false)
                .AddField("\uAD6C\uBD84", result.Type, inline: true)
                .AddField("\uCC98\uB9AC", $"{label} `{amount}`", inline: true)
                .AddField("HP", $"`{result.OldHp} -> {result.CurrentHp} / {result.MaxHp}`", inline: true)
                .AddField("\uC0C1\uD0DC", result.Status, inline: true)
                .AddField("\uBC18\uC601", syncText, inline: true)
                .WithFooter("PANDORA NETWORK / ACTIVE COMBAT")
                .WithCurrentTimestamp()
                .Build();
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

        private static string FormatPercent(int value, int total)
        {
            if (total <= 0)
            {
                return "0.0%";
            }

            return $"{value * 100.0 / total:0.0}%";
        }

        private sealed record EnemyStatInfo(string Code, string KoreanName, int Value, int Modifier);

        private sealed record EnemyJudgementOutcome(string Label, string Text, Color Color);

        private sealed record DropSummaryItem(string ItemName, int Count);
    }
}








