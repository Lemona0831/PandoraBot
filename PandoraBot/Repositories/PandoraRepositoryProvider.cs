namespace PandoraBot.Repositories;

public static class PandoraRepositoryProvider
{
    public static IEnemyRepository Enemies { get; private set; } = new SheetEnemyRepository();
    public static IDropRepository Drops { get; private set; } = new SheetDropRepository();
    public static IAdminLogRepository AdminLogs { get; private set; } = new SheetAdminLogRepository();
    public static ICombatSessionRepository CombatSessions { get; private set; } = new UnavailableCombatSessionRepository();
    public static ICombatParticipantRepository CombatParticipants { get; private set; } = new UnavailableCombatParticipantRepository();

    public static void Initialize(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Enemies = new SheetEnemyRepository();
            Drops = new SheetDropRepository();
            AdminLogs = new SheetAdminLogRepository();
            CombatSessions = new UnavailableCombatSessionRepository();
            CombatParticipants = new UnavailableCombatParticipantRepository();
            return;
        }

        Enemies = new DbEnemyRepository(connectionString);
        Drops = new DbDropRepository(connectionString);
        AdminLogs = new DbAdminLogRepository(connectionString);
        CombatSessions = new DbCombatSessionRepository(connectionString);
        CombatParticipants = new DbCombatParticipantRepository(connectionString);
    }
}
