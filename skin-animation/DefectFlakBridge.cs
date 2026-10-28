using Godot;
using HarmonyLib;

namespace SkinAnimationBridge;

internal static class DefectFlakBridge
{
    private static GDScript script=null!;
    private static Type T(string name)=>AccessTools.TypeByName("MoreDefectAnimations."+name)!;
    private static object F(object value,string name)=>AccessTools.Field(value.GetType(),name)?.GetValue(value)??AccessTools.Property(value.GetType(),name).GetValue(value)!;
    private static bool Selected(Node2D body)=>body.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUDefectThreeStages/");
    internal static void Install(Harmony harmony)
    {
        if(T("NativeFlak")==null)return;
        script=new GDScript{SourceCode="""
extends RefCounted
var shoulder
var torso
var direction
var skin = false
var rest_angle = 0.0
var reference_angle = 0.0
func bind_body(body):
    skin = body.get_skeleton_data_res().resource_path.begins_with("res://YUDefectThreeStages/")
    var skeleton = body.get_skeleton()
    shoulder = skeleton.find_bone("22" if skin else "arm_upper_l")
    torso = skeleton.find_bone("bone2" if skin else "torso")
    direction = skeleton.find_bone("bone2" if skin else "torso_upper")
    rest_angle = atan2(direction.get_c(),direction.get_a())
func read_pose():
    var axis = Vector2(direction.get_a(),direction.get_c())
    if skin: axis = Vector2.from_angle(atan2(axis.y,axis.x)-rest_angle+reference_angle)
    return PackedFloat32Array([shoulder.get_world_x(),shoulder.get_world_y(),torso.get_world_x(),torso.get_world_y(),axis.x,axis.y])
"""};
        if(script.Reload()!=Error.Ok)throw new InvalidDataException("Flak skin attachment script failed to compile.");
        DefectHeadBridge.AllowBinding(harmony,"FlakSession");
        DefectHeadBridge.AllowMethod(harmony,"FlakSession","CanAnimate");
        harmony.Patch(AccessTools.Constructor(T("NativeFlak"),[typeof(Node2D),T("FlakPlayback")]),prefix:new HarmonyMethod(typeof(DefectFlakBridge),nameof(Creating)),postfix:new HarmonyMethod(typeof(DefectFlakBridge),nameof(Created)));
    }
    private static void Creating(Node2D body){if(Selected(body))AccessTools.Field(T("NativeFlak"),"_script").SetValue(null,script);}
    private static void Created(object __instance,Node2D body)
    {
        if(!Selected(body))return;
        var profile=F(F(__instance,"Geometry"),"_profile");
        ((RefCounted)F(__instance,"_reader")).Set("reference_angle",Convert.ToSingle(F(F(profile,"Cannon"),"Angle")));
        AccessTools.Method(__instance.GetType(),"Refresh").Invoke(__instance,null);
    }
}
