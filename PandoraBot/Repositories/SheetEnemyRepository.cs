using PandoraBot.Services;
using PandoraShared.Models;

namespace PandoraBot.Repositories;

public sealed class SheetEnemyRepository : IEnemyRepository
{
    public Task<IReadOnlyList<EnemyRow>> GetEnemiesAsync()
        => GoogleSheetService.Instance.Enemies.GetEnemiesAsync();

    public Task<EnemySearchResult> GetEnemyByIdOrNameAsync(string idOrName)
        => GoogleSheetService.Instance.Enemies.GetEnemyByIdOrNameAsync(idOrName);

    public Task<EnemyRow> CreateEnemyAsync(EnemyCreateInput input)
        => throw new InvalidOperationException("에너미 추가는 PostgreSQL 모드에서만 지원합니다.");

    public Task<EnemyRow> UpdateEnemyAsync(string idOrName, EnemyEditInput input)
        => throw new InvalidOperationException("에너미 수정은 PostgreSQL 모드에서만 지원합니다.");

    public Task<EnemyRow> UpdateEnemyStatAsync(string idOrName, string statName, int value)
        => throw new InvalidOperationException("에너미 스탯 조작은 PostgreSQL 모드에서만 지원합니다.");
}
