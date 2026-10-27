using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;

namespace SkinAnimationBridge;

internal static class NecroPoseBridge
{
    private static GDScript script=null!;
    private static readonly ConditionalWeakTable<object,Renderer> clips=new();
    private static readonly ConditionalWeakTable<Node2D,Renderer> owners=new();
    private static readonly ConditionalWeakTable<object,Node2D> readers=new();
    private static Type T(string n)=>AccessTools.TypeByName("MoreNecrobinderAnimations."+n)!;
    private static object V(object value,string name)=>AccessTools.Field(value.GetType(),name)?.GetValue(value)??AccessTools.Property(value.GetType(),name).GetValue(value)!;
    private static bool Selected(Node2D body)=>body.HasMethod("get_skeleton_data_res")&&body.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUNecrobinderThreeStages/");
    private static HarmonyMethod H(string n)=>new(typeof(NecroPoseBridge),n);
    internal static void Install(Harmony harmony,string folder)
    {
        if(T("ClipPlayer")==null)return;
        script=new GDScript{SourceCode=File.ReadAllText(Path.Combine(folder,"NecroPose.gd"))};
        if(script.Reload()!=Error.Ok)throw new InvalidDataException("Necro skin pose script failed to compile.");
        harmony.Patch(AccessTools.Method(T("VisualSession"),"Start"),transpiler:H(nameof(SessionGuard)));
        harmony.Patch(AccessTools.Constructor(T("ClipPlayer"),[T("ClipAssets"),typeof(Node2D),typeof(Node2D),typeof(Node),typeof(bool),typeof(Node)]),postfix:H(nameof(Constructed)));
        harmony.Patch(AccessTools.Method(T("ClipPlayer"),"Advance"),postfix:H(nameof(Advance)));
        harmony.Patch(AccessTools.Method(T("ClipPlayer"),"CancelBody"),postfix:H(nameof(Cancel)));
        harmony.Patch(AccessTools.Method(T("ClipPlayer"),"YieldBody"),postfix:H(nameof(Cancel)));
        harmony.Patch(AccessTools.Method(T("ClipPlayer"),"Dispose"),postfix:H(nameof(Disposed)));
        harmony.Patch(AccessTools.Method(T("ClipPlayer"),"Visible"),postfix:H(nameof(Visible)));
        harmony.Patch(AccessTools.PropertyGetter(T("ClipPlayer"),"BodyRendered"),postfix:H(nameof(Rendered)));
        harmony.Patch(AccessTools.Constructor(T("SpineValues"),[typeof(Node2D)]),postfix:H(nameof(ReaderCreated)));
        harmony.Patch(AccessTools.Method(T("SpineValues"),"Matrix"),prefix:H(nameof(Matrix)));
        harmony.Patch(AccessTools.PropertyGetter(T("SpineValues"),"TorsoOpacity"),prefix:H(nameof(TorsoOpacity)));
    }
    private static IEnumerable<CodeInstruction> SessionGuard(IEnumerable<CodeInstruction> instructions)
    {
        var original=AccessTools.Method(T("SpineCompatibility"),"Allows");
        foreach(var instruction in instructions)
        {
            if(instruction.Calls(original)){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(NecroPoseBridge),nameof(AllowSession));}
            yield return instruction;
        }
    }
    // This changes only the session entry gate. ClipPlayer retains its original
    // rejection of native body meshes; the skin renderer owns those frames.
    private static bool AllowSession(Node2D body,string actor)=>actor=="necrobinder"&&Selected(body)||(bool)AccessTools.Method(T("SpineCompatibility"),"Allows").Invoke(null,[body,actor])!;
    private static void Constructed(object __instance,Node2D hero,bool ownBody)
    {
        var data=V(V(__instance,"Assets"),"Data");
        if(!ownBody||!Selected(hero)||!(bool)V(data,"Body")||(string)V(data,"BodyActor")!="hero")return;
        clips.Add(__instance,new Renderer(__instance,hero));
    }
    private static void Advance(object __instance,float age){if(clips.TryGetValue(__instance,out var renderer))renderer.Step(age);}
    private static void Cancel(object __instance){if(clips.TryGetValue(__instance,out var renderer))renderer.Release();}
    private static void Disposed(object __instance){if(clips.TryGetValue(__instance,out var renderer)){renderer.Dispose();clips.Remove(__instance);}}
    private static void Visible(object __instance,bool visible){if(clips.TryGetValue(__instance,out var renderer))renderer.Visible=visible;}
    private static void Rendered(object __instance,ref bool __result){if(clips.TryGetValue(__instance,out var renderer))__result|=renderer.Owned&&renderer.Visible;}
    private static void ReaderCreated(object __instance,Node2D body){if(Selected(body))readers.Add(__instance,body);}
    private static bool Matrix(object __instance,string bone,ref Transform2D __result)
    {
        if(!readers.TryGetValue(__instance,out var body))return true;
        string? target=bone switch{"body_upper" or "torso_upper" or "chest"=>"33","body_lower" or "hips"=>"22","head"=>"2","hand_l"=>"19","hand_r"=>"14",_=>null};
        if(target==null)return true;
        var joint=body.Call("get_skeleton").AsGodotObject().Call("find_bone",target).AsGodotObject();
        __result=new Transform2D(new Vector2(joint.Call("get_a").AsSingle(),joint.Call("get_c").AsSingle()),new Vector2(joint.Call("get_b").AsSingle(),joint.Call("get_d").AsSingle()),new Vector2(joint.Call("get_world_x").AsSingle(),joint.Call("get_world_y").AsSingle()));
        return false;
    }
    private static bool TorsoOpacity(object __instance,ref float __result)
    {
        if(!readers.TryGetValue(__instance,out var body))return true;
        __result=body.Call("get_skeleton").AsGodotObject().Call("get_color").AsColor().A;return false;
    }
    private sealed class Renderer : IDisposable
    {
        private readonly object clip;
        private readonly Node2D body;
        private readonly Resource data;
        private RefCounted? pose;
        private Node2D? display;
        private Color saved;
        private bool canceled;
        internal bool Owned {get;private set;}
        internal bool Visible {get;set;}=true;
        internal Renderer(object clip,Node2D body){this.clip=clip;this.body=body;data=body.Call("get_skeleton_data_res").As<Resource>();}
        internal void Step(float age)
        {
            if(canceled||!GodotObject.IsInstanceValid(body)||body.Call("get_skeleton_data_res").As<Resource>()!=data){Release();return;}
            var recipe=V(V(clip,"Assets"),"Data");
            float end=Convert.ToSingle(V(recipe,"BodyEnd"));
            if((bool)V(clip,"_bodyCanceled")||age>=end){Release();return;}
            var permit=AccessTools.Field(clip.GetType(),"BodyPermit").GetValue(clip) as Func<bool>;
            if(permit!=null&&!permit()){Release();return;}
            if(!Owned)
            {
                if(owners.TryGetValue(body,out _)||!body.IsVisibleInTree()||body.Modulate.A*body.SelfModulate.A<=.001f)return;
                pose=(RefCounted)script.New().AsGodotObject();display=pose.Call("bind",body).As<Node2D>();
                saved=body.Modulate;body.Modulate=new Color(saved.R,saved.G,saved.B,0);owners.Add(body,this);Owned=true;
            }
            else if(body.Modulate.A>.001f){Release();return;}
            display!.Visible=Visible;display.Modulate=saved;display.SelfModulate=body.SelfModulate;
            var duration=Convert.ToSingle(AccessTools.Property(clip.GetType(),"Duration").GetValue(clip));
            pose!.Call("pose",(string)V(recipe,"Name"),Mathf.Clamp(age/Math.Max(Math.Min(duration,end),.01f),0,1),age);
        }
        internal void Release()
        {
            canceled=true;
            if(Owned)
            {
                owners.Remove(body);
                if(GodotObject.IsInstanceValid(body)&&body.Modulate==new Color(saved.R,saved.G,saved.B,0))body.Modulate=saved;
                Owned=false;
            }
            if(pose!=null){pose.Call("release");pose.Dispose();pose=null;display=null;}
        }
        public void Dispose()=>Release();
    }
}
