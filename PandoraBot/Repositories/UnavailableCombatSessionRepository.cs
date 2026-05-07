namespace PandoraBot.Repositories;

public sealed class UnavailableCombatSessionRepository : ICombatSessionRepository
{
    private const string Message = "전투 세션 기능은 PandoraDb 연결이 설정된 상태에서만 사용할 수 있습니다.";

    public Task<CombatSessionSummary> StartCombatSessionAsync(
        string guildId,
        string channelId,
        string title,
        string createdByDiscordId,
        string memo = "")
        => Task.FromException<CombatSessionSummary>(new InvalidOperationException(Message));

    public Task<CombatSessionSummary?> GetActiveCombatSessionAsync(string guildId, string channelId)
        => Task.FromException<CombatSessionSummary?>(new InvalidOperationException(Message));

    public Task<CombatSessionSummary> EndCombatSessionAsync(string guildId, string channelId)
        => Task.FromException<CombatSessionSummary>(new InvalidOperationException(Message));
}
