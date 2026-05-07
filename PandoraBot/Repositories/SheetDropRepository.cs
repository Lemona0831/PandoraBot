using PandoraBot.Services;
using PandoraShared.Models;

namespace PandoraBot.Repositories;

public sealed class SheetDropRepository : IDropRepository
{
    public Task<IReadOnlyList<EnemyDropRow>> GetEnemyDropsAsync()
        => GoogleSheetService.Instance.Drops.GetEnemyDropsAsync();

    public Task<IReadOnlyList<EnemyDropSettingRow>> GetEnemyDropSettingsAsync()
        => GoogleSheetService.Instance.Drops.GetEnemyDropSettingsAsync();

    public Task<DropRollResult> RollDropAsync(string enemyId, bool writeLog = true)
        => GoogleSheetService.Instance.Drops.RollDropAsync(enemyId, writeLog);

    public Task<DropTestResult> TestDropAsync(string enemyId)
        => GoogleSheetService.Instance.Drops.TestDropAsync(enemyId);
}
