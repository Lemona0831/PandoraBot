namespace PandoraBot.Repositories;

public sealed class UnavailableAdminLogRepository : IAdminLogRepository
{
    public Task AppendAdminLogAsync(
        string action,
        string adminUserId,
        string adminUsername,
        string targetUserId,
        string characterName,
        string detail)
        => Task.CompletedTask;
}