using System.Collections;
using System.Diagnostics;
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
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;
using NecrobinderVfx;
using Environment = System.Environment;

namespace STS2CompatProbe;

/// <summary>Audits real permission methods, standby sessions, allocations and native card plays.</summary>
internal static class EffectsProbe
{
    private const string CompatOwner = "local.sts2.silent-effects-compat";
    private static readonly Harmony Observers = new("local.sts2.effects.observers");
    private static readonly Dictionary<string,int> hits = new();
    private static bool observing;
    private static void Count(string key) { if (observing) hits[key] = hits.GetValueOrDefault(key) + 1; }
    private static Type Fx(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "SilentFX").GetType("SilentFX." + name, true)!;
    private static Type Msa(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MoreSilentAnimations").GetType("MoreSilentAnimations." + name, true)!;
    private static object? Call(Type type, string method, params object?[] args) => AccessTools.Method(type, method).Invoke(null, args);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    internal static void Install(Harmony harmony)
    {
        var create = typeof(Player).GetMethods().Single(m => m.Name == "CreateForNewRun" && !m.IsGenericMethod && m.GetParameters()[0].ParameterType == typeof(CharacterModel));
        harmony.Patch(create, prefix: new HarmonyMethod(typeof(EffectsProbe), nameof(ChooseSilent)));
        harmony.Patch(AccessTools.Method(typeof(NativeDiagnostics), "RunCompatibility"), prefix: new HarmonyMethod(typeof(EffectsProbe), nameof(Suite)));
    }
    private static void ChooseSilent(ref CharacterModel __0) { if (__0 is Necrobinder) __0 = ModelDb.Character<Silent>(); }
    private static bool Suite(NGame __0, Player __1, ref Task __result) { __result = Run(__0, __1); return false; }
    private static void SawFx(MethodBase __originalMethod) {Count("fx." + __originalMethod.Name);Count("fx."+__originalMethod.DeclaringType!.Name+"."+__originalMethod.Name);}
    private static void SawPower(object __instance) => Count("fx.power." + __instance.GetType().Name);
    private static void SawMsa(MethodBase __originalMethod) => Count("msa." + __originalMethod.DeclaringType!.Name + "." + __originalMethod.Name);
    private static void SawBottle() => Count("native.bottle");
    private static void SawVfx(object[] __args)
    {
        if (!observing || !__args.OfType<string>().Any(s => s.Contains("grand_finale_impact", StringComparison.Ordinal))) return;
        if (new StackTrace().GetFrames().Any(f => f.GetMethod()?.DeclaringType?.Assembly.GetName().Name == "MeleeAttack")) Count("melee.impact");
    }
    private static void SawNode(Node node)
    {
        if (!observing) return;
        if (node.Name.ToString().StartsWith("SilentFX_Held", StringComparison.Ordinal)) Count("fx.heldNode");
        if (node.Name.ToString().StartsWith("MoreSilent", StringComparison.Ordinal)) Count("msa.node");
        if (node.Name.ToString()=="MoreSilentCompatFinaleBow")Count("msa.bow");
        if (node.SceneFilePath.Contains("谋杀02", StringComparison.Ordinal)) Count("melee.murderScene");
        if (node.SceneFilePath.Contains("华丽收场", StringComparison.Ordinal)) Count("melee.finaleScene");
    }

    private static async Task Run(NGame game, Player player)
    {
        var rows = new List<object>(); var routing = new List<object>();
        var output = Environment.GetEnvironmentVariable("NECROVFX_TEST_OUTPUT")!;
        var melee = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MeleeAttack");
        var settings = melee.GetType("MeleeAttack.SettingsUI", true)!;
        var entry = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "SilentEffectsCompat").GetType("SilentEffectsCompat.Entry", true)!;
        Call(settings, "IsGlobalSkillsEnabled");
        var config = (Dictionary<string,bool>)AccessTools.Field(settings, "_configCache").GetValue(null)!;
        var saved = new Dictionary<string,bool>(config);
        var snapshotField = AccessTools.Field(Fx("Core.PlayerSettings"), "_snapshot");
        _ = AccessTools.Property(Fx("Core.PlayerSettings"), "Enabled").GetValue(null);
        var snapshot = snapshotField.GetValue(null)!;
        var msaCurrent = AccessTools.Property(Msa("AnimationSettings"), "Current");
        var msaOriginal = msaCurrent.GetValue(null)!;
        var cardNames = (string[])AccessTools.Field(Msa("SettingsState"), "Cards").GetValue(null)!;
        var fxPlay = (HashSet<string>)AccessTools.Field(entry, "fxPlayback").GetValue(null)!;
        var heldMap = (IDictionary)AccessTools.Field(Fx("Cards.HeldFxManager"), "Map").GetValue(null)!;
        var map = (IDictionary)AccessTools.Field(Fx("Cards.CardFxManager"), "Map").GetValue(null)!;
        var cardFactory = typeof(ModelDb).GetMethods().Single(m => m.Name == "Card" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
        Exception? failure = null;
        bool differential=Environment.GetEnvironmentVariable("NECROVFX_TEST_CASES")=="effects-differential";
        bool confirm=Environment.GetEnvironmentVariable("NECROVFX_TEST_CASES")=="visual-confirm";
        bool visual=confirm || Environment.GetEnvironmentVariable("NECROVFX_TEST_CASES")=="visual-diagnostics";
        int expectedCases = confirm ? 2 : visual ? 6 : differential ? 18 : cardNames.Length + 27;
        var damages=new Dictionary<string,int[]>();
        try
        {
            Require(player.Character is Silent, "Fixture character is not Silent");
            Require(Harmony.GetAllPatchedMethods().Any(m => Harmony.GetPatchInfo(m)?.Owners.Contains(CompatOwner) == true), "Candidate failed initialization");
            Require(fxPlay.Intersect(cardNames).ToHashSet().SetEquals(cardNames.Except(new[]{"Slice","Neutralize","BouncingFlask","GrandFinale"})),"Playback registry disagrees with the independent 21-card source audit");
            foreach (var relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
            MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer.Instance?.Clear();
            var enemies = await (Task<Creature[]>)Call(typeof(NativeDiagnostics), "EnterSoulCombat", game, player)!;
            var actor = NCombatRoom.Instance!.GetCreatureNode(player.Creature)!;
            var home = actor.Position;
            var patchMap=cardNames.Append("DaggerThrow").Select(name=>
            {
                var type=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards."+name,true)!;
                var info=Harmony.GetPatchInfo(AccessTools.Method(type,"OnPlay"));
                return new {name,prefixes=info?.Prefixes.Select(p=>p.PatchMethod.DeclaringType?.FullName).ToArray(),transpilers=info?.Transpilers.Select(p=>p.PatchMethod.DeclaringType?.FullName).ToArray()};
            });
            File.WriteAllText(Path.Combine(output,"effect-patch-map.json"),JsonSerializer.Serialize(patchMap,new JsonSerializerOptions{WriteIndented=true}));
            game.GetTree().NodeAdded += SawNode;
            foreach (var fx in map.Values.Cast<object>())
                foreach (var method in fx.GetType().GetMethods().Where(m => m.Name is "OnPlay" or "OnHit" && m.DeclaringType != Fx("Cards.CardFx")))
                    if (Harmony.GetPatchInfo(method)?.Owners.Contains(Observers.Id) != true) Observers.Patch(method, postfix: new HarmonyMethod(typeof(EffectsProbe), nameof(SawFx)));
            Observers.Patch(AccessTools.Method(Fx("Cards.ShivFx"), "PlayCreate"), postfix: new HarmonyMethod(typeof(EffectsProbe), nameof(SawFx)));
            Observers.Patch(AccessTools.Method(Fx("Cards.ShivFx"), "OnShivHit"), postfix: new HarmonyMethod(typeof(EffectsProbe), nameof(SawFx)));
            Observers.Patch(AccessTools.Method(Fx("Powers.BasicPowerFx"), "Start"), postfix: new HarmonyMethod(typeof(EffectsProbe), nameof(SawPower)));
            foreach (var method in Msa("ActorPresentation").GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Where(m => m.Name is "Begin" or "BeginShiv" or "BeginFinale" or "BeginSteel"))
                Observers.Patch(method, postfix: new HarmonyMethod(typeof(EffectsProbe), nameof(SawMsa)));
            foreach (string name in new[] {"TrackingVisual", "ShivVisual", "KnifeTrapVisual", "BottleFlight"})
                foreach (var ctor in Msa(name).GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                    Observers.Patch(ctor, postfix: new HarmonyMethod(typeof(EffectsProbe), nameof(SawMsa)));
            foreach (var method in typeof(VfxCmd).GetMethods().Where(m => m.Name == "PlayOnCreatureCenter" && m.GetParameters().Any(p => p.ParameterType == typeof(string))))
                Observers.Patch(method, prefix: new HarmonyMethod(typeof(EffectsProbe), nameof(SawVfx)));
            Observers.Patch(AccessTools.Method(typeof(NItemThrowVfx),"Create"),postfix:new HarmonyMethod(typeof(EffectsProbe),nameof(SawBottle)));

            if(visual)
            {
                Engine.MaxFps=500;
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Adaptive);
                await RenderAudit.Sample(game,output,"idle-500-adaptive",4);
                Engine.MaxFps=165;
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
                await RenderAudit.Sample(game,output,"idle-165-enabled",4);
                foreach(string name in confirm?new[]{"KnifeTrap","GrandFinale"}:new[]{"Slice","Murder","KnifeTrap","GrandFinale","WraithForm","Shiv"})
                    await Play("visual-"+name,name,true,true,true,upgraded:confirm);
                await MegaCrit.Sts2.Core.Runs.RunManager.Instance.EnterRoomDebug(MegaCrit.Sts2.Core.Rooms.RoomType.Shop);
                await RenderAudit.Sample(game,output,"merchant-entry",3);
                NMerchantRoom.Instance!.OpenInventory();
                Require(NMerchantRoom.Instance.Inventory.IsOpen,"Merchant inventory did not open");
                await RenderAudit.Sample(game,output,"merchant-open",3);
                await Wait(game,2);
                await RenderAudit.Sample(game,output,"merchant-settled",2);
                Require(rows.Count==expectedCases,"Visual suite incomplete");
                return;
            }
            if(differential)
            {
                foreach(string name in new[]{"Murder","KnifeTrap","Slice","Shiv","Finisher","GrandFinale","Neutralize","PoisonedStab","PreciseCut"})
                {
                    foreach(bool patched in new[]{false,true})
                    {
                        observing=false;
                        enemies=await(Task<Creature[]>)Call(typeof(NativeDiagnostics),"EnterSoulCombat",game,player)!;
                        actor=NCombatRoom.Instance.GetCreatureNode(player.Creature)!;home=actor.Position;
                        if(name=="Finisher")
                        {
                            var seed=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Neutralize>(),player);
                            await CardPileCmd.Add(seed,PileType.Hand);
                            await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),seed,enemies[1]);
                            await Wait(game,1.2);
                        }
                        await Play((patched?"candidate-":"original-")+name,name,true,true,true,patched:patched,upgraded:name is "Murder" or "KnifeTrap" or "Shiv");
                    }
                    Require(damages["original-"+name].SequenceEqual(damages["candidate-"+name]),"Native damage changed: "+name);
                }
                Require(rows.Count==expectedCases,"Differential suite incomplete");
                return;
            }

            foreach (bool fxOn in new[] {true, false})
                foreach (bool msaOn in new[] {true, false})
                    foreach (bool global in new[] {true, false})
                    {
                        Set(fxOn, msaOn, global);
                        foreach (var name in cardNames)
                        {
                            bool allowed = (bool)AccessTools.Method(Msa("AnimationSettings"), "Allows", [typeof(string)]).Invoke(null, [name])!;
                            bool expected = msaOn && (name == "GrandFinale" ? !global : name=="KnifeTrap" || !(fxOn && fxPlay.Contains(name)));
                            Require(allowed == expected, "Animation route mismatch: " + name);
                            routing.Add(new {name, fxOn, msaOn, global, allowed});
                        }
                        foreach (var pair in (Dictionary<string,string>)AccessTools.Field(entry, "skills").GetValue(null)!)
                        {
                            bool allowed = (bool)Call(settings, "IsSkillEnabled", "meleeattack_skill_"+pair.Key)!;
                            bool expected = global && (pair.Value == "GrandFinale" || (!(fxOn && fxPlay.Contains(pair.Value)) && !(msaOn && cardNames.Contains(pair.Value))));
                            Require(allowed == expected, "Skill route mismatch: " + pair.Key);
                        }
                        if(!global)foreach(var key in config.Keys.Where(k=>k.StartsWith("meleeattack_skill_")))
                            Require(!(bool)Call(settings,"IsSkillEnabled",key)!,"Global switch bypass: "+key);
                    }
            // Original controls establish that both reported regressions are observable.
            await Play("control-murder-off", "Murder", false, false, false, patched:false, upgraded:true);
            Require(hits.GetValueOrDefault("melee.murderScene") > 0, "Original disabled-Murder leak not reproduced");
            await Play("candidate-murder-global-off", "Murder", false, false, false, upgraded:true);
            Require(MeleeCount()==0, "Murder leaked with global switch off");
            await Play("candidate-murder-card-off", "Murder", false, false, true, upgraded:true, cardOff:"murder");
            Require(MeleeCount()==0, "Murder leaked with card switch off");
            await Play("candidate-murder-card-on", "Murder", false, false, true);
            Require(hits.GetValueOrDefault("melee.murderScene")>0, "Murder toggle no longer enables its own effect");
            await Play("control-murder-preview", "Murder", true, true, true, patched:false);
            Require(hits.GetValueOrDefault("fx.heldNode")==0, "Original preview veto not reproduced");
            await Play("control-bottle", "BouncingFlask", true, true, true, patched:false);
            foreach (string name in cardNames) await Play("priority-"+name, name, true, true, true);
            await Play("fallback-murder-msa", "Murder", false, true, true);
            await Play("fallback-trap-msa", "KnifeTrap", false, true, true);
            await Play("fallback-finale-msa", "GrandFinale", true, true, false);
            await Play("candidate-trap-global-off", "KnifeTrap", false, false, false, upgraded:true);
            Require(MeleeCount()==0, "KnifeTrap generated shiv leaked with global switch off");
            await Play("candidate-shiv-global-off", "Shiv", false, false, false, upgraded:true);
            Require(MeleeCount()==0, "Upgraded Shiv leaked with global switch off");
            await Play("candidate-trap-card-off", "KnifeTrap", false, false, true, upgraded:true,cardOff:"knife_trap");
            Require(MeleeCount()==0,"Generated upgraded Shivs ignored the KnifeTrap switch");
            await Play("per-card-murder-fallback", "Murder", true, true, true,fxOff:["Murder"]);
            await Play("trap-shiv-only-fx", "KnifeTrap", true, true, true,upgraded:true,fxOff:["KnifeTrap"]);
            Require(hits.GetValueOrDefault("msa.KnifeTrapVisual..ctor")>0 && hits.GetValueOrDefault("fx.OnShivHit")==0,"KnifeTrap did not keep MoreSilent playback");
            await Play("trap-parent-only-fx", "KnifeTrap", true, true, true,upgraded:true,fxOff:["Shiv"]);
            Require(hits.GetValueOrDefault("msa.KnifeTrapVisual..ctor")>0 && hits.GetValueOrDefault("fx.KnifeTrapFx.OnPlay")==0,"KnifeTrap parent playback was not split");
            await Play("trap-all-fx-off-fallback", "KnifeTrap", true, true, true,fxOff:["KnifeTrap","Shiv"]);
            await Play("upgraded-slice", "Slice", true, true, true,upgraded:true);
            Require(MeleeCount()==0,"Generic upgraded hit overlapped Slice animation");
            await Play("upgraded-murder", "Murder", true, true, true,upgraded:true);
            foreach (string name in new[] {"BulletTime","Backflip","WraithForm","Afterimage","Pinpoint","Blur","Burst","Flechettes","DaggerThrow"}) await Play("additional-"+name, name, true, true, true);
            Require(rows.Count==expectedCases, "Actual-play suite did not finish");

            async Task Play(string id, string name, bool fxOn, bool msaOn, bool global, bool patched=true, bool upgraded=false, string? cardOff=null,string[]? fxOff=null)
            {
                observing=false;
                Call(Fx("Cards.HeldFxManager"), "Reset");
                foreach (var creature in enemies.Append(player.Creature))
                    foreach (var power in creature.Powers.ToArray()) await PowerCmd.Remove(power);
                foreach (var pile in new[] {PileType.Hand,PileType.Draw,PileType.Discard,PileType.Exhaust})
                    await CardPileCmd.RemoveFromCombat(pile.GetPile(player).Cards.ToArray(), skipVisuals:true);
                Call(Msa("VisualCleanup"), "Reset");
                actor.Position=home;
                foreach(var enemy in enemies) await CreatureCmd.SetMaxAndCurrentHp(enemy,10000m);
                if(patched) Call(entry,"Initialize"); else new Harmony(CompatOwner).UnpatchAll(CompatOwner);
                Set(fxOn, msaOn, global);
                if(cardOff is not null) config["meleeattack_skill_"+cardOff]=false;
                if(fxOff is not null)
                {
                    var current=snapshotField.GetValue(null)!;
                    var options=(Dictionary<string,bool>)AccessTools.Field(current.GetType(),"FxOptions").GetValue(current)!;
                    foreach(var off in fxOff)options["card."+off+".enabled"]=false;
                }
                var type=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards."+name,true)!;
                var card=CombatManager.Instance.DebugOnlyGetState()!.CreateCard((CardModel)cardFactory.MakeGenericMethod(type).Invoke(null,null)!,player);
                if(upgraded) CardCmd.Upgrade(card);
                await CardPileCmd.Add(card,PileType.Hand);
                if(name is "KnifeTrap" or "StormOfSteel" or "Flechettes")
                    for(int i=0;i<3;i++)
                    {
                        var shivType=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards."+(name=="Flechettes"?"DefendSilent":"Shiv"),true)!;
                        var shiv=CombatManager.Instance.DebugOnlyGetState()!.CreateCard((CardModel)cardFactory.MakeGenericMethod(shivType).Invoke(null,null)!,player);
                        if(upgraded)CardCmd.Upgrade(shiv);
                        await CardPileCmd.Add(shiv,name=="KnifeTrap"?PileType.Exhaust:PileType.Hand);
                    }
                if(name=="PreciseCut")for(int i=0;i<3;i++)
                {
                    var strike=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.StrikeSilent>(),player);
                    await CardPileCmd.Add(strike,PileType.Hand);
                }
                // Begin() checks CanPlay; keep the visual fixture sufficiently funded.
                player.PlayerCombatState!.Energy=99;
                await Wait(game,.3);
                hits.Clear(); observing=true;
                bool fxAllowed=(bool)AccessTools.Method(Fx("Core.CardFxSettings"),"Enabled",[typeof(string)]).Invoke(null,[name])!;
                bool heldExpected=fxAllowed && heldMap.Contains(type);
                if(heldExpected)
                {
                    Call(Fx("Cards.HeldFxManager"),"Begin",card,(Func<bool>)(()=>true));
                    Call(Fx("Cards.HeldFxManager"),"SetTarget",enemies[1]);
                    await Wait(game,.3);
                    if(id=="priority-Murder")SaveFrame(game,id+"-standby",output);
                    Call(Fx("Cards.HeldFxManager"),"End",true);
                }
                var hp=enemies.Select(e=>e.CurrentHp).ToArray();
                var task=CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,enemies[1]);
                if(visual)await RenderAudit.Sample(game,output,id+"-active",name=="GrandFinale"?5:2);
                if(id is "priority-Murder" or "priority-KnifeTrap" or "priority-GrandFinale")
                {
                    await Wait(game,.15);
                    SaveFrame(game,id+"-played",output);
                }
                var deadline=Time.GetTicksMsec()+45000UL;
                while(!task.IsCompleted && Time.GetTicksMsec()<deadline)await Wait(game,.05);
                Require(task.IsCompleted,"Card play timed out: "+id);
                await task;
                await Wait(game,name=="GrandFinale"?1.8:2.2);
                if(visual)await RenderAudit.Sample(game,output,id+"-settled",1);
                observing=false;
                var counts=new Dictionary<string,int>(hits);
                var damage=enemies.Select((e,i)=>hp[i]-e.CurrentHp).ToArray();damages[id]=damage;
                rows.Add(new {id,name,fxOn,msaOn,global,patched,upgraded,fxOff,counts,testMode=TestMode.IsOn,damage});
                GD.Print("[EffectsProbe] "+JsonSerializer.Serialize(rows[^1]));
                if(visual)
                {
                    if(name=="KnifeTrap")
                    {
                        Require(hits.GetValueOrDefault("fx.heldNode")>=2,"Trap preview missing");
                        Require(hits.GetValueOrDefault("msa.KnifeTrapVisual..ctor")>0,"Trap playback missing");
                        Require(hits.GetValueOrDefault("fx.KnifeTrapFx.OnPlay")==0,"SilentFX trap playback overlapped");
                        Require(hits.GetValueOrDefault("fx.OnShivHit")==0,"SilentFX child Shiv hit overlapped");
                    }
                    if(name=="GrandFinale")
                    {
                        Require(hits.GetValueOrDefault("melee.finaleScene")>0,"Finale cutscene missing");
                        Require(hits.GetValueOrDefault("msa.bow")==1,"Finale bow missing or duplicated");
                        Require(hits.GetValueOrDefault("msa.ActorPresentation.BeginFinale")==0,"Finale teleport overlapped");
                        Require(actor.Body.Modulate.A>.99f,"Bow did not restore actor");
                    }
                    return;
                }
                if(!patched)return;
                if(heldExpected)Require(hits.GetValueOrDefault("fx.heldNode")>=2,"Standby missing: "+id);
                if(fxAllowed && fxPlay.Contains(name) && !(name=="KnifeTrap" && msaOn))
                {
                    Require(hits.Any(p=>p.Key.StartsWith("fx.") && p.Key!="fx.heldNode" && p.Value>0),"SilentFX playback missing: "+id);
                    Require(!hits.Any(p=>p.Key.StartsWith("msa.") && p.Value>0),"Lower-priority animation played: "+id);
                    Require(MeleeCount()==0,"Lower-priority Melee effect played: "+id);
                }
                if(name=="GrandFinale" && global)Require(hits.GetValueOrDefault("melee.finaleScene")>0 && hits.GetValueOrDefault("msa.ActorPresentation.BeginFinale")==0 && (!msaOn || hits.GetValueOrDefault("msa.bow")==1),"Finale exception failed: "+id);
                if(msaOn && cardNames.Contains(name) && name!="BouncingFlask" && (!fxAllowed || !fxPlay.Contains(name) || name=="KnifeTrap") && (name!="GrandFinale" || !global))
                    Require(hits.Any(p=>p.Key.StartsWith("msa.") && p.Value>0),"Animation fallback missing: "+id);
                if(name=="BouncingFlask" && fxOn)Require(hits.Keys.Any(k=>k=="fx.power.PoisonFx"),"Disjoint poison status was lost");
                if(name=="GrandFinale" && global)await Wait(game,12);
            }
            int MeleeCount()=>hits.Where(p=>p.Key.StartsWith("melee.")).Sum(p=>p.Value);
            void Set(bool fxOn,bool msaOn,bool global)
            {
                foreach(var key in config.Keys.Where(k=>k.StartsWith("meleeattack_skill_")).ToArray())config[key]=true;
                config["meleeattack_global_skills"]=global;config["meleeattack_fast_mode"]=true;
                var next=Activator.CreateInstance(msaOriginal.GetType())!;
                AccessTools.Property(next.GetType(),"Enabled").SetValue(next,msaOn);
                msaCurrent.SetValue(null,next);
                var t=snapshot.GetType();
                object Field(string name)=>AccessTools.Field(t,name).GetValue(snapshot)!;
                var copy=t.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single().Invoke([
                    fxOn,Field("AudioPreference"),Field("Volume"),Field("MusicPreference"),Field("ShakePreference"),
                    Field("Reduce"),Field("Prewarm"),Field("Shader"),Field("Pace"),new Dictionary<string,bool>((Dictionary<string,bool>)Field("FxOptions"))]);
                snapshotField.SetValue(null,copy);
                (AccessTools.Field(Fx("Core.PlayerSettings"),"Changed").GetValue(null) as Action)?.Invoke();
            }
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            observing=false;game.GetTree().NodeAdded-=SawNode;
            Observers.UnpatchAll(Observers.Id);
            config.Clear();foreach(var pair in saved)config.Add(pair.Key,pair.Value);
            snapshotField.SetValue(null,snapshot);msaCurrent.SetValue(null,msaOriginal);
            Call(Fx("Cards.HeldFxManager"),"Reset");
            File.WriteAllText(Path.Combine(output,"effects-validation.json"),JsonSerializer.Serialize(new {success=failure is null,error=failure?.ToString(),expectedCases,cases=rows,routing},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
    private static async Task Wait(NGame game,double seconds)
    {
        var end=Time.GetTicksMsec()+(ulong)(seconds*1000);
        while(Time.GetTicksMsec()<end)await game.ToSignal(game.GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    private static void SaveFrame(NGame game,string name,string output)
    {
        using var pixels=game.GetViewport().GetTexture().GetImage();
        Require(pixels.SavePng(Path.Combine(output,name+".png"))==Error.Ok,"PNG capture failed");
    }
}
