namespace PandoraBot.Repositories;

public interface ICombatParticipantRepository
{
    Task<CombatParticipantSummary> AddPlayerAsync(string guildId, string channelId, string characterName, string createdByDiscordId, string memo = "");
    Task<IReadOnlyList<CombatParticipantSummary>> AddEnemiesAsync(string guildId, string channelId, string enemyIdOrName, int quantity, string createdByDiscordId, string memo = "");
    Task<CombatParticipantHpResult> AdjustHpAsync(string guildId, string channelId, string participantIdOrName, int amount, string action, string actorDiscordId, string memo = "");
}
