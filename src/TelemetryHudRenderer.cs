using UnityEngine;

namespace ExpandedHordes
{
    // Passive display only. It has no controls, input handlers, or per-repaint text formatting.
    internal sealed class TelemetryHudRenderer
    {
        internal const float Width = 380, Height = 282;
        private static readonly Color Panel = new Color(.055f, .069f, .086f, .97f);
        private static readonly Color Border = new Color(.19f, .23f, .28f, 1);
        private static readonly Color White = new Color(.94f, .96f, .98f, 1);
        private static readonly Color Muted = new Color(.65f, .71f, .77f, 1);
        private static readonly Color Accent = new Color(.35f, .86f, .70f, 1);
        private static readonly Color Amber = new Color(1, .75f, .36f, 1);
        private GUIStyle title, badge, status, number, small, right, hero, heroRight;
        private HudSnapshot cached;
        private readonly GUIContent brand = new GUIContent("EXPANDED HORDES"), debug = new GUIContent("DEBUG"),
            aliveLabel = new GUIContent("HORDE ENEMIES ALIVE"), fpsLabel = new GUIContent("AVG FPS");
        private readonly GUIContent state = new GUIContent(), horde = new GUIContent(), alive = new GUIContent(), target = new GUIContent(),
            spawned = new GUIContent(), remaining = new GUIContent(), fps = new GUIContent(), frame = new GUIContent(),
            age = new GUIContent(), conflicts = new GUIContent(), recording = new GUIContent();

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
                title = Style(13, White, FontStyle.Bold); badge = Style(10, Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
                status = Style(19, White, FontStyle.Bold); number = Style(38, White, FontStyle.Bold);
                small = Style(12, Muted); right = Style(12, Muted, FontStyle.Normal, TextAnchor.UpperRight);
                hero = Style(15, White, FontStyle.Bold); heroRight = Style(15, White, FontStyle.Bold, TextAnchor.UpperRight);
            }
            if (ReferenceEquals(cached, snapshot)) return;
            cached = snapshot;
            state.text = snapshot.Status; horde.text = snapshot.Horde; alive.text = snapshot.Alive; target.text = snapshot.AliveTarget;
            spawned.text = snapshot.Spawned; remaining.text = snapshot.Remaining; fps.text = snapshot.Fps; frame.text = snapshot.FrameTime;
            age.text = snapshot.WindowAge; conflicts.text = snapshot.Conflicts; recording.text = snapshot.Recording;
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
                GUI.depth = -10;
                GUI.enabled = true;
                GUI.contentColor = Color.white;
                GUI.matrix = Matrix4x4.TRS(new Vector3(x, y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
                Fill(0, 0, Width, Height, Border); Fill(1, 1, Width - 2, Height - 2, Panel);
                Fill(0, 0, 3, Height, snapshot.Spawning && snapshot.StateKnown ? Accent : Border);
                GUI.Label(new Rect(18, 16, 250, 18), brand, title);
                Fill(307, 13, 55, 23, new Color(.10f, .20f, .19f, 1));
                GUI.Label(new Rect(307, 13, 55, 23), debug, badge);
                GUI.Label(new Rect(18, 43, 263, 27), state, status);
                GUI.Label(new Rect(279, 48, 83, 18), horde, right);
                Fill(18, 80, 344, 1, Border);
                GUI.Label(new Rect(18, 90, 240, 17), aliveLabel, small);
                GUI.Label(new Rect(18, 104, 200, 46), alive, number);
                GUI.Label(new Rect(219, 123, 143, 20), target, heroRight);
                GUI.Label(new Rect(18, 150, 174, 18), spawned, small);
                GUI.Label(new Rect(192, 150, 170, 18), remaining, right);
                if (snapshot.HasProgress)
                {
                    Fill(18, 173, 344, 4, Border);
                    Fill(18, 173, 344 * snapshot.Progress, 4, Accent);
                }
                hero.normal.textColor = snapshot.FrameStale ? Amber : White;
                GUI.Label(new Rect(18, 189, 42, 20), fps, hero);
                GUI.Label(new Rect(64, 191, 60, 18), fpsLabel, small);
                GUI.Label(new Rect(125, 191, 237, 18), frame, right);
                GUI.Label(new Rect(18, 211, 344, 17), age, small);
                small.normal.textColor = snapshot.HasWarning ? Amber : Muted;
                GUI.Label(new Rect(18, 233, 344, 17), conflicts, small);
                small.normal.textColor = snapshot.RecordingWarning ? Amber : Muted;
                GUI.Label(new Rect(18, 250, 344, 17), recording, small);
                small.normal.textColor = Muted;
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.contentColor = oldContentColor; GUI.depth = oldDepth; GUI.enabled = oldEnabled; }
        }
    }
}
