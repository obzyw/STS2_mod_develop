using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SkinAnimationBridge;

internal static class IroncladGuardBridge
{
    private static GDScript script=null!;
    private static readonly ConditionalWeakTable<object,Guard> guards=new();
    private static Type T(string name)=>AccessTools.TypeByName("ShieldOnly."+name)!;
    private static object F(object obj,string name)=>AccessTools.Field(obj.GetType(),name)?.GetValue(obj)??AccessTools.Property(obj.GetType(),name).GetValue(obj)!;
    private static HarmonyMethod H(string name)=>new(typeof(IroncladGuardBridge),name);
    private static bool Selected(NCreature node)=>node.Body.HasMethod("get_skeleton_data_res")&&node.Body.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUIroncladThreeStages/");
    internal static void Install(Harmony harmony,string folder)
    {
        if(T("NativeShieldPresentation")==null)return;
        script=new GDScript{SourceCode=File.ReadAllText(Path.Combine(folder,"IroncladPose.gd"))};
        if(script.Reload()!=Error.Ok)throw new InvalidDataException("Ironclad skin guard script failed to compile.");
        // Only defensive sessions with a skin renderer may cross the body guard.
        foreach(var method in T("TurnSession").GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly)
            .Append(AccessTools.Method(T("NativeShieldPresentation"),"CanUse")))
            if(method.GetMethodBody()!=null)harmony.Patch(method,transpiler:H(nameof(RetargetPermission)));
        harmony.Patch(AccessTools.Constructor(T("NativeShieldPresentation"),[typeof(NCreature),typeof(bool)]),postfix:H(nameof(ShieldCreated)));
        harmony.Patch(AccessTools.Method(T("NativeShieldPresentation"),"ApplyPose"),postfix:H(nameof(ShieldPose)));
        harmony.Patch(AccessTools.Constructor(T("NativeImperviousPresentation"),[typeof(NCreature)]),postfix:H(nameof(ImperviousCreated)));
        harmony.Patch(AccessTools.Method(T("NativeImperviousPresentation"),"Pose"),postfix:H(nameof(ImperviousPose)));
        harmony.Patch(AccessTools.Method(T("NativeImperviousPresentation"),"Dispose"),postfix:H(nameof(Disposed)));
    }
    private static IEnumerable<CodeInstruction> RetargetPermission(IEnumerable<CodeInstruction> instructions)
    {
        var original=AccessTools.Method(T("CharacterSkinCompatibility"),"CanAnimateBody");
        foreach(var instruction in instructions)
        {
            if(instruction.Calls(original)){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(IroncladGuardBridge),nameof(Allowed));}
            yield return instruction;
        }
    }
    private static bool Allowed(NCreature? node)=>node!=null&&Selected(node)||(bool)AccessTools.Method(T("CharacterSkinCompatibility"),"CanAnimateBody").Invoke(null,[node])!;
    private static void ShieldCreated(object __instance,NCreature node)
    {
        if(!Selected(node))return;
        var guard=new Guard(__instance,node.Body,(Node2D)F(__instance,"Carrier"),(Node2D)F(__instance,"_sprite"),false,null);
        guards.Add(__instance,guard);guard.Pose("guard-raise",0,0);
        IroncladAccessoryBridge.ReplaceSurface(node,(Node2D)F(__instance,"_sprite"),guard.Display,(CanvasItem)F(__instance,"Carrier"),null);
    }
    private static void ShieldPose(object __instance,string name,float seconds,float breath)
    {if(guards.TryGetValue(__instance,out var guard))guard.Pose(name,seconds,breath);}
    private static void ImperviousCreated(object __instance,NCreature node)
    {
        if(!Selected(node))return;
        var guard=new Guard(__instance,node.Body,(Node2D)F(__instance,"Carrier"),(Node2D)F(__instance,"_sprite"),true,(ShaderMaterial)F(__instance,"_bodyMaterial"));
        guards.Add(__instance,guard);guard.Pose("impervious",0,0);
        IroncladAccessoryBridge.ReplaceSurface(node,(Node2D)F(__instance,"_sprite"),guard.Display,(CanvasItem)F(__instance,"Carrier"),(ShaderMaterial)F(__instance,"_bodyMaterial"));
    }
    private static void ImperviousPose(object __instance,float sourceTime)
    {if(guards.TryGetValue(__instance,out var guard))guard.Pose("impervious",sourceTime,0);}
    private static void Disposed(object __instance)
    {if(guards.TryGetValue(__instance,out var guard)){guard.Dispose();guards.Remove(__instance);}}
    private sealed class Guard : IDisposable
    {
        private readonly RefCounted reader;
        private readonly Node2D native;
        private readonly Node2D display;
        private readonly Resource data;
        private readonly Node2D body;
        private readonly bool impervious;
        private readonly ShaderMaterial? material;
        private readonly object owner;
        private Rect2? canvas;
        private bool disposed;
        internal Node2D Display=>display;
        internal Guard(object owner,Node2D body,Node2D carrier,Node2D native,bool impervious,ShaderMaterial? material)
        {
            this.owner=owner;
            this.body=body;this.native=native;this.impervious=impervious;this.material=material;
            data=body.Call("get_skeleton_data_res").As<Resource>();
            reader=(RefCounted)script.New().AsGodotObject();
            display=(Node2D)reader.Call("bind",body,impervious?native.GetParent():carrier).AsGodotObject();
            display.SetMeta("guard_carrier",carrier);
            if(material!=null)display.SetMeta("guard_gold",material);
            if(impervious){display.Transform=native.Transform;native.Hide();}
            else carrier.MoveChild(display,0);
            carrier.TreeExiting+=Dispose;
        }
        internal void Pose(string kind,float time,float breath)
        {
            if(disposed)return;
            if(!GodotObject.IsInstanceValid(body)||body.Call("get_skeleton_data_res").As<Resource>()!=data){display.Hide();return;}
            reader.Call("pose",kind,time,breath);
            if(impervious)
            {
                native.Hide();reader.Call("gold",material!);FitCanvas();
                ((Node2D)F(owner,"Carrier")).Modulate=body.Modulate*body.SelfModulate;
            }
            else{display.Modulate=body.SelfModulate;reader.Call("align_shield",native);}
        }
        private void FitCanvas()
        {
            var bounds=display.Call("get_skeleton").AsGodotObject().Call("get_bounds").AsRect2().Grow(120);
            canvas=canvas.HasValue?canvas.Value.Merge(bounds):bounds;
            var rect=canvas.Value;
            var view=(SubViewport)native.GetParent();
            view.Size=new Vector2I((int)Math.Ceiling(rect.Size.X*.75f),(int)Math.Ceiling(rect.Size.Y*.75f));
            display.Transform=new Transform2D(0,Vector2.One*.75f,0,-rect.Position*.75f);
            var composite=(Sprite2D)F(owner,"_composite");
            composite.Position=rect.Position;
            AccessTools.Field(owner.GetType(),"_compositeOrigin").SetValue(owner,rect.Position);
            var carrier=(Node2D)F(owner,"Carrier");
            foreach(var blur in carrier.GetChildren().OfType<SubViewport>().Where(v=>v!=view))
            {
                blur.Size=new Vector2I((view.Size.X+1)/2,(view.Size.Y+1)/2);
                foreach(var blit in blur.GetChildren().OfType<Sprite2D>())blit.Scale=blur.Name=="ImperviousBlurHorizontal"?new Vector2(.5f,.5f):Vector2.One;
            }
            var horizontal=new Vector2(11.145511f/view.Size.X,0);
            var vertical=new Vector2(0,11.145511f/view.Size.Y);
            AccessTools.Field(owner.GetType(),"_horizontalStep").SetValue(owner,horizontal);
            AccessTools.Field(owner.GetType(),"_verticalStep").SetValue(owner,vertical);
            ((ShaderMaterial)F(owner,"_horizontalBlur")).SetShaderParameter("blur_step",horizontal);
            ((ShaderMaterial)F(owner,"_verticalBlur")).SetShaderParameter("blur_step",vertical);
        }
        public void Dispose(){if(disposed)return;disposed=true;reader.Call("release");reader.Dispose();}
    }
}
