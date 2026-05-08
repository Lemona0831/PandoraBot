namespace PandoraBot.Repositories;

public sealed record UnifiedLogEntry(
    string Category,
    string Action,
    string Target,
    string Summary,
    DateTimeOffset CreatedAt);

