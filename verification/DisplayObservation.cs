using System.Text.Json;
using Godot;

namespace STS2CompatProbe;

/// <summary>Menu-only comparison. Pixel readback is measured separately from normal frame timing.</summary>
internal static class DisplayObservation
{
    private static readonly List<object> results = new();

    internal static async Task Compare(Node host, string scene, string output)
    {
        int oldLimit = Engine.MaxFps;
        var oldVsync = DisplayServer.WindowGetVsyncMode();
        int refresh = (int)Math.Round(DisplayServer.ScreenGetRefreshRate(DisplayServer.WindowGetCurrentScreen()));
        int limit = refresh > 0 ? refresh : 60;
        host.GetWindow().GrabFocus();
        try
        {
            await Sample(host, scene + "-original", output);
            Engine.MaxFps = limit;
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
            await Sample(host, scene + "-synchronized", output);
        }
        finally
        {
            Engine.MaxFps = oldLimit;
            DisplayServer.WindowSetVsyncMode(oldVsync);
        }
        File.WriteAllText(Path.Combine(output, "display-result.json"), JsonSerializer.Serialize(new
        {
            scope = "Original vs synchronized within the same native game process, all subscribed mods retained. Pre-presentation pixels cannot detect monitor/VRR/scanout flicker. Timing excludes GPU image readback.",
            displayRefreshHz = refresh, cases = results
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task Sample(Node host, string id, string output)
    {
        var tree = host.GetTree();
        await host.ToSignal(tree.CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        var milliseconds = new List<double>();
        ulong begin = Time.GetTicksUsec(), previous = begin;
        while (Time.GetTicksUsec() - begin < 5_000_000)
        {
            await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            ulong now = Time.GetTicksUsec();
            milliseconds.Add((now - previous) / 1000.0);
            previous = now;
        }
        milliseconds.Sort();
        int suspicious = 0, captures = 180;
        double maxDimmingFraction = 0;
        byte[]? previousLuminance = null;
        for (int frame = 0; frame < captures; frame++)
        {
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = host.GetViewport().GetTexture().GetImage();
            image.Convert(Image.Format.Rgba8);
            byte[] pixels = image.GetData();
            int width = image.GetWidth(), height = image.GetHeight();
            var luminance = new byte[96 * 54];
            int dimmed = 0;
            for (int y = 0; y < 54; y++)
                for (int x = 0; x < 96; x++)
                {
                    int index = y * 96 + x;
                    int pixel = (((2 * y + 1) * height / 108) * width + (2 * x + 1) * width / 192) * 4;
                    int value = (pixels[pixel] * 54 + pixels[pixel + 1] * 183 + pixels[pixel + 2] * 19) / 256;
                    luminance[index] = (byte)value;
                    if (previousLuminance != null && previousLuminance[index] > 25 && value < previousLuminance[index] * .25) dimmed++;
                }
            double fraction = dimmed / (double)luminance.Length;
            maxDimmingFraction = Math.Max(maxDimmingFraction, fraction);
            if (fraction > .03)
            {
                suspicious++;
                if (suspicious <= 3) image.SavePng(Path.Combine(output, id + "-dimming-" + frame + ".png"));
            }
            if (frame == 0 || frame == captures - 1) image.SavePng(Path.Combine(output, id + "-" + frame + ".png"));
            previousLuminance = luminance;
        }
        results.Add(new
        {
            id, fpsLimit = Engine.MaxFps, vsync = DisplayServer.WindowGetVsyncMode().ToString(),
            focused = host.GetWindow().HasFocus(), windowMode = DisplayServer.WindowGetMode().ToString(),
            frameSamples = milliseconds.Count, medianFrameMs = milliseconds[milliseconds.Count / 2],
            p95FrameMs = milliseconds[(int)((milliseconds.Count - 1) * .95)],
            pixelFrames = captures, suspectedLargeDimmingFrames = suspicious, maxDimmingFraction,
            detector = "Adjacent native frames: >3% of the viewport's 96x54 sampled pixels falling below 25% luminance, starting above 25/255. This is a heuristic observation, not proof of absence."
        });
        GD.Print("[DisplayObservation] " + id + " sampled; native dimming candidates=" + suspicious);
    }
}
