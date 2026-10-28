using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SkinAnimationBridge;

// Use the author's weighted accessory interface with anchors on the skin bones.
internal static class IroncladAccessoryBridge
{
    private static readonly ConditionalWeakTable<object,Node2D> weighted=new();
    private static Type T(string name)=>AccessTools.TypeByName("ShieldOnly."+name)!;
    private static bool Selected(Node2D sprite)=>sprite.HasMethod("get_skeleton_data_res")&&sprite.Call("get_skeleton_data_res").As<Resource>().ResourcePath.StartsWith("res://YUIroncladThreeStages/");
    private static HarmonyMethod H(string name)=>new(typeof(IroncladAccessoryBridge),name);
    internal static void Install(Harmony harmony)
    {
        if(T("DemonHorns")==null)return;
        foreach(string name in new[]{"Observe","Refresh"})
            harmony.Patch(AccessTools.Method(T("DemonHorns"),name,[typeof(NCreature)]),transpiler:new HarmonyMethod(typeof(IroncladGuardBridge),"RetargetPermission"));
        foreach(string type in new[]{"NativeDemonHorns","NativeDemonWings"})
        {
            var constructor=T(type).GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single();
            harmony.Patch(constructor,prefix:H(type=="NativeDemonHorns"?nameof(Horns):nameof(Wings)),transpiler:H(nameof(Names)),postfix:type=="NativeDemonHorns"?H(nameof(HornSlots)):null);
        }
        var surface=T("NativeWeightedSurface");
        harmony.Patch(AccessTools.Constructor(surface,[typeof(Node2D),typeof(string),typeof(JsonElement)]),prefix:H(nameof(WeightedSlot)),postfix:H(nameof(WeightedCreated)));
        harmony.Patch(AccessTools.Method(surface,"Refresh"),prefix:H(nameof(WeightedRefresh)));
    }
    private static void Horns(Node2D sprite,ref string profile,ref JsonElement? mesh)
    {if(Selected(sprite)){profile="shrug";mesh=Mesh(sprite,true);}}
    private static void Wings(Node2D sprite,ref string profile,ref JsonElement? mesh)
    {if(Selected(sprite)){profile="shrug";mesh=Mesh(sprite,false);}}
    private static string Name(string name,Node2D sprite)=>!Selected(sprite)?name:name switch{"top mask"=>"7","bod"=>"13","bod_upper"=>"5",_=>name};
    private static IEnumerable<CodeInstruction> Names(IEnumerable<CodeInstruction> instructions)
    {
        foreach(var instruction in instructions)
        {
            yield return instruction;
            if(instruction.opcode==OpCodes.Ldstr&&instruction.operand is string name&&name is "top mask" or "bod" or "bod_upper")
            {
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(IroncladAccessoryBridge),nameof(Name)));
            }
        }
    }
    private static void WeightedSlot(Node2D sprite,ref string slot){slot=Name(slot,sprite);}
    private static void WeightedCreated(object __instance,Node2D sprite){if(Selected(sprite))weighted.Add(__instance,sprite);}
    private static bool WeightedRefresh(object __instance)=>!weighted.TryGetValue(__instance,out _);
    private static void HornSlots(object __instance,Node2D sprite)
    {
        if(!Selected(sprite))return;
        foreach(var slot in (Node2D[])AccessTools.Field(__instance.GetType(),"_slots").GetValue(__instance)!)slot.Set("slot_name","7");
    }
    private static JsonElement Mesh(Node2D sprite,bool head)
    {
        string folder=Path.Combine(Path.GetDirectoryName(T("DemonHorns").Assembly.Location)!,"spine");
        using var metadata=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,head?"demon-horns.spmeta":"demon-wings.spmeta")));
        var reference=new Dictionary<int,Vector2>();
        void Read(JsonElement part)
        {
            var ids=part.GetProperty("indices").EnumerateArray().Select(p=>p.GetInt32()).ToArray();
            var points=part.GetProperty("referenceTriangle").EnumerateArray().ToArray();
            for(int i=0;i<ids.Length;i++)reference[ids[i]]=new Vector2(points[i][0].GetSingle(),points[i][1].GetSingle());
        }
        if(head)foreach(var part in metadata.RootElement.GetProperty("parts").EnumerateArray())Read(part);else Read(metadata.RootElement);
        string boneName=head?"1":"5",slotName=head?"7":"13";
        var skeleton=sprite.Call("get_skeleton").AsGodotObject();
        var bone=skeleton.Call("find_bone",boneName).AsGodotObject();
        float Get(string name)=>bone.Call(name).AsSingle();
        var transform=new Transform2D(new Vector2(Get("get_a"),Get("get_c")),new Vector2(Get("get_b"),Get("get_d")),new Vector2(Get("get_world_x"),Get("get_world_y")));
        var inverse=transform.AffineInverse();
        var center=reference.Values.Aggregate(Vector2.Zero,(a,b)=>a+b)/reference.Count;
        var attachment=skeleton.Call("find_slot",slotName).AsGodotObject().Call("get_attachment").AsGodotObject().Call("get_attachment_name").AsString();
        var pointsOut=reference.Select(pair=>
        {
            var local=inverse*(transform.Origin+(pair.Value-center)*.9f+new Vector2(head?0:-35,head?-60:-65));
            return new{index=pair.Key,influences=new[]{new{bone=boneName,x=local.X,y=local.Y,weight=1f,offset=0}}};
        }).ToArray();
        return JsonSerializer.SerializeToElement(new{attachment,deformLength=2,points=pointsOut});
    }
    internal static void ReplaceSurface(NCreature actor,Node2D native,Node2D display,CanvasItem carrier,ShaderMaterial? gold)
    {
        var surfaces=(System.Collections.IEnumerable)AccessTools.Property(T("DemonHorns"),"CurrentSurfaces").GetValue(null)!;
        var previous=surfaces.Cast<object>().FirstOrDefault(s=>(Node2D)AccessTools.Field(s.GetType(),"Sprite").GetValue(s)! ==native);
        if(previous!=null)AccessTools.Method(T("DemonHorns"),"Unregister").Invoke(null,[previous]);
        var method=AccessTools.Method(T("DemonHorns"),"Register");
        method.Invoke(null,[actor,display,carrier,"shrug",gold,null,null,null,null]);
    }
}
