using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using TakuAgentMod.Diagnostics;

namespace TakuAgentMod.Patches;

[HarmonyPatch]
internal static class BattleStateCapturePatches
{
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
    [HarmonyPostfix]
    private static void AfterSetUpCombat(CombatState state)
    {
        BattleStateCaptureService.CaptureSnapshot("combat_setup", state);
    }

    [HarmonyPatch(typeof(CombatManager), "SetupPlayerTurn")]
    [HarmonyPostfix]
    private static void AfterSetupPlayerTurn(Player player, ref Task __result)
    {
        __result = WrapSetupPlayerTurn(__result, player);
    }

    private static async Task WrapSetupPlayerTurn(Task originalTask, Player player)
    {
        await originalTask.ConfigureAwait(false);

        CombatState? state = player.Creature?.CombatState ?? CombatManager.Instance?.DebugOnlyGetState();
        BattleStateCaptureService.CaptureSnapshot("after_player_turn_start", state, player);
    }
}
