namespace PandoraBot.Repositories;

public sealed record CombatSessionSummary(
    Guid Id,
    string GuildId,
    string ChannelId,
    string Title,
    string Status,
    string CreatedByDiscordId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndedAt,
    string Memo);
