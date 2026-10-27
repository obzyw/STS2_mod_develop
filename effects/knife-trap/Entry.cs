using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;

namespace KnifeTrapStages;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    private static readonly AsyncLocal<CardModel?> trap=new();
    private static Func<string,bool> allows=null!;
    private static MethodInfo claim=null!,end=null!;
    public static int Handovers { get; private set; }
    public static void Initialize()
    {
        Type M(string n)=>AccessTools.TypeByName("MoreSilentAnimations."+n)??throw new TypeLoadException(n);
        Type S(string n)=>AccessTools.TypeByName("SilentFX."+n)??throw new TypeLoadException(n);
        allows=AccessTools.Method(M("AnimationSettings"),"Allows",[typeof(string)]).CreateDelegate<Func<string,bool>>();
        claim=AccessTools.Method(S("Cards.HeldFxManager"),"Claim");
        end=AccessTools.Method(S("Cards.HeldSession"),"End");
        var harmony=new Harmony("obzyw.sts2.knife-trap-stages");
        try
        {
            void Patch(MethodBase method,string hook)=>harmony.Patch(method,prefix:new HarmonyMethod(typeof(Entry),hook));
            Patch(AccessTools.Method(M("SilentFxCompatibility"),"Owns"),nameof(Preview));
            Patch(AccessTools.Method(S("Cards.CardFxManager"),"OnCardPlayed"),nameof(Play));
            harmony.Patch(AccessTools.Method(S("Core.CardFxSettings"),"Enabled",[typeof(string)]),postfix:new HarmonyMethod(typeof(Entry),nameof(Shiv)));
            harmony.Patch(AccessTools.Method(typeof(CardModel),"OnPlayWrapper"),
                prefix:new HarmonyMethod(typeof(Entry),nameof(Begin)){priority=Priority.First},
                postfix:new HarmonyMethod(typeof(Entry),nameof(Finish)){priority=Priority.Last},
                finalizer:new HarmonyMethod(typeof(Entry),nameof(Finish)));
            // Suppress only lower-priority visual callbacks inside this mother-card
            // invocation; independent Shivs and other cards retain their current rules.
            foreach(var (type,method) in new[]{
                ("ShivUpgradedEffectPatch+AfterDamageReceivedPatch","Postfix"),
                ("FloatingHitEffect+KnockbackPatch","Postfix"),
                ("KnifeCardsAnimPatch+TriggerAnimKnifePatch","Prefix"),
                ("SimpleTeleportPatch+CreatureCmdTriggerAnimPatch","Prefix")})
            {
                var t=AccessTools.TypeByName("MeleeAttack."+type);
                if(t==null) continue;
                var target=AccessTools.Method(t,method)??throw new MissingMethodException(type,method);
                Patch(target,target.ReturnType==typeof(bool)?nameof(AllowPrefix):nameof(Allow));
            }
            GD.Print("[KnifeTrapStages] SilentFX selection -> MoreSilentAnimations cast and generated Shivs.");
        }
        catch { harmony.UnpatchAll(harmony.Id);throw; }
    }
    private static bool IsTrap(CardModel? card)=>card is KnifeTrap&&card.Owner?.Character is Silent;
    private static bool Active=>trap.Value!=null&&allows("KnifeTrap");
    private static bool Preview(CardModel? __0,ref bool __result)
    {
        if(!IsTrap(__0)) return true;
        __result=false;return false;
    }
    private static bool Play(CardPlay __0)
    {
        if(!IsTrap(__0.Card)||!allows("KnifeTrap")) return true;
        var held=claim.Invoke(null,[__0.Card]);
        if(held!=null) end.Invoke(held,null);
        Handovers++;return false;
    }
    private static void Shiv(string __0,ref bool __result)
    {
        if(__0=="Shiv"&&Active) __result=false;
    }
    private static void Begin(CardModel __instance,out CardModel? __state)
    {
        __state=trap.Value;
        if(IsTrap(__instance)&&allows("KnifeTrap")) trap.Value=__instance;
    }
    private static void Finish(CardModel? __state)=>trap.Value=__state;
    private static bool Allow()=>!Active;
    private static bool AllowPrefix(ref bool __result)
    {
        if(!Active) return true;
        __result=true;return false;
    }
}
