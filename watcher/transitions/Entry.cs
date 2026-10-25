using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace WatcherStanceTransitions;

/// <summary>Animates the author's pooled rig swap without delaying the stance or card resolution.</summary>
#if !INTEGRATED
[ModInitializer(nameof(Initialize))]
#endif
public static class Entry
{
    private static GDScript script = null!;
    private static PropertyInfo active = null!, activeForm = null!, body = null!, stance = null!;
    private sealed record Swap(Node2D Previous, string From, string To);

    public static void Initialize()
    {
        string directory = Path.GetDirectoryName(typeof(Entry).Assembly.Location)!;
        script = new GDScript { SourceCode = File.ReadAllText(Path.Combine(directory, "StanceTransition.gd")) };
        if (script.Reload() != Error.Ok) throw new InvalidOperationException("Stance transition script failed to load.");
        var yu = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "YUWatcherForms");
        var pool = yu.GetType("YUWatcherForms.RigPool", true)!;
        active = AccessTools.Property(pool, "Active");
        activeForm = AccessTools.Property(pool, "ActiveForm");
        body = AccessTools.Property(yu.GetType("YUWatcherForms.RigPool+RigEntry", true)!, "Body");
        stance = AccessTools.Property(yu.GetType("YUWatcherForms.Form", true)!, "Stance");
        new Harmony("local.sts2.watcher-stance-transitions").Patch(AccessTools.Method(pool, "TryActivate"),
            prefix: new HarmonyMethod(typeof(Entry), nameof(BeforeSwap)), postfix: new HarmonyMethod(typeof(Entry), nameof(AfterSwap)));
        GD.Print("[WatcherStanceTransitions] Ready: anticipation, blend and stance-specific recovery.");
    }

    private static void BeforeSwap(object __instance, object __0, out Swap? __state)
    {
        __state = null;
        object? previousForm = activeForm.GetValue(__instance), previousEntry = active.GetValue(__instance);
        if (previousForm == null || previousEntry == null) return;
        string from = stance.GetValue(previousForm)!.ToString()!, to = stance.GetValue(__0)!.ToString()!;
        if (from == to) return; // The author already handles HP-only appearance changes.
        var previous = (Node2D)body.GetValue(previousEntry)!;
        previous.GetParent().GetNodeOrNull<Node2D>("WatcherStanceTransition")?.Call("finish");
        __state = new Swap(previous, from, to);
    }

    private static void AfterSwap(object __instance, bool __result, Swap? __state)
    {
        if (!__result || __state == null) return;
        var current = (Node2D)body.GetValue(active.GetValue(__instance)!)!;
        var parent = current.GetParent();
        var effect = parent.GetNodeOrNull<Node2D>("WatcherStanceTransition");
        if (effect == null)
        {
            effect = new Node2D { Name = "WatcherStanceTransition" };
            effect.SetScript(script);
            parent.AddChild(effect);
        }
        effect.Call("start", __state.Previous, current, __state.From, __state.To);
    }
}
