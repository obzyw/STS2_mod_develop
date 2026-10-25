using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Modding;

namespace WatcherDivinityPose;

/// <summary>Attaches the new arm artwork to the existing YU rig, preserving its body and effect controllers.</summary>
#if !INTEGRATED
[ModInitializer(nameof(Initialize))]
#endif
public static class Entry
{
    private static GDScript script = null!;
    private static MethodInfo actionActive = null!;
    private static string texturePath = "";

    public static void Initialize()
    {
        string directory = Path.GetDirectoryName(typeof(Entry).Assembly.Location)!;
        texturePath = Path.Combine(directory, "arms.png");
        script = new GDScript { SourceCode = File.ReadAllText(Path.Combine(directory, "DivinityPose.gd")) };
        if (script.Reload() != Error.Ok) throw new InvalidOperationException("Divinity pose script failed to load.");
        var yu = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "YUWatcherForms");
        actionActive = AccessTools.Method(yu.GetType("YUWatcherForms.DivinityActions"), "IsActive", [typeof(MegaSprite)]);
        var type = yu.GetType("YUWatcherForms.AnimationPolish", true)!;
        new Harmony("local.sts2.watcher-divinity-pose").Patch(AccessTools.Constructor(type, [typeof(MegaSprite)]),
            postfix: new HarmonyMethod(typeof(Entry), nameof(Attach)));
        GD.Print("[WatcherDivinityPose] Ready: original body and dynamic FX, new divine arms.");
    }

    private static void Attach(MegaSprite __0)
    {
        if (__0.BoundObject is not Node2D body || body.HasNode("WatcherDivinityPose")) return;
        var display = new Node2D { Name = "WatcherDivinityPose" };
        display.SetScript(script);
        display.SetMeta("texture_path", texturePath);
        display.SetMeta("action_check", Callable.From(() => (bool)actionActive.Invoke(null, [__0])!));
        body.AddChild(display);
    }
}
