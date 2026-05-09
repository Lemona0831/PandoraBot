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

    Task<CombatSessionEndResult> EndCombatSessionAsync(string guildId, string channelId, string endedByDiscordId);

    Task<bool> AppendLogIfActiveAsync(
        string guildId,
        string channelId,
        string actorDiscordId,
        string actionType,
        string targetName,
        string beforeValue,
        string afterValue,
        string message);
}
