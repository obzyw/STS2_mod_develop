using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace CorruptionWarriorVfx;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    private static Texture2D? body;
    private static Image generated = null!;
    public static int Replacements { get; private set; }

    public static void Initialize()
    {
        var assets = AccessTools.TypeByName("IroncladFx.Assets")
            ?? throw new InvalidOperationException("Enable IroncladFX before CorruptionWarriorVfx.");
        var method = AccessTools.DeclaredMethod(assets, "Texture", [typeof(string)])
            ?? throw new MissingMethodException("IroncladFX texture API changed.");
        string file = Path.Combine(Path.GetDirectoryName(typeof(Entry).Assembly.Location)!,"assets","corruption-warrior-body-v2.png");
        generated = Image.LoadFromFile(file);
        if (generated.IsEmpty()) throw new InvalidDataException("Corruption warrior artwork could not be loaded.");
        // Runtime texture import into the author's existing puppet coordinate system.
        // The generated source PNG is preserved, and the author files are never modified.
        generated.Resize(1001,1430,Image.Interpolation.Lanczos);
        generated.Convert(Image.Format.Rgba8);
        new Harmony("obzyw.sts2.corruption-warrior-vfx").Patch(method,
            postfix:new HarmonyMethod(typeof(Entry),nameof(Texture)));
        GD.Print("[CorruptionWarriorVfx] Corruption body now uses the masked Ironclad, without a beard.");
    }

    private static void Texture(string path,ref Texture2D? __result)
    {
        if(path.Replace('\\','/')!="corruption/rig2/body.png" || __result==null) return;
        body ??= ImportHead(__result);
        __result=body;
        Replacements++;
    }

    private static Texture2D ImportHead(Texture2D original)
    {
        using var image=original.GetImage();
        image.Convert(Image.Format.Rgba8);
        if(image.GetWidth()!=1001||image.GetHeight()!=1430)
            throw new InvalidDataException("IroncladFX corruption body layout changed; recheck head alignment.");
        byte[] pixels=image.GetData(), replacement=generated.GetData();
        // Preserve the author's torso, hanging arm and cloth pixels. Only the
        // head/hair/hood and old beard footprint receive the new identity art.
        for(int y=0;y<800;y++) for(int x=260;x<980;x++)
        {
            if(y<2) continue;
            float weight=MathF.Max(Rect(x,y,275,0,970,510),Rect(x,y,565,170,890,790));
            if(weight<=0) continue;
            int i=(y*1001+x)*4;
            int j=((y-2)*1001+x-13)*4; // Match the original eye anchors; no rig/timing edits.
            float a=pixels[i+3]/255f*(1-weight),b=replacement[j+3]/255f*weight,total=a+b;
            for(int c=0;c<3;c++) pixels[i+c]=(byte)Math.Clamp((int)MathF.Round(total>0?(pixels[i+c]*a+replacement[j+c]*b)/total:0),0,255);
            pixels[i+3]=(byte)Math.Clamp((int)MathF.Round(total*255),0,255);
        }
        image.SetData(1001,1430,false,Image.Format.Rgba8,pixels);
        return ImageTexture.CreateFromImage(image);
        static float Rect(int x,int y,int left,int top,int right,int bottom)
        {
            float edge=MathF.Min(MathF.Min(x-left,right-x),MathF.Min(y-top,bottom-y));
            float t=Math.Clamp(edge/28f,0,1);
            return t*t*(3-2*t);
        }
    }
}
