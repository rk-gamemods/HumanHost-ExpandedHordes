using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using ExpandedHordes;
using UnityEngine;

// Captures the actual production renderer's primitives. The SVG is a host layout
// preview, not a claim to reproduce Unity's font rasterization or game rendering.
internal static class HudPresentationChecks
{
    private sealed class PreviewSink : ITelemetrySink
    {
        public void Write(TelemetryBatch batch) { }
        public void Dispose() { }
    }
    private static HudSnapshot Fixture(bool spawning, bool unavailable = false, double now = 27000)
    {
        var collector = new TelemetryCollector(0, new TelemetryBatch());
        var hud = new TelemetryHud { LifecycleAvailable = !unavailable, PlacementAvailable = !unavailable,
            CorpseEventsAvailable = !unavailable, CategoryAvailable = !unavailable,
            ManagedBytes = unavailable ? -1 : 630718464, ManagedReadMs = 26000 };
        var state = PopulationSample.Unavailable;
        if (!unavailable)
        {
            state.Horde = 17; state.SpawningKnown = true; state.Spawning = spawning;
            state.Alive = spawning ? 75 : 0; state.LivingTarget = 75;
            state.Budget = 152; state.Emitted = 56; state.Corpses = 51; state.CorpseLimit = 300;
            // An aggregate matching the user's reported idle state, with an
            // actual frame distribution rather than hand-authored display text.
            for (int i = 1; i <= 1323; i++) collector.Frame(i * (15005d / 1323));
            collector.CloseBucket(15005, state); collector.Complete(15005, WindowEnd.Cadence);
            hud.CaptureWindow(collector.Batch);
        }
        var writer = new TelemetryWriter(new PreviewSink());
        try { return hud.BuildSnapshot(collector, writer, now, 0, unavailable ? 0 : 7, true); }
        finally { writer.Stop(2000); }
    }

    private static GuiCommand[] Capture(HudSnapshot snapshot, int width = 1280, int height = 720,
        float x = 20, float y = 20, float scale = 1)
    {
        Screen.width = width; Screen.height = height; GUI.Commands.Clear(); GUI.Capture = true;
        try { new TelemetryHudRenderer().Draw(snapshot, x, y, scale); }
        finally { GUI.Capture = false; }
        return GUI.Commands.ToArray();
    }

