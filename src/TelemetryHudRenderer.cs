using UnityEngine;

namespace ExpandedHordes
{
    // A combat glance, not a report. Input belongs to the separate debug command.
    internal sealed class TelemetryHudRenderer
    {
        internal const float Width = 310, Height = 136;
        private static readonly Color Panel = new Color(.055f, .069f, .086f, .97f);
        private static readonly Color Border = new Color(.19f, .23f, .28f, 1);
        private static readonly Color White = new Color(.94f, .96f, .98f, 1);
        private static readonly Color Muted = new Color(.65f, .71f, .77f, 1);
        private static readonly Color Accent = new Color(.35f, .86f, .70f, 1);
        private static readonly Color Amber = new Color(1, .75f, .36f, 1);
        private GUIStyle title, status, number, small, performance, hintStyle;
        private HudSnapshot cached;
        private readonly GUIContent brand = new GUIContent("EXPANDED HORDES"), aliveLabel = new GUIContent("HORDE ALIVE / CAP");
        private readonly GUIContent state = new GUIContent(), population = new GUIContent(), fps = new GUIContent(), hint = new GUIContent();

        private static GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var result = new GUIStyle { font = GUI.skin.label.font, fontSize = size, fontStyle = weight, alignment = alignment,
                clipping = TextClipping.Clip, wordWrap = false, richText = false };
            result.normal.textColor = color;
            return result;
        }
        private void Prepare(HudSnapshot snapshot)
        {
            if (title == null)
            {
                title = Style(12, White, FontStyle.Bold);
                status = Style(11, Muted, FontStyle.Normal, TextAnchor.UpperRight);
                number = Style(30, White, FontStyle.Bold);
                small = Style(11, Muted);
                performance = Style(11, White, FontStyle.Normal, TextAnchor.UpperRight);
                hintStyle = Style(12, Muted);
            }
            if (ReferenceEquals(cached, snapshot)) return;
            cached = snapshot;
            state.text = !snapshot.StateKnown ? "Status unknown" : snapshot.Spawning ? "Spawning" : "Not spawning";
            population.text = snapshot.Population; fps.text = snapshot.FpsSummary; hint.text = snapshot.Hint;
            status.normal.textColor = snapshot.Spawning && snapshot.StateKnown ? Accent : Muted;
            performance.normal.textColor = snapshot.FrameStale ? Amber : White;
        }
        private static void Fill(float x, float y, float width, float height, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        internal void Draw(HudSnapshot snapshot, float x, float y, float scale)
        {
            if (snapshot == null || Event.current.type != EventType.Repaint) return;
            Prepare(snapshot);
            scale = float.IsNaN(scale) || float.IsInfinity(scale) ? 1 : Mathf.Clamp(scale, .5f, 3f);
            scale = Mathf.Min(scale, Mathf.Min(Screen.width / Width, Screen.height / Height));
            if (scale <= 0) return;
            x = float.IsNaN(x) || float.IsInfinity(x) ? 0 : Mathf.Clamp(x, 0, Screen.width - Width * scale);
            y = float.IsNaN(y) || float.IsInfinity(y) ? 0 : Mathf.Clamp(y, 0, Screen.height - Height * scale);
            var oldMatrix = GUI.matrix; var oldColor = GUI.color; var oldContentColor = GUI.contentColor; int oldDepth = GUI.depth;
            bool oldEnabled = GUI.enabled;
            try
            {
                GUI.depth = -10; GUI.enabled = true; GUI.contentColor = Color.white;
                GUI.matrix = Matrix4x4.TRS(new Vector3(x, y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
                Fill(0, 0, Width, Height, Border); Fill(1, 1, Width - 2, Height - 2, Panel);
                Fill(0, 0, 3, Height, snapshot.Spawning && snapshot.StateKnown ? Accent : Border);
                GUI.Label(new Rect(14, 13, 177, 18), brand, title);
                GUI.Label(new Rect(191, 14, 105, 17), state, status);
                GUI.Label(new Rect(14, 40, 167, 38), population, number);
                GUI.Label(new Rect(181, 53, 115, 18), fps, performance);
                GUI.Label(new Rect(14, 79, 282, 16), aliveLabel, small);
                Fill(14, 102, 282, 1, Border);
                GUI.Label(new Rect(14, 111, 282, 18), hint, hintStyle);
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.contentColor = oldContentColor; GUI.depth = oldDepth; GUI.enabled = oldEnabled; }
        }
    }
}
