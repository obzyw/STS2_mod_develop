using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace ElementalProbe;

[ModInitializer(nameof(Initialize))]
public static partial class Probe
{
    private static bool started;
    private static string Output => System.Environment.GetEnvironmentVariable("STS2_ELEMENTAL_OUTPUT")!;
    private static string Character => System.Environment.GetEnvironmentVariable("STS2_ELEMENTAL_CHARACTER") ?? "Ironclad";
    public static void Initialize()
    {
        if (System.Environment.GetEnvironmentVariable("STS2_ELEMENTAL_TEST") != "1") return;
        new Harmony("obzyw.sts2.elemental.probe").Patch(AccessTools.Method(typeof(NMainMenu), "_Ready"), postfix: new HarmonyMethod(typeof(Probe), nameof(Ready)));
        if(System.Environment.GetEnvironmentVariable("STS2_WATCHER_TIME_TEST")=="1")
            new Harmony("obzyw.sts2.watcher-time-trace").Patch(AccessTools.PropertySetter(typeof(Engine),"TimeScale"),prefix:new HarmonyMethod(typeof(Probe),nameof(TimeWrite)));
    }
    private static void TimeWrite(double value)
    {
        if(Math.Abs(value-Engine.TimeScale)>.000001)
            File.AppendAllText(Path.Combine(Output,"time-writes.txt"),$"{Time.GetTicksMsec()} {Engine.TimeScale} -> {value}\n{System.Environment.StackTrace}\n");
    }
    private static void Ready()
    {
        if (started) return;
        started = true;
        _ = Run();
    }
    private static async Task Run()
    {
        var game = NGame.Instance!;
        var cases = new List<object>();
        Exception? failure = null;
        try
        {
            await Wait(2);
            var type = Character == "Watcher"
                ? AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Watcher").GetType("WatcherMod.Watcher", true)!
                : typeof(CharacterModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Characters." + Character, true)!;
            var character = (CharacterModel)typeof(ModelDb).GetMethods().Single(m => m.Name == "Character" && m.IsGenericMethodDefinition).MakeGenericMethod(type).Invoke(null, null)!;
            var player = Player.CreateForNewRun(character, SaveManager.Instance.GenerateUnlockStateFromProgress(), 1UL);
            bool cursorTest=System.Environment.GetEnvironmentVariable("STS2_WATCHER_CURSOR_TEST")=="1";
            bool trapTest=System.Environment.GetEnvironmentVariable("STS2_KNIFE_TRAP_TEST")=="1";
            bool movementTest=System.Environment.GetEnvironmentVariable("STS2_SILENT_MOVEMENT_TEST")=="1";
            bool effectsTest=System.Environment.GetEnvironmentVariable("STS2_SILENT_EFFECTS_TEST")=="1";
            bool skinTest=System.Environment.GetEnvironmentVariable("STS2_SKIN_ANIMATION_TEST")=="1";
            var acts=(cursorTest||trapTest||movementTest||effectsTest||skinTest)?ModelDb.ActsByIndex.Take(3).Select(a=>a.First(x=>x.IsDefault)).ToList():ActModel.GetDefaultList().ToList();
            var run = RunState.CreateForNewRun([player], acts.Select(a => a.ToMutable()).ToList(), [], GameMode.Standard, 0, "ELEMENTALVFX");
            RunManager.Instance.SetUpNewSingleplayer(run, shouldSave: false);
            await PreloadManager.LoadRunAssets([character]);
            await PreloadManager.LoadActAssets(run.Acts[0]);
            RunManager.Instance.Launch();
            if(cursorTest)
            {
                await CheckWatcherCursor(cases);
                goto Completed;
            }
            game.RootSceneContainer.SetCurrentScene(NRun.Create(run));
            await RunManager.Instance.SetActInternal(0);
            RunManager.Instance.RunLocationTargetedBuffer.OnLocationChanged(run.RunLocation);
            RunManager.Instance.MapSelectionSynchronizer.OnLocationChanged(run.MapLocation);
            foreach (var relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<AxebotsNormal>().ToMutable());
            await Until(() => CombatManager.Instance.IsInProgress && player.PlayerCombatState != null && !CombatManager.Instance.PlayerActionsDisabled, 25);
            await Wait(1);
            MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer.Instance?.Clear();
            var actor = player.Creature.GetCreatureNode()!;
            // Store native bone names and transforms for this machine's actual skin, not a copied rig.
            File.WriteAllText(Path.Combine(Output, Character + "-rig.txt"), actor.Visuals.GetTreeStringPretty());
            if(skinTest)
            {
                await CheckSkinAnimation(player,cases);
                goto Completed;
            }
            if(effectsTest)
            {
                await CheckSilentEffects(player,cases);
                goto Completed;
            }
            if(movementTest)
            {
                await CheckSilentMovement(player,cases);
                goto Completed;
            }
            if(trapTest)
            {
                await CheckKnifeTrap(player,cases);
                goto Completed;
            }
            if(System.Environment.GetEnvironmentVariable("STS2_WATCHER_CURSOR_TEST")=="1")
            {
                await CheckWatcherCursor(cases);
                goto Completed;
            }
            if(System.Environment.GetEnvironmentVariable("STS2_ELEMENTAL_UI_TEST")=="1")
            {
                await CheckFireUi(player,cases);
                goto Completed;
            }
            if(System.Environment.GetEnvironmentVariable("STS2_WATCHER_TIME_TEST")=="1")
            {
                await CheckWatcherTime(player,cases);
                goto Completed;
            }
            if(System.Environment.GetEnvironmentVariable("STS2_CORRUPTION_TEST")=="1")
            {
                await CheckCorruption(player,cases);
                goto Completed;
            }
            var stone = await Card<StoneArmor>(player);
            var holder = FindHolder(stone);
            AccessTools.Method(typeof(NHandCardHolder), "OnFocus").Invoke(holder, null);
            await Wait(.45);
            var fx = actor.Visuals.GetNode<Node2D>("ElementalDefenseVfx");
            Require(fx.Get("held_kind").AsString() == "stone", "Stone preview did not start from actual focus.");
            await Capture("stone-preview");
            AccessTools.Method(typeof(NHandCardHolder), "OnUnfocus").Invoke(holder, null);
            await Wait(.25);
            Require(fx.Get("held_kind").AsString() == "", "Stone preview was not cancelled.");
            await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), stone, null);
            await Wait(.24); await Capture("stone-merge");
            await Wait(.5); await Capture("stone-armor");
            File.WriteAllText(Path.Combine(Output, Character + "-skeleton.json"), JsonSerializer.Serialize(fx.Call("rig_audit").AsGodotDictionary().ToDictionary(p => p.Key.AsString(), p => p.Value.ToString())));
            Require(player.Creature.GetPowerAmount<PlatingPower>() == (int)stone.DynamicVars["PlatingPower"].BaseValue, "Stone Armor gameplay changed.");
            Require(fx.Get("armor_anchor_count").AsInt32() > 0, "Stone plates were not attached to native bones.");
            Require(fx.Call("audit").AsGodotDictionary()["coated_surfaces"].AsInt32()>0,"No persistent body surfaces received stone armor.");
            cases.Add(new { id = "stone", audit = Audit(fx) });
            if (System.Environment.GetEnvironmentVariable("STS2_ELEMENTAL_MOTION") == "1")
            {
                await CheckMotions(actor,fx,player,cases);
                goto Completed;
            }
            actor.SetAnimationTrigger("Attack"); await Wait(.20); await Capture("stone-action"); await Wait(1);
            if (Character == "Ironclad")
            {
                var rig = fx.Get("rig").AsGodotObject();
                var previous = rig.Get("normal_material");
                var ghost = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; uniform float ghost_opacity = 0.4; void fragment() { vec4 source = texture(TEXTURE,UV); COLOR = vec4(source.rgb * COLOR.rgb * vec3(0.6,0.8,1.0), source.a * COLOR.a * ghost_opacity); }" } };
                rig.Set("normal_material",ghost);
                try
                {
                    ghost.SetShaderParameter("ghost_opacity",.32f);
                    await Wait(.25);
                    var materials = fx.Get("coatings").AsGodotArray().Select(b=>b.AsGodotDictionary()["material"].AsGodotObject()).OfType<ShaderMaterial>().ToArray();
                    File.WriteAllText(Path.Combine(Output,Character+"-material-audit.json"),JsonSerializer.Serialize(materials.Select(m=>new { code=m.Shader.Code, opacity=m.GetShaderParameter("ghost_opacity").ToString() })));
                    Require(materials.Length > 0 && materials.All(m => m.Shader.Code.Contains("ghost_opacity") && Math.Abs(m.GetShaderParameter("ghost_opacity").AsDouble()-.32)<.001),"Ghost shader was overridden or its animated opacity was lost.");
                    await Capture("stone-ghost");
                    ghost.SetShaderParameter("ghost_opacity",.65f); await Wait(.12);
                    Require(materials.All(m => Math.Abs(m.GetShaderParameter("ghost_opacity").AsDouble()-.65)<.001),"Animated source material uniforms stopped following.");
                    cases.Add(new { id="material-composition", animatedOpacity=true });
                }
                finally { rig.Set("normal_material",previous); }
                await Wait(.2);
            }
            if (Character == "Silent")
            {
                var leaseType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("MoreSilentAnimations.DisplayLease")).FirstOrDefault(t=>t!=null)!;
                Require(leaseType != null,"Installed display lease is missing.");
                var lease = AccessTools.Constructor(leaseType,[typeof(NCreature)]).Invoke([actor]);
                try
                {
                    AccessTools.Method(leaseType,"Acquire").Invoke(lease,null);
                    AccessTools.Method(leaseType,"AttackLayer").Invoke(lease,[true]);
                    await Wait(.25);
                    var root = (Node2D)AccessTools.Field(leaseType,"Root").GetValue(lease)!;
                    var currentRig = fx.Get("rig").As<Node>();
                    Require(currentRig == root || root.IsAncestorOf(currentRig),"Armor stayed on the hidden body instead of following the presentation lease.");
                    Require(fx.Call("audit").AsGodotDictionary()["coated_surfaces"].AsInt32()>0,"Presentation lease has no coat.");
                    Require(Descendants(currentRig).OfType<CanvasItem>().Any(n=>n.Material is ShaderMaterial material && material.Shader.Code.Contains("edv_strength") && material.GetShaderParameter("edv_strength").AsDouble()>.9),"Frozen presentation mesh did not submit the stone material.");
                    await Capture("stone-display-lease");
                    cases.Add(new { id="display-lease", follows=true });
                }
                finally { ((IDisposable)lease).Dispose(); }
                await Wait(.25);
            }
            var fire = await Card<FlameBarrier>(player);
            holder = FindHolder(fire);
            holder.BeginDrag(); await Wait(.45);
            Require(fx.Get("held_kind").AsString() == "fire", "Fire selection did not start from drag.");
            await Capture("fire-preview");
            holder.CancelDrag(); await Wait(.2);
            Require(fx.Get("held_kind").AsString() == "", "Fire drag cancellation leaked preview.");
            var play = CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), fire, null);
            await Until(() => fx.Get("cast_count").AsInt32() >= 2, 8);
            await Wait(.15); await Capture("fire-wall"); await play;
            await Wait(.6);
            int reflect = (int)fire.DynamicVars["DamageBack"].BaseValue;
            Require(player.Creature.GetPowerAmount<FlameBarrierPower>() == reflect, "Flame Barrier power changed.");
            var state = CombatManager.Instance.DebugOnlyGetState()!;
            var enemy = state.Enemies.First();
            decimal hp = enemy.CurrentHp;
            for (int hit = 0; hit < 2; hit++)
            {
                var damage = CreatureCmd.Damage(new BlockingPlayerChoiceContext(), player.Creature, 1, ValueProp.Move, enemy);
                await Wait(.09);
                Require(Descendants(NCombatRoom.Instance!).Any(n => n.Name.ToString().StartsWith("FlameBarrierRetaliation")), "Missing retaliation projectile.");
                await Capture("fire-reflect-" + hit); await damage; await Wait(.55);
            }
            Require(hp - enemy.CurrentHp == reflect * 2, "Retaliation damage did not match the card per hit.");
            cases.Add(new { id = "fire", retaliationDamage = hp - enemy.CurrentHp, audit = Audit(fx) });
            await CreatureCmd.Damage(new BlockingPlayerChoiceContext(), player.Creature, 1, ValueProp.Unpowered, enemy);
            Require(!Descendants(NCombatRoom.Instance!).Any(n => n.Name.ToString().StartsWith("FlameBarrierRetaliation")), "Non-attack emitted retaliation.");
            await PowerCmd.Remove<PlatingPower>(player.Creature);
            await PowerCmd.Remove<FlameBarrierPower>(player.Creature);
            await Wait(.8);
            Require(fx.Get("stone_alpha").AsDouble() == 0 && fx.Get("fire_alpha").AsDouble() == 0 && !fx.Visible, "Expired power visuals remained.");
            await Capture("expired");
            cases.Add(new { id = "expired", clean = true });
            Completed:;
        }
        catch (Exception e) { failure = e; GD.PushError(e.ToString()); }
        File.WriteAllText(Path.Combine(Output, Character + "-result.json"), JsonSerializer.Serialize(new { success = failure == null, character = Character, error = failure?.ToString(), cases }, new JsonSerializerOptions { WriteIndented = true }));
        game.GetTree().Quit(failure == null ? 0 : 1);
    }
    private static object Audit(Node fx) => fx.Call("audit").AsGodotDictionary().ToDictionary(p => p.Key.AsString(), p => p.Value.ToString());
    private static async Task CheckSilentEffects(Player player,List<object> cases)
    {
        Require(Harmony.GetAllPatchedMethods().Any(m=>Harmony.GetPatchInfo(m)?.Owners.Contains("local.sts2.silent-effects-compat")==true),"Full Silent compatibility failed initialization.");
        var entry=AccessTools.TypeByName("SilentEffectsCompat.Entry")!;
        var playback=(HashSet<string>)AccessTools.Field(entry,"fxPlayback").GetValue(null)!;
        var msa=AccessTools.TypeByName("MoreSilentAnimations.AnimationSettings")!;
        var allows=AccessTools.Method(msa,"Allows",[typeof(string)]);
        var fx=AccessTools.TypeByName("SilentFX.Core.CardFxSettings")!;
        var enabled=AccessTools.Method(fx,"Enabled",[typeof(string)]);
        var current=AccessTools.Property(msa,"Current").GetValue(null)!;
        var disabled=(string[])AccessTools.Property(msa,"DisabledCards").GetValue(current)!;
        var names=(string[])AccessTools.Field(AccessTools.TypeByName("MoreSilentAnimations.SettingsState"),"Cards").GetValue(null)!;
        var routes=new List<object>();
        foreach(var name in names.Where(n=>n!="GrandFinale"))
        {
            bool expected=!disabled.Contains(name)&&(name=="KnifeTrap"||!playback.Contains(name)||!(bool)enabled.Invoke(null,[name])!);
            bool actual=(bool)allows.Invoke(null,[name])!;
            Require(expected==actual,"Visual route disagrees for "+name);
            routes.Add(new{card=name,moreSilentAllowed=actual,silentFxEnabled=(bool)enabled.Invoke(null,[name])!});
        }
        cases.Add(new{id="shared-card-routes",routes});
        var observer=new Harmony("obzyw.sts2.silent-effects-observer");
        observer.Patch(AccessTools.Method(AccessTools.TypeByName("SilentFX.Cards.PoisonedStabFx"),"OnHit"),postfix:new HarmonyMethod(typeof(Probe),nameof(SawTrap)));
        observer.Patch(AccessTools.Method(AccessTools.TypeByName("MoreSilentAnimations.ActorPresentation"),"Begin"),postfix:new HarmonyMethod(typeof(Probe),nameof(SawTrap)));
        try
        {
            foreach(var type in new[]{typeof(PoisonedStab),typeof(Slice)})
            {
                foreach(var enemy in CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies){enemy.SetMaxHpInternal(1000);enemy.SetCurrentHpInternal(1000);}
                var canonical=(CardModel)typeof(ModelDb).GetMethods().Single(m=>m.Name=="Card"&&m.IsGenericMethodDefinition).MakeGenericMethod(type).Invoke(null,null)!;
                var card=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(canonical,player);
                await CardPileCmd.Add(card,PileType.Hand);player.PlayerCombatState!.Energy=99;
                await Wait(.2);trapHits.Clear();
                var target=CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.First();
                await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,target);await Wait(.6);
                var counts=new Dictionary<string,int>(trapHits);
                if(type==typeof(PoisonedStab))Require(counts.GetValueOrDefault("PoisonedStabFx.OnHit")>0&&counts.GetValueOrDefault("ActorPresentation.Begin")==0,"SilentFX did not own shared-card hit playback.");
                else Require(counts.GetValueOrDefault("ActorPresentation.Begin")>0,"Nonconflicting MoreSilent animation was disabled.");
                cases.Add(new{id="actual-playback",card=type.Name,counts});
            }
        }
        finally{observer.UnpatchAll(observer.Id);}
        await CheckKnifeTrap(player,cases,true);
    }
    private static async Task CheckSilentMovement(Player player,List<object> cases)
    {
        var msa=AccessTools.TypeByName("MoreSilentAnimations.AnimationSettings")!;
        var current=AccessTools.Property(msa,"Current").GetValue(null)!;
        Require((bool)AccessTools.Property(msa,"Enabled").GetValue(current)!,"MoreSilentAnimations master switch is off.");
        cases.Add(new{id="author-settings",enabled=true,disabledCards=(string[])AccessTools.Property(msa,"DisabledCards").GetValue(current)!});
        var actor=player.Creature.GetCreatureNode()!;
        foreach(var type in new[]{typeof(Neutralize),typeof(Backstab)})
        {
            foreach(var enemy in CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies){enemy.SetMaxHpInternal(1000);enemy.SetCurrentHpInternal(1000);}
            var canonical=(CardModel)typeof(ModelDb).GetMethods().Single(m=>m.Name=="Card"&&m.IsGenericMethodDefinition).MakeGenericMethod(type).Invoke(null,null)!;
            var card=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(canonical,player);
            player.PlayerCombatState!.Energy=99;await CardPileCmd.Add(card,PileType.Hand);await Wait(.2);
            var target=CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.First();
            int visible=0,duplicates=0;float displacement=0;
            void Sample()
            {
                var display=Descendants(NGame.Instance!).OfType<Node2D>().FirstOrDefault(n=>n.Name=="MoreSilentDisplay"&&n.IsVisibleInTree()&&n.Modulate.A>.1);
                if(display==null)return;
                visible++;displacement=Math.Max(displacement,display.GlobalPosition.DistanceTo(actor.Body.GlobalPosition));
                if(actor.Body.Modulate.A>.1)duplicates++;
            }
            var tree=NGame.Instance!.GetTree();tree.ProcessFrame+=Sample;
            decimal hp=target.CurrentHp;
            try{await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,target);await Wait(1);}
            finally{tree.ProcessFrame-=Sample;}
            Require(visible>0&&displacement>5&&duplicates==0,$"Silent movement failed for {type.Name}: visible={visible}, distance={displacement}, duplicates={duplicates}");
            cases.Add(new{id="movement",card=type.Name,visibleFrames=visible,displacement,duplicateBodyFrames=duplicates,damage=hp-target.CurrentHp});
        }
    }
    private static readonly Dictionary<string,int> trapHits=new();
    private static void SawTrap(MethodBase __originalMethod,bool __runOriginal)
    {
        if(!__runOriginal) return;
        string key=__originalMethod.DeclaringType!.Name+"."+__originalMethod.Name;
        trapHits[key]=trapHits.GetValueOrDefault(key)+1;
    }
    private static async Task CheckKnifeTrap(Player player,List<object> cases,bool quick=false)
    {
        Type S(string n)=>AccessTools.TypeByName("SilentFX."+n)!;
        Type M(string n)=>AccessTools.TypeByName("MoreSilentAnimations."+n)!;
        var observer=new Harmony("obzyw.sts2.knife-trap-observer");
        foreach(var target in new[]{AccessTools.Method(S("Cards.KnifeTrapFx"),"OnPlay"),AccessTools.Method(S("Cards.ShivFx"),"OnShivHit")})
            observer.Patch(target,postfix:new HarmonyMethod(typeof(Probe),nameof(SawTrap)));
        foreach(var ctor in M("KnifeTrapVisual").GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            observer.Patch(ctor,postfix:new HarmonyMethod(typeof(Probe),nameof(SawTrap)));
        var settings=M("AnimationSettings");var current=AccessTools.Property(settings,"Current");var saved=current.GetValue(null)!;
        var held=S("Cards.HeldFxManager");var enabled=AccessTools.Method(S("Core.CardFxSettings"),"Enabled",[typeof(string)]);
        bool independent=(bool)enabled.Invoke(null,["Shiv"])!;
        try
        {
            foreach(var mode in quick?new[]{"base"}:new[]{"base","upgraded","fallback"})
            {
                if(mode=="fallback")
                {
                    var off=Activator.CreateInstance(settings)!;AccessTools.Property(settings,"Enabled").SetValue(off,false);current.SetValue(null,off);
                }
                foreach(var enemy in CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies){enemy.SetMaxHpInternal(1000);enemy.SetCurrentHpInternal(1000);}
                foreach(var old in PileType.Exhaust.GetPile(player).Cards.Where(c=>c is Shiv).ToArray()) await CardPileCmd.Add(old,PileType.Discard);
                var card=await Card<KnifeTrap>(player);
                if(mode=="upgraded") CardCmd.Upgrade(card);
                for(int i=0;i<3;i++)
                {
                    var shiv=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<Shiv>(),player);
                    if(mode=="upgraded") CardCmd.Upgrade(shiv);
                    await CardPileCmd.Add(shiv,PileType.Exhaust);
                }
                var enemyTarget=CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.First();
                AccessTools.Method(held,"Begin").Invoke(null,[card,(Func<bool>)(()=>true)]);
                AccessTools.Method(held,"SetTarget").Invoke(null,[enemyTarget]);await Wait(.35);
                var session=AccessTools.Field(held,"_held").GetValue(null);
                Require(session?.GetType().Name=="KnifeTrapHeld","SilentFX KnifeTrap selection preview was vetoed.");
                await Capture("trap-"+mode+"-preview");
                AccessTools.Method(held,"End").Invoke(null,[true]);
                trapHits.Clear();decimal hp=CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.Sum(e=>e.CurrentHp);
                await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,enemyTarget);await Wait(.6);
                var counts=new Dictionary<string,int>(trapHits);
                decimal damage=hp-CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.Sum(e=>e.CurrentHp);
                Require(damage==(mode=="upgraded"?18:12),"Trap damage changed: "+damage);
                if(mode=="fallback") Require(counts.GetValueOrDefault("KnifeTrapFx.OnPlay")>0&&counts.GetValueOrDefault("KnifeTrapVisual..ctor")==0,"Disabling MoreSilent did not restore SilentFX playback.");
                else Require(counts.GetValueOrDefault("KnifeTrapVisual..ctor")==3&&counts.GetValueOrDefault("KnifeTrapFx.OnPlay")==0&&counts.GetValueOrDefault("ShivFx.OnShivHit")==0,"Trap visuals did not use MoreSilent exclusively.");
                Require((bool)enabled.Invoke(null,["Shiv"])! == independent,"Trap context leaked into independent Shiv settings.");
                cases.Add(new{id="knife-trap-stages",mode,preview=session!.GetType().Name,counts,damage,independentShivUnchanged=true});
            }
        }
        finally{current.SetValue(null,saved);AccessTools.Method(held,"Reset").Invoke(null,null);observer.UnpatchAll(observer.Id);}
    }
    private static async Task CheckWatcherCursor(List<object> cases)
    {
        var core=AccessTools.TypeByName("CookieCursor.Core")!;
        var manager=NGame.Instance!.CursorManager;
        var normalField=AccessTools.Field(manager.GetType(),"_cursorNotTilted");
        var actual=(Image)normalField.GetValue(manager)!;
        var defaults=(Image)AccessTools.Field(core,"defaultCursorNotTilted").GetValue(null)!;
        Require(!ReferenceEquals(actual,defaults),"Actual Watcher run retained the default cursor.");
        var watcherPixels=actual.GetData();
        actual.SavePng(Path.Combine(Output,"watcher-cursor-active-64.png"));
        var get=AccessTools.Method(core,"GetCharCookies");
        var expectedWatcher=((Image,Image,Vector2))get.Invoke(null,["watcher"])!;
        using(expectedWatcher.Item1) using(expectedWatcher.Item2)
            Require(watcherPixels.SequenceEqual(expectedWatcher.Item1.GetData()),"Run launch did not install the Watcher image.");
        float scale=(float)AccessTools.Field(core,"CursorScale").GetValue(null)!;
        try
        {
            foreach(var key in new[]{"watcher","thewatcher"}) foreach(float factor in new[]{1f,1.5f})
            {
                AccessTools.Field(core,"CursorScale").SetValue(null,factor);
                var pair=((Image,Image,Vector2))get.Invoke(null,[key])!;
                using(pair.Item1) using(pair.Item2)
                {
                    Require(pair.Item1.GetWidth()==(int)(64*factor)&&pair.Item1.GetHeight()==(int)(64*factor),"Cursor scale ignored.");
                    Require(!pair.Item1.GetData().SequenceEqual(pair.Item2.GetData()),"Click tilt images are identical.");
                    Require(pair.Item1.GetPixel(0,0).A==0,"Cursor background is not transparent.");
                    Require(pair.Item3==new Vector2(10,4)*factor,"Click hot spot did not scale.");
                    cases.Add(new{id="cursor-assets",key,scale=factor,width=pair.Item1.GetWidth(),transparent=true,clickTilt=true,hotSpot=pair.Item3.ToString()});
                }
            }
        }
        finally{AccessTools.Field(core,"CursorScale").SetValue(null,scale);}
        // Use the author's application and revert APIs to verify lifecycle without writing preferences.
        AccessTools.Method(core,"ApplyLocalCursor").Invoke(null,["ironclad",manager]);
        var iron=(Image)normalField.GetValue(manager)!;
        Require(!iron.GetData().SequenceEqual(watcherPixels),"Another character did not keep its own cursor.");
        AccessTools.Method(core,"ApplyLocalCursor").Invoke(null,["watcher",manager]);
        AccessTools.Method(core,"RevertLocalCursor").Invoke(null,[manager]);
        var restored=(Image)normalField.GetValue(manager)!;
        var expected=((Image,Image,Vector2))get.Invoke(null,["default"])!;
        using(expected.Item1) using(expected.Item2)
            Require(restored.GetData().SequenceEqual(expected.Item1.GetData()),"Default cursor did not restore.");
        AccessTools.Method(core,"ApplyLocalCursor").Invoke(null,["watcher",manager]);
        cases.Add(new{id="cursor-lifecycle",actualRunUsedWatcher=true,otherCharacterRetained=true,defaultRestored=true});
        await Wait(.1);
    }
    private static async Task CheckFireUi(Player player,List<object> cases)
    {
        var fire=await Card<FlameBarrier>(player);
        await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),fire,null);await Wait(1);
        var actor=player.Creature.GetCreatureNode()!;
        var fx=actor.Visuals.GetNode<Node2D>("ElementalDefenseVfx");
        Require(fx.Visible&&fx.Get("fire_alpha").AsDouble()>.9,"Fire wall is not active.");
        int oldZ=actor.Visuals.ZIndex;
        try
        {
            actor.Visuals.ZIndex=100;await Wait(.15);
            Require(!fx.ZAsRelative&&fx.ZIndex==NCombatRoom.Instance!.CombatVfxContainer.ZIndex,"Fire wall inherited the skin's foreground Z.");
            var pause=NRun.Instance!.GlobalUi.TopBar.Pause;
            AccessTools.Method(pause.GetType(),"OnRelease").Invoke(pause,null);await Wait(.6);
            var menu=Descendants(NGame.Instance!).OfType<Control>().Single(n=>n.GetType().Name=="NPauseMenu"&&n.IsVisibleInTree());
            await Capture("fire-pause-menu");
            // An opaque probe in the real menu at the wall's exact position proves
            // actual GPU occlusion, rather than just comparing Z properties.
            var wall=fx.GetChildren().OfType<Sprite2D>().First(n=>n.Visible);
            var color=new Color(.1f,.8f,.2f,1);
            var marker=new ColorRect{Color=color,Size=new Vector2(40,40),MouseFilter=Control.MouseFilterEnum.Ignore};
            menu.AddChild(marker);marker.GlobalPosition=wall.GlobalPosition-new Vector2(20,20);
            await Wait(.1);await NGame.Instance!.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using(var image=NGame.Instance.GetViewport().GetTexture().GetImage())
            {
                var point=marker.GetGlobalTransformWithCanvas()*new Vector2(20,20);
                var size=NGame.Instance.GetViewport().GetVisibleRect().Size;
                var pixel=image.GetPixel((int)(point.X*image.GetWidth()/size.X),(int)(point.Y*image.GetHeight()/size.Y));
                Require(Math.Abs(pixel.R-color.R)<.03&&Math.Abs(pixel.G-color.G)<.03&&Math.Abs(pixel.B-color.B)<.03,"Fire wall painted over the menu's opaque control.");
                cases.Add(new{id="fire-ui-occlusion",character=Character,skinZ=100,fxZ=fx.ZIndex,menuVisible=true,opaqueMenuPixel=pixel.ToString(),fireAlpha=fx.Get("fire_alpha").AsDouble()});
            }
            marker.QueueFree();await Wait(.1);
            AccessTools.Method(pause.GetType(),"OnRelease").Invoke(pause,null);await Wait(.6);
            Require(fx.Visible&&fx.Get("fire_alpha").AsDouble()>.9,"Closing menu lost active fire wall.");
            cases.Add(new{id="fire-ui-resume",wallStillActive=true});
        }
        finally{actor.Visuals.ZIndex=oldZ;}
    }
    private static async Task CheckWatcherTime(Player player,List<object> cases)
    {
        var fx=AccessTools.TypeByName("WatcherMod.Vfx.Fx")!;
        var bullet=AccessTools.Method(fx,"BulletTime",[typeof(double),typeof(int)]);
        var patch=AccessTools.TypeByName("WatcherNoBulletTime.Entry")!;
        int Count() => (int)AccessTools.Property(patch,"Suppressed").GetValue(null)!;
        double original=Engine.TimeScale;
        try
        {
            foreach(double speed in new[]{1.0,5.0})
            {
                Engine.TimeScale=speed;
                var tasks=new[]{(Task)bullet.Invoke(null,[.35,150])!, (Task)bullet.Invoke(null,[.04,110])!, (Task)bullet.Invoke(null,[.6,600])!};
                Require(tasks.All(t=>t.IsCompletedSuccessfully),"Suppressed bullet time did not complete immediately.");
                double min=speed,max=speed;
                ulong end=Time.GetTicksMsec()+800;int frames=0;
                while(Time.GetTicksMsec()<end)
                {
                    await Wait(.001);frames++;min=Math.Min(min,Engine.TimeScale);max=Math.Max(max,Engine.TimeScale);
                }
                Require(Math.Abs(min-speed)<.000001&&Math.Abs(max-speed)<.000001,"Nested bullet time altered global speed.");
                cases.Add(new{id="nested-bullet-time",speed,min,max,frames});
            }
            var cardType=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Watcher").GetType("WatcherMod.WatcherRagnarok",true)!;
            var canonical=(CardModel)typeof(ModelDb).GetMethods().Single(m=>m.Name=="Card"&&m.IsGenericMethodDefinition).MakeGenericMethod(cardType).Invoke(null,null)!;
            foreach(double speed in new[]{1.0,5.0})
            {
                // Keep the same combat alive for repeated casts, without changing saved runs.
                foreach(var enemy in CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies)
                {
                    AccessTools.Method(enemy.GetType(),"SetMaxHpInternal").Invoke(enemy,[1000m]);
                    AccessTools.Method(enemy.GetType(),"SetCurrentHpInternal").Invoke(enemy,[1000m]);
                }
                foreach(bool upgraded in new[]{false,true})
                {
                    var card=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(canonical,player);
                    if(upgraded) card.UpgradeInternal();
                    player.PlayerCombatState!.Energy=99;
                    await CardPileCmd.Add(card,PileType.Hand);await Wait(.25);
                    Engine.TimeScale=speed;
                    int before=Count();decimal hp=CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.Sum(e=>e.CurrentHp);
                    var play=CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,null);
                    double min=speed,max=speed;int frames=0;ulong end=Time.GetTicksMsec()+2000;
                    while(!play.IsCompleted||Time.GetTicksMsec()<end)
                    {
                        await Wait(.001);frames++;min=Math.Min(min,Engine.TimeScale);max=Math.Max(max,Engine.TimeScale);
                    }
                    await play;
                    Require(Count()>before,"Actual Ragnarok did not reach the patched visual bullet-time call.");
                    Require(Math.Abs(min-speed)<.000001&&Math.Abs(max-speed)<.000001,$"Ragnarok changed global speed: speed={speed}, upgraded={upgraded}, min={min}, max={max}, after={Engine.TimeScale}.");
                    decimal damage=hp-CombatManager.Instance.DebugOnlyGetState()!.HittableEnemies.Sum(e=>e.CurrentHp);
                    Require(damage>0,"Ragnarok did no damage.");
                    await Capture("ragnarok-"+speed+"x-"+(upgraded?"upgraded":"base"));
                    cases.Add(new{id="ragnarok",speed,upgraded,min,max,frames,suppressed=Count()-before,damage});
                }
            }
        }
        finally{Engine.TimeScale=original;}
    }
    private static async Task CheckCorruption(Player player,List<object> cases)
    {
        var assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="IroncladFx");
        var assets=assembly.GetType("IroncladFx.Assets",true)!;
        var assetMethod=AccessTools.Method(assets,"Texture");
        var texture=(Texture2D)assetMethod.Invoke(null,["corruption/rig2/body.png"])!;
        using(var actual=texture.GetImage())
        using(var original=Image.LoadFromFile(Path.Combine(Path.GetDirectoryName(assembly.Location)!,"assets","corruption","rig2","body.png")))
        {
            actual.Convert(Image.Format.Rgba8);original.Convert(Image.Format.Rgba8);
            byte[] now=actual.GetData(),before=original.GetData();int changed=0,outside=0;
            Require(actual.GetSize()==original.GetSize(),"Puppet body dimensions changed.");
            for(int i=0;i<now.Length;i+=4)
            {
                if(now.AsSpan(i,4).SequenceEqual(before.AsSpan(i,4)))continue;
                changed++;int pixel=i/4,x=pixel%actual.GetWidth(),y=pixel/actual.GetWidth();
                if(x<260||x>=980||y>=800)outside++;
            }
            Require(changed>1000&&outside==0,"Replacement changed pixels outside the head/beard footprint.");
            actual.SavePng(Path.Combine(Output,"corruption-effective-body.png"));
            cases.Add(new{id="head-only-art",changedPixels=changed,changedOutsideHead=outside,size=actual.GetSize().ToString()});
        }
        var card=await Card<Corruption>(player);var holder=FindHolder(card);
        // IroncladFX previews start at multi-creature targeting, not hand hover.
        var ready=assembly.GetType("IroncladFx.Ready",true)!;
        AccessTools.Method(ready,"Show").Invoke(null,[card,holder.CardNode]);
        await Wait(1.25);
        var effect=assembly.GetType("IroncladFx.Cards.CorruptionFx",true)!;
        Require(AccessTools.Field(effect,"_preview").GetValue(null)!=null,"Original Corruption selection preview did not appear.");
        await Capture("corruption-preview");
        AccessTools.Method(ready,"Clear").Invoke(null,null);await Wait(.5);
        cases.Add(new{id="original-preview",appeared=true,entry="IroncladFx.Ready.Show (targeting renderer)"});
        var play=CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,null);
        await Wait(.25);await Capture("corruption-cast-025");
        await Wait(.5);await Capture("corruption-cast-075");
        await Wait(.7);await Capture("corruption-cast-145");await play;
        Require(player.Creature.GetPowerAmount<CorruptionPower>()>0,"Corruption power was not applied.");
        await Wait(6.0);await Capture("corruption-finished");
        cases.Add(new{id="original-play",powerApplied=true,animationImplementation="IroncladFx unchanged; texture postfix only"});
    }
    private static async Task CheckMotions(NCreature actor,Node2D fx,Player player,List<object> cases)
    {
        foreach(string trigger in new[]{"Attack","Cast","Hit","PowerUp","Relaxed","Idle"}.Concat(Character=="Ironclad"?new[]{"heavyAttack"}:Array.Empty<string>()))
        {
            actor.SetAnimationTrigger(trigger);
            cases.Add(await SampleMotion(fx,"trigger-"+trigger,.55));
            await Wait(.35);
        }
        var defendType=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards.Defend"+Character);
        if(defendType!=null)
        {
            var canonical=(CardModel)typeof(ModelDb).GetMethods().Single(m=>m.Name=="Card"&&m.IsGenericMethodDefinition).MakeGenericMethod(defendType).Invoke(null,null)!;
            var card=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(canonical,player);
            player.PlayerCombatState!.Energy=99;
            await CardPileCmd.Add(card,PileType.Hand);
            var play=CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,null);
            cases.Add(await SampleMotion(fx,"play-defend",.65)); await play;
        }
        if(Character=="Ironclad")
        {
            foreach(var cardType in new[]{typeof(ShrugItOff),typeof(Impervious)})
            {
                var canonical=(CardModel)typeof(ModelDb).GetMethods().Single(m=>m.Name=="Card"&&m.IsGenericMethodDefinition).MakeGenericMethod(cardType).Invoke(null,null)!;
                var card=CombatManager.Instance.DebugOnlyGetState()!.CreateCard(canonical,player);
                player.PlayerCombatState!.Energy=99;
                await CardPileCmd.Add(card,PileType.Hand);
                var play=CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),card,null);
                cases.Add(await SampleMotion(fx,"play-"+cardType.Name,.9));await play;await Wait(.7);
            }
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("ShieldOnly.NativeShieldPresentation")).First(t=>t!=null)!;
            var parts=Descendants(actor.Body).OfType<CanvasItem>().Where(n=>n.GetClass()=="SpineMesh2D").Select(n=>(n,n.Modulate)).ToList();
            var presentation=AccessTools.Constructor(type,[typeof(NCreature),typeof(bool)]).Invoke([actor,false]);
            var carrier=(Node2D)AccessTools.Property(type,"Carrier").GetValue(presentation)!;
            try
            {
                AccessTools.Method(type,"SetOwnership").Invoke(presentation,[parts,true]);
                foreach(var pose in new[]{"guard-raise","guard-lower"})
                {
                    AccessTools.Method(type,"Pose").Invoke(presentation,[pose,.18f]);
                    cases.Add(await SampleMotion(fx,"shield-"+pose,.45));
                    Require(fx.Get("rig").As<Node>().IsAncestorOf(carrier)==false && carrier.IsAncestorOf(fx.Get("rig").As<Node>()),"Armor did not follow nested shield body.");
                }
            }
            finally
            {
                AccessTools.Method(type,"SetOwnership").Invoke(presentation,[parts,false]);
                AccessTools.Method(type,"StopHolding").Invoke(presentation,null);
                carrier.QueueFree();
            }
            await Wait(.2);
        }
        await PowerCmd.Remove<PlatingPower>(player.Creature); await Wait(.5);
        var fire=await Card<FlameBarrier>(player);
        await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),fire,null); await Wait(1.0);
        foreach(double speed in new[]{1.0,5.0})
        {
            // Keep native gameplay intact; only acceleration of presentation delta is under test.
            if(player.Creature.GetPowerAmount<FlameBarrierPower>()==0)
            {var replay=await Card<FlameBarrier>(player);await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(),replay,null);await Wait(1.0);}
            double previous=Engine.TimeScale;
            try
            {
                Engine.TimeScale=speed;
                await PowerCmd.Remove<FlameBarrierPower>(player.Creature);
                await Wait(.15);
                double alpha=fx.Get("fire_alpha").AsDouble();
                Require(fx.Visible&&alpha>.1&&alpha<1,"Fire wall disappeared abruptly instead of fading.");
                await Capture("fire-fade-"+speed+"x-150ms");
                await Wait(.22);
                double middle=fx.Get("fire_alpha").AsDouble();
                Require(middle>0&&middle<alpha,"Fire wall fade did not progress gradually.");
                await Capture("fire-fade-"+speed+"x-370ms");
                await Wait(.4);
                Require(fx.Get("fire_alpha").AsDouble()==0&&!fx.Visible,"Fire wall fade failed to finish.");
                cases.Add(new{id="fire-fade",speed,alphaAt150ms=alpha,alphaAt370ms=middle,finished=true});
            }
            finally{Engine.TimeScale=previous;}
        }
    }
    private static async Task<object> SampleMotion(Node2D fx,string motion,double seconds)
    {
        var trace=new List<object>();ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);int minimum=int.MaxValue;
        Image? shot=null;
        while(Time.GetTicksMsec()<end||trace.Count<8)
        {
            await fx.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            var result=fx.Call("coverage_audit").AsGodotDictionary();
            int count=result["submitted"].AsInt32();minimum=Math.Min(minimum,count);
            trace.Add(result.ToDictionary(p=>p.Key.AsString(),p=>p.Value.ToString()));
            Require(result["unbound_body_slots"].AsGodotArray().Count==0,"Unbound body attachment during "+motion);
            shot ??= NGame.Instance!.GetViewport().GetTexture().GetImage();
        }
        using(shot){shot!.SavePng(Path.Combine(Output,Character+"-motion-"+motion+".png"));}
        File.WriteAllText(Path.Combine(Output,Character+"-motion-"+motion+".json"),JsonSerializer.Serialize(trace));
        Require(minimum>0,"Stone armor vanished during "+motion+"; see motion trace.");
        return new{id="motion",motion,minimumSubmitted=minimum,frames=trace.Count};
    }
    private static async Task<CardModel> Card<T>(Player player) where T : CardModel
    {
        var card = CombatManager.Instance.DebugOnlyGetState()!.CreateCard(ModelDb.Card<T>(), player);
        player.PlayerCombatState!.Energy = 99;
        await CardPileCmd.Add(card, PileType.Hand); await Wait(.4);
        return card;
    }
    private static NHandCardHolder FindHolder(CardModel card) => Descendants(NCombatRoom.Instance!).OfType<NHandCardHolder>().Single(h => h.CardNode?.Model == card);
    private static IEnumerable<Node> Descendants(Node node) { foreach (var child in node.GetChildren()) { yield return child; foreach (var n in Descendants(child)) yield return n; } }
    private static async Task Until(Func<bool> condition, double timeout) { ulong end = Time.GetTicksMsec() + (ulong)(timeout*1000); while (!condition()) { if (Time.GetTicksMsec() > end) throw new TimeoutException("Combat not ready."); await Wait(.05); } }
    private static async Task Wait(double seconds) { var game = NGame.Instance!; ulong end = Time.GetTicksMsec() + (ulong)(seconds*1000); while (Time.GetTicksMsec() < end) await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static async Task Capture(string stage) { var game = NGame.Instance!; await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); using var img = game.GetViewport().GetTexture().GetImage(); img.SavePng(Path.Combine(Output, Character + "-" + stage + ".png")); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