    internal static void Run(Action<bool, string> check)
    {
        var idle = Fixture(false);
        check(idle.Status == "Not spawning" && idle.Alive == "0" && idle.AliveTarget.Contains("75") &&
            idle.AliveTarget.Contains("cap") && idle.Remaining == "96 unspawned budget",
            "Idle presentation distinguishes current population, population cap and unused spawn budget");
        check(Fixture(false, false, 90000).FrameStale && !Fixture(false, true).HasProgress,
            "Stale frames are flagged and unknown spawn budgets do not render fabricated progress");
        var commands = Capture(idle);
        string text = string.Join("\n", commands.Where(c => c.Text != null).Select(c => c.Text));
        check(commands.Any(c => c.Text == null) && commands.Count(c => c.Text != null) > 5,
            "HUD renders structured surfaces and separately styled information rather than a raw text dump");
        check(!text.Contains("hotkey_configured_overlap") && !text.Contains("com.nf.") && !text.Contains("first=") &&
            !text.Contains("Registered fresh") && !text.Contains("CPU/GPU delayed") && !text.Contains("off-thread"),
            "HUD excludes raw conflict records, owner identifiers and low-level diagnostic counters");
        check(commands.Where(c => c.Text != null).Select(c => c.FontSize).Distinct().Count() >= 3,
            "HUD gives key values, section labels and supporting details distinct type sizes");
        foreach (var size in new[] { (1280, 720), (800, 600), (640, 480) })
        {
            commands = Capture(idle, size.Item1, size.Item2, 10000, 10000, 2);
            check(commands.All(c => c.Bounds.x >= 0 && c.Bounds.y >= 0 && c.Bounds.width >= 0 && c.Bounds.height >= 0 &&
                c.Bounds.x + c.Bounds.width <= size.Item1 + .1f && c.Bounds.y + c.Bounds.height <= size.Item2 + .1f),
                $"HUD clamps scale and saved position inside {size.Item1}x{size.Item2}");
        }
        var missing = Capture(Fixture(false, true));
        string missingText = string.Join(" ", missing.Select(c => c.Text));
        check(missingText.IndexOf("waiting", StringComparison.OrdinalIgnoreCase) >= 0 ||
            missingText.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0,
            "Missing frame samples are explained rather than presented as measured zero FPS");
        var renderer = new TelemetryHudRenderer(); Screen.width = 1280; Screen.height = 720;
        GUI.Capture = false; renderer.Draw(idle, 20, 20, 1);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) renderer.Draw(idle, 20, 20, 1);
        check(GC.GetAllocatedBytesForCurrentThread() == allocated,
            "Repeated production HUD draws reuse styles and cached snapshot strings without managed allocation in the host");
        var incomingMatrix = Matrix4x4.TRS(new Vector3(8, 13, 0), Quaternion.identity, new Vector3(2, 3, 1));
        var incomingColor = new Color(.1f, .2f, .3f, .4f);
        var incomingContentColor = new Color(.5f, .6f, .7f, .8f);
        GUI.matrix = incomingMatrix; GUI.color = incomingColor; GUI.contentColor = incomingContentColor; GUI.depth = 27; GUI.enabled = false;
        renderer.Draw(idle, 20, 20, 1);
        check(GUI.matrix.Equals(incomingMatrix) && GUI.color.Equals(incomingColor) && GUI.contentColor.Equals(incomingContentColor) &&
            GUI.depth == 27 && !GUI.enabled,
            "HUD restores another mod's incoming GUI transform, tint, text tint, depth and enabled state");
        GUI.matrix = Matrix4x4.identity; GUI.color = GUI.contentColor = Color.white; GUI.depth = 0; GUI.enabled = true;
    }

    internal static void WritePreviews(string directory)
    {
        Directory.CreateDirectory(directory);
        WritePreview(Path.Combine(directory, "idle.svg"), Fixture(false));
        WritePreview(Path.Combine(directory, "spawning.svg"), Fixture(true));
        WritePreview(Path.Combine(directory, "missing-data.svg"), Fixture(false, true));
        WritePreview(Path.Combine(directory, "stale.svg"), Fixture(false, false, 90000));
        WritePreview(Path.Combine(directory, "small-display.svg"), Fixture(false), 640, 480, 10000, 10000, 2);
        Console.WriteLine("Production Draw command previews: " + Path.GetFullPath(directory));
    }

    private static string N(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Paint(Color color) => $"rgb({Math.Round(color.r * 255)},{Math.Round(color.g * 255)},{Math.Round(color.b * 255)})";
    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";
    private static void WritePreview(string path, HudSnapshot snapshot, int width = 1280, int height = 720,
        float x = 20, float y = 20, float scale = 1)
    {
        var commands = Capture(snapshot, width, height, x, y, scale);
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"#283130\"/>");
        int index = 0;
        foreach (var command in commands)
        {
            Rect r = command.Bounds;
            if (command.Text == null)
                svg.Append($"<rect x=\"{N(r.x)}\" y=\"{N(r.y)}\" width=\"{N(r.width)}\" height=\"{N(r.height)}\" fill=\"{Paint(command.Color)}\" fill-opacity=\"{N(command.Color.a)}\"/>");
            else
            {
                string anchor = command.Alignment == TextAnchor.UpperRight || command.Alignment == TextAnchor.MiddleRight ? "end" :
                    command.Alignment == TextAnchor.UpperCenter || command.Alignment == TextAnchor.MiddleCenter ? "middle" : "start";
                float tx = anchor == "end" ? r.x + r.width : anchor == "middle" ? r.x + r.width / 2 : r.x;
                bool middle = command.Alignment == TextAnchor.MiddleLeft || command.Alignment == TextAnchor.MiddleCenter || command.Alignment == TextAnchor.MiddleRight;
                float ty = middle ? r.y + (r.height - command.FontSize) / 2 : r.y;
                string id = "clip" + index++;
                svg.Append($"<clipPath id=\"{id}\"><rect x=\"{N(r.x)}\" y=\"{N(r.y)}\" width=\"{N(r.width)}\" height=\"{N(r.height)}\"/></clipPath>");
                svg.Append($"<text x=\"{N(tx)}\" y=\"{N(ty + command.FontSize * .83f)}\" font-family=\"Arial,sans-serif\" font-size=\"{command.FontSize}\" font-weight=\"{(command.FontStyle == FontStyle.Bold ? "bold" : "normal")}\" text-anchor=\"{anchor}\" fill=\"{Paint(command.Color)}\" fill-opacity=\"{N(command.Color.a)}\" clip-path=\"url(#{id})\">{Escape(command.Text)}</text>");
            }
        }
        svg.Append("</svg>"); File.WriteAllText(path, svg.ToString());
    }
}
