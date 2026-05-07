using PandoraShared.Models;

namespace PandoraBot.Repositories;

public interface IEnemyRepository
{
    Task<IReadOnlyList<EnemyRow>> GetEnemiesAsync();
    Task<EnemySearchResult> GetEnemyByIdOrNameAsync(string idOrName);
}
