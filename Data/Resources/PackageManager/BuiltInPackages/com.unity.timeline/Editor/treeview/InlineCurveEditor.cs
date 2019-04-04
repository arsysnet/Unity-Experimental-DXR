using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    interface IClipCurveEditorOwner
    {
        ClipCurveEditor clipCurveEditor { get; }
        bool inlineCurvesSelected { get; set; }
        bool showLoops { get; }
        TrackAsset owner { get; }
    }

    class InlineCurveResizeHandle : IBounds
    {
        public Rect boundingRect { get; private set; }

        public TimelineTrackGUI trackGUI { get; }

        public InlineCurveResizeHandle(TimelineTrackGUI trackGUI)
        {
            this.trackGUI = trackGUI;
        }

        public void Draw(Rect headerRect, Rect trackRect, WindowState state)
        {
            var rect = new Rect(headerRect.xMax + 4, headerRect.yMax - 5.0f, trackRect.width - 4, 5.0f);

            var handleColor = Handles.color;
            Handles.color = Color.black;
            Handles.DrawAAPolyLine(1.0f,
                new Vector3(rect.x, rect.yMax, 0.0f),
                new Vector3(rect.xMax, rect.yMax, 0.0f));
            Handles.color = handleColor;

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.SplitResizeUpDown);

            boundingRect = trackGUI.ToWindowSpace(rect);

            if (Event.current.type == EventType.Repaint)
            {
                state.spacePartitioner.AddBounds(this);

                var dragStyle = new GUIStyle("RL DragHandle");
                dragStyle.Draw(rect, GUIContent.none, false, false, false, false);
            }
        }
    }

    class InlineCurveEditor : IBounds
    {
        Rect m_TrackRect;
        Rect m_HeaderRect;
        readonly TimelineTrackGUI m_TrackGUI;
        readonly InlineCurveResizeHandle m_ResizeHandle;

        TimelineClipGUI m_LastSelectedClipGUI;

        Rect IBounds.boundingRect { get { return m_TrackGUI.ToWindowSpace(m_TrackRect); } }

        public TimelineClipGUI currentClipGui
        {
            get { return m_LastSelectedClipGUI;  }
        }

        public InlineCurveEditor(TimelineTrackGUI trackGUI)
        {
            m_TrackGUI = trackGUI;
            m_ResizeHandle = new InlineCurveResizeHandle(trackGUI);
        }

        static bool MouseOverTrackArea(Rect curveRect, Rect trackRect)
        {
            curveRect.y = trackRect.y;
            curveRect.height = trackRect.height;

            // clamp the curve editor to the track. this allows the menu to scroll properly
            curveRect.xMin = Mathf.Max(curveRect.xMin, trackRect.xMin);
            curveRect.xMax = trackRect.xMax;

            return curveRect.Contains(Event.current.mousePosition);
        }

        static bool MouseOverHeaderArea(Rect headerRect, Rect trackRect)
        {
            headerRect.y = trackRect.y;
            headerRect.height = trackRect.height;

            return headerRect.Contains(Event.current.mousePosition);
        }

        static void DrawCurveEditor(IClipCurveEditorOwner clipCurveEditorOwner, WindowState state, Rect headerRect, Rect trackRect, Vector2 activeRange, bool locked)
        {
            ClipCurveEditor clipCurveEditor = clipCurveEditorOwner.clipCurveEditor;
            CurveDataSource dataSource = clipCurveEditor.dataSource;
            Rect curveRect = dataSource.GetBackgroundRect(state);

            bool newlySelected = false;

            if (Event.current.type == EventType.MouseDown)
                newlySelected = MouseOverTrackArea(curveRect, trackRect) || MouseOverHeaderArea(headerRect, trackRect);

            // make sure to not use any event before drawing the curve.
            clipCurveEditorOwner.clipCurveEditor.DrawHeader(headerRect);

            bool displayAsSelected = !locked && (clipCurveEditorOwner.inlineCurvesSelected || newlySelected);

            using (new EditorGUI.DisabledScope(locked))
            {
                using (new GUIViewportScope(trackRect))
                {
                    Rect animEditorRect = curveRect;
                    animEditorRect.y = trackRect.y;
                    animEditorRect.height = trackRect.height;

                    // clamp the curve editor to the track. this allows the menu to scroll properly
                    animEditorRect.xMin = Mathf.Max(animEditorRect.xMin, trackRect.xMin);
                    animEditorRect.xMax = trackRect.xMax;

                    if (activeRange == Vector2.zero)
                        activeRange = new Vector2(animEditorRect.xMin, animEditorRect.xMax);

                    clipCurveEditor.DrawCurveEditor(animEditorRect, state, activeRange, clipCurveEditorOwner.showLoops, displayAsSelected);
                }
            }

            if (newlySelected && !locked)
            {
                clipCurveEditorOwner.inlineCurvesSelected = true;
            }
        }

        public void Draw(Rect headerRect, Rect trackRect, WindowState state)
        {
            m_TrackRect = trackRect;
            m_TrackRect.height -= 5.0f;

            if (Event.current.type == EventType.Repaint)
                state.spacePartitioner.AddBounds(this);

            // Remove the indentation of this track to render it properly, otherwise every GUI elements will be offsetted.
            headerRect.x -= DirectorStyles.kBaseIndent;
            headerRect.width += DirectorStyles.kBaseIndent;

            // Remove the width of the color swatch.
            headerRect.x += 4.0f;
            headerRect.width -= 4.0f;

            m_HeaderRect = headerRect;

            EditorGUI.DrawRect(m_HeaderRect, DirectorStyles.Instance.customSkin.colorAnimEditorBinding);

            var animTrack = m_TrackGUI.track as AnimationTrack;
            if (animTrack != null && !animTrack.inClipMode)
            {
                DrawCurveEditorForInfiniteClip(m_HeaderRect, m_TrackRect, state);
            }
            else
            {
                DrawCurveEditorsForClipsOnTrack(m_HeaderRect, m_TrackRect, state);
            }

            m_ResizeHandle.Draw(headerRect, trackRect, state);

            // If MouseDown or ContextClick are not consumed by the curves, use the event to prevent it from going deeper into the treeview.
            if (Event.current.type == EventType.ContextClick)
            {
                var r = Rect.MinMaxRect(m_HeaderRect.xMin, m_HeaderRect.yMin, m_TrackRect.xMax, m_TrackRect.yMax);
                if (r.Contains(Event.current.mousePosition))
                    Event.current.Use();
            }
        }

        void DrawCurveEditorForInfiniteClip(Rect headerRect, Rect trackRect, WindowState state)
        {
            if (m_TrackGUI.clipCurveEditor == null)
                return;

            DrawCurveEditor(m_TrackGUI, state, headerRect, trackRect, Vector2.zero, m_TrackGUI.locked);
        }

        void DrawCurveEditorsForClipsOnTrack(Rect headerRect, Rect trackRect, WindowState state)
        {
            if (m_TrackGUI.clips.Count == 0)
                return;

            if (Event.current.type == EventType.Layout)
            {
                var selectedClip = SelectionManager.SelectedClipGUI().FirstOrDefault(x => x.parent == m_TrackGUI);
                if (selectedClip != null)
                {
                    m_LastSelectedClipGUI = selectedClip;
                }
                else if (state.recording && state.IsArmedForRecord(m_TrackGUI.track))
                {
                    if (m_LastSelectedClipGUI == null || !m_TrackGUI.track.IsRecordingToClip(m_LastSelectedClipGUI.clip))
                    {
                        var clip = m_TrackGUI.clips.FirstOrDefault(x => m_TrackGUI.track.IsRecordingToClip(x.clip));
                        if (clip != null)
                            m_LastSelectedClipGUI = clip;
                    }
                }

                if (m_LastSelectedClipGUI == null)
                    m_LastSelectedClipGUI = m_TrackGUI.clips[0];
            }

            if (m_LastSelectedClipGUI == null || m_LastSelectedClipGUI.clipCurveEditor == null || m_LastSelectedClipGUI.isInvalid)
                return;

            var inlineCurveActiveArea = new Vector2(state.TimeToPixel(m_LastSelectedClipGUI.clip.start), state.TimeToPixel(m_LastSelectedClipGUI.clip.end));
            DrawCurveEditor(m_LastSelectedClipGUI, state, headerRect, trackRect, inlineCurveActiveArea, m_TrackGUI.locked);
        }
    }
}
