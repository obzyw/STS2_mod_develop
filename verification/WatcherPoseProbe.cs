using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using NecrobinderVfx;

namespace STS2CompatProbe;

/// <summary>One ordinary fight: enter divinity, play an attack, then leave the stance.</summary>
internal static class WatcherPoseProbe
{
    private static Assembly Watcher => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Watcher");
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(typeof(Player).GetMethods().Single(m => m.Name == "CreateForNewRun" && !m.IsGenericMethod && m.GetParameters()[0].ParameterType == typeof(CharacterModel)),
            prefix: new HarmonyMethod(typeof(WatcherPoseProbe), nameof(Choose)));
        harmony.Patch(AccessTools.Method(typeof(NativeDiagnostics), "RunCompatibility"), prefix: new HarmonyMethod(typeof(WatcherPoseProbe), nameof(Suite)));
    }
    private static void Choose(ref CharacterModel __0)
    {
        if (__0 is Necrobinder) __0 = (CharacterModel)typeof(ModelDb).GetMethods().Single(m => m.Name == "Character" && m.IsGenericMethodDefinition).MakeGenericMethod(Watcher.GetType("WatcherMod.Watcher", true)!).Invoke(null, null)!;
    }
    private static bool Suite(NGame __0, Player __1, ref Task __result) { __result = Run(__0, __1); return false; }
    private static async Task Run(NGame game, Player player)
    {
        string output = System.Environment.GetEnvironmentVariable("NECROVFX_TEST_OUTPUT")!;
        var cases = new List<object>();
        Exception? error = null;
        try
        {
            foreach (var relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
            MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer.Instance?.Clear();
            var enemies = await (Task<MegaCrit.Sts2.Core.Entities.Creatures.Creature[]>)AccessTools.Method(typeof(NativeDiagnostics), "EnterSoulCombat").Invoke(null, [game, player])!;
            var helper = Watcher.GetType("WatcherMod.WatcherCombatHelper", true)!;
            await (Task)AccessTools.Method(helper, "EnterDivinity").Invoke(null, [player, null])!;
            await Wait(game, 1.2);
            var actor = NCombatRoom.Instance!.GetCreatureNode(player.Creature)!;
            var pose = Descendants(actor).FirstOrDefault(n => n.Name == "WatcherDivinityPose" && n.Get("active").AsBool());
            Require(pose != null, "Divinity arm display was not activated.");
            var audit = pose!.Call("audit").AsGodotDictionary();
            Require(audit["halo_alpha"].AsDouble() > .9 && audit["arm_layers"].AsInt32() == 2, "Original halo or arm layers missing.");
            Require(((CanvasItem)pose.GetParent()).Visible, "Original body was hidden.");
            Save(game, output, "divinity-idle");
            cases.Add(new { id = "divinity-idle", haloAlpha = audit["halo_alpha"].AsDouble(), originalBodyVisible = true });
            var cardType = Watcher.GetType("WatcherMod.WatcherStrike_P", true)!;
            var canonical = (CardModel)typeof(ModelDb).GetMethods().Single(m => m.Name == "Card" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(cardType).Invoke(null, null)!;
            var card = CombatManager.Instance.DebugOnlyGetState()!.CreateCard(canonical, player);
            await CardPileCmd.Add(card, PileType.Hand);
            player.PlayerCombatState!.Energy = 99;
            decimal hp = enemies[1].CurrentHp;
            await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, enemies[1]);
            await Wait(game, 1.3);
            Require(enemies[1].CurrentHp < hp, "Attack did not resolve damage.");
            Require(pose.Get("active").AsBool(), "Open arms did not resume after the attack.");
            Save(game, output, "after-attack");
            cases.Add(new { id = "attack-resume", damage = hp - enemies[1].CurrentHp });
            await (Task)AccessTools.Method(helper, "ExitStance").Invoke(null, [player])!;
            await Wait(game, .8);
            Require(!GodotObject.IsInstanceValid(pose) || !((CanvasItem)pose).IsVisibleInTree(), "Divinity arms remained after leaving the stance.");
            Save(game, output, "stance-exit");
            cases.Add(new { id = "stance-exit", restored = true });
        }
        catch (Exception ex) { error = ex; throw; }
        finally { File.WriteAllText(Path.Combine(output, "watcher-pose-validation.json"), JsonSerializer.Serialize(new { success = error == null, error = error?.ToString(), expectedCases = 3, cases }, new JsonSerializerOptions { WriteIndented = true })); }
    }
    private static IEnumerable<Node> Descendants(Node node) { foreach (var child in node.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static async Task Wait(NGame game, double seconds) { ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000); while (Time.GetTicksMsec() < end) await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Save(NGame game, string output, string id) { using var image = game.GetViewport().GetTexture().GetImage(); image.SavePng(Path.Combine(output, id + ".png")); }
}
