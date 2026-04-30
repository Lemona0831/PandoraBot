using Discord;
using Discord.Interactions;
using PandoraBot.Models;
using PandoraBot.Services;
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

                var builder = new StringBuilder();
                builder.AppendLine("```text");
                builder.AppendLine("PANDORA ADMIN / CHARACTER LIST");
                builder.AppendLine("--------------------------------");

                foreach (var character in characters)
                {
                    var selected = character.IsSelected ? "*" : " ";
                    builder.AppendLine($"{selected} row:{character.RowNumber} user:{character.UserId} / {character.CharacterName} HP {character.CurrentHp}/{character.MaxHp}");
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

        [SlashCommand("관리체력", "관리자용: 특정 캐릭터의 현재 HP를 조정합니다.")]
        public async Task SetCharacterHp(
            [Summary("유저ID", "Discord User ID")] string userId,
            [Summary("캐릭터", "캐릭터 이름")] string characterName,
            [Summary("현재HP", "변경할 현재 HP")] int currentHp)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.SetCharacterHpAsync(userId, characterName, currentHp);
                await FollowupAsync(
                    $"`{result.CharacterName}` HP를 `{result.CurrentHp} / {result.MaxHp}`로 변경했습니다. (row {result.RowNumber})",
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("관리선택해제", "관리자용: 특정 유저의 선택 캐릭터를 해제합니다.")]
        public async Task ClearUserSelection(
            [Summary("유저ID", "Discord User ID")] string userId)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var result = await GoogleSheetService.Instance.ClearSelectedCharacterForAdminAsync(userId);
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
                await FollowupAsync($"`{result.CharacterName}` 캐릭터 등록 정보를 삭제했습니다. (row {result.RowNumber})", ephemeral: true);
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
