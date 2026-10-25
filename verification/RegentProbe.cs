using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using NecrobinderVfx;
using Environment=System.Environment;

namespace STS2CompatProbe;

/// <summary>Actual Regent plays using the authors' existing switches, with subscribed skin and power triggers intact.</summary>
internal static class RegentProbe
{
    private static readonly string[] Attacks=["Comet","CrescentSpear","DyingStar","SevenStars","AstralPulse"];
    private static readonly string[] Powers=["BlackHole","Genesis","PillarOfCreation","SpectrumShift"];
    private static readonly List<Node> nodes=new();
    private static readonly List<(string Name,string Scene)> allocated=new();
    private static bool observing;
    private static Type Type(string assembly,string name)=>AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name==assembly).GetType(name,true)!;
    private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    private static void Added(Node node){if(observing){nodes.Add(node);allocated.Add((node.Name.ToString(),node.SceneFilePath));}}
    internal static void Install(Harmony harmony)
    {
        var create=typeof(Player).GetMethods().Single(m=>m.Name=="CreateForNewRun"&&!m.IsGenericMethod&&m.GetParameters()[0].ParameterType==typeof(CharacterModel));
        harmony.Patch(create,prefix:new HarmonyMethod(typeof(RegentProbe),nameof(Choose)));
        harmony.Patch(AccessTools.Method(typeof(NativeDiagnostics),"RunCompatibility"),prefix:new HarmonyMethod(typeof(RegentProbe),nameof(Suite)));
    }
    private static void Choose(ref CharacterModel __0){if(__0 is Necrobinder)__0=ModelDb.Character<Regent>();}
    private static bool Suite(NGame __0,Player __1,ref Task __result){__result=Run(__0,__1);return false;}
    private static async Task Run(NGame game,Player player)
    {
        string output=Environment.GetEnvironmentVariable("NECROVFX_TEST_OUTPUT")!;
        var rows=new List<object>();Exception? failure=null;
        const bool settingsOnly=true;
        const int expectedCases=10;
        var hot=(IDictionary)AccessTools.Field(Type("RegentFX","RegentFX.ThirdParty.RitsuLibModConfig"),"Hot").GetValue(null)!;
        var settings=Type("RegentAnimations","RegentAnimations.PresentationSettings");
        var values=(IDictionary)AccessTools.Field(settings,"Values").GetValue(null)!;
        var savedHot=Snapshot(hot);var savedValues=Snapshot(values);
        var allows=AccessTools.Method(settings,"Allows");
        var factory=typeof(ModelDb).GetMethods().Single(m=>m.Name=="Card"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0);
        try
        {
            Require(player.Character is Regent,"Wrong fixture character");
            bool patched=Harmony.GetAllPatchedMethods().Any(m=>Harmony.GetPatchInfo(m)?.Owners.Contains("local.sts2.regent-effects-compat")==true);
            Require(!patched,"Settings-only verification must not load the prototype");
            foreach(var relic in player.Relics.ToArray())await RelicCmd.Remove(relic);
            MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer.Instance?.Clear();
            game.GetTree().NodeAdded+=Added;
            foreach(string name in Powers)await Play("coexist-"+name,name,true);
            foreach(string name in Attacks)await Play("priority-"+name,name,true);
            await Play("skin-low-health-BlackHole","BlackHole",true,lowHealth:true);
            Require(rows.Count==expectedCases,"Regent suite incomplete");

            async Task Play(string id,string name,bool? fxOn,bool lowHealth=false)
            {
                observing=false;
                foreach(var pair in savedHot)hot[pair.Key]=pair.Value;
                values["Enabled"]=true;
                foreach(string key in Attacks.Concat(Powers))values[key]=true;
                foreach(string key in Attacks)values[key]=false;
                if(fxOn.HasValue)hot[(Powers.Contains(name)?"power_":"card_")+name]=JsonValue.Create(fxOn.Value);
                var enemies=await(Task<Creature[]>)AccessTools.Method(typeof(NativeDiagnostics),"EnterSoulCombat").Invoke(null,[game,player])!;
                var actor=NCombatRoom.Instance!.GetCreatureNode(player.Creature)!;
                foreach(var enemy in enemies)await CreatureCmd.SetMaxAndCurrentHp(enemy,10000m);
                if(lowHealth){await CreatureCmd.SetCurrentHp(player.Creature,10m);await Wait(game,.6);}
                var type=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards."+name,true)!;
                var card=CombatManager.Instance.DebugOnlyGetState()!.CreateCard((CardModel)factory.MakeGenericMethod(type).Invoke(null,null)!,player);
                await CardPileCmd.Add(card,PileType.Hand);
                player.PlayerCombatState!.Energy=99;player.PlayerCombatState.Stars=99;
                await Wait(game,.2);
                nodes.Clear();allocated.Clear();observing=true;
                var hp=enemies.Select(e=>e.CurrentHp).ToArray();
                var play=CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,enemies[1]);
                await Wait(game,.4);
                if(name is "BlackHole" or "Genesis" or "PillarOfCreation" or "SpectrumShift")Save(game,output,id+"-cast");
                await Bounded(game,play,45,id);
                await Wait(game,.8);
                if(name=="BlackHole")await PlayerCmd.GainStars(1,player);
                if(name=="Genesis")await player.Creature.GetPower<GenesisPower>()!.AfterEnergyReset(player);
                if(name=="PillarOfCreation")
                {
                    var generated=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.StrikeRegent>(),player);
                    await CardPileCmd.AddGeneratedCardsToCombat([generated],PileType.Hand,player);
                }
                if(name=="SpectrumShift")await player.Creature.GetPower<SpectrumShiftPower>()!.BeforeHandDraw(player,new BlockingPlayerChoiceContext(),CombatManager.Instance.DebugOnlyGetState()!);
                await Wait(game,.25);
                if(Powers.Contains(name))Save(game,output,id+"-trigger");
                var valid=nodes.Where(GodotObject.IsInstanceValid).ToArray();
                var rf=valid.Where(n=>n.SceneFilePath.StartsWith("res://RegentFX/",StringComparison.Ordinal)).ToArray();
                var ra=valid.Where(n=>n.Name.ToString()=="Regent"+name).ToArray();
                var rfCreated=allocated.Where(n=>n.Scene.StartsWith("res://RegentFX/",StringComparison.Ordinal)).ToArray();
                int raCreated=allocated.Count(n=>n.Name=="Regent"+name);
                var positions=valid.OfType<Node2D>().Where(n=>rf.Contains(n)||ra.Contains(n)).Select(n=>new {node=n.Name.ToString(),scene=n.SceneFilePath,visible=n.IsVisibleInTree(),position=n.GlobalPosition.ToString()}).ToArray();
                bool permit=(bool)allows.Invoke(null,[name])!;
                var damage=enemies.Select((e,i)=>hp[i]-e.CurrentHp).ToArray();
                rows.Add(new {id,name,fxOn,lowHealth,allowedAnimation=permit,rfScenes=rfCreated.Select(n=>n.Scene).ToArray(),raCount=raCreated,positions,damage,body=actor.Body.SceneFilePath});
                GD.Print("[RegentProbe] "+JsonSerializer.Serialize(rows[^1]));
                if(fxOn==true)
                {
                    string main=name switch
                    {
                        "BlackHole"=>"vfx/Blackhole", "Genesis"=>"vfx/genesis", "PillarOfCreation"=>"vfx/pillar",
                        "SpectrumShift"=>"vfx/spectrum_shift", "Comet"=>"vfx/comet", "CrescentSpear"=>"vfx/crescent_spear",
                        "DyingStar"=>"vfx/dying_star", "SevenStars"=>"Blade1", "AstralPulse"=>"vfx/astral_pulse",
                        _=>throw new InvalidOperationException("Unknown scene source")
                    };
                    Require(rfCreated.Any(n=>n.Scene=="res://RegentFX/scenes/"+main+".tscn"),"RegentFX main effect missing: "+id);
                    if(Attacks.Contains(name))Require(!permit&&raCreated==0,"Lower-priority attack overlapped: "+id);
                    else Require(permit&&raCreated>0,"Independent ability missing: "+id);
                }
                if(name=="BlackHole"&&fxOn==true)
                {
                    var black=rf.OfType<Node2D>().First(n=>n.GetType().Name=="Blackhole");
                    var animation=(Node2D)ra.Single();
                    Vector2 center=animation.ToGlobal(animation.Get("center").AsVector2());
                    Require(black.GlobalPosition.DistanceTo(center)>150,"Black holes overlap in the current skin");
                }
                observing=false;
                await Wait(game,2.5);
            }
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            observing=false;game.GetTree().NodeAdded-=Added;
            hot.Clear();foreach(var pair in savedHot)hot[pair.Key]=pair.Value;
            values.Clear();foreach(var pair in savedValues)values[pair.Key]=pair.Value;
            File.WriteAllText(Path.Combine(output,"regent-validation.json"),JsonSerializer.Serialize(new {success=failure is null,error=failure?.ToString(),settingsOnly,expectedCases,cases=rows},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
    private static async Task Wait(NGame game,double seconds)
    {
        ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);
        while(Time.GetTicksMsec()<end)await game.ToSignal(game.GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    private static DictionaryEntry[] Snapshot(IDictionary dictionary)
    {
        var result=new List<DictionaryEntry>();var entries=dictionary.GetEnumerator();
        while(entries.MoveNext())result.Add(entries.Entry);
        return result.ToArray();
    }
    private static async Task Bounded(NGame game,Task task,int seconds,string id)
    {
        ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);
        while(!task.IsCompleted&&Time.GetTicksMsec()<end)await Wait(game,.03);
        Require(task.IsCompleted,"Play timeout: "+id);await task;
    }
    private static void Save(NGame game,string output,string id)
    {
        using var image=game.GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.Combine(output,id+".png"))==Error.Ok,"Screenshot failed");
    }
}
