using PandoraBot.Services;
using PandoraShared.Models;

namespace PandoraBot.Repositories;

public sealed class SheetEnemyRepository : IEnemyRepository
{
    public Task<IReadOnlyList<EnemyRow>> GetEnemiesAsync()
        => GoogleSheetService.Instance.Enemies.GetEnemiesAsync();

    public Task<EnemySearchResult> GetEnemyByIdOrNameAsync(string idOrName)
        => GoogleSheetService.Instance.Enemies.GetEnemyByIdOrNameAsync(idOrName);
}
