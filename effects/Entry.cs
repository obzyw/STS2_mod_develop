using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SilentEffectsCompat;

/// <summary>Coordinates the audited three visual mods without editing their files or preferences.</summary>
#if !INTEGRATED
[ModInitializer(nameof(Initialize))]
#endif
public static class Entry
{
    private const string Owner = "local.sts2.silent-effects-compat";
    private static readonly Harmony Harmony = new(Owner);
    private static Func<string, bool> fxEnabled = null!, sharedEnabled = null!, skillEnabled = null!;
    private static Func<bool> globalSkills = null!;
    private static PropertyInfo msaCurrent = null!, msaEnabled = null!, msaDisabled = null!;
    private static readonly HashSet<string> fxPlayback = new(StringComparer.Ordinal);
    private static readonly HashSet<string> msaCards = new(StringComparer.Ordinal);
    private static AsyncLocal<AttackCommand> attack = null!;
    private sealed record PlayFrame(CardModel Card, PlayFrame? Previous);
    private static readonly AsyncLocal<PlayFrame?> plays = new();
    private static readonly Dictionary<MethodBase, string?> guards = new();
    private static MethodInfo claimHeld = null!, endHeld = null!;
    private static readonly Dictionary<string, string> skills = new()
    {
        ["bullet_time"]="BulletTime", ["knife_trap"]="KnifeTrap", ["backflip"]="Backflip",
        ["grand_finale"]="GrandFinale", ["wraith_form"]="WraithForm", ["afterimage"]="Afterimage",
        ["slice"]="Slice", ["pinpoint"]="Pinpoint", ["blur"]="Blur", ["burst"]="Burst", ["murder"]="Murder"
    };

