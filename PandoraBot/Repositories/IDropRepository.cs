using PandoraShared.Models;

namespace PandoraBot.Repositories;

public interface IDropRepository
{
    Task<IReadOnlyList<EnemyDropRow>> GetEnemyDropsAsync();
    Task<IReadOnlyList<EnemyDropSettingRow>> GetEnemyDropSettingsAsync();
    Task<DropRollResult> RollDropAsync(string enemyId, bool writeLog = true);
    Task<DropTestResult> TestDropAsync(string enemyId);
}
