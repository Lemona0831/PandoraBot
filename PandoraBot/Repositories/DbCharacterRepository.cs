using Microsoft.EntityFrameworkCore;
using PandoraShared.Data;

namespace PandoraBot.Repositories;

public sealed class DbCharacterRepository : ICharacterRepository
{
    private readonly string connectionString;

    public DbCharacterRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<CharacterRecord> SelectCharacterAsync(string ownerDiscordId, string query)
    {
        await using var db = CreateDb();
        var entity = await FindOwnedCharacterEntityAsync(db, ownerDiscordId, query, track: true);
        EnsureApproved(entity);

        var selection = await db.CharacterSelections
            .SingleOrDefaultAsync(x => x.DiscordUserId == ownerDiscordId);

        if (selection is null)
        {
            db.CharacterSelections.Add(new CharacterSelectionEntity
            {
                Id = Guid.NewGuid(),
                DiscordUserId = ownerDiscordId,
                CharacterId = entity.Id,
                SelectedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            selection.CharacterId = entity.Id;
            selection.SelectedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        return Map(entity);
    }

    public async Task<CharacterRecord> GetSelectedCharacterAsync(string ownerDiscordId)
    {
        await using var db = CreateDb();
        var selection = await db.CharacterSelections
            .AsNoTracking()
            .Include(x => x.Character)
            .SingleOrDefaultAsync(x => x.DiscordUserId == ownerDiscordId);

        var entity = selection?.Character;
        if (entity is null)
        {
            throw new InvalidOperationException("No selected character was found. Use /select first.");
        }

        EnsureApproved(entity);
        return Map(entity);
    }

    public async Task<CharacterRecord> GetCharacterAsync(string ownerDiscordId, string query)
    {
        await using var db = CreateDb();
        var entity = await FindOwnedCharacterEntityAsync(db, ownerDiscordId, query, track: false);
        EnsureApproved(entity);
        return Map(entity);
    }

    public async Task<IReadOnlyList<CharacterListItem>> ListCharactersAsync(string ownerDiscordId)
    {
        await using var db = CreateDb();

        var selectedCharacterId = await db.CharacterSelections
            .AsNoTracking()
            .Where(x => x.DiscordUserId == ownerDiscordId)
            .Select(x => (Guid?)x.CharacterId)
            .SingleOrDefaultAsync();

        var rows = await db.Characters
            .AsNoTracking()
            .Where(x => x.DiscordUserId == ownerDiscordId)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.ImportedCharacterName)
            .ToListAsync();

        return rows.Select(row => new CharacterListItem(
            row.Id,
            row.DisplayName,
            row.CurrentHp,
            row.MaxHp,
            selectedCharacterId.HasValue && selectedCharacterId.Value == row.Id,
            NormalizeReviewStatus(row.ReviewStatus),
            row.SourceSheetId,
            row.SourceDocumentTitle)).ToList();
    }

    public async Task<int> ClearSelectedCharacterAsync(string ownerDiscordId)
    {
        await using var db = CreateDb();
        var selection = await db.CharacterSelections
            .SingleOrDefaultAsync(x => x.DiscordUserId == ownerDiscordId);

        if (selection is null)
        {
            return 0;
        }

        db.CharacterSelections.Remove(selection);
        await db.SaveChangesAsync();
        return 1;
    }

    public async Task<CharacterDeleteResult> DeleteCharacterAsync(string ownerDiscordId, string query)
    {
        await using var db = CreateDb();
        var entity = await FindOwnedCharacterEntityAsync(db, ownerDiscordId, query, track: true);
        await RemoveSelectionIfExistsAsync(db, entity.Id, ownerDiscordId);
        db.Characters.Remove(entity);
        await db.SaveChangesAsync();
        return new CharacterDeleteResult(entity.Id, entity.DisplayName, entity.DiscordUserId);
    }

    public async Task<IReadOnlyList<AdminCharacterListItem>> ListAllCharactersAsync(int limit = 25)
    {
        await using var db = CreateDb();
        var take = Math.Clamp(limit, 1, 50);

        var rows = await db.Characters
            .AsNoTracking()
            .OrderBy(x => x.DiscordUserId)
            .ThenBy(x => x.DisplayName)
            .Take(take)
            .ToListAsync();

        return await MapAdminListAsync(db, rows);
    }

    public async Task<IReadOnlyList<AdminCharacterListItem>> ListReviewCharactersAsync(string status = "pending", int limit = 25)
    {
        await using var db = CreateDb();
        var normalized = NormalizeReviewStatus(status);
        var take = Math.Clamp(limit, 1, 50);
        var rows = await db.Characters
            .AsNoTracking()
            .Where(x => NormalizeReviewStatus(x.ReviewStatus) == normalized)
            .OrderBy(x => x.DiscordUserId)
            .ThenBy(x => x.DisplayName)
            .Take(take)
            .ToListAsync();

        return await MapAdminListAsync(db, rows);
    }

    public async Task<IReadOnlyList<CharacterRecentRollLog>> ListRecentRollLogsAsync(string ownerDiscordId, int limit = 10)
    {
        await using var db = CreateDb();
        var owned = await db.Characters
            .AsNoTracking()
            .Where(x => x.DiscordUserId == ownerDiscordId)
            .Select(x => new { x.Id, x.DisplayName, x.ImportedCharacterName })
            .ToListAsync();

        if (owned.Count == 0)
        {
            return Array.Empty<CharacterRecentRollLog>();
        }

        var ids = owned.Select(x => x.Id).ToArray();
        var names = owned.SelectMany(x => new[] { x.DisplayName, x.ImportedCharacterName })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = await db.RollLogs
            .AsNoTracking()
            .Where(x => (x.CharacterId.HasValue && ids.Contains(x.CharacterId.Value)) || names.Contains(x.CharacterDisplayName))
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 20))
            .ToListAsync();

        return rows.Select(row => new CharacterRecentRollLog(
            row.CreatedAt,
            row.CharacterDisplayName,
            row.StatName,
            row.Total,
            row.ResultTier)).ToList();
    }

