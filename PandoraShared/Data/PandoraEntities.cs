namespace PandoraShared.Data;

public sealed class CharacterEntity
{
    public Guid Id { get; set; }
    public string DiscordUserId { get; set; } = "";
    public string SourceSheetId { get; set; } = "";
    public string SourceSheetUrl { get; set; } = "";
    public string SourceDocumentTitle { get; set; } = "";
    public string ImportedCharacterName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string NormalizedDisplayName { get; set; } = "";
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public string ReviewStatus { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CharacterSelectionEntity
{
    public Guid Id { get; set; }
    public string DiscordUserId { get; set; } = "";
    public Guid CharacterId { get; set; }
    public DateTimeOffset SelectedAt { get; set; }

    public CharacterEntity? Character { get; set; }
}

public sealed class RollLogEntity
{
    public Guid Id { get; set; }
    public Guid? CharacterId { get; set; }
    public string CharacterDisplayName { get; set; } = "";
    public string StatName { get; set; } = "";
    public int Dice1 { get; set; }
    public int Dice2 { get; set; }
    public int Modifier { get; set; }
    public int Total { get; set; }
    public string ResultTier { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public CharacterEntity? Character { get; set; }
}

public sealed class AdminLogEntity
{
    public Guid Id { get; set; }
    public string AdminDiscordId { get; set; } = "";
    public string AdminDisplayName { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string TargetDisplayName { get; set; } = "";
    public string BeforeValue { get; set; } = "";
    public string AfterValue { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class EnemyEntity
{
    public Guid Id { get; set; }
    public string EnemyCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }
    public int MaxHp { get; set; }
    public string EncounterTag { get; set; } = "";
    public string Memo { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public ICollection<EnemyDropEntity> Drops { get; set; } = new List<EnemyDropEntity>();
    public EnemyDropSettingEntity? DropSetting { get; set; }
}

public sealed class EnemyDropEntity
{
    public Guid Id { get; set; }
    public Guid EnemyId { get; set; }
    public string ItemName { get; set; } = "";
    public decimal Probability { get; set; }
    public int MinQuantity { get; set; }
    public int MaxQuantity { get; set; }
    public int Weight { get; set; }
    public string Rarity { get; set; } = "";
    public string Tag { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string Memo { get; set; } = "";

    public EnemyEntity? Enemy { get; set; }
}

public sealed class EnemyDropSettingEntity
{
    public Guid Id { get; set; }
    public Guid EnemyId { get; set; }
    public decimal DropRate { get; set; }
    public int DropSlots { get; set; }
    public bool AllowDuplicate { get; set; }
    public string Memo { get; set; } = "";

    public EnemyEntity? Enemy { get; set; }
}

public sealed class CombatSessionEntity
{
    public Guid Id { get; set; }
    public string GuildId { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "active";
    public string CreatedByDiscordId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string Memo { get; set; } = "";

    public ICollection<CombatParticipantEntity> Participants { get; set; } = new List<CombatParticipantEntity>();
    public ICollection<CombatLogEntity> Logs { get; set; } = new List<CombatLogEntity>();
}

public sealed class CombatParticipantEntity
{
    public Guid Id { get; set; }
    public Guid CombatSessionId { get; set; }
    public string Type { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string NormalizedDisplayName { get; set; } = "";
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public string Status { get; set; } = "";
    public string CreatedByDiscordId { get; set; } = "";
    public string Memo { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CombatSessionEntity? CombatSession { get; set; }
}

public sealed class CombatLogEntity
{
    public Guid Id { get; set; }
    public Guid CombatSessionId { get; set; }
    public string ActorDiscordId { get; set; } = "";
    public string ActionType { get; set; } = "";
    public Guid? TargetParticipantId { get; set; }
    public string TargetName { get; set; } = "";
    public string BeforeValue { get; set; } = "";
    public string AfterValue { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public CombatSessionEntity? CombatSession { get; set; }
}
