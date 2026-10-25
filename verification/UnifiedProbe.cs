using System.Collections;
using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using NecrobinderVfx;

namespace STS2CompatProbe;

/// <summary>Integration smoke check: runtime policy, original gold DLL recovery and two actual ownership cases.</summary>
internal static class UnifiedProbe
{
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(NativeDiagnostics), "RunCompatibility"), prefix: new HarmonyMethod(typeof(UnifiedProbe), nameof(Suite)));
        harmony.Patch(AccessTools.Method(typeof(Hook), "BeforeCardPlayed"), postfix: new HarmonyMethod(typeof(Probe), "BeforePlay"));
        harmony.Patch(AccessTools.Method(typeof(Hook), "AfterCardPlayed"), postfix: new HarmonyMethod(typeof(Probe), "AfterPlay"));
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name is "NecrobinderVfx" or "DebilitateRedKnife" or "MoreNecrobinderAnimations"))
            foreach (var type in assembly.GetTypes().Where(t => !t.IsNested && t.Name is "NativeHang" or "NativeBanshee" or "BansheeEffect" or "ClipPlayer"))
                foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(Probe), "Created"));
    }
    private static bool Suite(NGame __0, Player __1, ref Task __result) { __result = Run(__0, __1); return false; }
    private static async Task Run(NGame game, Player player)
    {
        var cases = new List<object>(); Exception? failure = null;
        try
        {
            var unified = AssemblyOf("STS2UnifiedCompat");
            var status = (IDictionary)unified.GetType("STS2UnifiedCompat.Entry", true)!.GetProperty("Status")!.GetValue(null)!;
            Require(status.Count == 7 && status.Values.Cast<string>().All(v => v == "installed"), "A module did not install.");
            string[] owners = ["obzyw.sts2.unified.necro", "local.sts2.silent-movement-compat", "local.sts2.silent-effects-compat", "obzyw.sts2.unified.regent", "obzyw.sts2.unified.watcher-preset", "local.sts2.watcher-divinity-pose", "local.sts2.watcher-stance-transitions"];
            var patchedOwners = Harmony.GetAllPatchedMethods().SelectMany(m => Harmony.GetPatchInfo(m)!.Owners).ToHashSet();
            Require(owners.All(patchedOwners.Contains), "A module reported installed but has no patches.");
            cases.Add(new { id = "seven-modules-active", owners });
            var predicate = AccessTools.Method(typeof(NecrobinderVfx.Main), "Supported");
            foreach (string name in new[] { "BansheesCry", "Eradicate", "EndOfDays", "Neurosurge", "ReaperForm", "Hang" })
            {
                var card = (MegaCrit.Sts2.Core.Models.CardModel)typeof(MegaCrit.Sts2.Core.Models.ModelDb).GetMethods().Single(m => m.Name == "Card" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                    .MakeGenericMethod(typeof(Hang).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards." + name, true)!).Invoke(null, null)!;
                Require((bool)predicate.Invoke(null, [card])! == (name == "Hang"), "Gold routing differs from accepted profile: " + name);
            }
            cases.Add(new { id = "gold-routing", sharedExcluded = 5 });
            var ra = AssemblyOf("RegentAnimations").GetType("RegentAnimations.PresentationSettings", true)!;
            var rf = AssemblyOf("RegentFX").GetType("RegentFX.ThirdParty.RitsuLibModConfig", true)!;
            foreach (string name in new[] { "BlackHole", "Genesis", "PillarOfCreation", "SpectrumShift" })
                Require((bool)AccessTools.Method(ra, "CardEnabled").Invoke(null, [name])! && (bool)AccessTools.Method(rf, "GetRitsuLibSettingBool").Invoke(null, ["power_" + name])!, "Power coexistence: " + name);
            foreach (string name in new[] { "Comet", "CrescentSpear", "DyingStar", "SevenStars", "AstralPulse" })
                Require(!(bool)AccessTools.Method(ra, "CardEnabled").Invoke(null, [name])! && (bool)AccessTools.Method(rf, "GetRitsuLibSettingBool").Invoke(null, ["card_" + name])!, "Attack priority: " + name);
            cases.Add(new { id = "regent-nine-card-policy" });
            var watcher = AssemblyOf("WatcherReimagined").GetType("WatcherMod.WatcherSettings", true)!;
            Require(!(bool)watcher.GetProperty("CardArt")!.GetValue(null)! && !(bool)watcher.GetProperty("UseModMotion")!.GetValue(null)! && (bool)watcher.GetProperty("Effects")!.GetValue(null)!, "Watcher effects-only profile.");
            cases.Add(new { id = "watcher-effects-only" });
            foreach (Type card in new[] { typeof(Hang), typeof(BansheesCry) })
                await (Task)AccessTools.Method(typeof(Probe), "Play").MakeGenericMethod(card).Invoke(null, [game, player, card == typeof(BansheesCry), false, false])!;
            cases.AddRange(((IEnumerable)AccessTools.Field(typeof(Probe), "Results").GetValue(null)!).Cast<object>());
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            File.WriteAllText(Path.Combine(System.Environment.GetEnvironmentVariable("NECROVFX_TEST_OUTPUT")!, "unified-validation.json"), JsonSerializer.Serialize(new { success = failure == null, error = failure?.ToString(), expectedCases = 6, cases }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static Assembly AssemblyOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == name);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
