namespace PandoraBot.Models
{
    public sealed record ActiveCombatParticipant(
        int RowNumber,
        string ParticipantId,
        string Type,
        string SourceId,
        string DisplayName,
        int CurrentHp,
        int MaxHp,
        string Status,
        string CreatedBy,
        string CreatedAt,
        string Memo);
}
