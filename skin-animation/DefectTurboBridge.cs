using Godot;
using HarmonyLib;

namespace SkinAnimationBridge;

internal static class DefectTurboBridge
{
    private static GDScript script=null!;
    internal static void Install(Harmony harmony,string folder)
    {
        var type=AccessTools.TypeByName("MoreDefectAnimations.TurboNativeRig");
        if(type==null)return;
        script=new GDScript{SourceCode=File.ReadAllText(Path.Combine(folder,"TurboSkin.gd"))};
        if(script.Reload()!=Error.Ok)throw new InvalidDataException("Turbo skin accessory script failed to compile.");
        DefectHeadBridge.AllowBinding(harmony,"TurboSession");
        DefectHeadBridge.AllowStart(harmony,"TurboSession");
        harmony.Patch(AccessTools.Method(type,"Create"),prefix:new HarmonyMethod(typeof(DefectTurboBridge),nameof(Create)));
    }
    private static bool Create(Node2D body,ref RefCounted __result)
    {
        if(!body.HasMethod("get_skeleton_data_res")||!body.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUDefectThreeStages/"))return true;
        var helper=(Node2D)ClassDB.Instantiate("SpineSprite").AsGodotObject();
        var data=(Resource)ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();
        data.Call("set_atlas_res",ResourceLoader.Load<Resource>("res://animations/characters/defect/defect.atlas"));
        data.Call("set_skeleton_file_res",ResourceLoader.Load<Resource>("res://animations/characters/defect/defect.skel"));
        helper.Call("set_skeleton_data_res",data);helper.Call("set_update_mode",2);
        helper.Name="YUDefectTurboAccessories";
        body.AddChild(helper);helper.Hide();
        try
        {
            var native=(RefCounted)AccessTools.Method(AccessTools.TypeByName("MoreDefectAnimations.TurboNativeRig"),"Create").Invoke(null,[helper])!;
            var reader=(RefCounted)script.New().AsGodotObject();
            reader.Call("bind",body,helper,native);
            __result=reader;return false;
        }
        catch{helper.QueueFree();throw;}
    }
}
