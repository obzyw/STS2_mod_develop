using HarmonyLib;

namespace STS2UnifiedCompat;

/// <summary>Apply the accepted author-setting combinations at query time, without persisting author preferences.</summary>
internal static class Presets
{
    private static readonly HashSet<string> powers = ["BlackHole", "Genesis", "PillarOfCreation", "SpectrumShift"];
    private static readonly HashSet<string> attacks = ["Comet", "CrescentSpear", "DyingStar", "SevenStars", "AstralPulse"];

    internal static void Regent()
    {
        var fx = Entry.Require("RegentFX", "0F0262B3C4304C824C81D908CA7F3CEDABD00AF4C6B522A02513FF380C36EF06");
        var ra = Entry.Require("RegentAnimations", "389CEF1F20B66499E04C0CE12D9F33D3A8FB1A4DB9A158DEB45FE279D8BC31A2");
        var harmony = new Harmony("obzyw.sts2.unified.regent");
        harmony.Patch(AccessTools.Method(fx.GetType("RegentFX.ThirdParty.RitsuLibModConfig", true)!, "GetRitsuLibSettingBool"),
            postfix: new HarmonyMethod(typeof(Presets), nameof(FxToggle)));
        harmony.Patch(AccessTools.Method(ra.GetType("RegentAnimations.PresentationSettings", true)!, "CardEnabled"),
            postfix: new HarmonyMethod(typeof(Presets), nameof(AnimationToggle)));
    }
    private static void FxToggle(string __0, ref bool __result)
    {
        if ((__0.StartsWith("power_") && powers.Contains(__0[6..])) || (__0.StartsWith("card_") && attacks.Contains(__0[5..]))) __result = true;
    }
    private static void AnimationToggle(string __0, ref bool __result)
    {
        if (powers.Contains(__0)) __result = true;
        if (attacks.Contains(__0)) __result = false;
    }

    internal static void Watcher()
    {
        var assembly = Entry.Require("WatcherReimagined");
        var settings = assembly.GetType("WatcherMod.WatcherSettings", true)!;
        var harmony = new Harmony("obzyw.sts2.unified.watcher-preset");
        foreach (string property in new[] { "CardArt", "Motion", "UseModMotion" })
            harmony.Patch(AccessTools.PropertyGetter(settings, property), prefix: new HarmonyMethod(typeof(Presets), nameof(Off)));
        foreach (string property in new[] { "Effects", "EffectsActive" })
            harmony.Patch(AccessTools.PropertyGetter(settings, property), prefix: new HarmonyMethod(typeof(Presets), nameof(On)));
    }
    private static bool Off(ref bool __result) { __result = false; return false; }
    private static bool On(ref bool __result) { __result = true; return false; }
}
