// Test doubles at the Unity/game/config/Harmony boundary. The production
// PerformanceMonitor, collector, formatter and background writer run unchanged.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    internal struct FrameTiming { public ulong frameStartTimestamp; public double cpuMainThreadFrameTime, gpuFrameTime; }
    internal static class FrameTimingManager
    {
        internal static int Captures; internal static bool Throw;
        public static void CaptureFrameTimings() { Captures++; if (Throw) throw new InvalidOperationException(); }
        public static uint GetLatestTimings(uint count, FrameTiming[] values)
        { values[0] = new FrameTiming { frameStartTimestamp = (ulong)Captures, cpuMainThreadFrameTime = 2, gpuFrameTime = 3 }; return 1; }
    }
    internal enum KeyCode { F8, F9, F10, F11 }
    internal static class Input
    {
        internal static readonly HashSet<KeyCode> Pressed = new HashSet<KeyCode>();
        public static bool GetKeyDown(KeyCode key) => Pressed.Remove(key);
    }
    internal struct Rect { public float width, height; public Rect(float x, float y, float w, float h) { width = w; height = h; } }
    internal enum TextAnchor { UpperLeft }
    internal enum EventType { Repaint, Layout }
    internal sealed class Event { public static Event current = new Event(); public EventType type = EventType.Repaint; }
    internal sealed class GUIStyle
    {
        public TextAnchor alignment; public int fontSize; public bool richText, wordWrap;
        public GUIStyle() { } public GUIStyle(GUIStyle original) { }
        internal static int Layouts;
        public float CalcHeight(GUIContent content, float width) { Layouts++; return 300; }
    }
    internal sealed class GUIContent { public string text; }
    internal sealed class GUISkin { public GUIStyle box = new GUIStyle(); }
    internal static class GUI
    {
        public static GUISkin skin = new GUISkin(); internal static int Draws;
        public static void Box(Rect bounds, GUIContent content, GUIStyle style) { Draws++; }
    }
    internal static class Mathf { public static int RoundToInt(float value) => (int)Math.Round(value); }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
}
internal sealed class Zombie_Agent { }
internal sealed class GPUI_Dead_Body_Mgr { }
internal sealed class NPC_Horde_Mgr { }
namespace ExpandedHordes
{
    internal sealed class TestSetting<T> { internal T Value; internal TestSetting(T value) { Value = value; } }
    internal static class ModSettings
    {
        internal static TestSetting<bool> DebugHud = new TestSetting<bool>(false), DebugMode = new TestSetting<bool>(false),
            Profiling = new TestSetting<bool>(false), DetailedTimings = new TestSetting<bool>(false);
        internal static TestSetting<float> HudScale = new TestSetting<float>(1), HudX = new TestSetting<float>(0), HudY = new TestSetting<float>(0);
        internal static TestSetting<int> ReportFileMiB = new TestSetting<int>(5);
        internal static TestSetting<string> MarkerNote = new TestSetting<string>("");
        internal static TestSetting<UnityEngine.KeyCode> HudKey = new TestSetting<UnityEngine.KeyCode>(UnityEngine.KeyCode.F8),
            MarkerKey = new TestSetting<UnityEngine.KeyCode>(UnityEngine.KeyCode.F9), RescanKey = new TestSetting<UnityEngine.KeyCode>(UnityEngine.KeyCode.F10),
            FolderKey = new TestSetting<UnityEngine.KeyCode>(UnityEngine.KeyCode.F11);
    }
    internal enum Feature { Profiling, Diagnostics, CorpseDiagnostics, Catalog }
    internal static class FeatureRuntime
    {
        internal static readonly HashSet<Feature> Installed = new HashSet<Feature>(), Failures = new HashSet<Feature>();
        internal static bool Enabled(Feature feature) => !Failures.Contains(feature);
        internal static string DisabledSummary => string.Join(", ", Failures);
        internal static void InstallProfiler() => Install(Feature.Profiling);
        internal static void Install(Feature feature, params Type[] patches) { if (Enabled(feature)) Installed.Add(feature); }
    }
    internal static class GameTelemetry
    {
        internal static int RestoreDepth, DeathDepth, Initializations, Samples;
        internal static bool LifecycleAvailable, CorpseEventsAvailable, MembershipAvailable = true;
        internal static int HordeId => 1;
        internal static void Initialize() { Initializations++; }
        internal static PopulationSample Sample() { Samples++; return PopulationSample.Unavailable; }
    }
    internal static class DiagnosticEnvironment
    {
        internal static int Captures; internal static bool Throw;
        internal static string Capture(string session) { Captures++; if (Throw) throw new InvalidOperationException("private path must not escape"); return "fixture_environment session=" + session; }
    }
    internal sealed class TestLog
    {
        internal readonly List<string> Lines = new List<string>();
        internal void LogWarning(object value) => Lines.Add(value.ToString());
        internal void LogInfo(object value) => Lines.Add(value.ToString());
        internal void LogError(object value) => Lines.Add(value.ToString());
    }
    internal static class Plugin { internal static TestLog Log = new TestLog(); }
    internal sealed class PlacementResult { } internal sealed class ContextResult { }
    internal sealed class ObserveRegistration { } internal sealed class ObserveRemoval { }
    internal sealed class ObserveRestore { } internal sealed class ObserveDeath { }
    internal sealed class ObserveCorpseAdded { } internal sealed class ObserveCorpseRemoved { }
}
