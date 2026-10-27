using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using GArray=Godot.Collections.Array;

namespace SkinAnimationBridge;

internal static class DefectHologramBridge
{
    private static readonly AsyncLocal<int> scope=new();
    private static readonly ConditionalWeakTable<object,Node2D> maps=new();
    private static readonly ConditionalWeakTable<object,Capture> captures=new();
    private static GDScript script=null!;
    private static Type T(string n)=>AccessTools.TypeByName("MoreDefectAnimations."+n)!;
    private static object F(object obj,string n)=>AccessTools.Field(obj.GetType(),n).GetValue(obj)!;
    private static bool Selected(Node2D body)=>body.HasMethod("get_skeleton_data_res")&&body.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUDefectThreeStages/");
    private static HarmonyMethod H(string n)=>new(typeof(DefectHologramBridge),n);
    internal static void Install(Harmony harmony,string folder)
    {
        if(T("HologramBodySource")==null)return;
        script=new GDScript{SourceCode=File.ReadAllText(Path.Combine(folder,"HologramPose.gd"))};
        if(script.Reload()!=Error.Ok)throw new InvalidDataException("Hologram skin snapshot script failed to compile.");
        DefectHeadBridge.AllowBinding(harmony,"HologramSession");
        harmony.Patch(AccessTools.Method(T("HologramSession"),"RefreshBinding"),prefix:H(nameof(Begin)),postfix:H(nameof(End)),finalizer:H(nameof(End)));
        harmony.Patch(AccessTools.Constructor(T("CharacterSurfaceMaps"),[typeof(Node2D)]),postfix:H(nameof(MapCreated)));
        harmony.Patch(AccessTools.Method(T("CharacterSurfaceMaps"),"Refresh"),prefix:H(nameof(MapRefresh)));
        harmony.Patch(AccessTools.Constructor(T("HologramBodySource"),[typeof(Node2D),typeof(Node)]),postfix:H(nameof(SourceCreated)));
        harmony.Patch(AccessTools.Method(T("HologramBodySource"),"Capture"),prefix:H(nameof(Snapshot)));
        harmony.Patch(AccessTools.Method(T("HologramBodySource"),"Delayed"),prefix:H(nameof(Delayed)));
        harmony.Patch(AccessTools.Method(T("HologramBodySource"),"Dispose"),postfix:H(nameof(Disposed)));
    }
    private static void Begin(out int __state){__state=scope.Value;scope.Value++;}
    private static void End(int __state)=>scope.Value=__state;
    private static void MapCreated(object __instance,Node2D body){if(scope.Value>0&&Selected(body))maps.Add(__instance,body);}
    private static bool MapRefresh(object __instance)
    {
        var body=(Node2D)F(__instance,"_body");
        if(!maps.TryGetValue(__instance,out _)&&!(scope.Value>0&&Selected(body)))return true;
        var rect=body.Call("get_skeleton").AsGodotObject().Call("get_bounds").AsRect2();
        AccessTools.Property(__instance.GetType(),"Rect").SetValue(__instance,rect);
        AccessTools.Property(__instance.GetType(),"Frame").SetValue(__instance,Transform2D.Identity);
        return false;
    }
    private static void SourceCreated(object __instance,Node2D body){if(Selected(body))captures.Add(__instance,new Capture(__instance,body));}
    private static bool Snapshot(object __instance,double time)
    {
        if(!captures.TryGetValue(__instance,out var capture))return true;
        capture.Snapshot(time);return false;
    }
    private static bool Delayed(object __instance,double time,ref object? __result)
    {
        if(!captures.TryGetValue(__instance,out var capture))return true;
        __result=capture.Delayed(time);return false;
    }
    private static void Disposed(object __instance){if(captures.TryGetValue(__instance,out var capture)){capture.Dispose();captures.Remove(__instance);}}
    private sealed class Capture : IDisposable
    {
        private sealed record Frame(double Time,GArray Pose,object Sample,Rect2 Rect);
        private readonly object source;
        private readonly Node2D body;
        private readonly SubViewport view;
        private readonly RefCounted reader;
        private readonly Vector2 origin;
        private readonly List<Frame> frames=new();
        private double last=double.NegativeInfinity;
        private Frame? displayed;
        internal Capture(object source,Node2D body)
        {
            this.source=source;this.body=body;view=(SubViewport)F(source,"View");
            reader=(RefCounted)script.New().AsGodotObject();reader.Call("bind",body,view);
            origin=(Vector2)AccessTools.Field(T("HologramBodySource"),"SourceOrigin").GetValue(null)!;
        }
        internal void Snapshot(double time)
        {
            if(time-last<1.0/60||!body.IsVisibleInTree()||body.Modulate.A*body.SelfModulate.A<.001f||body.GetTree().Paused)return;
            var pose=reader.Call("capture").AsGodotArray();
            var bounds=body.Call("get_skeleton").AsGodotObject().Call("get_bounds").AsRect2();
            var content=new Rect2(bounds.Position*.36469603f+origin,bounds.Size*.36469603f);
            var rect=content.Grow(28).Merge(new Rect2(540,245,108,810).Grow(20));
            rect=new Rect2(new Vector2(Mathf.Floor(rect.Position.X/16)*16,Mathf.Floor(rect.Position.Y/16)*16),rect.Size+Vector2.One*32);
            var sample=Activator.CreateInstance(T("HologramBodySource").GetNestedType("Sample",BindingFlags.NonPublic)!,nonPublic:true)!;
            void Set(string n,object value)=>AccessTools.Field(sample.GetType(),n).SetValue(sample,value);
            Set("Time",time);Set("SourceRect",rect);Set("ContentRect",content);
            Set("Points",Array.Empty<Vector2[]>());Set("Meshes",Array.CreateInstance(T("ModdedAssets").GetNestedType("Mesh",BindingFlags.NonPublic)!,0));
            Set("Alphas",Array.Empty<float>());Set("Order",Array.Empty<int>());
            frames.Add(new Frame(time,pose,sample,rect));last=time;
            if(frames.Count>18){frames[0].Pose.Dispose();frames.RemoveAt(0);}
        }
        internal object? Delayed(double time)
        {
            if(frames.Count==0)return null;
            var frame=frames.LastOrDefault(f=>f.Time<=time-.12)??frames[0];
            if(displayed==frame)return frame.Sample;
            view.Size=new Vector2I(Math.Max(64,(int)Math.Ceiling(frame.Rect.Size.X*.5f/64)*64),Math.Max(64,(int)Math.Ceiling(frame.Rect.Size.Y*.5f/64)*64));
            reader.Call("show_pose",frame.Pose,new Transform2D(0,Vector2.One*.18234801f,0,(origin-frame.Rect.Position)*.5f));
            view.RenderTargetUpdateMode=SubViewport.UpdateMode.Once;
            var revision=AccessTools.Property(source.GetType(),"Revision");revision.SetValue(source,(int)revision.GetValue(source)!+1);
            displayed=frame;return frame.Sample;
        }
        public void Dispose(){foreach(var frame in frames)frame.Pose.Dispose();frames.Clear();reader.Call("release");reader.Dispose();}
    }
}
