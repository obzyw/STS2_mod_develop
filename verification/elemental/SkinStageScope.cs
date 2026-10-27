using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace ElementalProbe;

internal sealed class SkinStageScope : IDisposable
{
    private readonly object settings;
    private readonly Dictionary<PropertyInfo,object?> saved;
    internal SkinStageScope(string character)
    {
        var type=AccessTools.TypeByName($"YU{character}ThreeStages.CharacterSettings")!;
        var current=AccessTools.Property(type,"Current").GetValue(null)!;
        settings=((IDictionary)AccessTools.Property(type,"Characters").GetValue(current)!)[character.ToLowerInvariant()]!;
        saved=settings.GetType().GetProperties().Where(p=>p.CanRead&&p.CanWrite).ToDictionary(p=>p,p=>p.GetValue(settings));
    }
    internal void Select(string stage)
    {
        AccessTools.Property(settings.GetType(),"Enabled").SetValue(settings,stage!="native");
        AccessTools.Property(settings.GetType(),"AdultMode").SetValue(settings,stage!="classic");
        AccessTools.Property(settings.GetType(),"FixedStage").SetValue(settings,stage=="hurt"?2:stage=="critical"?3:0);
    }
    public void Dispose(){foreach(var pair in saved)pair.Key.SetValue(settings,pair.Value);}
}
