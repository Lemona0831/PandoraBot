using PandoraShared.Models;

namespace PandoraBot.Repositories;

public sealed class UnavailableEnemyRepository : IEnemyRepository
{
    private static InvalidOperationException CreateException()
        => new("PandoraDb 연결이 설정되지 않았습니다. appsettings.Development.json 또는 ConnectionStrings:PandoraDb 값을 먼저 확인해 주세요.");

    public Task<IReadOnlyList<EnemyRow>> GetEnemiesAsync() => throw CreateException();
    public Task<EnemySearchResult> GetEnemyByIdOrNameAsync(string idOrName) => throw CreateException();
    public Task<EnemyRow> CreateEnemyAsync(EnemyCreateInput input) => throw CreateException();
    public Task<EnemyRow> UpdateEnemyAsync(string idOrName, EnemyEditInput input) => throw CreateException();
    public Task<EnemyRow> UpdateEnemyStatAsync(string idOrName, string statName, int value) => throw CreateException();
}