    /// <summary>Fails closed on a changed binary; removes this mod's partial patches on initialization failure.</summary>
    public static void Initialize()
    {
        Harmony.UnpatchAll(Owner);
        guards.Clear(); fxPlayback.Clear(); msaCards.Clear();
        var expected = new Dictionary<string, string>
        {
            ["sts2"]="0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9",
            ["MeleeAttack"]="7BB0DE0F078D97EDFEBB0D4494B5B0A1224CF75DA940971EC6106870F4AF663B",
            ["MoreSilentAnimations"]="95F6E7A7C003FBA2B9FA1C64E470373DCBF349100BB5EB5C0173B7ED5C3D23AC",
            ["SilentFX"]="F0FEF25F3E01F7827F03FA394D366511D09D13826B20689318F474622366CCC7"
        };
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        try
        {
            foreach (var pair in expected)
            {
                var assembly = assemblies.SingleOrDefault(a => a.GetName().Name == pair.Key);
                if (assembly is null || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) != pair.Value)
                    throw new InvalidOperationException("Unsupported or missing " + pair.Key);
            }
            var melee = assemblies.Single(a => a.GetName().Name == "MeleeAttack");
            var msa = assemblies.Single(a => a.GetName().Name == "MoreSilentAnimations");
            var fx = assemblies.Single(a => a.GetName().Name == "SilentFX");
            var settings = melee.GetType("MeleeAttack.SettingsUI", true)!;
            skillEnabled = Method(settings, "IsSkillEnabled").CreateDelegate<Func<string, bool>>();
            globalSkills = Method(settings, "IsGlobalSkillsEnabled").CreateDelegate<Func<bool>>();
            attack = (AsyncLocal<AttackCommand>)AccessTools.Field(melee.GetType("MeleeAttack.SimpleTeleportPatch"), "_currentAttack").GetValue(null)!;
            var fxSettings = fx.GetType("SilentFX.Core.CardFxSettings", true)!;
            fxEnabled = AccessTools.Method(fxSettings, "Enabled", [typeof(string)]).CreateDelegate<Func<string, bool>>();
            sharedEnabled = Method(fxSettings, "SharedEnabled").CreateDelegate<Func<string, bool>>();
            var msaSettings = msa.GetType("MoreSilentAnimations.AnimationSettings", true)!;
            msaCurrent = msaSettings.GetProperty("Current")!;
            msaEnabled = msaSettings.GetProperty("Enabled")!;
            msaDisabled = msaSettings.GetProperty("DisabledCards")!;
            msaCards.UnionWith((string[])AccessTools.Field(msa.GetType("MoreSilentAnimations.SettingsState"), "Cards").GetValue(null)!);

            // Use actual playback registrations, not the larger preference list:
            // hand-only effects (e.g. DaggerThrow) and later poison can coexist.
            AddKeys(fx.GetType("SilentFX.Cards.CardFxManager", true)!, "Map");
            AddKeys(fx.GetType("SilentFX.Cards.ShivFx", true)!, "Styles");
            fxPlayback.Add("Shiv");
            var option = Method(fxSettings, "PowerOption").CreateDelegate<Func<string, string?>>();
            foreach (Type power in ((IDictionary)AccessTools.Field(fx.GetType("SilentFX.Powers.PowerFxManager"), "Factories").GetValue(null)!).Keys)
            {
                var name = option(power.Name);
                if (name is not null && !name.StartsWith("shared.", StringComparison.Ordinal)) fxPlayback.Add(name);
            }

            Harmony.Patch(Method(settings, "IsSkillEnabled"), postfix: Hook(nameof(FilterSkill)));
            Harmony.Patch(AccessTools.Method(msaSettings, "Allows", [typeof(string)]), postfix: Hook(nameof(FilterMsa)));
            // Remove the author's whole-card veto. Its moving-anchor and tint
            // synchronization patches remain active; Held -> OnPlay stays intact.
            Harmony.Patch(Method(msa.GetType("MoreSilentAnimations.SilentFxCompatibility", true)!, "Owns"), prefix: Hook(nameof(ReleaseFx)));
            Harmony.Patch(Method(typeof(CardModel), "OnPlayWrapper"),
                prefix: new HarmonyMethod(typeof(Entry), nameof(BeginPlay)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(Entry), nameof(EndPlay)) { priority = Priority.Last },
                finalizer: Hook(nameof(EndPlay)));
            // MoreSilent deliberately bypasses its Shiv switch inside its own
            // KnifeTrap invocation; the higher-priority source still wins there.
            Harmony.Patch(Method(msa.GetType("MoreSilentAnimations.ShivPresentationPatch", true)!, "Prefix"), prefix: Hook(nameof(AllowMsaShiv)));
            // KnifeTrap keeps SilentFX's aim preview, then hands playback and
            // its generated Shivs to MoreSilent. Ordinary Shivs keep their priority.
            claimHeld = Method(fx.GetType("SilentFX.Cards.HeldFxManager", true)!, "Claim");
            endHeld = Method(fx.GetType("SilentFX.Cards.HeldSession", true)!, "End");
            Harmony.Patch(Method(fx.GetType("SilentFX.Cards.CardFxManager", true)!, "OnCardPlayed"), prefix: Hook(nameof(SplitTrap)));
            Harmony.Patch(AccessTools.Method(fxSettings, "Enabled", [typeof(string)]), postfix: Hook(nameof(FilterTrapShiv)));
            FinaleBow.Configure(msa);
            Harmony.Patch(Method(melee.GetType("MeleeAttack.SequenceScreenEffect", true)!, "PlayShunshaEffectAsync"), postfix: Hook(nameof(AddFinaleBow)));

            Guard("MurderSettlementEffect+MurderBeforeDamagePatch", "Postfix", "murder");
            Guard("SimpleTeleportPatch+DefectDamageReceivedPatch", "Postfix");
            Guard("SimpleTeleportPatch+IroncladDamageReceivedPatch", "Postfix");
            Guard("SimpleTeleportPatch+NecroDamageReceivedPatch", "Postfix");
            Guard("SimpleTeleportPatch+GrandFinaleDamageReceivedPatch", "Postfix");
            Guard("ShivUpgradedEffectPatch+AfterDamageReceivedPatch", "Postfix");
            Guard("FloatingHitEffect+KnockbackPatch", "Postfix");
            Guard("InkBladeEffect+CardOnPlayPatch", "Prefix");
            Guard("InkBladeEffect+InkBladeDamagePatch", "Postfix");
            Guard("KnifeCardsAnimPatch+TriggerAnimKnifePatch", "Prefix");
            Guard("DelayDamagePatch+CardOnPlayPatch", "Prefix", "murder");
            Guard("DelayDamagePatch+TriggerAnimPatch", "Prefix", "murder");
            Guard("AttackZoomEffect+BeforeDamagePatch", "Postfix");
            Guard("AttackZoomEffect+GrandFinaleCardEnqueueVfxPatch", "Prefix");
            Guard("AttackMotionBlur+BlurTriggerAnimPatch", "Prefix");
            Guard("CardMirrorEffect+GrandFinaleWhiteFlashPatch", "Postfix", "grand_finale");
            // A queued NoDraw event may arrive after its initial card callback.
            Guard("BulletTimeEffect", "StartBulletTime", "bullet_time");
            Harmony.Patch(Method(melee.GetType("MeleeAttack.AlphaBreath", true)!, "OnCreaturePowerApplied"), prefix: Hook(nameof(AllowIntangible)));
            GD.Print($"[SilentEffectsCompat] Loaded: {msaCards.Count} animation cards audited, {fxPlayback.Count} SilentFX playback sources; GrandFinale exception active.");

            void AddKeys(Type type, string field)
            {
                foreach (Type card in ((IDictionary)AccessTools.Field(type, field).GetValue(null)!).Keys) fxPlayback.Add(card.Name);
            }
            void Guard(string type, string method, string? skill = null)
            {
                var target = Method(melee.GetType("MeleeAttack." + type, true)!, method);
                guards.Add(target, skill);
                Harmony.Patch(target, prefix: Hook(target.ReturnType == typeof(bool) ? nameof(AllowReplacement) : nameof(AllowEffect)));
            }
        }
        catch (Exception error)
        {
            Harmony.UnpatchAll(Owner);
            GD.PushWarning("[SilentEffectsCompat] No patches retained: " + error);
        }
    }

    private static MethodInfo Method(Type type, string name) => AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);
    private static HarmonyMethod Hook(string name) => new(typeof(Entry), name);
    private static bool FxOwns(string card) => !(card == "KnifeTrap" && RawMsaAllows("KnifeTrap")) && fxPlayback.Contains(card) && fxEnabled(card);
    private static bool InTrap()
    {
        for (var frame = plays.Value; frame is not null; frame = frame.Previous)
            if (frame.Card.GetType().Name == "KnifeTrap") return true;
        return false;
    }
    private static bool SilentOwns(string card) => FxOwns(card) || (card == "Shiv" && InTrap() && FxOwns("KnifeTrap"));
    private static bool RawMsaAllows(string card)
    {
        var current = msaCurrent.GetValue(null)!;
        return msaCards.Contains(card) && (bool)msaEnabled.GetValue(current)! && !((string[])msaDisabled.GetValue(current)!).Contains(card, StringComparer.Ordinal);
    }
    private static bool HigherOwns(string card) => card != "GrandFinale" && (SilentOwns(card) || RawMsaAllows(card));
    private static void FilterSkill(string __0, ref bool __result)
    {
        if (__result && __0.StartsWith("meleeattack_skill_", StringComparison.Ordinal) && skills.TryGetValue(__0[18..], out var card) && HigherOwns(card)) __result = false;
    }
    private static void FilterMsa(string __0, ref bool __result)
    {
        if (!__result || !msaCards.Contains(__0)) return;
        __result = __0 == "GrandFinale" ? !skillEnabled("meleeattack_skill_grand_finale") : !SilentOwns(__0);
    }
    private static bool ReleaseFx(ref bool __result) { __result = false; return false; }
    private static bool SplitTrap(CardPlay __0)
    {
        if (__0.Card.GetType().Name != "KnifeTrap" || __0.Card.Owner?.Character is not Silent || !RawMsaAllows("KnifeTrap")) return true;
        var held = claimHeld.Invoke(null, [__0.Card]);
        if (held is not null) endHeld.Invoke(held, null);
        return false;
    }
    private static void FilterTrapShiv(string __0, ref bool __result)
    {
        if (__result && __0 == "Shiv" && InTrap() && RawMsaAllows("KnifeTrap")) __result = false;
    }
    private static void AddFinaleBow(MegaCrit.Sts2.Core.Entities.Creatures.Creature? __0)
    {
        if (__0?.Player?.Character is Silent && skillEnabled("meleeattack_skill_grand_finale") && RawMsaAllows("GrandFinale"))
            FinaleBow.Start(__0.GetCreatureNode());
    }
    private static void BeginPlay(CardModel __instance, out PlayFrame? __state)
    {
        __state = plays.Value;
        plays.Value = new PlayFrame(__instance, __state);
    }
    private static void EndPlay(PlayFrame? __state) => plays.Value = __state;
    private static bool AllowMsaShiv(AttackCommand __0) => __0.ModelSource is not CardModel card ||
        card.Owner?.Character is not Silent || !SilentOwns(card.GetType().Name);

    private static bool AllowEffect(MethodBase __originalMethod, object[] __args)
    {
        if (!globalSkills()) return false;
        var skill = guards[__originalMethod];
        if (skill is not null && !skillEnabled("meleeattack_skill_" + skill)) return false;
        var card = __args.OfType<CardModel>().FirstOrDefault() ?? attack.Value?.ModelSource as CardModel;
        if (card?.Owner?.Character is not Silent) return true;
        if (card.GetType().Name == "Shiv" && InTrap() && !skillEnabled("meleeattack_skill_knife_trap")) return false;
        var cardSkill = skills.FirstOrDefault(p => p.Value == card.GetType().Name).Key;
        return (cardSkill is null || skillEnabled("meleeattack_skill_" + cardSkill)) && !HigherOwns(card.GetType().Name);
    }
    private static bool AllowReplacement(MethodBase __originalMethod, object[] __args, ref bool __result)
    {
        if (AllowEffect(__originalMethod, __args)) return true;
        // The intercepted method itself is a Harmony prefix. Its true return
        // lets the game's native animation execute when Melee declines it.
        __result = true;
        return false;
    }
    private static bool AllowIntangible(PowerModel __0) => globalSkills() &&
        !(__0?.Owner?.Player?.Character is Silent && sharedEnabled("Intangible"));
}
