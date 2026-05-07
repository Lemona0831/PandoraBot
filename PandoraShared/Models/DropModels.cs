namespace PandoraShared.Models;

public sealed record EnemyDropRow(
    int RowNumber,
    string EnemyId,
    string ItemName,
    int Chance,
    int MinCount,
    int MaxCount,
    int Weight,
    string Rarity,
    string Tag,
    string Memo);

public sealed record EnemyDropCreateInput(
    string EnemyId,
    string ItemName,
    int Chance,
    int MinCount,
    int MaxCount,
    int Weight,
    string Rarity,
    string Tag,
    string Memo);

public sealed record EnemyDropSettingRow(
    int RowNumber,
    string EnemyId,
    int DropRate,
    int DropCount,
    bool AllowDuplicate,
    string Memo);

public sealed record EnemyDropSettingInput(
    string EnemyId,
    int DropRate,
    int DropCount,
    string Memo);

public sealed record DropRollItem(
    string ItemName,
    int Count,
    int Chance,
    string Rarity,
    string Tag);

public sealed record DropRollResult(
    string EnemyId,
    string EnemyName,
    bool Occurred,
    int OccurrenceRoll,
    int DropRate,
    IReadOnlyList<DropRollItem> Items,
    string Message);

public sealed record DropTestResult(
    string Message,
    DropRollResult Result);
