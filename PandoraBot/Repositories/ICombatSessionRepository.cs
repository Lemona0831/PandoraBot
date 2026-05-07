namespace PandoraBot.Repositories;

public interface ICombatSessionRepository
{
    Task<CombatSessionSummary> StartCombatSessionAsync(
        string guildId,
        string channelId,
        string title,
        string createdByDiscordId,
        string memo = "");

    Task<CombatSessionSummary?> GetActiveCombatSessionAsync(string guildId, string channelId);

    Task<CombatSessionSummary> EndCombatSessionAsync(string guildId, string channelId);
}
