using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace STS2CompatProbe;

/// <summary>Read-only menu check: opens character selection, never embarks or abandons a run.</summary>
internal static class WatcherMenuProbe
{
    private static bool started;

    internal static void Install() => new Harmony("local.sts2.watcher.menu.verification").Patch(
        AccessTools.Method(typeof(NMainMenu), "_Ready"),
        postfix: new HarmonyMethod(typeof(WatcherMenuProbe), nameof(Ready)));

    private static void Ready(NMainMenu __instance)
    {
        if (started) return;
        started = true;
        _ = Run(__instance);
    }

    private static async Task Run(NMainMenu menu)
    {
        string output = System.Environment.GetEnvironmentVariable("STS2_WATCHER_MENU_OUTPUT")!;
        try
        {
            var donor = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "WatcherReimagined");
            var settings = donor.GetType("WatcherMod.WatcherSettings", true)!;
            bool Read(string name) => (bool)settings.GetProperty(name)!.GetValue(null)!;
            if (Read("Motion") || Read("CardArt") || !Read("Effects") || Read("UseModMotion"))
                throw new InvalidOperationException("Expected effects-only author settings.");
            var tree = menu.GetTree();
            await menu.ToSignal(tree.CreateTimer(3), SceneTreeTimer.SignalName.Timeout);
            bool observeDisplay = System.Environment.GetEnvironmentVariable("STS2_DISPLAY_OBSERVATION") == "1";
            if (observeDisplay) await DisplayObservation.Compare(menu, "main-menu", output);
            var screen = menu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
            screen.InitializeSingleplayer();
            menu.SubmenuStack.Push(screen);
            await menu.ToSignal(tree.CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
            var button = screen.GetNode("CharSelectButtons/ButtonContainer").GetChildren()
                .OfType<NCharacterSelectButton>().Single(b => b.Character?.GetType().Assembly.GetName().Name == "Watcher"
                    && b.Character.GetType().FullName == "WatcherMod.Watcher");
            if (button.IsLocked) throw new InvalidOperationException("Watcher is locked; do not alter unlock state.");
            button.Select();
            await menu.ToSignal(tree.CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
            if (observeDisplay)
            {
                await DisplayObservation.Compare(menu, "character-selection", output);
                // Long observations may span user selection/focus changes; reselect before the rig assertion.
                button.Select();
                await menu.ToSignal(tree.CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
            }
            var background = screen.GetNode("AnimatedBg").GetNode(button.Character.Id.Entry + "_bg");
            var spine = background.GetNode<Node2D>("WatcherSpine");
            var skeleton = spine.Get("skeleton_data_res").AsGodotObject() as Resource;
            const string expected = "res://YUWatcherForms/animations/character_select/watcher/characterselect_watcher_skel_data.tres";
            bool passed = skeleton?.ResourcePath == expected && spine.IsVisibleInTree();
            var unified = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "STS2UnifiedCompat");
            var unifiedStatus = unified?.GetType("STS2UnifiedCompat.Entry")?.GetProperty("Status")?.GetValue(null) as Dictionary<string, string>;
            if (unifiedStatus != null && unifiedStatus.Values.All(v => v == "off"))
            {
                string[] featureOwners = ["obzyw.sts2.unified.necro", "local.sts2.silent-movement-compat", "local.sts2.silent-effects-compat", "obzyw.sts2.unified.regent", "obzyw.sts2.unified.watcher-preset", "local.sts2.watcher-divinity-pose", "local.sts2.watcher-stance-transitions"];
                passed &= unifiedStatus.Count == 7 && !Harmony.GetAllPatchedMethods().Any(m => Harmony.GetPatchInfo(m)!.Owners.Any(featureOwners.Contains));
            }
            File.WriteAllText(Path.Combine(output, "menu-result.json"), JsonSerializer.Serialize(new {
                success = passed, cardArt = Read("CardArt"), motion = Read("Motion"), effects = Read("Effects"),
                background = button.Character.CharacterSelectBg, skeleton = skeleton?.ResourcePath,
                expectedSkeleton = expected, visible = spine.IsVisibleInTree(), position = spine.Position.ToString(),
                unifiedStatus,
                display = new { fpsLimit = Engine.MaxFps, vsync = DisplayServer.WindowGetVsyncMode().ToString(),
                    refreshHz = DisplayServer.ScreenGetRefreshRate(DisplayServer.WindowGetCurrentScreen()),
                    windowMode = DisplayServer.WindowGetMode().ToString(), focused = menu.GetWindow().HasFocus() },
                scope = "Fresh process; actual character selection scene; no embark, combat or per-card assertion."
            }, new JsonSerializerOptions { WriteIndented = true }));
            // Save a native render as supporting evidence; the skeleton assertion is the acceptance check.
            await menu.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            menu.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, "selection.png"));
            tree.Quit(passed ? 0 : 1);
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "menu-result.json"), JsonSerializer.Serialize(new { success = false, error = ex.ToString() }));
            menu.GetTree().Quit(1);
        }
    }
}
