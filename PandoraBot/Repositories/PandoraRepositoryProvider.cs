namespace PandoraBot.Repositories;

public static class PandoraRepositoryProvider
{
    public static IEnemyRepository Enemies { get; private set; } = new UnavailableEnemyRepository();
    public static IDropRepository Drops { get; private set; } = new UnavailableDropRepository();
    public static IAdminLogRepository AdminLogs { get; private set; } = new UnavailableAdminLogRepository();
    public static ILogRepository Logs { get; private set; } = new UnavailableLogRepository();
    public static ICombatSessionRepository CombatSessions { get; private set; } = new UnavailableCombatSessionRepository();
    public static ICombatParticipantRepository CombatParticipants { get; private set; } = new UnavailableCombatParticipantRepository();
    public static ICharacterRepository Characters { get; private set; } = new UnavailableCharacterRepository();

    public static void Initialize(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Enemies = new UnavailableEnemyRepository();
            Drops = new UnavailableDropRepository();
            AdminLogs = new UnavailableAdminLogRepository();
            Logs = new UnavailableLogRepository();
            CombatSessions = new UnavailableCombatSessionRepository();
            CombatParticipants = new UnavailableCombatParticipantRepository();
            Characters = new UnavailableCharacterRepository();
            return;
        }

        Enemies = new DbEnemyRepository(connectionString);
        Drops = new DbDropRepository(connectionString);
        AdminLogs = new DbAdminLogRepository(connectionString);
        Logs = new DbLogRepository(connectionString);
        CombatSessions = new DbCombatSessionRepository(connectionString);
        CombatParticipants = new DbCombatParticipantRepository(connectionString);
        Characters = new DbCharacterRepository(connectionString);
    }
}
