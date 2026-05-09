namespace PandoraBot.Repositories;

public sealed class UnavailableLogRepository : ILogRepository
{
    public Task AppendRollLogAsync(
        Guid? characterId,
        string characterDisplayName,
        string statName,
        int dice1,
        int dice2,
        int modifier,
        int total,
        string resultTier)
        => throw new InvalidOperationException("PandoraDb connection string is not configured.");

    public Task<IReadOnlyList<UnifiedLogEntry>> GetLogsAsync(string target, int limit)
        => throw new InvalidOperationException("PandoraDb connection string is not configured.");
}

