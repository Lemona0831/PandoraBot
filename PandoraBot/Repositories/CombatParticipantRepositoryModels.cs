namespace PandoraBot.Repositories;

public sealed record CombatParticipantSummary(
    Guid Id,
    Guid CombatSessionId,
    string Type,
    string SourceId,
    string DisplayName,
    int CurrentHp,
    int MaxHp,
    string Status,
    string Memo,
    DateTimeOffset CreatedAt);

public sealed record CombatParticipantHpResult(
    Guid CombatSessionId,
    string ParticipantId,
    string Type,
    string SourceId,
    string DisplayName,
    int OldHp,
    int CurrentHp,
    int MaxHp,
    string Status,
    bool CharacterSynced);
