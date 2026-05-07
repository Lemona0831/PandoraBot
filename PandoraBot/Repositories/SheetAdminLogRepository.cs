using PandoraBot.Services;

namespace PandoraBot.Repositories;

public sealed class SheetAdminLogRepository : IAdminLogRepository
{
    public Task AppendAdminLogAsync(
        string action,
        string adminUserId,
        string adminUsername,
        string targetUserId,
        string characterName,
        string detail)
        => GoogleSheetService.Instance.AppendAdminLogAsync(
            action,
            adminUserId,
            adminUsername,
            targetUserId,
            characterName,
            detail);
}
