namespace PandoraShared.Models;

public sealed record EnemyRow(
    int RowNumber,
    string EnemyId,
    string Region,
    string Name,
    string Category,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    string DamageFormula,
    int Dp,
    int CurrentHp,
    int MaxHp,
    string Description,
    string IsEnabled);

public sealed record EnemyCreateInput(
    string Region,
    string Name,
    string Category,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    string DamageFormula,
    int Dp,
    int MaxHp,
    string Description);

public sealed record EnemyEditInput(
    string Region,
    string Name,
    string Category,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    string DamageFormula,
    int Dp,
    int MaxHp,
    string Description,
    bool IsEnabled);

public sealed record EnemySearchResult(
    EnemyRow? Enemy,
    IReadOnlyList<EnemyRow> Matches,
    string? ErrorMessage)
{
    public bool Found => Enemy is not null;
    public bool HasMultipleMatches => Matches.Count > 1;
}
