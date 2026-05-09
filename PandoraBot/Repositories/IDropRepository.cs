using PandoraShared.Models;

namespace PandoraBot.Repositories;

public interface IDropRepository
{
    Task<IReadOnlyList<EnemyDropRow>> GetEnemyDropsAsync(bool includeDeleted = false);
    Task<IReadOnlyList<EnemyDropSettingRow>> GetEnemyDropSettingsAsync();
    Task<DropRollResult> RollDropAsync(string enemyId, bool writeLog = true);
    Task<DropTestResult> TestDropAsync(string enemyId);
    Task<EnemyDropRow> CreateDropAsync(EnemyDropCreateInput input);
    Task<EnemyDropRow> UpdateDropAsync(string enemyId, string itemName, EnemyDropCreateInput input);
    Task<EnemyDropRow> DeleteDropAsync(string enemyId, string itemName);
    Task<EnemyDropSettingRow> UpsertDropSettingAsync(EnemyDropSettingInput input, bool allowDuplicate);
}
