using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace ElementalProbe;

public static partial class Probe
{
    // Baseline observation, not a visual acceptance test. Compare actual native
    // and skin casts before choosing which components need retargeting.
    private static async Task CheckOtherSkinBaseline(Player player,List<object> cases)
    {
        string ns=Character switch{"Necrobinder"=>"MoreNecrobinderAnimations","Ironclad"=>"ShieldOnly",_=>"MoreDefectAnimations"};
        var settings=AccessTools.TypeByName(ns+".AnimationSettings")!;
        var current=AccessTools.Property(settings,"Current");var saved=current.GetValue(null)!;
        var enabled=Activator.CreateInstance(settings,nonPublic:true)!;
        foreach(var p in settings.GetProperties().Where(p=>p.PropertyType==typeof(bool)&&p.CanWrite))p.SetValue(enabled,true);
        current.SetValue(null,enabled);
        var actor=player.Creature.GetCreatureNode()!;var body=actor.Body;
        using var skinStage=new SkinStageScope(Character);
        var original=body.Get("skeleton_data_res");var scale=body.Scale;
        var observer=new Harmony("obzyw.sts2.other-skin-observer");
        foreach(var t in settings.Assembly.GetTypes().Where(t=>t.Namespace==ns&&!t.IsInterface&&(t.Name.StartsWith("Native")||t.Name.EndsWith("Effect")||t.Name.EndsWith("Visual")||t.Name=="ClipPlayer"||t.GetInterfaces().Any(i=>i.Name=="IMulticastEffect"))))
            foreach(var ctor in t.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                try{observer.Patch(ctor,postfix:new HarmonyMethod(typeof(Probe),nameof(SawTrap)));}
                catch(Exception error){GD.Print("[OtherSkinAudit] Observer unavailable for "+t.Name+": "+error.GetType().Name);}
        var resets=settings.Assembly.GetTypes().Where(t=>t.Namespace==ns&&t.Name.EndsWith("Session"))
            .Select(t=>t.GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static)).Where(m=>m!=null).Cast<MethodInfo>().ToArray();
        void Reset(){foreach(var m in resets)m.Invoke(null,m.GetParameters().Select(p=>p.DefaultValue).ToArray());if(Character=="Ironclad")AccessTools.Method(AccessTools.TypeByName("ShieldOnly.DemonHorns"),"Reset",Type.EmptyTypes).Invoke(null,null);}
        var names=Character=="Ironclad"
            ?new[]{"DefendIronclad","Impervious","SecondWind","Brand","BattleTrance","Feed","DemonForm","DarkEmbrace","ShrugItOff","Bash","Break","Bludgeon","Barricade","BodySlam","Bloodletting","Offering","Hemokinesis"}
            :Character=="Necrobinder"
            ?new[]{"Scourge","GraveWarden","Invoke","Friendship","NegativePulse","Neurosurge","Deathbringer","Dirge","CaptureSpirit","EndOfDays","BansheesCry","Unleash","Protector","Lethality","ReaperForm","Eradicate"}
            :new[]{"Coolheaded","ChargeBattery","BiasedCognition","Hotfix","SignalBoost","Buffer","Supercritical","Defragment","Modded","FlakCannon","BulkUp","Hologram","IceLance","Dualcast","Quadcast","ConsumingShadow","GunkUp","CompileDriver","Turbo","MeteorStrike"};
        var only=System.Environment.GetEnvironmentVariable("STS2_SKIN_ONLY_CARDS");if(!string.IsNullOrWhiteSpace(only))names=SelectSkinCards(only,Character,names);
        var stages=(System.Environment.GetEnvironmentVariable("STS2_SKIN_STAGES")??"native,normal").Split(',');
        Node? native=null;
        try
        {
            if(Character=="Necrobinder"&&player.Osty==null)
            {
                await OstyCmd.Summon(new BlockingPlayerChoiceContext(),player,30,null);
                await Wait(.3);
                Require(player.Osty?.GetCreatureNode()!=null,"Necrobinder animation audit requires a summoned Osty.");
            }
            native=ResourceLoader.Load<PackedScene>($"res://scenes/creature_visuals/{Character.ToLowerInvariant()}.tscn").Instantiate();
            var nativeBody=Descendants(native).OfType<Node2D>().First(n=>n.HasMethod("get_skeleton_data_res"));
            var nativeData=nativeBody.Call("get_skeleton_data_res");
            foreach(var stage in stages)
            {
                Reset();
                skinStage.Select(stage);
                string suffix=Character=="Defect"&&stage=="classic"?"classic/normal":stage;
                var selected=stage=="native"?nativeData:Variant.From(ResourceLoader.Load<Resource>($"res://YU{Character}ThreeStages/characters/{Character.ToLowerInvariant()}/{suffix}/rig.tres"));
                body.Set("skeleton_data_res",selected);
                body.Scale=stage=="native"?nativeBody.Scale:Vector2.One*.29f;
                await Wait(.4);
                Require(body.Call("get_skeleton_data_res").As<Resource>()==selected.As<Resource>(),"Skin controller changed selected stage: "+stage);
                foreach(var name in names)
                {
                    Reset();
                    var combat=CombatManager.Instance.DebugOnlyGetState()!;
                    foreach(var creature in combat.HittableEnemies.Append(player.Creature))
                        foreach(var power in creature.Powers.ToArray())await PowerCmd.Remove(power);
                    foreach(var enemy in combat.HittableEnemies){enemy.SetMaxHpInternal(10000);enemy.SetCurrentHpInternal(10000);}
                    player.Creature.SetCurrentHpInternal(player.Creature.MaxHp);
                    foreach(var pile in new[]{PileType.Hand,PileType.Draw,PileType.Discard,PileType.Exhaust})await CardPileCmd.RemoveFromCombat(pile.GetPile(player).Cards.ToArray(),skipVisuals:true);
                    var type=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards."+name,true)!;
                    var canonical=(CardModel)typeof(ModelDb).GetMethods().Single(m=>m.Name=="Card"&&m.IsGenericMethodDefinition).MakeGenericMethod(type).Invoke(null,null)!;
                    var card=combat.CreateCard(canonical,player);player.PlayerCombatState!.Energy=3;
                    if(Character=="Defect"&&name is "Dualcast" or "Quadcast" or "ConsumingShadow")
                        await OrbCmd.Channel<MegaCrit.Sts2.Core.Models.Orbs.LightningOrb>(new BlockingPlayerChoiceContext(),player);
                    await CardPileCmd.Add(card,PileType.Hand);await Wait(.1);
                    var target=combat.HittableEnemies.First();trapHits.Clear();
                    int displayFrames=0,wrongSkin=0,duplicates=0,hologramFrames=0,guardFrames=0,turboFrames=0;
                    bool guardShot=false;
                    void Sample()
                    {
                        if(body.Call("get_skeleton_data_res").As<Resource>()!=selected.As<Resource>())wrongSkin++;
                        foreach(var display in Descendants(actor.Visuals).OfType<Node2D>().Where(n=>n.Name=="YUNecroActionDisplay"&&n.IsVisibleInTree()&&n.Modulate.A>.01f))
                        {
                            displayFrames++;
                            if(display.Call("get_skeleton_data_res").As<Resource>()!=selected.As<Resource>())wrongSkin++;
                            if(body.Modulate.A>.01f)duplicates++;
                        }
                        foreach(var display in Descendants(actor.Visuals).OfType<Node2D>().Where(n=>n.Name=="YUHologramSkinCapture"))
                        {
                            hologramFrames++;
                            if(display.Call("get_skeleton_data_res").As<Resource>()!=selected.As<Resource>())wrongSkin++;
                        }
                        foreach(var display in Descendants(actor.Visuals).OfType<Node2D>().Where(n=>n.Name=="YUIroncladGuardDisplay"&&n.IsVisibleInTree()))
                        {
                            var carrier=display.GetMeta("guard_carrier").As<Node2D>();
                            if(!carrier.IsVisibleInTree()||carrier.Modulate.A<.01f)continue;
                            guardFrames++;
                            if(display.Call("get_skeleton_data_res").As<Resource>()!=selected.As<Resource>())wrongSkin++;
                            if(body.GetChildren().OfType<CanvasItem>().Any(n=>n.GetClass()=="SpineMesh2D"&&n.IsVisibleInTree()&&n.Modulate.A>.01f)&&body.Modulate.A>.01f)duplicates++;
                            bool goldReady=!display.HasMeta("guard_gold")||display.GetMeta("guard_gold").As<ShaderMaterial>().GetShaderParameter("strength").AsSingle()>.8f;
                            if(guardFrames>=5&&goldReady&&!guardShot)
                            {
                                guardShot=true;
                                using var shot=NGame.Instance!.GetViewport().GetTexture().GetImage();
                                shot.SavePng(Path.Combine(Output,$"{stage}-{name}-guard.png"));
                            }
                        }
                        foreach(var display in Descendants(actor.Visuals).OfType<Node2D>().Where(n=>n.Name=="YUDefectTurboAccessories"&&n.IsVisibleInTree()))
                        {
                            var skeleton=display.Call("get_skeleton").AsGodotObject();
                            if(!skeleton.Call("get_slots").AsGodotArray().Any(s=>s.AsGodotObject().Call("get_attachment").AsGodotObject()!=null&&s.AsGodotObject().Call("get_color").AsColor().A>.01f))continue;
                            turboFrames++;
                            if(turboFrames==5)
                            {
                                using var shot=NGame.Instance!.GetViewport().GetTexture().GetImage();
                                shot.SavePng(Path.Combine(Output,$"{stage}-{name}-turbo.png"));
                            }
                        }
                    }
                    var tree=NGame.Instance!.GetTree();tree.ProcessFrame+=Sample;
                    var sessionType=AccessTools.TypeByName(ns+".VisualSession");
                    var starts=sessionType==null?null:AccessTools.Property(sessionType,"Starts");
                    long beforeStarts=starts==null?0:Convert.ToInt64(starts.GetValue(null));
                    try
                    {
                        var play=CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,target);
                        await Wait(.18);
                        using(var shot=NGame.Instance!.GetViewport().GetTexture().GetImage())shot.SavePng(Path.Combine(Output,$"{stage}-{name}-early.png"));
                        if(await Task.WhenAny(play,Wait(8))!=play)throw new TimeoutException("Card choice or animation did not finish: "+name);
                        await play;
                        if(Character=="Ironclad"&&name is "DefendIronclad" or "Impervious")
                        {
                            int turn=player.PlayerCombatState!.TurnNumber;
                            var button=Descendants(NGame.Instance!).OfType<MegaCrit.Sts2.Core.Nodes.Combat.NEndTurnButton>().First();
                            AccessTools.Method(button.GetType(),"SecretEndTurnLogicViaFtue").Invoke(button,null);
                            await Until(()=>player.PlayerCombatState!.TurnNumber>turn&&!CombatManager.Instance.PlayerActionsDisabled,20);
                        }
                        await Wait(name=="Hologram"?1.0:name=="Turbo"?.7:.15);
                    }
                    finally{tree.ProcessFrame-=Sample;}
                    using(var shot=NGame.Instance!.GetViewport().GetTexture().GetImage())shot.SavePng(Path.Combine(Output,$"{stage}-{name}-late.png"));
                    var row=new{stage,card=name,actualResource=body.Call("get_skeleton_data_res").As<Resource>().ResourcePath,displayFrames,hologramFrames,guardFrames,turboFrames,wrongSkin,duplicates,starts=starts==null?0:Convert.ToInt64(starts.GetValue(null))-beforeStarts,constructors=new Dictionary<string,int>(trapHits),bodyAlpha=body.Modulate.A,note="Observation only; constructor presence is not visual acceptance"};
                    cases.Add(row);GD.Print("[OtherSkinAudit] "+System.Text.Json.JsonSerializer.Serialize(row));
                    File.WriteAllText(Path.Combine(Output,"skin-card-progress.json"),System.Text.Json.JsonSerializer.Serialize(cases));
                    foreach(var power in player.Creature.Powers.ToArray())await PowerCmd.Remove(power);
                    Reset();await Wait(.05);
                    Require(body.Modulate.A>.99f,"Body transparency was not restored after "+name);
                }
            }
        }
        finally{Reset();body.Set("skeleton_data_res",original);body.Scale=scale;current.SetValue(null,saved);observer.UnpatchAll(observer.Id);native?.Free();}
    }
}
