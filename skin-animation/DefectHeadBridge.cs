using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;

namespace SkinAnimationBridge;

// First Defect component: retain the author's ice texture, UVs, weights,
// particles and clock, but fit the accessory to the skin's own moving head.
internal static class DefectHeadBridge
{
    private static readonly AsyncLocal<int> scope=new();
    private static readonly ConditionalWeakTable<object,Node2D> guards=new();
    private static readonly ConditionalWeakTable<object,Surface> surfaces=new();
    private static readonly ConditionalWeakTable<Node2D,Dictionary<string,float>> restAngles=new();
    private static readonly Dictionary<string,Transform2D> nativeRest=new();
    private static readonly ConditionalWeakTable<object,Projection> projections=new();
    private static readonly ConditionalWeakTable<object,FaceSurface> faceSurfaces=new();
    private static readonly Dictionary<string,string> bones=new()
    {
        ["head"]="7",["head_pipe_attach"]="7",["torso_upper"]="bone2",["hips"]="20",["foot_l"]="14",["foot_r"]="21"
    };
    private static Type T(string name)=>AccessTools.TypeByName("MoreDefectAnimations."+name)!;
    private static object F(object value,string name)=>AccessTools.Field(value.GetType(),name)?.GetValue(value)??AccessTools.Property(value.GetType(),name).GetValue(value)!;
    private static float P(object value,string name)=>Convert.ToSingle(AccessTools.Property(value.GetType(),name).GetValue(value));
    private static bool Selected(Node2D body)=>body.HasMethod("get_skeleton_data_res")&&body.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUDefectThreeStages/");
    internal static void Install(Harmony harmony)
    {
        if(T("SpineGuard")==null)return;
        harmony.Patch(AccessTools.Method(T("CoolheadedSession"),"RefreshBinding"),prefix:H(nameof(Begin)),postfix:H(nameof(End)),finalizer:H(nameof(End)));
        harmony.Patch(AccessTools.Method(T("ChargeBatterySession"),"RefreshBinding"),prefix:H(nameof(Begin)),postfix:H(nameof(End)),finalizer:H(nameof(End)));
        harmony.Patch(AccessTools.Method(T("BiasedCognitionSession"),"RefreshBinding"),prefix:H(nameof(Begin)),postfix:H(nameof(End)),finalizer:H(nameof(End)));
        harmony.Patch(AccessTools.Method(T("HotfixSession"),"RefreshBinding"),prefix:H(nameof(Begin)),postfix:H(nameof(End)),finalizer:H(nameof(End)));
        harmony.Patch(AccessTools.Method(T("SpineGuard"),"Get"),prefix:H(nameof(GuardGet)));
        harmony.Patch(AccessTools.Constructor(T("SpineGuard"),[typeof(Node2D),T("CoolheadedAssets")]),postfix:H(nameof(GuardConstructed)));
        harmony.Patch(AccessTools.Method(T("SpineGuard"),"AllowsDisplay"),prefix:H(nameof(Display)));
        harmony.Patch(AccessTools.Constructor(T("WeightedHeadSurface"),[typeof(Node2D),T("CoolheadedAssets"),T("CoolheadedAssets").GetNestedType("HeadProfile",BindingFlags.NonPublic)!,typeof(bool)]),postfix:H(nameof(SurfaceConstructed)));
        harmony.Patch(AccessTools.Method(T("WeightedHeadSurface"),"Refresh"),prefix:H(nameof(Refresh)));
        harmony.Patch(AccessTools.Method(T("HeadLayers"),"Attach"),postfix:H(nameof(HeadLayer)));
        harmony.Patch(AccessTools.Method(T("NativeSpineValues"),"Matrix"),prefix:H(nameof(Matrix)));
        harmony.Patch(AccessTools.Constructor(T("NativeChargeBattery"),[typeof(Node2D),typeof(CanvasItem)]),postfix:H(nameof(ChargeLayer)));
        harmony.Patch(AccessTools.Constructor(T("ProjectionRig"),[typeof(Node2D),T("BiasedCognitionAssets").GetNestedType("Profile",BindingFlags.NonPublic)!]),postfix:H(nameof(ProjectionCreated)));
        harmony.Patch(AccessTools.Method(T("ProjectionRig"),"Refresh"),prefix:H(nameof(ProjectionRefresh)));
        var surface=T("HotfixRig").GetNestedType("Surface",BindingFlags.NonPublic)!;
        harmony.Patch(AccessTools.Constructor(surface,[T("NativeSpineValues"),T("HotfixAssets").GetNestedType("Mesh",BindingFlags.NonPublic)!,typeof(string[])]),postfix:H(nameof(FaceCreated)));
        harmony.Patch(AccessTools.Method(surface,"Read"),prefix:H(nameof(FaceRead)));
    }
    private static HarmonyMethod H(string name)=>new(typeof(DefectHeadBridge),name);
    internal static void AllowBinding(Harmony harmony,string session)=>harmony.Patch(AccessTools.Method(T(session),"RefreshBinding"),prefix:H(nameof(Begin)),postfix:H(nameof(End)),finalizer:H(nameof(End)));
    private static void Begin(out int __state){__state=scope.Value;scope.Value++;}
    private static void End(int __state)=>scope.Value=__state;
    private static bool GuardGet(Node2D sprite,ref object? __result)
    {
        if(!Selected(sprite))return true;
        if(scope.Value>0)
        {
            var table=AccessTools.Field(T("SpineGuard"),"Bodies").GetValue(null)!;
            object?[] args=[sprite,null];
            if((bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table,args)!&&!(bool)F(args[1]!,"<Compatible>k__BackingField"))
                table.GetType().GetMethod("Remove")!.Invoke(table,[sprite]);
            return true;
        }
        __result=null;return false;
    }
    private static void GuardConstructed(object __instance,Node2D sprite)
    {
        if(scope.Value==0||!Selected(sprite))return;
        int bones=sprite.Call("get_skeleton").AsGodotObject().Call("get_bones").AsGodotArray().Count;
        if(bones is not (88 or 89))return;
        AccessTools.Field(__instance.GetType(),"<Compatible>k__BackingField").SetValue(__instance,true);
        guards.Add(__instance,sprite);
    }
    private static bool Display(object __instance,ref bool __result)
    {
        if(!guards.TryGetValue(__instance,out var body))return true;
        __result=GodotObject.IsInstanceValid(body)&&body.IsVisibleInTree()&&body.Modulate.A*body.SelfModulate.A>.001f;
        return false;
    }
    private static void HeadLayer(Node2D sprite,ref Node2D __result)
    {
        if(Selected(sprite))__result.Set("slot_name","11");
    }
    private static void ChargeLayer(object __instance,Node2D sprite)
    {
        if(Selected(sprite))((Node2D)F(__instance,"_clampSlot")).Set("slot_name","11");
    }
    private static bool Matrix(object __instance,string name,ref Transform2D __result)
    {
        var body=(Node2D)F(__instance,"_body");
        if(!Selected(body)||!bones.TryGetValue(name,out var target)||!nativeRest.TryGetValue(name,out var reference))return true;
        var bone=body.Call("get_skeleton").AsGodotObject().Call("find_bone",target).AsGodotObject();
        var current=BoneMatrix(bone);
        var angles=restAngles.GetOrCreateValue(body);
        var key=body.Call("get_skeleton_data_res").As<Resource>().ResourcePath+":"+name;
        if(!angles.TryGetValue(key,out var angle))angles[key]=angle=current.X.Angle();
        __result=new Transform2D(reference.X.Rotated(current.X.Angle()-angle),reference.Y.Rotated(current.X.Angle()-angle),current.Origin);
        return false;
    }
    private static void ProjectionCreated(object __instance,Node2D sprite)
    {
        if(Selected(sprite))projections.Add(__instance,new Projection(__instance,sprite));
    }
    private static bool ProjectionRefresh(object __instance,bool scan)
    {
        if(!projections.TryGetValue(__instance,out var projection))return true;
        projection.Update(scan);return false;
    }
    private static void FaceCreated(object __instance,object values,object mesh,string[] names)
    {
        var body=(Node2D)F(values,"_body");
        if(Selected(body))faceSurfaces.Add(__instance,new FaceSurface(__instance,body,mesh,names));
    }
    private static bool FaceRead(object __instance)
    {
        if(!faceSurfaces.TryGetValue(__instance,out var surface))return true;
        surface.Update();return false;
    }
    private sealed class FaceSurface
    {
        private readonly GodotObject head;
        private readonly Vector2[] local,points;
        private readonly Vector2 origin;
        internal FaceSurface(object surface,Node2D body,object mesh,string[] names)
        {
            points=(Vector2[])F(surface,"Points");var rows=(Array)F(mesh,"Points");
            var scene=ResourceLoader.Load<PackedScene>("res://scenes/creature_visuals/defect.tscn").Instantiate();
            var source=Descendants(scene).OfType<Node2D>().First(n=>n.HasMethod("get_skeleton_data_res"));
            var native=(Node2D)ClassDB.Instantiate("SpineSprite").AsGodotObject();
            native.Set("skeleton_data_res",source.Call("get_skeleton_data_res"));native.Set("update_mode",2);native.Hide();body.GetTree().Root.AddChild(native);
            try
            {
                native.Call("get_skeleton").AsGodotObject().Call("set_to_setup_pose");native.Call("update_skeleton",0.0);
                var reader=AccessTools.Method(T("NativeSpineValues"),"Get").Invoke(null,[native])!;
                var matrix=AccessTools.Method(T("NativeSpineValues"),"Matrix");
                foreach(var name in bones.Keys.Concat(names).Distinct())nativeRest[name]=(Transform2D)matrix.Invoke(reader,[name])!;
                var template=new Vector2[points.Length];
                for(int i=0;i<template.Length;i++)
                    foreach(var influence in (Array)rows.GetValue(i)!)
                        template[i]+=nativeRest[(string)F(influence,"Bone")]*new Vector2(P(influence,"X"),P(influence,"Y"))*P(influence,"WeightValue");
                var min=new Vector2(template.Min(v=>v.X),template.Min(v=>v.Y));var max=new Vector2(template.Max(v=>v.X),template.Max(v=>v.Y));
                bool eye=((string)F(mesh,"Slot")).Contains("eye",StringComparison.OrdinalIgnoreCase);
                var rect=eye?new Rect2(0,-70,160,95):new Rect2(-210,-220,410,340);
                head=body.Call("get_skeleton").AsGodotObject().Call("find_bone","7").AsGodotObject();
                var rest=BoneMatrix(head);var inverse=rest.AffineInverse();
                local=template.Select(v=>inverse*(rest.Origin+rect.Position+(v-min)/(max-min)*rect.Size)).ToArray();
                origin=(Vector2)AccessTools.Field(T("ChargeBatteryAssets"),"Origin").GetValue(null)!;
            }
            finally{native.Free();scene.Free();}
        }
        internal void Update(){var matrix=BoneMatrix(head);for(int i=0;i<points.Length;i++)points[i]=(matrix*local[i])*.3f+origin;}
    }
    private sealed class Projection
    {
        private readonly object rig;
        private readonly GodotObject head,shoulder;
        private readonly float headAngle,shoulderAngle;
        private readonly Array targets;
        private readonly ConstructorInfo pose;
        private readonly Vector2 origin;
        internal Projection(object rig,Node2D body)
        {
            this.rig=rig;var skeleton=body.Call("get_skeleton").AsGodotObject();
            head=skeleton.Call("find_bone","7").AsGodotObject();shoulder=skeleton.Call("find_bone","22").AsGodotObject();
            headAngle=BoneMatrix(head).X.Angle();shoulderAngle=BoneMatrix(shoulder).X.Angle();
            targets=(Array)F(rig,"Targets");pose=AccessTools.Constructor(T("ProjectionPose"),[typeof(Vector2),typeof(float)]);
            origin=(Vector2)AccessTools.Field(T("BiasedCognitionAssets"),"Origin").GetValue(null)!;
        }
        internal void Update(bool scan)
        {
            var h=BoneMatrix(head);var s=BoneMatrix(shoulder);
            var headTurn=h.X.Angle()-headAngle;
            var points=new[]{h.Origin+new Vector2(110,-15).Rotated(headTurn),h.Origin+new Vector2(-130,5).Rotated(headTurn),s.Origin};
            for(int i=0;i<3;i++)targets.SetValue(pose.Invoke([points[i]*.3f+origin,i<2?headTurn:s.X.Angle()-shoulderAngle]),i);
            if(scan)AccessTools.Property(rig.GetType(),"Emitter").SetValue(rig,(h.Origin+new Vector2(20,20).Rotated(headTurn))*.3f+origin);
        }
    }
    private static void SurfaceConstructed(object __instance,Node2D sprite)
    {
        if(scope.Value>0&&Selected(sprite))surfaces.Add(__instance,new Surface(__instance,sprite));
    }
    private static bool Refresh(object __instance)
    {
        if(!surfaces.TryGetValue(__instance,out var surface))return true;
        surface.Update();return false;
    }
    private static Transform2D BoneMatrix(GodotObject bone)=>new(
        new Vector2(bone.Call("get_a").AsSingle(),bone.Call("get_c").AsSingle()),
        new Vector2(bone.Call("get_b").AsSingle(),bone.Call("get_d").AsSingle()),
        new Vector2(bone.Call("get_world_x").AsSingle(),bone.Call("get_world_y").AsSingle()));
    private sealed class Surface
    {
        private readonly GodotObject headBone;
        private readonly Vector2[] local;
        private readonly Vector2[] head;
        private readonly Vector2[] ice;
        private readonly float[][] weights;
        internal Surface(object instance,Node2D body)
        {
            head=(Vector2[])F(instance,"Head");ice=(Vector2[])F(instance,"Ice");
            weights=(float[][])F(F(instance,"_assets"),"Weights");
            var profile=F(instance,"_profile");
            var names=(string[])F(profile,"Bones");var points=(Array)F(profile,"Points");
            using var scene=ResourceLoader.Load<PackedScene>("res://scenes/creature_visuals/defect.tscn").Instantiate();
            var source=Descendants(scene).OfType<Node2D>().First(n=>n.HasMethod("get_skeleton_data_res"));
            var native=(Node2D)ClassDB.Instantiate("SpineSprite").AsGodotObject();
            native.Set("skeleton_data_res",source.Call("get_skeleton_data_res"));native.Set("update_mode",2);native.Hide();
            body.GetTree().Root.AddChild(native);
            try
            {
                native.Call("get_skeleton").AsGodotObject().Call("set_to_setup_pose");native.Call("update_skeleton",0.0);
                var reader=AccessTools.Method(T("NativeSpineValues"),"Get").Invoke(null,[native])!;
                var matrix=AccessTools.Method(T("NativeSpineValues"),"Matrix");
                foreach(var name in bones.Keys)nativeRest[name]=(Transform2D)matrix.Invoke(reader,[name])!;
                var template=new Vector2[head.Length];
                for(int i=0;i<template.Length;i++)
                    foreach(var influence in (Array)points.GetValue(i)!)
                        template[i]+=(Transform2D)matrix.Invoke(reader,[names[(int)P(influence,"Bone")]])!*new Vector2(P(influence,"X"),P(influence,"Y"))*P(influence,"Weight");
                var min=new Vector2(template.Min(v=>v.X),template.Min(v=>v.Y));
                var max=new Vector2(template.Max(v=>v.X),template.Max(v=>v.Y));
                headBone=body.Call("get_skeleton").AsGodotObject().Call("find_bone","7").AsGodotObject();
                var rest=BoneMatrix(headBone);var inverse=rest.AffineInverse();
                local=template.Select(v=>inverse*(rest.Origin+new Vector2(Mathf.Lerp(-210,200,(v.X-min.X)/(max.X-min.X)),Mathf.Lerp(-220,120,(v.Y-min.Y)/(max.Y-min.Y))))).ToArray();
            }
            finally{native.Free();}
        }
        internal void Update()
        {
            var matrix=BoneMatrix(headBone);
            for(int i=0;i<head.Length;i++)head[i]=matrix*local[i];
            for(int i=0;i<ice.Length;i++)
            {
                ice[i]=Vector2.Zero;
                for(int j=0;j<head.Length;j++)ice[i]+=head[j]*weights[i][j];
            }
        }
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach(var child in root.GetChildren()){yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
}
