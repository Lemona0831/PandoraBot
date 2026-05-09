namespace PandoraBot.Repositories;

public sealed record CharacterRecord(
    Guid CharacterId,
    string OwnerDiscordId,
    string SourceSheetId,
    string SourceSheetUrl,
    string SourceDocumentTitle,
    string ImportedCharacterName,
    string DisplayName,
    string NormalizedDisplayName,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    int CurrentHp,
    int MaxHp,
    string ReviewStatus);

public sealed record CharacterListItem(
    Guid CharacterId,
    string CharacterName,
    int CurrentHp,
    int MaxHp,
    bool IsSelected,
    string ReviewStatus,
    string SourceSheetId,
    string SourceDocumentTitle);

public sealed record AdminCharacterListItem(
    Guid CharacterId,
    string UserId,
    string CharacterName,
    int CurrentHp,
    int MaxHp,
    bool IsSelected,
    string ReviewStatus,
    string SelectedByUserId,
    string SourceSheetId,
    string SourceDocumentTitle);

public sealed record CharacterHpUpdateResult(
    Guid CharacterId,
    string CharacterName,
    string UserId,
    int OldHp,
    int CurrentHp,
    int MaxHp);

public sealed record CharacterDeleteResult(Guid CharacterId, string CharacterName, string UserId);

public sealed record CharacterRecentRollLog(
    DateTimeOffset CreatedAt,
    string CharacterName,
    string StatCode,
    int Total,
    string Outcome);

public sealed record CharacterReviewUpdateResult(
    Guid CharacterId,
    string CharacterName,
    string UserId,
    string ReviewStatus);
