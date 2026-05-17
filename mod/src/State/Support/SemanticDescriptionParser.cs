using System.Text.RegularExpressions;
using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.State.Support;

internal static partial class SemanticDescriptionParser
{
    [GeneratedRegex(@"造成\s*(\d+)\s*点伤害", RegexOptions.IgnoreCase)]
    private static partial Regex ZhDamageRegex();

    [GeneratedRegex(@"获得\s*(\d+)\s*点格挡", RegexOptions.IgnoreCase)]
    private static partial Regex ZhBlockRegex();

    [GeneratedRegex(@"抽\s*(\d+)\s*张牌", RegexOptions.IgnoreCase)]
    private static partial Regex ZhDrawRegex();

    [GeneratedRegex(@"获得\s*(\d+)\s*点能量", RegexOptions.IgnoreCase)]
    private static partial Regex ZhEnergyRegex();

    [GeneratedRegex(@"回复\s*(\d+)\s*点生命", RegexOptions.IgnoreCase)]
    private static partial Regex ZhHealRegex();

    [GeneratedRegex(@"(?:给予|施加|获得)\s*(\d+)\s*层\s*([A-Za-z0-9_\-\u4e00-\u9fff]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ZhStatusRegex();

    [GeneratedRegex(@"Deal\s+(\d+)\s+damage", RegexOptions.IgnoreCase)]
    private static partial Regex EnDamageRegex();

    [GeneratedRegex(@"Gain\s+(\d+)\s+Block", RegexOptions.IgnoreCase)]
    private static partial Regex EnBlockRegex();

    [GeneratedRegex(@"Draw\s+(\d+)\s+cards?", RegexOptions.IgnoreCase)]
    private static partial Regex EnDrawRegex();

    [GeneratedRegex(@"Gain\s+(\d+)\s+Energy", RegexOptions.IgnoreCase)]
    private static partial Regex EnEnergyRegex();

    [GeneratedRegex(@"Heal\s+(\d+)\s+HP", RegexOptions.IgnoreCase)]
    private static partial Regex EnHealRegex();

    [GeneratedRegex(@"(?:Apply|Gain)\s+(\d+)\s+(?:stack|stacks)\s+of\s+([A-Za-z][A-Za-z ]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EnStatusRegex();

    public static CombatActionSemanticSnapshot BuildActionSemantic(
        string targetType,
        int? energyCost,
        int? starCost,
        bool isXCost,
        string? description)
    {
        string normalizedDescription = ObservationText.StripRichTextTags(description ?? string.Empty).Replace("\n", " ").Trim();
        string normalizedTarget = NormalizeTarget(targetType);
        var effects = new List<SemanticEffectSnapshot>();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int damage = SumNumericMatches(normalizedDescription, ZhDamageRegex(), EnDamageRegex());
        if (damage > 0)
        {
            effects.Add(new SemanticEffectSnapshot("damage", damage, normalizedTarget, null));
            tags.Add("damage");
        }

        int block = SumNumericMatches(normalizedDescription, ZhBlockRegex(), EnBlockRegex());
        if (block > 0)
        {
            effects.Add(new SemanticEffectSnapshot("block", block, "self", null));
            tags.Add("block");
        }

        int draw = SumNumericMatches(normalizedDescription, ZhDrawRegex(), EnDrawRegex());
        if (draw > 0)
        {
            effects.Add(new SemanticEffectSnapshot("draw", draw, "self", null));
            tags.Add("draw");
        }

        int energyGain = SumNumericMatches(normalizedDescription, ZhEnergyRegex(), EnEnergyRegex());
        if (energyGain > 0)
        {
            effects.Add(new SemanticEffectSnapshot("energy", energyGain, "self", null));
            tags.Add("energy");
        }

        int heal = SumNumericMatches(normalizedDescription, ZhHealRegex(), EnHealRegex());
        if (heal > 0)
        {
            effects.Add(new SemanticEffectSnapshot("heal", heal, "self", null));
            tags.Add("heal");
        }

        foreach ((int amount, string statusName, string target) in ParseStatusEffects(normalizedDescription, normalizedTarget))
        {
            effects.Add(new SemanticEffectSnapshot("status", amount, target, statusName));
            tags.Add("status");
            tags.Add($"status:{statusName}");
        }

        AddKeywordTag(normalizedDescription, tags, "消耗", "Exhaust", "exhaust");
        AddKeywordTag(normalizedDescription, tags, "保留", "Retain", "retain");
        AddKeywordTag(normalizedDescription, tags, "虚无", "Ethereal", "ethereal");
        AddKeywordTag(normalizedDescription, tags, "力量", "Strength", "strength_related");
        AddKeywordTag(normalizedDescription, tags, "易伤", "Vulnerable", "vulnerable_related");
        AddKeywordTag(normalizedDescription, tags, "虚弱", "Weak", "weak_related");

        string summary = BuildSummary(damage, block, draw, energyGain, heal, effects.Where(effect => effect.Kind == "status").ToArray(), normalizedTarget);

        return new CombatActionSemanticSnapshot(
            Summary: string.IsNullOrWhiteSpace(summary) ? (string.IsNullOrWhiteSpace(normalizedDescription) ? "No semantic summary extracted." : normalizedDescription) : summary,
            TargetType: normalizedTarget,
            EnergyCost: energyCost,
            StarCost: starCost,
            IsXCost: isXCost,
            Damage: damage,
            Block: block,
            Draw: draw,
            EnergyGain: energyGain,
            Heal: heal,
            Effects: effects,
            Tags: tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IEnumerable<(int Amount, string StatusName, string Target)> ParseStatusEffects(string description, string defaultTarget)
    {
        foreach (Match match in ZhStatusRegex().Matches(description))
        {
            if (!TryGetAmount(match, out int amount))
            {
                continue;
            }

            string statusName = match.Groups[2].Value.Trim();
            string target = description.Contains($"获得{amount}层{statusName}", StringComparison.OrdinalIgnoreCase)
                ? "self"
                : defaultTarget;
            yield return (amount, statusName, target);
        }

        foreach (Match match in EnStatusRegex().Matches(description))
        {
            if (!TryGetAmount(match, out int amount))
            {
                continue;
            }

            string statusName = match.Groups[2].Value.Trim();
            string verb = match.Value.StartsWith("Gain", StringComparison.OrdinalIgnoreCase) ? "Gain" : "Apply";
            string target = string.Equals(verb, "Gain", StringComparison.OrdinalIgnoreCase) ? "self" : defaultTarget;
            yield return (amount, statusName, target);
        }
    }

    private static void AddKeywordTag(string description, ISet<string> tags, string zhKeyword, string enKeyword, string tag)
    {
        if (description.Contains(zhKeyword, StringComparison.OrdinalIgnoreCase) ||
            description.Contains(enKeyword, StringComparison.OrdinalIgnoreCase))
        {
            tags.Add(tag);
        }
    }

    private static string BuildSummary(
        int damage,
        int block,
        int draw,
        int energyGain,
        int heal,
        IReadOnlyList<SemanticEffectSnapshot> statuses,
        string target)
    {
        var parts = new List<string>();

        if (damage > 0)
        {
            parts.Add($"damage {damage} to {target}");
        }

        if (block > 0)
        {
            parts.Add($"gain {block} block");
        }

        if (draw > 0)
        {
            parts.Add($"draw {draw}");
        }

        if (energyGain > 0)
        {
            parts.Add($"gain {energyGain} energy");
        }

        if (heal > 0)
        {
            parts.Add($"heal {heal}");
        }

        foreach (SemanticEffectSnapshot status in statuses)
        {
            parts.Add($"apply {status.Detail} {status.Amount} to {status.Target}");
        }

        return string.Join("; ", parts);
    }

    private static int SumNumericMatches(string description, params Regex[] regexes)
    {
        int total = 0;
        foreach (Regex regex in regexes)
        {
            foreach (Match match in regex.Matches(description))
            {
                if (TryGetAmount(match, out int amount))
                {
                    total += amount;
                }
            }
        }

        return total;
    }

    private static bool TryGetAmount(Match match, out int amount)
    {
        return int.TryParse(match.Groups[1].Value, out amount);
    }

    private static string NormalizeTarget(string? targetType)
    {
        return targetType switch
        {
            "AnyEnemy" => "enemy",
            "Self" => "self",
            "AnyAlly" or "AnyPlayer" => "ally",
            "AllEnemies" => "all_enemies",
            "Everyone" => "everyone",
            _ => "none"
        };
    }
}
