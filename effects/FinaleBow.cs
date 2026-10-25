using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Saves;

namespace SilentEffectsCompat;

/// <summary>Reuses the author's native bow pose and body lease without its teleport/slash presentation.</summary>
internal static class FinaleBow
{
    private static ConstructorInfo leaseConstructor = null!;
    private static MethodInfo acquire = null!, load = null!, sample = null!;
    private static PropertyInfo current = null!;
    private static FieldInfo pose = null!, root = null!;
    private static readonly HashSet<NCreature> active = new();

    internal static void Configure(Assembly msa)
    {
        var lease = msa.GetType("MoreSilentAnimations.DisplayLease", true)!;
        leaseConstructor = AccessTools.Constructor(lease, [typeof(NCreature)]);
        acquire = AccessTools.Method(lease, "Acquire"); current = AccessTools.Property(lease, "Current");
        pose = AccessTools.Field(lease, "Pose"); root = AccessTools.Field(lease, "Root");
        var native = msa.GetType("MoreSilentAnimations.NativePose", true)!;
        load = AccessTools.Method(native, "LoadFinale"); sample = AccessTools.Method(native, "SampleFinaleGhost");
    }

    internal static void Start(NCreature? actor)
    {
        if (actor is not null && active.Add(actor)) _ = Play(actor);
    }

    private static async Task Play(NCreature actor)
    {
        object? lease = null;
        try
        {
            lease = leaseConstructor.Invoke([actor]);
            var native = pose.GetValue(lease)!;
            var display = (Node2D)root.GetValue(lease)!;
            display.Name = "MoreSilentCompatFinaleBow";
            load.Invoke(native, null);
            string clip = "finale-" + SaveManager.Instance.PrefsSave.FastMode.ToString().ToLowerInvariant();
            sample.Invoke(native, [clip, 0.0]);
            acquire.Invoke(lease, null);
            var tree = actor.GetTree(); ulong start = Time.GetTicksMsec();
            while (GodotObject.IsInstanceValid(actor) && actor.IsInsideTree() && actor.Entity.IsAlive && (bool)current.GetValue(lease)!)
            {
                double age = (Time.GetTicksMsec() - start) / 1000.0;
                if (age >= 3.5) break;
                sample.Invoke(native, [clip, age]);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }
        catch (Exception error) { GD.PushWarning("[SilentEffectsCompat] Finale bow stopped: " + error); }
        finally
        {
            // DisplayLease restores the exact original tint and hidden meshes,
            // including early combat exits and exceptions.
            (lease as IDisposable)?.Dispose();
            active.Remove(actor);
        }
    }
}
