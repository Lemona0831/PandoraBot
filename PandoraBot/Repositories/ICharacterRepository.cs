namespace PandoraBot.Repositories;

public interface ICharacterRepository
{
    Task<CharacterRecord> SelectCharacterAsync(string ownerDiscordId, string query);
    Task<CharacterRecord> GetSelectedCharacterAsync(string ownerDiscordId);
    Task<CharacterRecord> GetCharacterAsync(string ownerDiscordId, string query);
    Task<IReadOnlyList<CharacterListItem>> ListCharactersAsync(string ownerDiscordId);
    Task<int> ClearSelectedCharacterAsync(string ownerDiscordId);
    Task<CharacterDeleteResult> DeleteCharacterAsync(string ownerDiscordId, string query);
    Task<IReadOnlyList<AdminCharacterListItem>> ListAllCharactersAsync(int limit = 25);
    Task<IReadOnlyList<AdminCharacterListItem>> ListReviewCharactersAsync(string status = "pending", int limit = 25);
    Task<IReadOnlyList<CharacterRecentRollLog>> ListRecentRollLogsAsync(string ownerDiscordId, int limit = 10);
    Task<CharacterRecord> GetCharacterForAdminAsync(string query);
    Task<CharacterHpUpdateResult> SetCharacterHpAsync(string query, int currentHp);
    Task<CharacterHpUpdateResult> AdjustCharacterHpAsync(string query, int amount, string action);
    Task<int> ClearSelectedCharacterForAdminAsync(string query);
    Task<CharacterDeleteResult> DeleteCharacterForAdminAsync(string query);
    Task<CharacterReviewUpdateResult> SetReviewStatusAsync(string query, string status);
}
