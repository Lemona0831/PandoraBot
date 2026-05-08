using PandoraShared.Models;

namespace PandoraBot.Repositories;

public sealed class UnavailableDropRepository : IDropRepository
{
    private static InvalidOperationException CreateException()
        => new("PandoraDb 연결이 설정되지 않았습니다. appsettings.Development.json 또는 ConnectionStrings:PandoraDb 값을 먼저 확인해 주세요.");

    public Task<IReadOnlyList<EnemyDropRow>> GetEnemyDropsAsync() => throw CreateException();
    public Task<IReadOnlyList<EnemyDropSettingRow>> GetEnemyDropSettingsAsync() => throw CreateException();
    public Task<DropRollResult> RollDropAsync(string enemyId, bool writeLog = true) => throw CreateException();
    public Task<DropTestResult> TestDropAsync(string enemyId) => throw CreateException();
    public Task<EnemyDropRow> CreateDropAsync(EnemyDropCreateInput input) => throw CreateException();
}