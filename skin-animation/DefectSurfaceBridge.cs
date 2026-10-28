using System.Collections;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;

namespace SkinAnimationBridge;

// Alpha masks come from the skin itself. Never render original character meshes.
internal static class DefectSurfaceBridge
{
    private static readonly ConditionalWeakTable<object,Surface> surfaces=new();
    private static GDScript script=null!;
    private static readonly ShaderMaterial alpha=new(){Shader=new Shader{Code="shader_type canvas_item; render_mode unshaded; void fragment(){COLOR=vec4(1.0,1.0,1.0,texture(TEXTURE,UV).a*COLOR.a);}"}};
    private static Type T(string name)=>AccessTools.TypeByName("MoreDefectAnimations."+name)!;
    private static object F(object obj,string name)=>AccessTools.Field(obj.GetType(),name)?.GetValue(obj)??AccessTools.Property(obj.GetType(),name).GetValue(obj)!;
    private static HarmonyMethod H(string name)=>new(typeof(DefectSurfaceBridge),name);
    internal static void Install(Harmony harmony,string folder)
    {
        if(T("CharacterSurfaceMaps")==null)return;
        script=new GDScript{SourceCode=File.ReadAllText(Path.Combine(folder,"HologramPose.gd"))};
        if(script.Reload()!=Error.Ok)throw new InvalidDataException("Skin surface snapshot script failed to compile.");
        harmony.Patch(AccessTools.Method(T("CharacterSurfaceMaps"),"Refresh"),prefix:H(nameof(Refresh)));
        harmony.Patch(AccessTools.Method(T("CharacterSurfaceMaps"),"Texture"),prefix:H(nameof(Texture)));
        harmony.Patch(AccessTools.Method(T("CharacterSurfaceMaps"),"Dispose"),postfix:H(nameof(Dispose)));
        DefectHeadBridge.AllowBinding(harmony,"SignalSession");
    }
    private static Surface? For(object map)
    {
        if(surfaces.TryGetValue(map,out var surface))return surface;
        var body=(Node2D)F(map,"_body");
        if(!body.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUDefectThreeStages/"))return null;
        surface=new Surface(map,body);surfaces.Add(map,surface);return surface;
    }
    private static bool Refresh(object __instance){var surface=For(__instance);if(surface==null)return true;surface.Refresh();return false;}
    private static bool Texture(object __instance,string name,ref Texture2D __result)
    {var surface=For(__instance);if(surface==null)return true;__result=surface.Texture(name);return false;}
    private static void Dispose(object __instance){if(surfaces.TryGetValue(__instance,out var surface)){surface.Dispose();surfaces.Remove(__instance);}}
    private sealed class Surface : IDisposable
    {
        private sealed record Mask(SubViewport View,RefCounted Reader,Polygon2D? Ice);
        private readonly object map;
        private readonly Node2D body;
        private readonly Node host;
        private readonly Dictionary<string,Mask> masks=new();
        private Rect2 rect;
        internal Surface(object map,Node2D body){this.map=map;this.body=body;host=(Node)F(map,"_host");}
        internal void Refresh()
        {
            rect=body.Call("get_skeleton").AsGodotObject().Call("get_bounds").AsRect2().Grow(24);
            AccessTools.Property(map.GetType(),"Rect").SetValue(map,rect);
            AccessTools.Property(map.GetType(),"Frame").SetValue(map,Transform2D.Identity);
            foreach(var pair in masks)Draw(pair.Key,pair.Value);
        }
        internal Texture2D Texture(string name)
        {
            if(!masks.TryGetValue(name,out var mask))
            {
                var view=new SubViewport{Name="YUSkinMask_"+name,Size=new Vector2I(256,256),TransparentBg=true,Disable3D=true,World2D=new World2D(),RenderTargetUpdateMode=SubViewport.UpdateMode.Disabled};
                host.AddChild(view);
                var reader=(RefCounted)script.New().AsGodotObject();reader.Call("bind",body,view).As<Node2D>().Name="YUSkinMaskSource";
                Polygon2D? ice=null;
                if(name=="ice"){ice=new Polygon2D{Material=alpha};view.AddChild(ice);}
                mask=new Mask(view,reader,ice);masks.Add(name,mask);Draw(name,mask);
            }
            return mask.View.GetTexture();
        }
        private void Draw(string name,Mask mask)
        {
            if(body.GetTree().Paused||!body.IsVisibleInTree()||body.Modulate.A*body.SelfModulate.A<.001f)return;
            var scale=new Vector2(256,256)/rect.Size;
            var transform=new Transform2D(0,scale,0,-rect.Position*scale);
            using var snapshot=mask.Reader.Call("capture").AsGodotArray();
            mask.Reader.Call("mask",snapshot,transform,name,alpha);
            if(mask.Ice!=null)
            {
                var ices=(IDictionary)AccessTools.Field(T("CharacterSurfaceMaps"),"Ices").GetValue(null)!;
                mask.Ice.Visible=ices.Contains(body);
                if(mask.Ice.Visible)
                {
                    var source=(Polygon2D)F(ices[body]!,"Ice");
                    mask.Ice.Texture=source.Texture;mask.Ice.Polygon=source.Polygon;mask.Ice.UV=source.UV;mask.Ice.Polygons=source.Polygons;
                    mask.Ice.Transform=transform;mask.Ice.Modulate=source.Modulate;mask.Ice.SelfModulate=source.SelfModulate;
                }
            }
            mask.View.RenderTargetUpdateMode=SubViewport.UpdateMode.Once;
        }
        public void Dispose(){foreach(var mask in masks.Values){mask.Reader.Call("release");mask.Reader.Dispose();}masks.Clear();}
    }
}
