using Discord;
using Discord.Interactions;
using PandoraBot.Repositories;
using System.Text;

namespace PandoraBot.Modules
{
    public class PlayerModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("검수상태", "내가 등록한 캐릭터들의 검수 상태를 확인합니다.")]
        public async Task ShowReviewStatus()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await PandoraRepositoryProvider.Characters.ListCharactersAsync(Context.User.Id.ToString());
                if (characters.Count == 0)
                {
                    await FollowupAsync("등록한 캐릭터가 없습니다. 먼저 `/등록`으로 원본 시트를 불러와 주세요.", ephemeral: true);
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle("[System:PANDORA] 검수 상태")
                    .WithColor(new Color(86, 190, 255))
                    .WithDescription($"{Context.User.Mention} 님의 캐릭터 등록 현황")
                    .WithFooter("pending: 검수 대기 / approved: 승인 / rejected: 반려")
                    .WithCurrentTimestamp();

                foreach (var character in characters)
                {
                    var reviewText = FormatReviewStatus(character.ReviewStatus);
                    embed.AddField(
                        character.CharacterName,
                        $"상태: `{reviewText}`\nHP: `{character.CurrentHp} / {character.MaxHp}`",
                        inline: false);
                }

                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyPlayerError(ex.Message), ephemeral: true);
            }
        }

        [SlashCommand("내정보", "현재 캐릭터 상태와 최근 판정 기록을 확인합니다.")]
        public async Task ShowMyInfo()
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var characters = await PandoraRepositoryProvider.Characters.ListCharactersAsync(Context.User.Id.ToString());
                if (characters.Count == 0)
                {
                    await FollowupAsync("등록한 캐릭터가 없습니다. 먼저 `/등록`으로 원본 시트를 불러와 주세요.", ephemeral: true);
                    return;
                }

                var selected = characters.FirstOrDefault(character => character.IsSelected);
                var recentLogs = await PandoraRepositoryProvider.Characters.ListRecentRollLogsAsync(Context.User.Id.ToString(), 3);

                var embed = new EmbedBuilder()
                    .WithTitle("[System:PANDORA] 내 정보")
                    .WithColor(new Color(60, 190, 255))
                    .WithDescription(Context.User.Mention)
                    .WithCurrentTimestamp();

                if (selected == null)
                {
                    embed.AddField("현재 선택", "선택한 캐릭터가 없습니다. `/선택`으로 사용할 캐릭터를 먼저 골라 주세요.", inline: false);
                }
                else
                {
                    embed.AddField(
                        "현재 선택",
                        $"**{selected.CharacterName}**\nHP `{selected.CurrentHp} / {selected.MaxHp}`\n검수 `{FormatReviewStatus(selected.ReviewStatus)}`",
                        inline: false);
                }

                var statusLines = characters
                    .Select(character =>
                    {
                        var marker = character.IsSelected ? "선택" : "대기";
                        return $"`{marker}` {character.CharacterName} | HP {character.CurrentHp}/{character.MaxHp} | {FormatReviewStatus(character.ReviewStatus)}";
                    });

                embed.AddField("보유 캐릭터", string.Join("\n", statusLines), inline: false);

                if (recentLogs.Count == 0)
                {
                    embed.AddField("최근 판정", "아직 기록된 판정이 없습니다.", inline: false);
                }
                else
                {
                    var builder = new StringBuilder();
                    foreach (var log in recentLogs)
                    {
                        builder.AppendLine($"{log.CreatedAt.LocalDateTime:yyyy-MM-dd HH:mm} | {log.CharacterName} {log.StatCode} {log.Total} | {log.Outcome}");
                    }

                    embed.AddField("최근 판정", builder.ToString(), inline: false);
                }

                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(ToFriendlyPlayerError(ex.Message), ephemeral: true);
            }
        }

        private static string FormatReviewStatus(string reviewStatus)
        {
            return reviewStatus switch
            {
                "pending" => "검수 대기",
                "approved" => "검수 완료",
                "rejected" => "검수 반려",
                _ => reviewStatus
            };
        }

        private static string ToFriendlyPlayerError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "조회 중 문제가 발생했습니다. 잠시 후 다시 시도해 주세요.";
            }

            if (message.Contains("PandoraDb connection string", StringComparison.OrdinalIgnoreCase))
            {
                return "플레이어 정보용 DB 연결이 아직 준비되지 않았습니다. 설정을 확인한 뒤 다시 시도해 주세요.";
            }

            if (message.Contains("No registered character", StringComparison.OrdinalIgnoreCase))
            {
                return "등록한 캐릭터를 찾을 수 없습니다. 먼저 `/등록`으로 원본 시트를 불러와 주세요.";
            }

            return $"조회 중 문제가 발생했습니다: {message}";
        }
    }
}
