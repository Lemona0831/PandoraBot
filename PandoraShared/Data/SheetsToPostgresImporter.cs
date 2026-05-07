using Google.Apis.Sheets.v4;
using PandoraShared.Models;
using PandoraShared.Services;
using Microsoft.EntityFrameworkCore;

namespace PandoraShared.Data;

public sealed class SheetsToPostgresImporter
{
    private const string CharacterStorageSheet = "캐릭터 저장소";
    private const string SelectionSheet = "선택 상태";
    private const string JudgementLogSheet = "판정 로그";
    private const string AdminLogSheet = "관리 로그";
    private const string NoticeLogSheet = "공지 로그";

    private readonly SheetsService sheetsService;
    private readonly string spreadsheetId;
    private readonly PandoraDbContext? dbContext;
    private readonly EnemyService enemyService;
    private readonly DropService dropService;

    public SheetsToPostgresImporter(SheetsService sheetsService, string spreadsheetId, PandoraDbContext? dbContext)
    {
        this.sheetsService = sheetsService;
        this.spreadsheetId = spreadsheetId;
        this.dbContext = dbContext;
        enemyService = new EnemyService(sheetsService, spreadsheetId);
        dropService = new DropService(sheetsService, spreadsheetId, enemyService);
    }

    public async Task<SheetsImportResult> RunAsync(SheetsImportOptions options, CancellationToken cancellationToken = default)
    {
        var result = new SheetsImportResult
        {
            DryRun = options.DryRun,
            SpreadsheetId = options.SpreadsheetId
        };

        var characterRows = await ReadSheetRowsAsync(CharacterStorageSheet, "A2:N", cancellationToken);
        var selectionRows = await ReadSheetRowsAsync(SelectionSheet, "A2:D", cancellationToken);
        var judgementRows = await ReadSheetRowsAsync(JudgementLogSheet, "A2:M", cancellationToken);
        var adminRows = await ReadSheetRowsAsync(AdminLogSheet, "A2:G", cancellationToken);
        var noticeRows = await ReadSheetRowsAsync(NoticeLogSheet, "A2:G", cancellationToken);
        var enemies = await enemyService.GetEnemiesAsync();
        var enemyDrops = await dropService.GetEnemyDropsAsync();
        var enemyDropSettings = await dropService.GetEnemyDropSettingsAsync();

        var characters = BuildCharacters(characterRows, result);
        var characterBySelectionKey = characters.ToDictionary(
            character => BuildSelectionKey(character.DiscordUserId, character.DisplayName),
            character => character,
            StringComparer.Ordinal);
        var characterByRollKey = characters.ToDictionary(
            character => BuildSelectionKey(character.DiscordUserId, character.ImportedCharacterName),
            character => character,
            StringComparer.Ordinal);

        var selections = BuildCharacterSelections(selectionRows, characterBySelectionKey, result);
        var rollLogs = BuildRollLogs(judgementRows, characterByRollKey, result);
        var adminLogs = BuildAdminLogs(adminRows, noticeRows, result);
        var dbEnemies = BuildEnemies(enemies, result);
        var enemyByCode = dbEnemies.ToDictionary(enemy => enemy.EnemyCode, StringComparer.OrdinalIgnoreCase);
        var drops = BuildEnemyDrops(enemyDrops, enemyByCode, result);
        var dropSettings = BuildEnemyDropSettings(enemyDropSettings, enemyByCode, result);

        result.CharacterCount = characters.Count;
        result.CharacterSelectionCount = selections.Count;
        result.RollLogCount = rollLogs.Count;
        result.AdminLogCount = adminRows.Count;
        result.NoticeLogCount = noticeRows.Count;
        result.EnemyCount = dbEnemies.Count;
        result.EnemyDropCount = drops.Count;
        result.EnemyDropSettingCount = dropSettings.Count;
        result.CombatSessionCount = 0;
        result.CombatParticipantCount = 0;
        result.CombatLogCount = 0;

        if (options.DryRun)
        {
            return result;
        }

        if (dbContext is null)
        {
            throw new InvalidOperationException("PandoraDb connection is required for apply mode.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ReplaceAllDataAsync(
            characters,
            selections,
            rollLogs,
            adminLogs,
            dbEnemies,
            drops,
            dropSettings,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private async Task ReplaceAllDataAsync(
        IReadOnlyList<CharacterEntity> characters,
        IReadOnlyList<CharacterSelectionEntity> selections,
        IReadOnlyList<RollLogEntity> rollLogs,
        IReadOnlyList<AdminLogEntity> adminLogs,
        IReadOnlyList<EnemyEntity> enemies,
        IReadOnlyList<EnemyDropEntity> drops,
        IReadOnlyList<EnemyDropSettingEntity> dropSettings,
        CancellationToken cancellationToken)
    {
        await ClearTableAsync(dbContext!.CombatLogs, cancellationToken);
        await ClearTableAsync(dbContext.CombatParticipants, cancellationToken);
        await ClearTableAsync(dbContext.CombatSessions, cancellationToken);
        await ClearTableAsync(dbContext.EnemyDrops, cancellationToken);
        await ClearTableAsync(dbContext.EnemyDropSettings, cancellationToken);
        await ClearTableAsync(dbContext.Enemies, cancellationToken);
        await ClearTableAsync(dbContext.RollLogs, cancellationToken);
        await ClearTableAsync(dbContext.AdminLogs, cancellationToken);
        await ClearTableAsync(dbContext.CharacterSelections, cancellationToken);
        await ClearTableAsync(dbContext.Characters, cancellationToken);

        await dbContext.Characters.AddRangeAsync(characters, cancellationToken);
        await dbContext.CharacterSelections.AddRangeAsync(selections, cancellationToken);
        await dbContext.RollLogs.AddRangeAsync(rollLogs, cancellationToken);
        await dbContext.AdminLogs.AddRangeAsync(adminLogs, cancellationToken);
        await dbContext.Enemies.AddRangeAsync(enemies, cancellationToken);
        await dbContext.EnemyDrops.AddRangeAsync(drops, cancellationToken);
        await dbContext.EnemyDropSettings.AddRangeAsync(dropSettings, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ClearTableAsync<TEntity>(DbSet<TEntity> set, CancellationToken cancellationToken)
        where TEntity : class
    {
        await set.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<IList<object>>> ReadSheetRowsAsync(string sheetName, string range, CancellationToken cancellationToken)
    {
        var response = await sheetsService.Spreadsheets.Values.Get(
            spreadsheetId,
            $"{RangeSheet(sheetName)}!{range}").ExecuteAsync(cancellationToken);

        return (response.Values ?? new List<IList<object>>()).ToList();
    }

    private static List<CharacterEntity> BuildCharacters(
        IReadOnlyList<IList<object>> rows,
        SheetsImportResult result)
    {
        var characters = new List<CharacterEntity>();

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var userId = GetString(row, 0);
            var characterName = GetString(row, 1);
            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(characterName))
            {
                continue;
            }

            var sourceSheetId = GetString(row, 12);
            var sourceDocumentTitle = GetString(row, 13);

            if (string.IsNullOrWhiteSpace(sourceSheetId))
            {
                sourceSheetId = $"legacy:{userId}:{Normalize(characterName)}:{index + 2}";
                result.MigrationNotes.Add($"characters row {index + 2}: missing source_sheet_id, synthesized '{sourceSheetId}'.");
            }

            if (!string.IsNullOrWhiteSpace(sourceDocumentTitle))
            {
                result.MigrationNotes.Add($"characters row {index + 2}: source_document_title approximated from legacy source sheet name '{sourceDocumentTitle}'.");
            }
            else
            {
                sourceDocumentTitle = characterName;
                result.MigrationNotes.Add($"characters row {index + 2}: missing source_document_title, defaulted to display/imported character name.");
            }

            characters.Add(new CharacterEntity
            {
                Id = Guid.NewGuid(),
                DiscordUserId = userId,
                SourceSheetId = sourceSheetId,
                SourceSheetUrl = BuildSheetUrl(sourceSheetId),
                SourceDocumentTitle = sourceDocumentTitle,
                ImportedCharacterName = characterName,
                DisplayName = characterName,
                NormalizedDisplayName = Normalize(characterName),
                Strength = GetInt(row, 2),
                Dexterity = GetInt(row, 3),
                Constitution = GetInt(row, 4),
                Intelligence = GetInt(row, 5),
                Wisdom = GetInt(row, 6),
                Charisma = GetInt(row, 7),
                MaxHp = Math.Max(1, GetInt(row, 8)),
                CurrentHp = ClampHp(GetInt(row, 9), Math.Max(1, GetInt(row, 8))),
                ReviewStatus = NormalizeReviewStatus(GetString(row, 11)),
                CreatedAt = ParseTimestamp(GetString(row, 2), fallbackToNow: true),
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        return characters;
    }

    private static List<CharacterSelectionEntity> BuildCharacterSelections(
        IReadOnlyList<IList<object>> rows,
        IReadOnlyDictionary<string, CharacterEntity> characterBySelectionKey,
        SheetsImportResult result)
    {
        var selections = new List<CharacterSelectionEntity>();

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var userId = GetString(row, 0);
            var characterName = GetString(row, 1);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(characterName))
            {
                continue;
            }

            var key = BuildSelectionKey(userId, characterName);
            if (!characterBySelectionKey.TryGetValue(key, out var character))
            {
                result.MigrationNotes.Add($"selection row {index + 2}: could not match '{characterName}' for user '{userId}', skipped.");
                continue;
            }

            selections.Add(new CharacterSelectionEntity
            {
                Id = Guid.NewGuid(),
                DiscordUserId = userId,
                CharacterId = character.Id,
                SelectedAt = ParseTimestamp(GetString(row, 2), fallbackToNow: true)
            });
        }

        return selections;
    }

    private static List<RollLogEntity> BuildRollLogs(
        IReadOnlyList<IList<object>> rows,
        IReadOnlyDictionary<string, CharacterEntity> characterByRollKey,
        SheetsImportResult result)
    {
        var logs = new List<RollLogEntity>();

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Count == 0)
            {
                continue;
            }

            var userId = GetString(row, 3);
            var characterName = GetString(row, 5);
            var statName = GetString(row, 7);
            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(characterName) && string.IsNullOrWhiteSpace(statName))
            {
                continue;
            }

            CharacterEntity? character = null;
            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(characterName))
            {
                characterByRollKey.TryGetValue(BuildSelectionKey(userId, characterName), out character);
            }

            if (character is null)
            {
                result.MigrationNotes.Add($"roll_logs row {index + 2}: could not match character '{characterName}' for user '{userId}', imported without character_id.");
            }

            logs.Add(new RollLogEntity
            {
                Id = Guid.NewGuid(),
                CharacterId = character?.Id,
                CharacterDisplayName = characterName,
                StatName = string.IsNullOrWhiteSpace(statName) ? GetString(row, 6) : statName,
                Dice1 = GetInt(row, 8),
                Dice2 = GetInt(row, 9),
                Modifier = GetInt(row, 10),
                Total = GetInt(row, 11),
                ResultTier = NormalizeOutcome(GetString(row, 12)),
                CreatedAt = ParseTimestamp(GetString(row, 0), fallbackToNow: true)
            });
        }

        return logs;
    }

    private static List<AdminLogEntity> BuildAdminLogs(
        IReadOnlyList<IList<object>> adminRows,
        IReadOnlyList<IList<object>> noticeRows,
        SheetsImportResult result)
    {
        var logs = new List<AdminLogEntity>();

        for (var index = 0; index < adminRows.Count; index++)
        {
            var row = adminRows[index];
            if (row.Count == 0)
            {
                continue;
            }

            var targetUserId = GetString(row, 4);
            var characterName = GetString(row, 5);
            logs.Add(new AdminLogEntity
            {
                Id = Guid.NewGuid(),
                AdminDiscordId = GetString(row, 2),
                ActionType = GetString(row, 1),
                TargetType = string.IsNullOrWhiteSpace(characterName) ? "record" : "character",
                TargetId = string.IsNullOrWhiteSpace(characterName)
                    ? targetUserId
                    : $"{targetUserId}::{characterName}",
                BeforeValue = "",
                AfterValue = "",
                Message = GetString(row, 6),
                CreatedAt = ParseTimestamp(GetString(row, 0), fallbackToNow: true)
            });
        }

        for (var index = 0; index < noticeRows.Count; index++)
        {
            var row = noticeRows[index];
            if (row.Count == 0)
            {
                continue;
            }

            var noticeType = GetString(row, 1);
            var title = GetString(row, 2);
            var content = GetString(row, 3);
            var channelId = GetString(row, 6);
            logs.Add(new AdminLogEntity
            {
                Id = Guid.NewGuid(),
                AdminDiscordId = GetString(row, 4),
                ActionType = $"notice:{NormalizeNoticeType(noticeType)}",
                TargetType = "notice_channel",
                TargetId = channelId,
                BeforeValue = "",
                AfterValue = "",
                Message = $"[{title}] {content}".Trim(),
                CreatedAt = ParseTimestamp(GetString(row, 0), fallbackToNow: true)
            });
            result.MigrationNotes.Add($"notice_logs row {index + 2}: imported into admin_logs as notice:{NormalizeNoticeType(noticeType)}.");
        }

        return logs;
    }

    private static List<EnemyEntity> BuildEnemies(
        IReadOnlyList<EnemyRow> rows,
        SheetsImportResult result)
    {
        var enemies = new List<EnemyEntity>();

        foreach (var row in rows)
        {
            var categoryPart = string.IsNullOrWhiteSpace(row.Category) ? "" : $"category={row.Category}; ";
            var damagePart = string.IsNullOrWhiteSpace(row.DamageFormula) ? "" : $"damage_formula={row.DamageFormula}; ";
            var dpPart = row.Dp > 0 ? $"dp={row.Dp}; " : "";
            var descriptionPart = string.IsNullOrWhiteSpace(row.Description) ? "" : row.Description;

            enemies.Add(new EnemyEntity
            {
                Id = Guid.NewGuid(),
                EnemyCode = row.EnemyId,
                Name = row.Name,
                NormalizedName = Normalize(row.Name),
                Strength = row.Strength,
                Dexterity = row.Dexterity,
                Constitution = row.Constitution,
                Intelligence = row.Intelligence,
                Wisdom = row.Wisdom,
                Charisma = row.Charisma,
                MaxHp = Math.Max(1, row.MaxHp),
                EncounterTag = row.Region,
                Memo = $"{categoryPart}{damagePart}{dpPart}{descriptionPart}".Trim(),
                IsActive = !string.Equals(row.IsEnabled, "N", StringComparison.OrdinalIgnoreCase)
            });

            if (!string.IsNullOrWhiteSpace(row.Category))
            {
                result.MigrationNotes.Add($"enemy '{row.EnemyId}': legacy category preserved in memo because 1.0 schema uses encounter_tag and memo.");
            }
        }

        return enemies;
    }

    private static List<EnemyDropEntity> BuildEnemyDrops(
        IReadOnlyList<EnemyDropRow> rows,
        IReadOnlyDictionary<string, EnemyEntity> enemyByCode,
        SheetsImportResult result)
    {
        var drops = new List<EnemyDropEntity>();

        foreach (var row in rows)
        {
            if (!enemyByCode.TryGetValue(row.EnemyId, out var enemy))
            {
                result.MigrationNotes.Add($"enemy_drop row {row.RowNumber}: enemy '{row.EnemyId}' not found, skipped.");
                continue;
            }

            var memoParts = new List<string>();
            if (row.Weight > 0)
            {
                memoParts.Add($"weight={row.Weight}");
            }
            if (!string.IsNullOrWhiteSpace(row.Rarity))
            {
                memoParts.Add($"rarity={row.Rarity}");
            }
            if (!string.IsNullOrWhiteSpace(row.Tag))
            {
                memoParts.Add($"tag={row.Tag}");
            }
            if (!string.IsNullOrWhiteSpace(row.Memo))
            {
                memoParts.Add(row.Memo);
            }

            drops.Add(new EnemyDropEntity
            {
                Id = Guid.NewGuid(),
                EnemyId = enemy.Id,
                ItemName = row.ItemName,
                Probability = NormalizeProbability(row.Chance),
                MinQuantity = Math.Max(0, row.MinCount),
                MaxQuantity = Math.Max(Math.Max(0, row.MinCount), row.MaxCount),
                Memo = string.Join("; ", memoParts)
            });
        }

        return drops;
    }

    private static List<EnemyDropSettingEntity> BuildEnemyDropSettings(
        IReadOnlyList<EnemyDropSettingRow> rows,
        IReadOnlyDictionary<string, EnemyEntity> enemyByCode,
        SheetsImportResult result)
    {
        var settings = new List<EnemyDropSettingEntity>();

        foreach (var row in rows)
        {
            if (!enemyByCode.TryGetValue(row.EnemyId, out var enemy))
            {
                result.MigrationNotes.Add($"enemy_drop_settings row {row.RowNumber}: enemy '{row.EnemyId}' not found, skipped.");
                continue;
            }

            var memo = row.AllowDuplicate
                ? string.IsNullOrWhiteSpace(row.Memo) ? "allow_duplicate=true" : $"allow_duplicate=true; {row.Memo}"
                : row.Memo;

            settings.Add(new EnemyDropSettingEntity
            {
                Id = Guid.NewGuid(),
                EnemyId = enemy.Id,
                DropRate = NormalizeProbability(row.DropRate),
                DropSlots = Math.Max(0, row.DropCount),
                Memo = memo
            });
        }

        return settings;
    }

    private static string BuildSelectionKey(string userId, string name)
    {
        return $"{userId}::{Normalize(name)}";
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string NormalizeReviewStatus(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "" => "approved",
            "승인" or "approve" or "approved" => "approved",
            "대기" or "검수중" or "pending" => "pending",
            "반려" or "reject" or "rejected" => "rejected",
            _ => normalized
        };
    }

    private static string NormalizeOutcome(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "성공" or "success" => "success",
            "부분 성공" or "부분성공" or "partial success" or "partial_success" => "partial_success",
            "실패" or "failure" or "fail" => "failure",
            _ => normalized
        };
    }

