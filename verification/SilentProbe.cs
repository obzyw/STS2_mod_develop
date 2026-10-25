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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using NecrobinderVfx;
using Environment = System.Environment;

namespace STS2CompatProbe;

/// <summary>Real-frame regression for MeleeAttack/MoreSilentAnimations ownership.</summary>
internal static class SilentProbe
{
    internal static void Install(Harmony harmony)
    {
        var create = typeof(Player).GetMethods().Single(m => m.Name == "CreateForNewRun" && !m.IsGenericMethod && m.GetParameters()[0].ParameterType == typeof(CharacterModel));
        harmony.Patch(create, prefix: new HarmonyMethod(typeof(SilentProbe), nameof(ChooseSilent)));
        harmony.Patch(AccessTools.Method(typeof(NativeDiagnostics), "RunCompatibility"), prefix: new HarmonyMethod(typeof(SilentProbe), nameof(Suite)));
        GD.Print("[SilentProbe] Real Silent fixture installed.");
    }

    private static void ChooseSilent(ref CharacterModel __0) { if (__0 is Necrobinder) __0 = ModelDb.Character<Silent>(); }
    private static bool Suite(NGame __0, Player __1, ref Task __result) { __result = Run(__0, __1); return false; }
    private static async Task Run(NGame game, Player player)
    {
        var output = Environment.GetEnvironmentVariable("NECROVFX_TEST_OUTPUT")!;
        var rows = new List<object>();
        Exception? failure = null;
        var melee = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MeleeAttack");
        var settings = melee.GetType("MeleeAttack.SettingsUI")!;
        AccessTools.Method(settings, "IsTeleportEnabledForCharacter").Invoke(null, ["Silent"]);
        var config = (Dictionary<string, bool>)AccessTools.Field(settings, "_configCache").GetValue(null)!;
        var saved = new Dictionary<string, bool>(config);
        try
        {
            if (player.Character is not Silent) throw new Exception("Fixture is not Silent");
            // The user's starter-relic rework prompts for a discard before the
            // first playable turn. This motion fixture does not exercise relics.
            foreach (var relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
            MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer.Instance?.Clear();
            var enemies = await (Task<Creature[]>)AccessTools.Method(typeof(NativeDiagnostics), "EnterSoulCombat").Invoke(null, [game, player])!;
            foreach (var enemy in enemies) await CreatureCmd.SetMaxAndCurrentHp(enemy, 10000m);
            var actor = NCombatRoom.Instance.GetCreatureNode(player.Creature) ?? throw new Exception("Silent actor missing");
            var home = actor.Position;
            var entry = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "SilentMovementCompat").GetType("SilentMovementCompat.Entry")!;
            int controlDuplicates = 0;
            foreach (bool patched in new[] {false, true})
            {
                new Harmony("local.sts2.silent-movement-compat").UnpatchAll("local.sts2.silent-movement-compat");
                if (patched) AccessTools.Method(entry, "Initialize").Invoke(null, null);
                foreach (bool toggle in new[] {false, true})
                {
                    config["meleeattack_global_teleport"] = toggle;
                    config["meleeattack_silent"] = toggle;
                    await Play<Neutralize>();
                    await Play<PoisonedStab>();
                    await Play<Backstab>();
                    async Task Play<T>() where T : CardModel
                    {
                        actor.Position = home;
                        var card = CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<T>(), player);
                        await CardPileCmd.Add(card, PileType.Hand);
                        int frames = 0, duplicates = 0, displayFrames = 0;
                        float displacement = 0;
                        var body = actor.Body;
                        void Sample()
                        {
                            var display = Find(game, "MoreSilentDisplay");
                            if (display is null || !display.IsVisibleInTree() || display.Modulate.A < .1f) return;
                            displayFrames++;
                            displacement = Math.Max(displacement, display.GlobalPosition.DistanceTo(body.GlobalPosition));
                            if (body.Modulate.A > .1f) duplicates++;
                            frames++;
                        }
                        game.GetTree().ProcessFrame += Sample;
                        int hp = enemies[1].CurrentHp;
                        try { await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, enemies[1]); await Wait(game, 2.5); }
                        finally { game.GetTree().ProcessFrame -= Sample; }
                        var row = new {card=typeof(T).Name, patched, toggle, frames, displayFrames, duplicates, displacement, damage=hp-enemies[1].CurrentHp};
                        rows.Add(row);
                        GD.Print("[SilentProbe] " + JsonSerializer.Serialize(row));
                        controlDuplicates += patched ? 0 : duplicates;
                        if (patched && (displayFrames == 0 || duplicates != 0 || displacement < 5)) throw new Exception("Silent ownership/visibility failed: " + JsonSerializer.Serialize(row));
                    }
                }
            }
            if (controlDuplicates == 0) throw new Exception("Reported duplication was not reproduced in control");
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            config.Clear(); foreach (var item in saved) config.Add(item.Key, item.Value);
            File.WriteAllText(Path.Combine(output, "silent-validation.json"), JsonSerializer.Serialize(new {success=failure is null,error=failure?.ToString(),cases=rows}, new JsonSerializerOptions {WriteIndented=true}));
        }
    }
    private static Node2D? Find(Node node, string name)
    {
        if (node.Name == name && node is Node2D item && item.IsVisibleInTree() && item.Modulate.A >= .1f) return item;
        foreach (var child in node.GetChildren()) { var match = Find(child, name); if (match is not null) return match; }
        return null;
    }
    private static async Task Wait(NGame game, double seconds)
    {
        for (double elapsed=0;elapsed<seconds;elapsed+=game.GetProcessDeltaTime()) await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
