namespace PandoraBot.Repositories;

internal sealed record EnemyMemoParts(string Category, string DamageFormula, int Dp, string Description);

internal sealed record DropMemoParts(int Weight, string Rarity, string Tag, string Memo);

internal static class RepositoryMemoParser
{
    public static EnemyMemoParts ParseEnemyMemo(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new EnemyMemoParts("", "", 0, "");
        }

        var category = "";
        var damageFormula = "";
        var dp = 0;
        var descriptionParts = new List<string>();

        foreach (var segment in raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryReadKeyValue(segment, "category", out var categoryValue))
            {
                category = categoryValue;
                continue;
            }

            if (TryReadKeyValue(segment, "damage_formula", out var damageValue))
            {
                damageFormula = damageValue;
                continue;
            }

            if (TryReadKeyValue(segment, "dp", out var dpValue) && int.TryParse(dpValue, out var parsedDp))
            {
                dp = parsedDp;
                continue;
            }

            descriptionParts.Add(segment);
        }

        return new EnemyMemoParts(category, damageFormula, dp, string.Join("; ", descriptionParts).Trim());
    }

    public static DropMemoParts ParseDropMemo(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new DropMemoParts(0, "", "", "");
        }

        var weight = 0;
        var rarity = "";
        var tag = "";
        var memoParts = new List<string>();

        foreach (var segment in raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryReadKeyValue(segment, "weight", out var weightValue) && int.TryParse(weightValue, out var parsedWeight))
            {
                weight = parsedWeight;
                continue;
            }

            if (TryReadKeyValue(segment, "rarity", out var rarityValue))
            {
                rarity = rarityValue;
                continue;
            }

            if (TryReadKeyValue(segment, "tag", out var tagValue))
            {
                tag = tagValue;
                continue;
            }

            memoParts.Add(segment);
        }

        return new DropMemoParts(weight, rarity, tag, string.Join("; ", memoParts).Trim());
    }

    private static bool TryReadKeyValue(string segment, string key, out string value)
    {
        value = "";
        var prefix = $"{key}=";
        if (!segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = segment[prefix.Length..].Trim();
        return true;
    }
}
