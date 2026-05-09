using PandoraShared.Models;

namespace PandoraBot.Repositories;

public interface IEnemyRepository
{
    Task<IReadOnlyList<EnemyRow>> GetEnemiesAsync();
    Task<EnemySearchResult> GetEnemyByIdOrNameAsync(string idOrName);
    Task<EnemyRow> CreateEnemyAsync(EnemyCreateInput input);
    Task<EnemyRow> UpdateEnemyAsync(string idOrName, EnemyEditInput input);
    Task<EnemyRow> UpdateEnemyStatAsync(string idOrName, string statName, int value);
    Task<EnemyRow> SetEnemyActiveAsync(string idOrName, bool isActive);
}
