using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace STS2CompatProbe;

/// <summary>Records actual frame cadence separately from GPU readbacks and inventories visible overlays.</summary>
internal static class RenderAudit
{
    internal static async Task Sample(NGame game,string output,string phase,double seconds)
    {
        var cadence=new List<double>();var images=new List<object>();
        ulong start=Time.GetTicksUsec(),last=start,next=start+150000;
        int index=0;
        while(Time.GetTicksUsec()-start<seconds*1000000)
        {
            await game.ToSignal(game.GetTree(),SceneTree.SignalName.ProcessFrame);
            ulong now=Time.GetTicksUsec();cadence.Add((now-last)/1000.0);last=now;
            // Idle phases measure FPS without readback stalls. Active phases inspect rendered pixels.
            if(!phase.StartsWith("idle") && now>=next)
            {
                next=now+200000;
                using var image=game.GetViewport().GetTexture().GetImage();
                double light=0;int black=0;
                for(int y=1;y<12;y++)for(int x=1;x<20;x++)
                {
                    var c=image.GetPixel(image.GetWidth()*x/20,image.GetHeight()*y/12);
                    double l=(c.R+c.G+c.B)/3.0;light+=l;if(l<.015)black++;
                }
                var overlays=Walk(game.GetTree().Root).OfType<CanvasItem>()
                    .Where(n=>n.IsVisibleInTree() && (n is ColorRect || n.Material is ShaderMaterial))
                    .Select(n=>new {path=n.GetPath().ToString(),alpha=n.Modulate.A,
                        rect=n is Control control?control.GetGlobalRect().ToString():null,
                        color=n is ColorRect color?color.Color.ToString():null,
                        shader=n.Material is ShaderMaterial mat?mat.Shader?.Code:null}).ToArray();
                string? png=null;
                if(index is 0 or 2 or 5 or 8 or 12 or 18 || black>180)
                {
                    png=phase+"-"+index+".png";image.SavePng(Path.Combine(output,png));
                }
                images.Add(new {ms=(now-start)/1000.0,luminance=light/209,blackFraction=black/209.0,png,overlays});
                index++;last=Time.GetTicksUsec();
            }
        }
        cadence.Sort();
        File.WriteAllText(Path.Combine(output,phase+".json"),JsonSerializer.Serialize(new {
            phase,maxFps=Engine.MaxFps,vsync=DisplayServer.WindowGetVsyncMode().ToString(),
            refresh=DisplayServer.ScreenGetRefreshRate(),reportedFps=Engine.GetFramesPerSecond(),
            frames=cadence.Count,p50ms=cadence.Count>0?cadence[cadence.Count/2]:0,
            p95ms=cadence.Count>0?cadence[(int)((cadence.Count-1)*.95)]:0,images
        },new JsonSerializerOptions{WriteIndented=true}));
        GD.Print("[RenderAudit] "+phase+" fps="+Engine.GetFramesPerSecond());
    }
    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach(var child in node.GetChildren())foreach(var nested in Walk(child))yield return nested;
    }
}
