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
    internal enum KeyCode { None, F8, F9, F10, F11, LeftControl, LeftAlt }
    internal static class Input
    {
        internal static readonly HashSet<KeyCode> Pressed = new HashSet<KeyCode>();
        public static bool GetKeyDown(KeyCode key) => Pressed.Remove(key);
    }
    internal struct Rect { public float x, y, width, height; public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; } }
    internal enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight }
    internal enum FontStyle { Normal, Bold }
    internal enum TextClipping { Overflow, Clip }
    internal struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
    }
    internal sealed class Texture2D { public static readonly Texture2D whiteTexture = new Texture2D(); }
    internal struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    internal struct Quaternion { public static Quaternion identity => new Quaternion(); }
    internal struct Matrix4x4
    {
        internal float X, Y, ScaleX, ScaleY;
        public static Matrix4x4 identity => new Matrix4x4 { ScaleX = 1, ScaleY = 1 };
        public static Matrix4x4 TRS(Vector3 position, Quaternion rotation, Vector3 scale) =>
            new Matrix4x4 { X = position.x, Y = position.y, ScaleX = scale.x, ScaleY = scale.y };
    }
    internal sealed class Font { }
    internal static class Screen { public static int width = 1280, height = 720; }
    internal sealed class RectOffset
    {
        public int left, right, top, bottom;
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
    }
    internal sealed class GUIStyleState { public Color textColor = Color.white; }
    internal enum EventType { Repaint, Layout }
    internal sealed class Event { public static Event current = new Event(); public EventType type = EventType.Repaint; }
    internal sealed class GUIStyle
    {
        public TextAnchor alignment; public int fontSize; public bool richText, wordWrap;
        public FontStyle fontStyle; public TextClipping clipping; public RectOffset padding = new RectOffset();
        public Font font;
        public GUIStyleState normal = new GUIStyleState();
        public GUIStyle() { } public GUIStyle(GUIStyle original) { }
        internal static int Layouts;
        public float CalcHeight(GUIContent content, float width) { Layouts++; return 300; }
    }
    internal sealed class GUIContent { public string text; public GUIContent() { } public GUIContent(string text) { this.text = text; } public static readonly GUIContent none = new GUIContent(""); }
    internal sealed class GUISkin { public GUIStyle box = new GUIStyle(), label = new GUIStyle(); }
    internal sealed class GuiCommand
    {
        internal Rect Bounds; internal string Text; internal Color Color;
        internal int FontSize; internal FontStyle FontStyle; internal TextAnchor Alignment; internal bool WordWrap;
    }
    internal static class GUI
    {
        public static GUISkin skin = new GUISkin(); internal static int Draws;
        public static Color color = Color.white;
        public static Color contentColor = Color.white;
        public static bool enabled = true;
        public static int depth;
        public static Matrix4x4 matrix = Matrix4x4.identity;
        internal static bool Capture;
        internal static readonly List<GuiCommand> Commands = new List<GuiCommand>();
        private static void Record(Rect bounds, string text, GUIStyle style)
        {
            Draws++;
            if (Capture) Commands.Add(new GuiCommand { Bounds = new Rect(matrix.X + bounds.x * matrix.ScaleX, matrix.Y + bounds.y * matrix.ScaleY,
                    bounds.width * matrix.ScaleX, bounds.height * matrix.ScaleY), Text = text, Color = style == null ? color : style.normal.textColor,
                FontSize = (int)Math.Round((style?.fontSize ?? 0) * matrix.ScaleY), FontStyle = style?.fontStyle ?? FontStyle.Normal,
                Alignment = style?.alignment ?? TextAnchor.UpperLeft, WordWrap = style?.wordWrap ?? false });
        }
        public static void Box(Rect bounds, GUIContent content, GUIStyle style) => Record(bounds, content.text, style);
        public static void Label(Rect bounds, GUIContent content, GUIStyle style) => Record(bounds, content.text, style);
        public static void Label(Rect bounds, string text, GUIStyle style) => Record(bounds, text, style);
        public static void DrawTexture(Rect bounds, Texture2D texture) => Record(bounds, null, null);
    }
    internal static class Mathf
    {
        public static int RoundToInt(float value) => (int)Math.Round(value);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float value, float minimum, float maximum) => Math.Min(maximum, Math.Max(minimum, value));
        public static float Clamp01(float value) => Clamp(value, 0, 1);
    }
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
    internal static class ModIdentity { internal const string Guid = "expanded.hordes"; }
    internal sealed class TestSetting<T> { internal T Value; internal TestSetting(T value) { Value = value; } }
    internal static class ModSettings
    {
        internal static TestSetting<bool> DebugMode = new TestSetting<bool>(false),
            Profiling = new TestSetting<bool>(false), DetailedTimings = new TestSetting<bool>(false);
        internal static TestSetting<float> HudScale = new TestSetting<float>(1), HudX = new TestSetting<float>(0), HudY = new TestSetting<float>(0);
        internal static TestSetting<int> ReportFileMiB = new TestSetting<int>(5);
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
    internal static class DebugHordeTrigger { internal static string Hint = "Ctrl + Shift + Pause: Start Horde Now"; }
    internal sealed class PlacementResult { } internal sealed class ContextResult { }
    internal sealed class ObserveRegistration { } internal sealed class ObserveRemoval { }
    internal sealed class ObserveRestore { } internal sealed class ObserveDeath { }
    internal sealed class ObserveCorpseAdded { } internal sealed class ObserveCorpseRemoved { }
}
