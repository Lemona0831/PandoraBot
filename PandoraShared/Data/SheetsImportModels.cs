namespace PandoraShared.Data;

public sealed record SheetsImportOptions(
    bool DryRun,
    string SpreadsheetId);

public sealed class SheetsImportResult
{
    public bool DryRun { get; init; }
    public string SpreadsheetId { get; init; } = "";
    public int CharacterCount { get; set; }
    public int CharacterSelectionCount { get; set; }
    public int RollLogCount { get; set; }
    public int AdminLogCount { get; set; }
    public int NoticeLogCount { get; set; }
    public int EnemyCount { get; set; }
    public int EnemyDropCount { get; set; }
    public int EnemyDropSettingCount { get; set; }
    public int CombatSessionCount { get; set; }
    public int CombatParticipantCount { get; set; }
    public int CombatLogCount { get; set; }
    public List<string> MigrationNotes { get; } = new();
}
