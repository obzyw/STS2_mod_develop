using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;

namespace ElementalProbe;

public static partial class Probe
{
    private static readonly List<string> skinTriggers=new();
    private static void SkinTrigger(string trigger){skinTriggers.Add(trigger);}
    private static async Task CheckSkinAnimation(Player player,List<object> cases)
    {
        if(Character!="Silent"){await CheckOtherSkinBaseline(player,cases);return;}
        var actor=player.Creature.GetCreatureNode()!;
        using var skinStage=new SkinStageScope(Character);
        var fx=AccessTools.TypeByName("SilentFX.Core.PlayerSettings")!;
        var snapshot=AccessTools.Field(fx,"_snapshot").GetValue(null)!;
        bool fxBefore=(bool)AccessTools.Field(snapshot.GetType(),"Enabled").GetValue(snapshot)!;
        AccessTools.Field(snapshot.GetType(),"Enabled").SetValue(snapshot,false);
        var melee=(Dictionary<string,bool>)AccessTools.Field(AccessTools.TypeByName("MeleeAttack.SettingsUI"),"_configCache").GetValue(null)!;
        var meleeBefore=new Dictionary<string,bool>(melee);
        melee["meleeattack_global_skills"]=false;melee["meleeattack_skill_grand_finale"]=false;
        var msa=AccessTools.TypeByName("MoreSilentAnimations.AnimationSettings")!;
        var current=AccessTools.Property(msa,"Current");var saved=current.GetValue(null)!;
        current.SetValue(null,Activator.CreateInstance(msa));
        var body=actor.Body;var original=body.Get("skeleton_data_res");var originalScale=body.Scale;
        bool full=System.Environment.GetEnvironmentVariable("STS2_SKIN_FULL_AUDIT")=="1";
        var names=full?(string[])AccessTools.Field(AccessTools.TypeByName("MoreSilentAnimations.SettingsState"),"Cards").GetValue(null)!:new[]{"Neutralize","SuckerPunch","Backstab"};
        string? only=System.Environment.GetEnvironmentVariable("STS2_SKIN_ONLY_CARDS");
        if(!string.IsNullOrWhiteSpace(only))names=SelectSkinCards(only,Character,names);
        var observer=new Harmony("obzyw.sts2.skin-card-observer");
        observer.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Combat.NCreature),"SetAnimationTrigger"),prefix:new HarmonyMethod(typeof(Probe),nameof(SkinTrigger)));
        var assembly=msa.Assembly;
        foreach(var observerType in assembly.GetTypes().Where(t=>t.Namespace=="MoreSilentAnimations"&&(t.Name.EndsWith("Visual")||t.Name=="BottleFlight"||t.Name=="SteelWind")))
            foreach(var ctor in observerType.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                observer.Patch(ctor,postfix:new HarmonyMethod(typeof(Probe),nameof(SawTrap)));
        foreach(var method in AccessTools.TypeByName("MoreSilentAnimations.ActorPresentation")!.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Where(m=>m.Name.StartsWith("Begin")))
            observer.Patch(method,postfix:new HarmonyMethod(typeof(Probe),nameof(SawTrap)));
        var failures=new List<string>();
        try
        {
            foreach(string stage in (System.Environment.GetEnvironmentVariable("STS2_SKIN_STAGES")??"classic,normal,hurt,critical").Split(','))
            {
                AccessTools.Method(AccessTools.TypeByName("MoreSilentAnimations.VisualCleanup"),"Reset").Invoke(null,null);
                skinStage.Select(stage);
                var resource=ResourceLoader.Load<Resource>($"res://YUSilentThreeStages/characters/silent/{stage}/rig.tres");
                body.Set("skeleton_data_res",resource);body.Scale=Vector2.One*.29f;
                await Wait(.3);
                Require(body.Call("get_skeleton_data_res").As<Resource>()==resource,"Skin controller changed selected stage: "+stage);
                foreach(var name in names)
                {
                    var type=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards."+name,true)!;
                    AccessTools.Method(AccessTools.TypeByName("MoreSilentAnimations.VisualCleanup"),"Reset").Invoke(null,null);
                    foreach(var creature in CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.Append(player.Creature))
                        foreach(var power in creature.Powers.ToArray()) await PowerCmd.Remove(power);
                    foreach(var enemy in CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies){enemy.SetMaxHpInternal(10000);enemy.SetCurrentHpInternal(10000);}
                    foreach(var pile in new[]{PileType.Hand,PileType.Draw,PileType.Discard,PileType.Exhaust})
                        await CardPileCmd.RemoveFromCombat(pile.GetPile(player).Cards.ToArray(),skipVisuals:true);
                    var canonical=(CardModel)typeof(ModelDb).GetMethods().Single(m=>m.Name=="Card"&&m.IsGenericMethodDefinition).MakeGenericMethod(type).Invoke(null,null)!;
                    var card=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(canonical,player);
                    player.PlayerCombatState!.Energy=99;await CardPileCmd.Add(card,PileType.Hand);await Wait(.1);
                    if(name is "PreciseCut" or "StormOfSteel")
                        for(int i=0;i<3;i++)await CardPileCmd.Add(CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<StrikeSilent>(),player),PileType.Hand);
                    if(name=="KnifeTrap")
                        for(int i=0;i<3;i++)await CardPileCmd.Add(CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<Shiv>(),player),PileType.Exhaust);
                    var target=CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.First();
                    int frames=0,wrongSkin=0,duplicates=0;float distance=0;Image? shot=null;
                    ulong castAt=Time.GetTicksMsec();int snakeFrames=0;
                    void Sample()
                    {
                        if(body.Call("get_skeleton_data_res").As<Resource>()!=resource)wrongSkin++;
                        if(name=="Snakebite")
                        {
                            var snake=Descendants(NGame.Instance!).OfType<Node2D>().FirstOrDefault(n=>n.Name=="MoreSilentSnakeFront");
                            if(snake!=null&&snake.GetChildren().OfType<MeshInstance2D>().Any(n=>n.Mesh?.GetSurfaceCount()>0))
                            {
                                snakeFrames++;
                                if(shot==null&&snakeFrames>=8)shot=NGame.Instance!.GetViewport().GetTexture().GetImage();
                            }
                        }
                        var display=Descendants(NGame.Instance!).OfType<Node2D>().FirstOrDefault(n=>n.Name=="MoreSilentDisplay"&&n.IsVisibleInTree()&&n.Modulate.A>.1);
                        if(display==null)return;
                        frames++;distance=Math.Max(distance,display.GlobalPosition.DistanceTo(body.GlobalPosition));
                        if(body.Modulate.A>.1)duplicates++;
                        if(display.HasMethod("get_skeleton_data_res")&&display.Call("get_skeleton_data_res").As<Resource>()!=resource)wrongSkin++;
                        if(shot==null&&frames>8)shot=NGame.Instance!.GetViewport().GetTexture().GetImage();
                    }
                    var tree=NGame.Instance!.GetTree();tree.ProcessFrame+=Sample;
                    decimal hp=target.CurrentHp;
                    trapHits.Clear();
                    skinTriggers.Clear();
                    try{await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,target);await Wait(.8);}
                    finally{tree.ProcessFrame-=Sample;}
                    using(shot){shot?.SavePng(Path.Combine(Output,$"{stage}-{type.Name}.png"));}
                    bool moving=name is "LeadingStrike" or "Slice" or "PreciseCut" or "Finisher" or "EchoingSlash" or "Predator" or "Pounce" or "PoisonedStab" or "Neutralize" or "Assassinate" or "Dash" or "SuckerPunch" or "Backstab" or "Murder" or "Suppress" or "TheHunt";
                    bool knownExternal=name=="BouncingFlask";
                    bool? passed=knownExternal?null:wrongSkin==0&&duplicates==0&&(moving?frames>0&&distance>5:trapHits.Count>0)&&(name!="Snakebite"||snakeFrames>3);
                    var row=new{stage,card=type.Name,frames,snakeFrames,wrongSkin,duplicates,distance,damage=hp-target.CurrentHp,counts=new Dictionary<string,int>(trapHits),triggers=skinTriggers.ToArray(),passed,note=knownExternal?"Guida replaces native bottle throw; not a skin failure":null};
                    cases.Add(row);GD.Print("[SkinAudit] "+System.Text.Json.JsonSerializer.Serialize(row));
                    File.WriteAllText(Path.Combine(Output,"skin-card-progress.json"),System.Text.Json.JsonSerializer.Serialize(cases));
                    if(passed==false)failures.Add(stage+":"+name);
                }
            }
        }
        finally
        {
            AccessTools.Method(AccessTools.TypeByName("MoreSilentAnimations.VisualCleanup"),"Reset").Invoke(null,null);
            body.Set("skeleton_data_res",original);body.Scale=originalScale;current.SetValue(null,saved);
            AccessTools.Field(snapshot.GetType(),"Enabled").SetValue(snapshot,fxBefore);
            melee.Clear();foreach(var item in meleeBefore)melee[item.Key]=item.Value;
            observer.UnpatchAll(observer.Id);
        }
        Require(failures.Count==0,"Skin card failures: "+string.Join(",",failures));
    }
    private static string[] SelectSkinCards(string input,string character,string[] fallback)
    {
        var selected=input.Split(',').Where(s=>!s.Contains('/')||s.StartsWith(character+"/",StringComparison.Ordinal)).Select(s=>s.Contains('/')?s[(s.IndexOf('/')+1)..]:s).ToArray();
        return selected.Length>0?selected:fallback;
    }
}