    public async Task<CharacterRecord> GetCharacterForAdminAsync(string query)
    {
        await using var db = CreateDb();
        var entity = await FindCharacterEntityAsync(db, query, track: false);
        return Map(entity);
    }

    public async Task<CharacterHpUpdateResult> SetCharacterHpAsync(string query, int currentHp)
    {
        await using var db = CreateDb();
        var entity = await FindCharacterEntityAsync(db, query, track: true);
        var nextHp = Math.Clamp(currentHp, 0, entity.MaxHp);
        var oldHp = entity.CurrentHp;
        entity.CurrentHp = nextHp;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return new CharacterHpUpdateResult(entity.Id, entity.DisplayName, entity.DiscordUserId, oldHp, nextHp, entity.MaxHp);
    }

    public async Task<CharacterHpUpdateResult> AdjustCharacterHpAsync(string query, int amount, string action)
    {
        await using var db = CreateDb();
        var entity = await FindCharacterEntityAsync(db, query, track: true);
        var delta = string.Equals(action, "heal", StringComparison.OrdinalIgnoreCase) ? amount : -amount;
        var oldHp = entity.CurrentHp;
        var nextHp = Math.Clamp(entity.CurrentHp + delta, 0, entity.MaxHp);
        entity.CurrentHp = nextHp;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return new CharacterHpUpdateResult(entity.Id, entity.DisplayName, entity.DiscordUserId, oldHp, nextHp, entity.MaxHp);
    }

    public async Task<int> ClearSelectedCharacterForAdminAsync(string query)
    {
        await using var db = CreateDb();
        var entity = await FindCharacterEntityAsync(db, query, track: false);
        var selection = await db.CharacterSelections.SingleOrDefaultAsync(x => x.CharacterId == entity.Id);
        if (selection is null)
        {
            return 0;
        }

        db.CharacterSelections.Remove(selection);
        await db.SaveChangesAsync();
        return 1;
    }

    public async Task<CharacterDeleteResult> DeleteCharacterForAdminAsync(string query)
    {
        await using var db = CreateDb();
        var entity = await FindCharacterEntityAsync(db, query, track: true);
        await RemoveSelectionIfExistsAsync(db, entity.Id, ownerDiscordId: null);
        db.Characters.Remove(entity);
        await db.SaveChangesAsync();
        return new CharacterDeleteResult(entity.Id, entity.DisplayName, entity.DiscordUserId);
    }

    public async Task<CharacterReviewUpdateResult> SetReviewStatusAsync(string query, string status)
    {
        await using var db = CreateDb();
        var entity = await FindCharacterEntityAsync(db, query, track: true);
        var normalized = NormalizeReviewStatus(status);
        entity.ReviewStatus = normalized;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (normalized == "rejected")
        {
            await RemoveSelectionIfExistsAsync(db, entity.Id, ownerDiscordId: null);
        }

        await db.SaveChangesAsync();
        return new CharacterReviewUpdateResult(entity.Id, entity.DisplayName, entity.DiscordUserId, normalized);
    }

    private PandoraDbContext CreateDb()
        => PandoraDbContextFactory.CreateOrNull(connectionString)
           ?? throw new InvalidOperationException("PandoraDb connection string is not configured.");

