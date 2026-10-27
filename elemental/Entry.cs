using Godot;
using System.Text.Json;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace ElementalDefenseVfx;

/// <summary>Presentation only. The holder, power owner and retaliation dealer are the actual actors.</summary>
[ModInitializer(nameof(Initialize))]
public static class Entry
{
    private static GDScript script = null!;
    private static Texture2D shards = null!, surface = null!, firewall = null!;
    private static Shader coat = null!, trail = null!;
    private static readonly Godot.Collections.Array<Rect2> regions = new();
    private static readonly HashSet<NCreature> tracking = new();
    private sealed record DisplayBinding(WeakReference<object> Lease, NCreature Actor, FieldInfo Root, PropertyInfo Current, FieldInfo? Owned);
    private static readonly List<DisplayBinding> displays = new();
    private const string FxName = "ElementalDefenseVfx";

    public static void Initialize()
    {
        string directory = Path.GetDirectoryName(typeof(Entry).Assembly.Location)!;
        script = new GDScript { SourceCode = File.ReadAllText(Path.Combine(directory, "DefenseFx.gd")) };
        if (script.Reload() != Error.Ok) throw new InvalidOperationException("DefenseFx.gd failed to load.");
        shards = Texture("stone-fragments-v2.png"); surface = Texture("stone-surface-v2.png");
        firewall = Texture("fire-wall-v3.png");
        coat = new Shader { Code = File.ReadAllText(Path.Combine(directory, "StoneCoat.gdshader")) };
        trail = new Shader { Code = File.ReadAllText(Path.Combine(directory, "FlameTrail.gdshader")) };
        using var atlas = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "assets", "stone-fragments-v2.regions.txt")));
        regions.Clear();
        foreach (var rect in atlas.RootElement.EnumerateArray()) regions.Add(new Rect2(rect[0].GetInt32(),rect[1].GetInt32(),rect[2].GetInt32(),rect[3].GetInt32()));
        Texture2D Texture(string file) { using var img = Image.LoadFromFile(Path.Combine(directory,"assets",file)); return ImageTexture.CreateFromImage(img); }
        var h = new Harmony("obzyw.sts2.elemental-defense-vfx");
        // Observe the authors' shared presentation lease, regardless of which card uses it.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(assembly.GetName().Name + ".DisplayLease");
            if (type == null || AccessTools.Field(type, "Root") == null || AccessTools.Property(type, "Current") == null) continue;
            var constructor = AccessTools.Constructor(type, [typeof(NCreature)]);
            if (constructor != null) h.Patch(constructor, postfix: new HarmonyMethod(typeof(Entry), nameof(DisplayCreated)));
        }
        Patch(typeof(NCreature), "_Ready", nameof(ActorReady));
        Patch(typeof(NHandCardHolder), "OnFocus", nameof(Focus));
        Patch(typeof(NHandCardHolder), "BeginDrag", nameof(Drag));
        h.Patch(AccessTools.DeclaredMethod(typeof(NHandCardHolder), "OnUnfocus"), prefix: new HarmonyMethod(typeof(Entry), nameof(Unfocus)));
        foreach (string method in new[] { "CancelDrag", "Clear", "_ExitTree" })
            h.Patch(AccessTools.DeclaredMethod(typeof(NHandCardHolder), method), prefix: new HarmonyMethod(typeof(Entry), nameof(Cancel)));
        h.Patch(AccessTools.Method(typeof(NCardPlay), "Cleanup"), prefix: new HarmonyMethod(typeof(Entry), nameof(EndSelection)));
        // Patch the card implementation, after playability/queue checks, including each automatic replay.
        foreach (var type in new[] { typeof(FlameBarrier), typeof(StoneArmor) })
            h.Patch(AccessTools.DeclaredMethod(type, "OnPlay"), prefix: new HarmonyMethod(typeof(Entry), nameof(Played)));
        h.Patch(AccessTools.DeclaredMethod(typeof(FlameBarrierPower), "AfterDamageReceived"), prefix: new HarmonyMethod(typeof(Entry), nameof(Retaliate)));
        GD.Print("[ElementalDefenseVfx] Ready: owner-bound preview, wall, retaliation and articulated stone plating.");
        void Patch(Type type, string method, string callback) => h.Patch(AccessTools.DeclaredMethod(type, method), postfix: new HarmonyMethod(typeof(Entry), callback));
    }

    private static bool Supported(CardModel? card) => card is FlameBarrier or StoneArmor;
    private static void DisplayCreated(object __instance, NCreature __0)
    {
        var type = __instance.GetType();
        displays.Add(new(new(__instance), __0, AccessTools.Field(type,"Root"), AccessTools.Property(type,"Current"), AccessTools.Field(type,"_owned")));
    }

    private static Node2D DisplayRoot(NCreature actor)
    {
        Node2D result = actor.Body;
        for (int i = displays.Count - 1; i >= 0; i--)
        {
            var entry = displays[i];
            if (!GodotObject.IsInstanceValid(entry.Actor) || !entry.Lease.TryGetTarget(out var lease)) { displays.RemoveAt(i); continue; }
            if (entry.Actor != actor) continue;
            if (entry.Current.GetValue(lease) is not true || entry.Owned?.GetValue(lease) is false) continue;
            if (entry.Root.GetValue(lease) is Node2D root && GodotObject.IsInstanceValid(root) && root.IsInsideTree() && root.IsVisibleInTree())
            { result = root; break; }
        }
        return result;
    }
    private static void ActorReady(NCreature __instance)
    {
        if (__instance.Entity.IsPlayer) _ = AfterActorReady(__instance);
    }

    private static async Task AfterActorReady(NCreature actor)
    {
        // Child _Ready can precede the room's own _Ready and skin postfixes.
        var tree = actor.GetTree();
        await actor.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (GodotObject.IsInstanceValid(actor) && actor.IsInsideTree()) Ensure(actor);
    }

    internal static Node2D? Ensure(NCreature? actor)
    {
        if (actor == null || !GodotObject.IsInstanceValid(actor) || !actor.IsInsideTree() || NCombatRoom.Instance == null) return null;
        var fx = ForVisuals(actor);
        if (tracking.Add(actor)) _ = Track(actor);
        return fx;
    }

    private static Node2D ForVisuals(NCreature actor)
    {
        var fx = actor.Visuals.GetNodeOrNull<Node2D>(FxName);
        if (fx == null)
        {
            // Positive Z escapes scene-tree order and can draw over pause/modal UI.
            // Use the game's combat VFX depth, independent of skin/action Z values.
            fx = new Node2D { Name = FxName, ZAsRelative = false, ZIndex = -9 };
            fx.SetScript(script);
            Configure(fx);
            actor.Visuals.AddChild(fx);
        }
        return fx;
    }

    private static void Configure(Node fx)
    {
        fx.SetMeta("shards", shards); fx.SetMeta("surface", surface); fx.SetMeta("regions", regions);
        fx.SetMeta("coat_shader", coat); fx.SetMeta("trail_shader", trail);
        fx.SetMeta("fire_wall", firewall);
    }

    private static async Task Track(NCreature actor)
    {
        try
        {
            var tree = actor.GetTree();
            Node2D? previous = null;
            while (GodotObject.IsInstanceValid(actor) && actor.IsInsideTree())
            {
                // Some skins replace the complete Visuals after NCreature._Ready, rather than its body.
                var fx = ForVisuals(actor);
                fx.ZIndex = NCombatRoom.Instance!.CombatVfxContainer.ZIndex;
                if (previous != fx && previous != null && GodotObject.IsInstanceValid(previous)) previous.QueueFree();
                previous = fx;
                var bounds = actor.Visuals.Bounds;
                Vector2 top = fx.ToLocal(bounds.GlobalPosition);
                Vector2 bottom = fx.ToLocal(bounds.GetGlobalTransform() * bounds.Size);
                var display = DisplayRoot(actor);
                fx.SetMeta("render_root", display);
                if (display != actor.Body)
                {
                    var pose = display.GlobalTransform * actor.Body.GlobalTransform.AffineInverse();
                    top = fx.ToLocal(pose * fx.ToGlobal(top));
                    bottom = fx.ToLocal(pose * fx.ToGlobal(bottom));
                }
                fx.Call("sync_actor", actor.Body, new Rect2(top, bottom - top).Abs(),
                    actor.Entity.IsAlive ? actor.Entity.GetPowerAmount<FlameBarrierPower>() : 0,
                    actor.Entity.IsAlive ? actor.Entity.GetPowerAmount<PlatingPower>() : 0,
                    actor.Entity.IsAlive);
                await actor.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }
        catch (Exception e) { GD.PushWarning("[ElementalDefenseVfx] Actor tracking stopped: " + e.Message); }
        finally { tracking.Remove(actor); }
    }

    private static void Focus(NHandCardHolder __instance) => Select(__instance, false);
    private static void Drag(NHandCardHolder __instance) => Select(__instance, true);
    private static void Select(NHandCardHolder holder, bool dragging)
    {
        var card = holder.CardNode?.Model;
        if (!Supported(card) || holder.InSelectMode) return;
        Ensure(card!.Owner.Creature.GetCreatureNode())?.Call("select_card", holder, card is FlameBarrier ? "fire" : "stone", dragging);
    }
    private static void Unfocus(NHandCardHolder __instance) => ClearSelection(__instance, false);
    private static void Cancel(NHandCardHolder __instance) => ClearSelection(__instance, true);
    private static void EndSelection(NCardPlay __instance) => ClearSelection(__instance.Holder, true);
    private static void ClearSelection(NHandCardHolder holder, bool force)
    {
        var card = holder.CardNode?.Model;
        if (!Supported(card)) return;
        var actor = card!.Owner.Creature.GetCreatureNode();
        actor?.Visuals.GetNodeOrNull<Node2D>(FxName)?.Call("unselect_card", holder, force);
    }
    private static void Played(CardModel __instance)
    {
        Ensure(__instance.Owner.Creature.GetCreatureNode())?.Call("cast", __instance is FlameBarrier ? "fire" : "stone");
    }
    private static void Retaliate(FlameBarrierPower __instance, Creature target, ValueProp props, Creature? dealer)
    {
        // Exactly the native predicate: includes fully blocked attacks, excludes poison and other non-attacks.
        if (target != __instance.Owner || dealer == null || !props.IsPoweredAttack()) return;
        var source = target.GetCreatureNode();
        var destination = dealer.GetCreatureNode();
        var room = NCombatRoom.Instance;
        if (source == null || destination == null || room == null) return;
        var bolt = new Node2D { Name = "FlameBarrierRetaliation", ZAsRelative = true, ZIndex = 0 };
        bolt.SetScript(script);
        Configure(bolt);
        room.CombatVfxContainer.AddChild(bolt);
        bolt.Call("launch", source.Visuals.VfxSpawnPosition, destination.Visuals.VfxSpawnPosition);
    }
}
