namespace PandoraBot.Repositories;

public sealed class UnavailableCombatParticipantRepository : ICombatParticipantRepository
{
    private const string Message = "전투 참가자 기능은 PandoraDb 연결이 설정된 상태에서만 사용할 수 있습니다.";

    public Task<CombatParticipantSummary> AddPlayerAsync(string guildId, string channelId, string characterName, string createdByDiscordId, string memo = "")
        => Task.FromException<CombatParticipantSummary>(new InvalidOperationException(Message));

    public Task<IReadOnlyList<CombatParticipantSummary>> AddEnemiesAsync(string guildId, string channelId, string enemyIdOrName, int quantity, string createdByDiscordId, string memo = "")
        => Task.FromException<IReadOnlyList<CombatParticipantSummary>>(new InvalidOperationException(Message));

    public Task<CombatParticipantHpResult> AdjustHpAsync(string guildId, string channelId, string participantIdOrName, int amount, string action, string actorDiscordId, string memo = "")
        => Task.FromException<CombatParticipantHpResult>(new InvalidOperationException(Message));
}
