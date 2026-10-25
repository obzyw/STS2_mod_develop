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

/// <summary>One normal battle: the four stance destinations and an actual divinity attack.</summary>
internal static class StanceTransitionProbe
{
    private static Assembly Watcher => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Watcher");
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(typeof(Player).GetMethods().Single(m => m.Name == "CreateForNewRun" && !m.IsGenericMethod && m.GetParameters()[0].ParameterType == typeof(CharacterModel)), prefix: new HarmonyMethod(typeof(StanceTransitionProbe), nameof(Choose)));
        harmony.Patch(AccessTools.Method(typeof(NativeDiagnostics), "RunCompatibility"), prefix: new HarmonyMethod(typeof(StanceTransitionProbe), nameof(Suite)));
    }
    private static void Choose(ref CharacterModel __0)
    {
        if (__0 is Necrobinder) __0 = (CharacterModel)typeof(ModelDb).GetMethods().Single(m => m.Name == "Character" && m.IsGenericMethodDefinition).MakeGenericMethod(Watcher.GetType("WatcherMod.Watcher", true)!).Invoke(null, null)!;
    }
    private static bool Suite(NGame __0, Player __1, ref Task __result) { __result = Run(__0, __1); return false; }
    private static async Task Run(NGame game, Player player)
    {
        string output = System.Environment.GetEnvironmentVariable("NECROVFX_TEST_OUTPUT")!;
        var cases = new List<object>(); Exception? error = null;
        try
        {
            foreach (var relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
            MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer.Instance?.Clear();
            var enemies = await (Task<MegaCrit.Sts2.Core.Entities.Creatures.Creature[]>)AccessTools.Method(typeof(NativeDiagnostics), "EnterSoulCombat").Invoke(null, [game, player])!;
            var actor = NCombatRoom.Instance!.GetCreatureNode(player.Creature)!;
            var helper = Watcher.GetType("WatcherMod.WatcherCombatHelper", true)!;
            foreach (string destination in new[] { "Calm", "Wrath", "Divinity", "Normal" })
            {
                var oldFx = Descendants(actor).FirstOrDefault(n => n.Name == "WatcherStanceTransition");
                int started = oldFx?.Get("started_count").AsInt32() ?? 0;
                Task change = (Task)AccessTools.Method(helper, destination == "Normal" ? "ExitStance" : "Enter" + destination).Invoke(null, destination == "Normal" ? [player] : [player, null])!;
                Node? fx = null; bool sampled = false;
                ulong deadline = Time.GetTicksMsec() + 10000;
                while (Time.GetTicksMsec() < deadline)
                {
                    fx = Descendants(actor).FirstOrDefault(n => n.Name == "WatcherStanceTransition");
                    if (fx != null && fx.Get("started_count").AsInt32() > started)
                    {
                        double progress = fx.Get("progress").AsDouble();
                        if (!sampled && progress > .25 && progress < .55)
                        {
                            Save(game, output, destination + "-transition"); sampled = true;
                        }
                        if (!fx.Get("running").AsBool()) break;
                    }
                    await Wait(game, .016);
                }
                await change;
                Require(fx != null && fx.Get("last_completed").AsString() == destination && sampled, "Transition did not animate and finish: " + destination);
                var body = (Node2D)fx!.Get("current").AsGodotObject();
                Require(body.Visible && body.Modulate.A > .99f, "New stance stayed hidden: " + destination);
                Save(game, output, destination + "-settled");
                cases.Add(new { id = "to-" + destination, intermediateFrame = sampled, completed = true });
                if (destination == "Divinity")
                {
                    var pose = Descendants(actor).FirstOrDefault(n => n.Name == "WatcherDivinityPose" && n.Get("active").AsBool());
                    Require(pose != null && pose.Call("audit").AsGodotDictionary()["halo_alpha"].AsDouble() > .9, "Divine arms or original halo missing after transition.");
                    var type = Watcher.GetType("WatcherMod.WatcherStrike_P", true)!;
                    var canonical = (CardModel)typeof(ModelDb).GetMethods().Single(m => m.Name == "Card" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(type).Invoke(null, null)!;
                    var card = CombatManager.Instance.DebugOnlyGetState()!.CreateCard(canonical, player);
                    await CardPileCmd.Add(card, PileType.Hand); player.PlayerCombatState!.Energy = 99;
                    decimal hp = enemies[1].CurrentHp;
                    await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, enemies[1]);
                    await Wait(game, 1.1);
                    Require(enemies[1].CurrentHp < hp && pose!.Get("active").AsBool(), "Attack or divine idle resume failed.");
                    cases.Add(new { id = "divinity-attack", damage = hp - enemies[1].CurrentHp, resumed = true });
                }
            }
        }
        catch (Exception ex) { error = ex; throw; }
        finally { File.WriteAllText(Path.Combine(output, "stance-transition-validation.json"), JsonSerializer.Serialize(new { success = error == null, error = error?.ToString(), expectedCases = 5, cases }, new JsonSerializerOptions { WriteIndented = true })); }
    }
    private static IEnumerable<Node> Descendants(Node node) { foreach (var child in node.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static async Task Wait(NGame game, double seconds) { ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000); while (Time.GetTicksMsec() < end) await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Save(NGame game, string output, string id) { using var image = game.GetViewport().GetTexture().GetImage(); image.SavePng(Path.Combine(output, id + ".png")); }
}
