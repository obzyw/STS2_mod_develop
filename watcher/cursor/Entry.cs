using System.Collections;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace WatcherCookieCursor;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public static int Registrations { get; private set; }
    private static Type core=null!;
    private static Image normal=null!, pressed=null!;

    public static void Initialize()
    {
        core=AccessTools.TypeByName("CookieCursor.Core")
            ?? throw new InvalidOperationException("CookieCursor must load first.");
        using var image=Image.LoadFromFile(Path.Combine(Path.GetDirectoryName(typeof(Entry).Assembly.Location)!,"assets","watcher-cookie-v1.png"));
        if(image.IsEmpty()) throw new InvalidDataException("Watcher cookie artwork missing.");
        image.Resize(256,256,Image.Interpolation.Lanczos);
        var rotate=AccessTools.DeclaredMethod(core,"RotateImageManually",[typeof(Image),typeof(float)])
            ?? throw new MissingMethodException("CookieCursor rotation API changed.");
        normal=(Image)rotate.Invoke(null,[image,5f])!;
        pressed=(Image)rotate.Invoke(null,[image,-5f])!;
        new Harmony("obzyw.sts2.watcher-cookie-cursor").Patch(
            AccessTools.DeclaredMethod(core,"CacheCharCookies") ?? throw new MissingMethodException("CookieCursor cache API changed."),
            postfix:new HarmonyMethod(typeof(Entry),nameof(Register)));
        Register();
        GD.Print("[WatcherCookieCursor] Watcher cookie registered with CookieCursor; author files untouched.");
    }

    private static void Register()
    {
        var cache=AccessTools.Field(core,"RotatedCookieCache")?.GetValue(null) as IDictionary
            ?? throw new MissingFieldException("CookieCursor cache layout changed.");
        // Existing type names cover Boninall Watcher and standalone Reimagined.
        foreach(var key in new[]{"watcher","thewatcher"})
            cache[key]=(normal,pressed,new Vector2(10,4),DateTime.UnixEpoch);
        Registrations++;
    }
}
