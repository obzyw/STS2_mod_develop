using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Models;

namespace SkinAnimationBridge;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    private static string poseSource=null!;
    private static readonly AsyncLocal<Node2D?> snakeScope=new();
    public static void Initialize()
    {
        string folder=Path.GetDirectoryName(typeof(Entry).Assembly.Location)!;
        poseSource=File.ReadAllText(Path.Combine(folder,"SilentPose.gd"));
        var rig=AccessTools.TypeByName("MoreSilentAnimations.CharacterRig");
        var pose=AccessTools.TypeByName("MoreSilentAnimations.NativePose");
        var harmony=new Harmony("obzyw.sts2.skin-animation-bridge");
        try
        {
        if(rig!=null&&pose!=null)
        {
            harmony.Patch(AccessTools.Constructor(rig,[typeof(Node2D)]),postfix:new HarmonyMethod(typeof(Entry),nameof(SilentRig)));
            harmony.Patch(AccessTools.DeclaredMethod(pose,"Compile",[typeof(string)]),postfix:new HarmonyMethod(typeof(Entry),nameof(SilentScript)));
            var snake=AccessTools.TypeByName("MoreSilentAnimations.SnakeVisual")!;
            harmony.Patch(AccessTools.DeclaredMethod(snake,"Compile"),postfix:new HarmonyMethod(typeof(Entry),nameof(SnakeScript)));
            harmony.Patch(AccessTools.DeclaredMethod(snake,"Begin"),prefix:new HarmonyMethod(typeof(Entry),nameof(BeginSnake)),postfix:new HarmonyMethod(typeof(Entry),nameof(EndSnake)),finalizer:new HarmonyMethod(typeof(Entry),nameof(EndSnake)));
            harmony.Patch(AccessTools.DeclaredMethod(rig,"Find"),postfix:new HarmonyMethod(typeof(Entry),nameof(SnakeRig)));
            harmony.Patch(AccessTools.Constructor(AccessTools.TypeByName("MoreSilentAnimations.SnakeGeometry"),[typeof(string)]),postfix:new HarmonyMethod(typeof(Entry),nameof(SnakeReference)));
        }
        DefectHeadBridge.Install(harmony);
        DefectHologramBridge.Install(harmony,folder);
        NecroPoseBridge.Install(harmony,folder);
        GD.Print("[SkinAnimationBridge] YU pose and head bridges loaded (development).");
        }
        catch(Exception error)
        {
            harmony.UnpatchAll(harmony.Id);
            GD.PushWarning("[SkinAnimationBridge] Disabled after initialization failure: "+error);
        }
    }

    private static bool Selected(Node2D body)=>body.HasMethod("get_skeleton_data_res")&&body.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUSilentThreeStages/",StringComparison.Ordinal);
    private static void BeginSnake(NCreature actor,out Node2D? __state){__state=snakeScope.Value;snakeScope.Value=Selected(actor.Body)?actor.Body:null;}
    private static void EndSnake(Node2D? __state)=>snakeScope.Value=__state;
    private static void SnakeRig(Node2D body,bool allowSkin,ref object? __result)
    {
        if(allowSkin||__result!=null||snakeScope.Value!=body||!Selected(body))return;
        __result=AccessTools.DeclaredMethod(AccessTools.TypeByName("MoreSilentAnimations.CharacterRig"),"ForPresentation").Invoke(null,[body]);
    }
    private static void SnakeScript(ref GDScript __result)
    {
        string source=__result.SourceCode;
        foreach(var (native,skin) in new[]{("hand_r","24"),("arm_lower_r","23"),("arm_upper_r","22")})
            source=source.Replace("skeleton.find_bone(\""+native+"\")","yu_snake_bone(\""+native+"\",\""+skin+"\")");
        var bank=AccessTools.TypeByName("MoreSilentAnimations.MotionBank")!;
        var content=AccessTools.TypeByName("MoreSilentAnimations.GameContent")!;
        var version=AccessTools.Property(content,"Version").GetValue(null)!;
        var motion=AccessTools.Method(bank,"Get").Invoke(null,[version])!;
        var hash=(string)AccessTools.Field(bank,"SkeletonHash").GetValue(motion)!;
        // Only the independent snake's timing/geometry template uses this identity.
        // The character guard and character mesh remain the YU rig.
        source=source.Replace("return skeleton.get_data().get_hash()","return \""+hash+"\" if source.get_skeleton_data_res().resource_path.begins_with(\"res://YUSilentThreeStages/\") else skeleton.get_data().get_hash()");
        source+="\nfunc yu_snake_bone(native_name, skin_name):\n    var yu = source.get_skeleton_data_res().resource_path.begins_with(\"res://YUSilentThreeStages/\")\n    return skeleton.find_bone(skin_name if yu else native_name)\n";
        var combined=new GDScript{SourceCode=source};
        if(combined.Reload()!=Error.Ok)throw new InvalidDataException("YU snake anchor script failed to compile.");
        __result=combined;
    }

    private static void SnakeReference(object __instance)
    {
        var body=snakeScope.Value;if(body==null)return;
        var skeleton=body.Call("get_skeleton").AsGodotObject();
        var hand=skeleton.Call("find_bone","24").AsGodotObject();
        double scale=Math.Abs(skeleton.Call("get_scale_x").AsDouble());
        var type=__instance.GetType();var point=type.GetNestedType("Point",BindingFlags.NonPublic)!;
        var origin=AccessTools.Constructor(point,[typeof(double),typeof(double)]).Invoke([hand.Call("get_world_x").AsDouble()/scale,-hand.Call("get_world_y").AsDouble()/scale]);
        AccessTools.Field(type,"_reference").SetValue(__instance,origin);
        AccessTools.Field(type,"_referenceAngle").SetValue(__instance,Math.Atan2(-hand.Call("get_c").AsDouble(),hand.Call("get_a").AsDouble()));
    }

    private static void SilentRig(object __instance,Node2D body)
    {
        var resource=body.Call("get_skeleton_data_res").As<Resource>();
        if(!resource.ResourcePath.StartsWith("res://YUSilentThreeStages/",StringComparison.Ordinal))return;
        // This opt-in is restricted to the inspected 98-bone YU rig. The author's
        // original guard remains in place for every other skin.
        var skeleton=body.Call("get_skeleton").AsGodotObject();
        if(skeleton.Call("get_bones").AsGodotArray().Count!=98)return;
        AccessTools.Field(__instance.GetType(),"<Adaptable>k__BackingField").SetValue(__instance,true);
        body.TreeExiting+=((IDisposable)__instance).Dispose;
    }

    private static void SilentScript(string name,ref GDScript __result)
    {
        if(name!="skin_pose")return;
        var source=__result.SourceCode;
        foreach(string method in new[]{"custom","finale_ghost","departure"})
        {
            string token="func "+method+"(";
            if(!source.Contains(token))throw new InvalidDataException("Silent skin adapter API changed: "+method);
            source=source.Replace(token,"func yu_original_"+method+"(");
        }
        var combined=new GDScript{SourceCode=source+"\n"+poseSource};
        if(combined.Reload()!=Error.Ok)throw new InvalidDataException("YU Silent pose script failed to compile.");
        __result=combined;
    }
}
