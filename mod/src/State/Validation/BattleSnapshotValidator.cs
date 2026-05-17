using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.State.Validation;

internal sealed class BattleSnapshotValidator
{
    public SnapshotValidationResult Validate(BattleSnapshot snapshot)
    {
        List<string> warnings = [];

        if (snapshot.Player is null)
        {
            warnings.Add("Player snapshot is null.");
            return new SnapshotValidationResult(warnings);
        }

        ValidatePile(snapshot.Player.Hand, "hand", warnings);
        ValidatePile(snapshot.Player.DrawPile, "draw_pile", warnings);
        ValidatePile(snapshot.Player.DiscardPile, "discard_pile", warnings);
        ValidatePile(snapshot.Player.ExhaustPile, "exhaust_pile", warnings);

        if (snapshot.Player.Energy < 0)
        {
            warnings.Add($"Player energy is negative: {snapshot.Player.Energy}.");
        }

        foreach (StatusEffectSnapshot power in snapshot.Player.Powers)
        {
            if (string.IsNullOrWhiteSpace(power.Title))
            {
                warnings.Add("Player has a power with empty title.");
            }
        }

        foreach (EnemySnapshot enemy in snapshot.Enemies)
        {
            if (enemy.CurrentHp > enemy.MaxHp)
            {
                warnings.Add($"Enemy '{enemy.Name}' has hp above max ({enemy.CurrentHp}/{enemy.MaxHp}).");
            }

            if (enemy.Intent.IntendsToAttack == true && enemy.Intent.Intents.Count == 0)
            {
                warnings.Add($"Enemy '{enemy.Name}' intends to attack but has no detailed intents.");
            }

            foreach (StatusEffectSnapshot power in enemy.Powers)
            {
                if (string.IsNullOrWhiteSpace(power.Title))
                {
                    warnings.Add($"Enemy '{enemy.Name}' has a power with empty title.");
                }
            }
        }

        return new SnapshotValidationResult(warnings);
    }

    private static void ValidatePile(PileSnapshot pile, string pileName, List<string> warnings)
    {
        if (pile.Count != pile.Cards.Count)
        {
            warnings.Add(
                $"Pile '{pileName}' count mismatch: declared={pile.Count}, cards={pile.Cards.Count}.");
        }
    }
}
