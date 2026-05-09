namespace PandoraBot.Repositories;

public sealed class UnavailableCharacterRepository : ICharacterRepository
{
    private static InvalidOperationException CreateException()
        => new("PandoraDb connection string is not configured.");

    public Task<CharacterRecord> SelectCharacterAsync(string ownerDiscordId, string query) => Task.FromException<CharacterRecord>(CreateException());
    public Task<CharacterRecord> GetSelectedCharacterAsync(string ownerDiscordId) => Task.FromException<CharacterRecord>(CreateException());
    public Task<CharacterRecord> GetCharacterAsync(string ownerDiscordId, string query) => Task.FromException<CharacterRecord>(CreateException());
    public Task<IReadOnlyList<CharacterListItem>> ListCharactersAsync(string ownerDiscordId) => Task.FromException<IReadOnlyList<CharacterListItem>>(CreateException());
    public Task<int> ClearSelectedCharacterAsync(string ownerDiscordId) => Task.FromException<int>(CreateException());
    public Task<CharacterDeleteResult> DeleteCharacterAsync(string ownerDiscordId, string query) => Task.FromException<CharacterDeleteResult>(CreateException());
    public Task<IReadOnlyList<AdminCharacterListItem>> ListAllCharactersAsync(int limit = 25) => Task.FromException<IReadOnlyList<AdminCharacterListItem>>(CreateException());
    public Task<IReadOnlyList<AdminCharacterListItem>> ListReviewCharactersAsync(string status = "pending", int limit = 25) => Task.FromException<IReadOnlyList<AdminCharacterListItem>>(CreateException());
    public Task<IReadOnlyList<CharacterRecentRollLog>> ListRecentRollLogsAsync(string ownerDiscordId, int limit = 10) => Task.FromException<IReadOnlyList<CharacterRecentRollLog>>(CreateException());
    public Task<CharacterRecord> GetCharacterForAdminAsync(string query) => Task.FromException<CharacterRecord>(CreateException());
    public Task<CharacterHpUpdateResult> SetCharacterHpAsync(string query, int currentHp) => Task.FromException<CharacterHpUpdateResult>(CreateException());
    public Task<CharacterHpUpdateResult> AdjustCharacterHpAsync(string query, int amount, string action) => Task.FromException<CharacterHpUpdateResult>(CreateException());
    public Task<int> ClearSelectedCharacterForAdminAsync(string query) => Task.FromException<int>(CreateException());
    public Task<CharacterDeleteResult> DeleteCharacterForAdminAsync(string query) => Task.FromException<CharacterDeleteResult>(CreateException());
    public Task<CharacterReviewUpdateResult> SetReviewStatusAsync(string query, string status) => Task.FromException<CharacterReviewUpdateResult>(CreateException());
}