    private static string NormalizeNoticeType(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "generic" : Normalize(trimmed).ToLowerInvariant();
    }

    private static decimal NormalizeProbability(int percent)
    {
        return Math.Clamp(percent, 0, 100) / 100m;
    }

    private static string BuildSheetUrl(string sourceSheetId)
    {
        return sourceSheetId.StartsWith("legacy:", StringComparison.OrdinalIgnoreCase)
            ? ""
            : $"https://docs.google.com/spreadsheets/d/{sourceSheetId}/edit";
    }

    private static int GetInt(IList<object> row, int index)
    {
        return int.TryParse(GetString(row, index), out var value) ? value : 0;
    }

    private static string GetString(IList<object> row, int index)
    {
        return row.Count > index ? row[index]?.ToString()?.Trim() ?? "" : "";
    }

    private static int ClampHp(int currentHp, int maxHp)
    {
        return maxHp > 0 ? Math.Clamp(currentHp, 0, maxHp) : Math.Max(currentHp, 0);
    }

    private static DateTimeOffset ParseTimestamp(string value, bool fallbackToNow)
    {
        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return fallbackToNow ? DateTimeOffset.UtcNow : DateTimeOffset.MinValue;
    }

    private static string RangeSheet(string sheetName)
    {
        return $"'{sheetName.Trim().Replace("'", "''")}'";
    }
}
