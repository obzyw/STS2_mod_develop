using System.Reflection;
using System.Security.Cryptography;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using ModConfig;

namespace STS2UnifiedCompat;

/// <summary>One distributable DLL. Character switches are snapshotted once at startup; changes require restart.</summary>
[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public const string Id = "STS2UnifiedCompat";
    private static bool started;
    private static readonly Dictionary<string, bool> enabled = new();
    public static Dictionary<string, string> Status { get; } = new();
    private static readonly (string Key, string Label, bool Default)[] options = [
        ("necro.enabled", "亡灵契约师：启用本组功能", false),
        ("necro.gold", "　金卡恢复与低优先级兼容", true),
        ("silent.enabled", "静默猎手：启用本组功能", false),
        ("silent.movement", "　由更多猎人动画负责攻击位移", true),
        ("silent.effects", "　三方特效、开关修复及两张牌混合例外", true),
        ("regent.enabled", "储君：启用本组功能", false),
        ("regent.effects", "　四能力共存、五攻击万象优先", true),
        ("watcher.enabled", "观者：启用本组功能", false),
        ("watcher.effects_only", "　保留美化人物与卡面，使用重塑特效音效", true),
        ("watcher.pose", "　神格摊掌与原光影共存", true),
        ("watcher.transitions", "　姿态之间的过渡动画", true)
    ];

    public static void Initialize()
    {
        ModConfigApi.Register(Id, "角色动画与特效整合（开关重启生效）", options.Select(o => new ConfigEntry
        {
            Key = o.Key, Label = o.Label, Type = ConfigType.Toggle, DefaultValue = o.Default,
            Description = "开关仅控制本整合包的功能；不禁用原作者Mod。修改后请完整退出并重启游戏。"
        }).ToArray());
        foreach (var option in options) enabled[option.Key] = ModConfigApi.GetValue<bool>(Id, option.Key);
        // Mod loading runs inside NGame._Ready; the first main menu is the later, stable initialization point.
        new Harmony("obzyw.sts2.unified.bootstrap").Patch(AccessTools.Method(typeof(NMainMenu), "_Ready"),
            prefix: new HarmonyMethod(typeof(Entry), nameof(Start)) { priority = Priority.First });
    }

    private static void Start()
    {
        if (started) return;
        started = true;
        Run("necro.gold", () => { Require("NecrobinderVfx", "1C68A4699FDC0BB25BC0737FE811E9A368442A47D922F9A6B3F493DED28433AA"); Necro.Install(); });
        Run("silent.movement", SilentMovementCompat.Entry.Initialize);
        Run("silent.effects", SilentEffectsCompat.Entry.Initialize);
        Run("regent.effects", Presets.Regent);
        Run("watcher.effects_only", Presets.Watcher);
        Run("watcher.pose", () => { RequireYu(); WatcherDivinityPose.Entry.Initialize(); });
        Run("watcher.transitions", () => { RequireYu(); WatcherStanceTransitions.Entry.Initialize(); });
        foreach (var result in Status) GD.Print($"[{Id}] {result.Key}: {result.Value}");
    }

    private static void Run(string key, Action action)
    {
        if (!enabled[key.Split('.')[0] + ".enabled"] || !enabled[key]) { Status[key] = "off"; return; }
        try { action(); Status[key] = "installed"; }
        catch (Exception error) { Status[key] = "unavailable: " + error.GetBaseException().Message; GD.PushWarning($"[{Id}] {key}: {error}"); }
    }

    internal static Assembly Require(string name, string? hash = null)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == name)
            ?? throw new InvalidOperationException("Missing enabled prerequisite: " + name);
        if (hash != null && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) != hash)
            throw new InvalidOperationException("Unsupported updated prerequisite: " + name);
        return assembly;
    }
    private static void RequireYu() => Require("YUWatcherForms", "08BA9AA8606E0AA6FAA0971E537C13D22E523BF6A280D3C169E6942552A6DE37");
}