    private static async Task<CharacterEntity> FindOwnedCharacterEntityAsync(PandoraDbContext db, string ownerDiscordId, string query, bool track)
    {
        var source = track ? db.Characters : db.Characters.AsNoTracking();
        var rows = await source
            .Where(x => x.DiscordUserId == ownerDiscordId)
            .ToListAsync();

        return FindSingle(rows, query, notFoundMessage: "No registered character was found with that name.", duplicateMessage: "Multiple characters matched that query. Use a more exact character name or source sheet title.");
    }

    private static async Task<CharacterEntity> FindCharacterEntityAsync(PandoraDbContext db, string query, bool track)
    {
        var source = track ? db.Characters : db.Characters.AsNoTracking();
        var rows = await source.ToListAsync();
        return FindSingle(rows, query, notFoundMessage: "No registered character was found with that query.", duplicateMessage: "Multiple characters matched that query. Use a more exact character name or source sheet title.");
    }

    private static CharacterEntity FindSingle(IReadOnlyList<CharacterEntity> rows, string query, string notFoundMessage, string duplicateMessage)
    {
        var trimmed = (query ?? string.Empty).Trim();
        var normalized = Normalize(trimmed);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Character name or source sheet ID is required.");
        }

        var exact = rows.Where(row =>
                string.Equals(row.SourceSheetId, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.ImportedCharacterName, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.SourceDocumentTitle, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count == 1)
        {
            return exact[0];
        }

        if (exact.Count > 1)
        {
            throw new InvalidOperationException(duplicateMessage);
        }

        var partial = rows.Where(row =>
                row.SourceSheetId.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                row.DisplayName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                row.ImportedCharacterName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                row.SourceDocumentTitle.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                row.NormalizedDisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return partial.Count switch
        {
            0 => throw new InvalidOperationException(notFoundMessage),
            1 => partial[0],
            _ => throw new InvalidOperationException(duplicateMessage)
        };
    }

    private static void EnsureApproved(CharacterEntity entity)
    {
        var review = NormalizeReviewStatus(entity.ReviewStatus);
        if (!string.Equals(review, "approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"This character is not approved yet. Current review status: {review}.");
        }
    }

    private static CharacterRecord Map(CharacterEntity entity)
        => new(
            entity.Id,
            entity.DiscordUserId,
            entity.SourceSheetId,
            entity.SourceSheetUrl,
            entity.SourceDocumentTitle,
            entity.ImportedCharacterName,
            entity.DisplayName,
            entity.NormalizedDisplayName,
            entity.Strength,
            entity.Dexterity,
            entity.Constitution,
            entity.Intelligence,
            entity.Wisdom,
            entity.Charisma,
            entity.CurrentHp,
            entity.MaxHp,
            NormalizeReviewStatus(entity.ReviewStatus));

    private static string Normalize(string value)
        => value.Trim().ToLowerInvariant();

    private static string NormalizeReviewStatus(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "approved" => "approved",
            "rejected" => "rejected",
            _ => "pending"
        };
    }

    private static async Task<IReadOnlyList<AdminCharacterListItem>> MapAdminListAsync(PandoraDbContext db, IReadOnlyList<CharacterEntity> rows)
    {
        var characterIds = rows.Select(x => x.Id).ToArray();
        var selections = await db.CharacterSelections
            .AsNoTracking()
            .Where(x => characterIds.Contains(x.CharacterId))
            .ToListAsync();
        var selectedByCharacterId = selections.ToDictionary(x => x.CharacterId, x => x.DiscordUserId);

        return rows.Select(row => new AdminCharacterListItem(
            row.Id,
            row.DiscordUserId,
            row.DisplayName,
            row.CurrentHp,
            row.MaxHp,
            selectedByCharacterId.ContainsKey(row.Id),
            NormalizeReviewStatus(row.ReviewStatus),
            selectedByCharacterId.TryGetValue(row.Id, out var selectedBy) ? selectedBy : string.Empty,
            row.SourceSheetId,
            row.SourceDocumentTitle)).ToList();
    }

    private static async Task RemoveSelectionIfExistsAsync(PandoraDbContext db, Guid characterId, string? ownerDiscordId)
    {
        var query = db.CharacterSelections.Where(x => x.CharacterId == characterId);
        if (!string.IsNullOrWhiteSpace(ownerDiscordId))
        {
            query = query.Where(x => x.DiscordUserId == ownerDiscordId);
        }

        var selection = await query.SingleOrDefaultAsync();
        if (selection is not null)
        {
            db.CharacterSelections.Remove(selection);
        }
    }
}
