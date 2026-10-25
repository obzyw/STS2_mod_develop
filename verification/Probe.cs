using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;
using NecrobinderVfx;
using Environment = System.Environment;

namespace STS2CompatProbe;

/// <summary>Temporary observer: reuses the shipped no-save diagnostic fixture.</summary>
[ModInitializer(nameof(Initialize))]
public static class Probe
{
    private static readonly Type Diagnostics = typeof(NativeDiagnostics);
    private static readonly Dictionary<string, int> Creations = new();
    private static readonly List<object> Results = new();
    private static CardModel? observedCard;
    private static int before, after;
    private static readonly string[] Shared = ["BansheesCry", "Eradicate", "EndOfDays", "Neurosurge", "ReaperForm"];
    private static bool Control => Environment.GetEnvironmentVariable("STS2_COMPAT_CONTROL") == "1";

    /// <summary>Installs observers only when explicitly enabled for a no-save test.</summary>
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("STS2_WATCHER_MENU_PROBE") == "1") { WatcherMenuProbe.Install(); return; }
        if (Environment.GetEnvironmentVariable("STS2_COMPAT_PROBE") != "1") return;
        var harmony = new Harmony("local.sts2.compat.verification");
        if (Environment.GetEnvironmentVariable("STS2_UNIFIED_TEST") == "1")
        {
            NativeDiagnostics.Install(harmony);
            if (Environment.GetEnvironmentVariable("STS2_STANCE_TRANSITION_PROBE") != "1") { UnifiedProbe.Install(harmony); return; }
        }
        if (Environment.GetEnvironmentVariable("STS2_STANCE_TRANSITION_PROBE") == "1") { StanceTransitionProbe.Install(harmony); return; }
        if (Environment.GetEnvironmentVariable("STS2_WATCHER_POSE_PROBE") == "1") { WatcherPoseProbe.Install(harmony); return; }
        if (Environment.GetEnvironmentVariable("STS2_REGENT_PROBE") == "1") { RegentProbe.Install(harmony); return; }
        if (Environment.GetEnvironmentVariable("STS2_EFFECTS_PROBE") == "1") { EffectsProbe.Install(harmony); return; }
        if (Environment.GetEnvironmentVariable("STS2_SILENT_PROBE") == "1") { SilentProbe.Install(harmony); return; }
        // The unmodified gold mod fails before installing its diagnostics.
        // Install only the shipped no-save fixture for the control run.
        if (Control) NativeDiagnostics.Install(harmony);
        harmony.Patch(AccessTools.Method(Diagnostics, "RunCompatibility"), new HarmonyMethod(typeof(Probe), nameof(ReplaceSuite)));
        harmony.Patch(AccessTools.Method(typeof(Hook), "BeforeCardPlayed"), postfix: new HarmonyMethod(typeof(Probe), nameof(BeforePlay)));
        harmony.Patch(AccessTools.Method(typeof(Hook), "AfterCardPlayed"), postfix: new HarmonyMethod(typeof(Probe), nameof(AfterPlay)));
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a =>
                     a.GetName().Name is "NecrobinderVfx" or "MoreNecrobinderAnimations" or "DebilitateRedKnife"))
        {
            // Observe actual effect allocation, not merely a permission check.
            string[] observedTypes = ["NativeBanshee", "NativeEradicate", "NativeEndOfDays", "NativeNeurosurge", "NativeReaperForm", "NativeHang", "NativeSoulStorm", "NativeSqueeze", "BansheeEffect", "ClipPlayer", "EndOfDaysEffect", "ReaperFormEffect", "NeuroSequenceV5", "EradicateSequenceV1", "EndOfDaysCarrier"];
            foreach (var type in assembly.GetTypes().Where(t => !t.IsNested && observedTypes.Contains(t.Name)))
                foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(Probe), nameof(Created)));
        }
        GD.Print("[STS2CompatProbe] Actual-play observers installed.");
    }

    private static void Created(MethodBase __originalMethod)
    {
        if (observedCard is null) return;
        string name = __originalMethod.DeclaringType!.FullName!;
        Creations[name] = Creations.GetValueOrDefault(name) + 1;
    }

    private static void BeforePlay(CardPlay __1) { if (__1.Card == observedCard) before++; }
    private static void AfterPlay(CardPlay __2) { if (__2.Card == observedCard) after++; }
    private static bool ReplaceSuite(NGame __0, Player __1, ref Task __result)
    {
        __result = Run(__0, __1);
        return false;
    }

    private static async Task Run(NGame game, Player player)
    {
        string directory = Environment.GetEnvironmentVariable("NECROVFX_TEST_OUTPUT")!;
        Exception? failure = null;
        try
        {
            // Execute the compiled predicate against real registered models too.
            var predicate = AccessTools.Method(typeof(NecrobinderVfx.Main), "Supported");
            var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "..", "build", "necro-v1", "build-report.json")));
            foreach (string name in Shared.Concat(report.RootElement.GetProperty("retainedCards").EnumerateArray().Select(e => e.GetString()!)))
            {
                var modelType = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards." + name, true)!;
                var factory = typeof(ModelDb).GetMethods().Single(m => m.Name == "Card" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                var model = (CardModel)factory.MakeGenericMethod(modelType).Invoke(null, null)!;
                Require((bool)predicate.Invoke(null, [model])! == (Control || !Shared.Contains(name)), "Runtime routing mismatch: " + name);
            }
            await Play<BansheesCry>(game, player, true, false);
            await Play<Eradicate>(game, player, true, false);
            await Play<EndOfDays>(game, player, true, false);
            await Play<Neurosurge>(game, player, true, true);
            await Play<ReaperForm>(game, player, true, true);
            await Play<Hang>(game, player, false, false, reuseCombat: true);
            await Play<Hang>(game, player, false, false);
            await Play<SoulStorm>(game, player, false, false);
            await Play<Squeeze>(game, player, false, false);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            observedCard = null;
            File.WriteAllText(Path.Combine(directory, "compatibility-probe.json"), JsonSerializer.Serialize(new
            {
                success = failure is null, control = Control, error = failure?.ToString(),
                scope = "Real native AutoPlay, one play/finish, actual effect allocations, 26 registered-model routing checks. All three target mods and other subscriptions remain loaded.",
                cases = Results
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static async Task Play<T>(NGame game, Player player, bool shared, bool self, bool reuseCombat = false) where T : CardModel
    {
        var enter = AccessTools.Method(Diagnostics, "EnterSoulCombat");
        var enemies = reuseCombat
            ? CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.ToArray()
            : await (Task<Creature[]>)enter.Invoke(null, [game, player])!;
        foreach (var enemy in enemies) await CreatureCmd.SetMaxAndCurrentHp(enemy, 10000m);
        await PlayerCmd.SetEnergy(20m, player);
        var card = CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<T>(), player);
        await CardPileCmd.Add(card, PileType.Hand);
        observedCard = card;
        before = after = 0;
        Creations.Clear();
        int castBefore = NativeDirector.CastCount;
        int[] hpBefore = enemies.Select(e => e.CurrentHp).ToArray();
        await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, self ? player.Creature : enemies[1]);
        await Wait(game, 3.5);
        int casts = NativeDirector.CastCount - castBefore;
        var creations = new Dictionary<string, int>(Creations);
        bool companion = creations.Keys.Any(n => n.StartsWith("MoreNecrobinderAnimations.") || n.StartsWith("DebilitateRedKnife."));
        bool goldShared = creations.Keys.Any(n => Shared.Any(c => n == "NecrobinderVfx.Native" + (c == "BansheesCry" ? "Banshee" : c)));
        var result = new { card = typeof(T).Name, shared, reuseCombat, before, after, goldCasts = casts, companionAllocated = companion, goldSharedAllocated = goldShared, effectAllocations = creations, hpBefore, hpAfter = enemies.Select(e => e.CurrentHp).ToArray() };
        Results.Add(result);
        GD.Print("[STS2CompatProbe] " + JsonSerializer.Serialize(result));
        Require(before == 1 && after == 1, "Missing/duplicated native play: " + typeof(T).Name);
        Require(!goldShared, "Shared gold effect allocated: " + typeof(T).Name);
        Require(Control ? casts == 0 && (!shared || companion) : shared ? casts == 0 && companion : casts == 1, "Effect ownership failed: " + typeof(T).Name);
        observedCard = null;
    }

    private static async Task Wait(NGame game, double seconds)
    {
        for (double elapsed = 0; elapsed < seconds; elapsed += game.GetProcessDeltaTime())
            await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
