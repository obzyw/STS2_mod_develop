using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace WatcherNoBulletTime;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public static int Suppressed { get; private set; }

    public static void Initialize()
    {
        var fx = AccessTools.TypeByName("WatcherMod.Vfx.Fx")
            ?? throw new InvalidOperationException("WatcherReimagined must be loaded first.");
        var method = AccessTools.DeclaredMethod(fx, "BulletTime", [typeof(double), typeof(int)])
            ?? throw new MissingMethodException("WatcherReimagined BulletTime API changed.");
        if (method.ReturnType != typeof(Task))
            throw new InvalidOperationException("WatcherReimagined BulletTime return type changed.");
        new Harmony("obzyw.sts2.watcher-no-bullet-time").Patch(method,
            prefix: new HarmonyMethod(typeof(Entry), nameof(Skip)) { priority = Priority.First });
        // MeleeAttack also pauses every hit of upgraded cards. Use its existing
        // exclusion API for Ragnarok rather than disabling unrelated effects.
        var hitStops = AccessTools.TypeByName("MeleeAttack.HitStopController");
        if(hitStops != null)
        {
            var excluded = AccessTools.Field(hitStops,"ExcludedHitStopCards")?.GetValue(null) as ISet<string>
                ?? throw new MissingFieldException("MeleeAttack upgraded hit-stop exclusion API changed.");
            excluded.Add("WATCHER_RAGNAROK");
        }
        GD.Print("[WatcherNoBulletTime] WatcherReimagined global bullet time disabled; visuals and sound retained.");
    }

    private static bool Skip(ref Task __result)
    {
        // Skip before any global speed is captured or changed, timer is created,
        // or restoration hook is registered. Leave SpeedX and other speed owners alone.
        Suppressed++;
        __result = Task.CompletedTask;
        return false;
    }
}
