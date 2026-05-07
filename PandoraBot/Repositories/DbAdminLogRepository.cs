using PandoraShared.Data;

namespace PandoraBot.Repositories;

public sealed class DbAdminLogRepository : IAdminLogRepository
{
    private readonly string connectionString;

    public DbAdminLogRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task AppendAdminLogAsync(
        string action,
        string adminUserId,
        string adminUsername,
        string targetUserId,
        string characterName,
        string detail)
    {
        await using var db = PandoraDbContextFactory.CreateOrNull(connectionString)
            ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");

        db.AdminLogs.Add(new AdminLogEntity
        {
            Id = Guid.NewGuid(),
            AdminDiscordId = string.IsNullOrWhiteSpace(adminUserId) ? adminUsername : adminUserId,
            ActionType = action,
            TargetType = string.IsNullOrWhiteSpace(targetUserId) ? "system" : "user",
            TargetId = string.IsNullOrWhiteSpace(targetUserId) ? characterName : targetUserId,
            BeforeValue = "",
            AfterValue = detail,
            Message = $"{characterName} | {detail}",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
