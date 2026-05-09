namespace PandoraBot.Repositories;

public interface ILogRepository
{
    Task AppendRollLogAsync(
        Guid? characterId,
        string characterDisplayName,
        string statName,
        int dice1,
        int dice2,
        int modifier,
        int total,
        string resultTier);

    Task<IReadOnlyList<UnifiedLogEntry>> GetLogsAsync(string target, int limit);
}

