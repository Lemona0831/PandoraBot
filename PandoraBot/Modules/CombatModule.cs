using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PandoraBot.Repositories;
using System.Text;

namespace PandoraBot.Modules;

public class CombatModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("전투상태", "현재 채널의 활성 전투 세션 참가자 상태를 확인합니다.")]
    public async Task ShowCombatStatus()
    {
        await DeferAsync(ephemeral: true);

        try
        {
            if (Context.Guild is null)
            {
                await FollowupAsync("전투 관련 명령은 서버 채널에서만 사용할 수 있습니다.", ephemeral: true);
                return;
            }

            var session = await PandoraRepositoryProvider.CombatSessions.GetActiveCombatSessionAsync(
                Context.Guild.Id.ToString(),
                Context.Channel.Id.ToString());

            if (session is null)
            {
                await FollowupAsync("현재 채널에 활성 전투 세션이 없습니다. 먼저 `/전투시작`으로 세션을 열어주세요.", ephemeral: true);
                return;
            }

            var participants = await PandoraRepositoryProvider.CombatParticipants.GetParticipantsAsync(
                Context.Guild.Id.ToString(),
                Context.Channel.Id.ToString());

            var isAdminView = Context.User is SocketGuildUser guildUser &&
                guildUser.GuildPermissions.ManageGuild;

            var embed = isAdminView
                ? BuildAdminCombatStatusEmbed(session, participants)
                : BuildPlayerCombatStatusEmbed(session, participants);

            await FollowupAsync(embed: embed, ephemeral: true);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("활성 전투 세션", StringComparison.OrdinalIgnoreCase))
            {
                await FollowupAsync("현재 채널에 활성 전투 세션이 없습니다. 먼저 `/전투시작`으로 세션을 열어주세요.", ephemeral: true);
                return;
            }

            await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
        }
    }

    private static Embed BuildAdminCombatStatusEmbed(
        CombatSessionSummary session,
        IReadOnlyList<CombatParticipantSummary> participants)
    {
        var players = participants
            .Where(x => string.Equals(x.Type, "player", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DisplayName)
            .ToList();
        var enemies = participants
            .Where(x => string.Equals(x.Type, "enemy", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DisplayName)
            .ToList();

        var embed = new EmbedBuilder()
            .WithTitle("PANDORA | COMBAT STATUS (ADMIN)")
            .WithColor(new Color(90, 190, 255))
            .WithDescription($"**{session.Title}**")
            .AddField("세션", $"`{session.Id}`", inline: false)
            .AddField("참가자 수", $"{participants.Count}명", inline: true)
            .AddField("플레이어", $"{players.Count}명", inline: true)
            .AddField("에너미", $"{enemies.Count}명", inline: true)
            .WithFooter("PANDORA NETWORK / ADMIN COMBAT BOARD")
            .WithCurrentTimestamp();

        embed.AddField("플레이어", BuildAdminParticipantLines(players, includeHp: true), inline: false);
        embed.AddField("에너미", BuildAdminParticipantLines(enemies, includeHp: true), inline: false);

        return embed.Build();
    }

    private static Embed BuildPlayerCombatStatusEmbed(
        CombatSessionSummary session,
        IReadOnlyList<CombatParticipantSummary> participants)
    {
        var players = participants
            .Where(x => string.Equals(x.Type, "player", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DisplayName)
            .ToList();
        var enemies = participants
            .Where(x => string.Equals(x.Type, "enemy", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DisplayName)
            .ToList();

        var embed = new EmbedBuilder()
            .WithTitle("PANDORA | COMBAT STATUS")
            .WithColor(new Color(90, 190, 255))
            .WithDescription($"**{session.Title}**")
            .AddField("플레이어", BuildPlayerParticipantLines(players), inline: false)
            .AddField("에너미", BuildEnemyNameOnlyLines(enemies), inline: false)
            .WithFooter("PANDORA NETWORK / PLAYER COMBAT BOARD")
            .WithCurrentTimestamp();

        return embed.Build();
    }

    private static string BuildAdminParticipantLines(IReadOnlyList<CombatParticipantSummary> participants, bool includeHp)
    {
        if (participants.Count == 0)
        {
            return "- 없음";
        }

        var builder = new StringBuilder();
        foreach (var participant in participants)
        {
            builder.Append("- ")
                .Append(participant.DisplayName)
                .Append(" [").Append(participant.Status).Append(']');

            if (includeHp)
            {
                builder.Append(" `")
                    .Append(participant.CurrentHp)
                    .Append('/')
                    .Append(participant.MaxHp)
                    .Append('`');
            }

            builder.Append(" (`").Append(participant.Id).Append("`)")
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildPlayerParticipantLines(IReadOnlyList<CombatParticipantSummary> participants)
    {
        if (participants.Count == 0)
        {
            return "- 없음";
        }

        var builder = new StringBuilder();
        foreach (var participant in participants)
        {
            builder.Append("- ")
                .Append(participant.DisplayName)
                .Append(" `")
                .Append(participant.CurrentHp)
                .Append('/')
                .Append(participant.MaxHp)
                .Append("` [")
                .Append(participant.Status)
                .AppendLine("]");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildEnemyNameOnlyLines(IReadOnlyList<CombatParticipantSummary> participants)
    {
        if (participants.Count == 0)
        {
            return "- 없음";
        }

        var builder = new StringBuilder();
        foreach (var participant in participants)
        {
            builder.Append("- ")
                .Append(participant.DisplayName)
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
