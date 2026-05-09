namespace PandoraBot.Repositories;

public interface IAdminLogRepository
{
    Task AppendAdminLogAsync(
        string action,
        string adminUserId,
        string adminUsername,
        string targetUserId,
        string characterName,
        string detail);
}
