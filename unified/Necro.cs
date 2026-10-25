using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace STS2UnifiedCompat;

/// <summary>Runtime equivalent of necro-v1, applied to the untouched author assembly. No DLL rewriting.</summary>
internal static class Necro
{
    private static readonly HashSet<string> excluded = ["BansheesCry", "Eradicate", "EndOfDays", "Neurosurge", "ReaperForm"];
    internal static void Install()
    {
        var assembly = Entry.Require("NecrobinderVfx");
        Entry.Require("DebilitateRedKnife"); Entry.Require("MoreNecrobinderAnimations");
        var main = assembly.GetType("NecrobinderVfx.Main", true)!;
        var harmony = new Harmony("obzyw.sts2.unified.necro");
        foreach (string installer in new[] { "EndOfDaysPresentation", "NeurosurgeRelay", "ReaperFormRelay" })
            harmony.Patch(AccessTools.Method(assembly.GetType("NecrobinderVfx." + installer, true)!, "Install"),
                prefix: new HarmonyMethod(typeof(Necro), nameof(Skip)));
        harmony.Patch(AccessTools.Method(main, "Supported"), postfix: new HarmonyMethod(typeof(Necro), nameof(Filter)));
        // Original initialization may already have rolled back because another transpiler replaced the missile call.
        // Reinitialize once before menus/combat, with only the conflicting installers suppressed in memory.
        harmony.UnpatchAll("jackt.NecrobinderVfx");
        AccessTools.Field(main, "_harmony").SetValue(null, null);
        AccessTools.Method(main, "Initialize").Invoke(null, null);
        if (AccessTools.Field(main, "_harmony").GetValue(null) == null)
            throw new InvalidOperationException("Gold VFX runtime recovery did not initialize.");
    }
    private static bool Skip() => false;
    private static void Filter(CardModel? __0, ref bool __result)
    { if (__0 != null && excluded.Contains(__0.GetType().Name)) __result = false; }
}
