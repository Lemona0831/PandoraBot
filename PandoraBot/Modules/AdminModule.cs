using Discord;
using Discord.Interactions;
using PandoraBot.Models;
using PandoraBot.Services;
using System.Text;

namespace PandoraBot.Modules
{
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

                var builder = new StringBuilder();
                builder.AppendLine("```text");
                builder.AppendLine("PANDORA ADMIN / CHARACTER LIST");
                builder.AppendLine("--------------------------------");

                foreach (var character in characters)
                {
                    var selected = character.IsSelected ? "*" : " ";
                    builder.AppendLine($"{selected} row:{character.RowNumber} user:{character.UserId} / {character.CharacterName} HP {character.CurrentHp}/{character.MaxHp} status:{character.ReviewStatus}");
                }

                builder.AppendLine("--------------------------------");
                builder.AppendLine("* = selected");
                builder.Append("```");

                await FollowupAsync(builder.ToString(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리정보", "관리자용: 특정 캐릭터 정보를 조회합니다.")]
        public async Task ShowCharacterForAdmin(
            [Summary("유저ID", "Discord User ID")] string userId,
            [Summary("캐릭터", "캐릭터 이름")] string characterName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var hunter = await GoogleSheetService.Instance.GetCharacterForAdminAsync(userId, characterName);
                await FollowupAsync(embed: BuildHunterEmbed(hunter), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리체력", "관리자용: 특정 캐릭터의 현재 HP를 설정합니다.")]
        public async Task SetCharacterHp(
            [Summary("유저ID", "Discord User ID")] string userId,
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("현재HP", "변경할 현재 HP")] int currentHp)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.SetCharacterHpAsync(userId, characterName, currentHp);
                await GoogleSheetService.Instance.AppendAdminLogAsync(
                    "HP설정",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    userId,
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
            [Summary("유저ID", "Discord User ID")] string userId,
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("수치", "적용할 피해량")] int amount,
            [Summary("메모", "선택 사항: 피해 사유")] string? memo = null)
        {
            await AdjustHpAsync(userId, characterName, amount, "damage", memo);
        }

        [SlashCommand("회복", "관리자용: 특정 캐릭터를 회복합니다.")]
        public async Task HealCharacter(
            [Summary("유저ID", "Discord User ID")] string userId,
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("수치", "적용할 회복량")] int amount,
            [Summary("메모", "선택 사항: 회복 사유")] string? memo = null)
        {
            await AdjustHpAsync(userId, characterName, amount, "heal", memo);
        }

        [SlashCommand("관리선택해제", "관리자용: 특정 유저의 선택 캐릭터를 해제합니다.")]
        public async Task ClearUserSelection(
            [Summary("유저ID", "Discord User ID")] string userId)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.ClearSelectedCharacterForAdminAsync(userId);
                await GoogleSheetService.Instance.AppendAdminLogAsync(
                    "선택해제",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    userId,
                    "",
                    $"{result.ClearedCount} row(s) cleared");

                await FollowupAsync($"선택 상태 {result.ClearedCount}개를 해제했습니다.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리삭제", "관리자용: 특정 유저의 캐릭터를 삭제합니다.")]
        public async Task DeleteCharacterForAdmin(
            [Summary("유저ID", "Discord User ID")] string userId,
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("확인", "True로 설정하면 캐릭터를 삭제합니다.")] bool confirm = false)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                if (!confirm)
                {
                    await FollowupAsync(
                        $"`{characterName}` 삭제를 진행하려면 `/관리삭제 유저ID:{userId} 캐릭터:{characterName} 확인:True`로 다시 실행해주세요.",
                        ephemeral: true);
                    return;
                }

                var result = await GoogleSheetService.Instance.DeleteCharacterForAdminAsync(userId, characterName);
                await GoogleSheetService.Instance.AppendAdminLogAsync(
                    "삭제",
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    userId,
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
            [Summary("유저ID", "Discord User ID")] string userId,
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("메모", "선택 사항: 검수 메모")] string? memo = null)
        {
            await SetReviewStatusAsync(userId, characterName, "approved", memo);
        }

        [SlashCommand("관리반려", "관리자용: 캐릭터를 반려합니다.")]
        public async Task RejectCharacter(
            [Summary("유저ID", "Discord User ID")] string userId,
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("메모", "선택 사항: 반려 사유")] string? memo = null)
        {
            await SetReviewStatusAsync(userId, characterName, "rejected", memo);
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

                var builder = new StringBuilder();
                builder.AppendLine("```text");
                builder.AppendLine($"PANDORA ADMIN / REVIEW {status}");
                builder.AppendLine("--------------------------------");

                foreach (var character in characters)
                {
                    builder.AppendLine($"row:{character.RowNumber} user:{character.UserId} / {character.CharacterName} HP {character.CurrentHp}/{character.MaxHp}");
                }

                builder.Append("```");
                await FollowupAsync(builder.ToString(), ephemeral: true);
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
            [Summary("내용", "공지 내용")] string content)
        {
            await DeferAsync();

            try
            {
                var embed = new EmbedBuilder()
                    .WithTitle($"PROJECT:PANDORA | {title}")
                    .WithColor(new Color(60, 190, 255))
                    .WithDescription(content)
                    .AddField("분류", noticeType, inline: true)
                    .AddField("작성", Context.User.Mention, inline: true)
                    .WithFooter("PANDORA NETWORK / OPERATOR NOTICE")
                    .WithCurrentTimestamp()
                    .Build();

                await GoogleSheetService.Instance.AppendNoticeLogAsync(
                    noticeType,
                    title,
                    content,
                    Context.User.Id.ToString(),
                    Context.User.Username,
                    Context.Channel.Id.ToString());

                await FollowupAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        private async Task AdjustHpAsync(string userId, string characterName, int amount, string action, string? memo)
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
                    userId,
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
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        private async Task SetReviewStatusAsync(string userId, string characterName, string status, string? memo)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.SetCharacterReviewStatusAsync(
                    userId,
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

        private static string FormatModifier(int modifier)
        {
            return modifier >= 0 ? $"+{modifier}" : modifier.ToString();
        }
    }
}
