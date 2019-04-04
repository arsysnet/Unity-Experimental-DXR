using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace UnityEditor.Timeline
{
    class TimelineTrackGUI : TimelineGroupGUI, IClipCurveEditorOwner, IRowGUI
    {
        static class Styles
        {
            public static readonly string kArmForRecordDisabled = L10n.Tr("Recording is not permitted when Track Offsets are set to Auto. Track Offset settings can be changed in the track menu of the base track.");
        }

        static GUIContent s_ArmForRecordContentOn;
        static GUIContent s_ArmForRecordContentOff;
        static GUIContent s_ArmForRecordDisabled;


        bool m_HadProblems;
        bool m_InitHadProblems;
        bool m_InlineCurvesSkipped;
        int m_TrackHash = -1;
        int m_BlendHash = -1;
        readonly PlayableBinding[] m_Bindings;
        bool? m_TrackAllowsRecording;
        readonly InfiniteTrackDrawer m_InfiniteTrackDrawer;
        TrackItemsDrawer m_ItemsDrawer;

        public override bool expandable
        {
            get { return hasChildren; }
        }

        internal InlineCurveEditor inlineCurveEditor { get; set; }

        public ClipCurveEditor clipCurveEditor { get; private set; }

        public bool inlineCurvesSelected
        {
            get { return SelectionManager.IsCurveEditorFocused(this); }
            set
            {
                if (!value && SelectionManager.IsCurveEditorFocused(this))
                    SelectionManager.SelectInlineCurveEditor(null);
                else
                    SelectionManager.SelectInlineCurveEditor(this);
            }
        }

        bool IClipCurveEditorOwner.showLoops
        {
            get { return false; }
        }

        TrackAsset IClipCurveEditorOwner.owner
        {
            get { return track; }
        }

        bool trackAllowsRecording
        {
            get
            {
                // if the root animation track is in auto mode, recording is not allowed
                var animTrack = TimelineUtility.GetSceneReferenceTrack(track) as AnimationTrack;
                if (animTrack != null)
                    return animTrack.trackOffset != TrackOffset.Auto;

                // cache this value to avoid the recomputation
                if (!m_TrackAllowsRecording.HasValue)
                    m_TrackAllowsRecording = track.clips.Any(c => c.HasAnyAnimatableParameters());
                return m_TrackAllowsRecording.Value;
            }
        }

        public bool locked
        {
            get { return track.lockedInHierarchy; }
        }

        public bool showMarkers
        {
            get { return track.GetShowMarkers(); }
        }

        public bool muted
        {
            get { return track.muted; }
        }

        public List<TimelineClipGUI> clips
        {
            get
            {
                return m_ItemsDrawer == null ? new List<TimelineClipGUI>(0) : m_ItemsDrawer.clips.ToList();
            }
        }

        TrackAsset IRowGUI.asset { get { return track; } }

        bool showTrackRecordingDisabled
        {
            get
            {
                // if the root animation track is in auto mode, recording is not allowed
                var animTrack = TimelineUtility.GetSceneReferenceTrack(track) as AnimationTrack;
                return animTrack != null && animTrack.trackOffset == TrackOffset.Auto;
            }
        }

        public TimelineTrackGUI(TreeViewController tv, TimelineTreeViewGUI w, int id, int depth, TreeViewItem parent, string displayName, TrackAsset sequenceActor)
            : base(tv, w, id, depth, parent, displayName, sequenceActor, false)
        {
            AnimationTrack animationTrack = sequenceActor as AnimationTrack;
            if (animationTrack != null)
            {
                m_InfiniteTrackDrawer = new InfiniteTrackDrawer(new AnimationTrackKeyDataSource(animationTrack));
                UpdateInfiniteClipEditor(animationTrack, w.TimelineWindow);

                if (animationTrack.ShouldShowInfiniteClipEditor())
                    clipCurveEditor = new ClipCurveEditor(new InfiniteClipCurveDataSource(this), w.TimelineWindow);
            }

            m_HadProblems = false;
            m_InitHadProblems = false;
            m_Bindings = track.outputs.ToArray();
        }

        public override float GetVerticalSpacingBetweenTracks()
        {
            if (track != null && track.isSubTrack)
                return 1.0f; // subtracks have less of a gap than tracks
            return base.GetVerticalSpacingBetweenTracks();
        }

        void UpdateInfiniteClipEditor(AnimationTrack animationTrack, TimelineWindow window)
        {
            if (animationTrack != null && clipCurveEditor == null && animationTrack.ShouldShowInfiniteClipEditor())
                clipCurveEditor = new ClipCurveEditor(new InfiniteClipCurveDataSource(this), window);
        }

        bool IsMuted()
        {
            if (track == null)
                return false;
            if (track is GroupTrack)
                return false;

            return track.muted;
        }

        public override void Draw(Rect headerRect, Rect contentRect, WindowState state)
        {
            UpdateInfiniteClipEditor(track as AnimationTrack, state.GetWindow());

            var trackHeaderRect = headerRect;
            var trackContentRect = contentRect;

            float inlineCurveHeight = contentRect.height - GetTrackContentHeight(state);
            bool hasInlineCurve = inlineCurveHeight > 0.0f;

            if (hasInlineCurve)
            {
                trackHeaderRect.height -= inlineCurveHeight;
                trackContentRect.height -= inlineCurveHeight;
            }

            if (Event.current.type == EventType.Repaint)
            {
                m_TreeViewRect = trackContentRect;

                int newBlendHash = BlendHash();

                if (m_BlendHash != newBlendHash)
                {
                    UpdateClipOverlaps();
                    m_BlendHash = newBlendHash;
                }
            }

            if (s_ArmForRecordContentOn == null)
                s_ArmForRecordContentOn = new GUIContent(TimelineWindow.styles.autoKey.active.background);

            if (s_ArmForRecordContentOff == null)
                s_ArmForRecordContentOff = new GUIContent(TimelineWindow.styles.autoKey.normal.background);

            if (s_ArmForRecordDisabled == null)
                s_ArmForRecordDisabled = new GUIContent(TimelineWindow.styles.autoKey.normal.background, Styles.kArmForRecordDisabled);

            track.SetCollapsed(!isExpanded);

            RebuildGUICacheIfNecessary();

            // Prevents from drawing outside of bounds, but does not effect layout or markers
            bool isOwnerDrawSucceed = false;

            Vector2 visibleTime = state.timeAreaShownRange;

            if (drawer != null)
                isOwnerDrawSucceed = drawer.DrawTrack(trackContentRect, track, visibleTime, state);

            if (!isOwnerDrawSucceed)
            {
                using (new GUIViewportScope(trackContentRect))
                {
                    DrawBackground(trackContentRect, track, visibleTime, state);

                    // draw after user customization so overlay text shows up
                    m_ItemsDrawer.Draw(trackContentRect, drawer, state);
                }

                if (m_InfiniteTrackDrawer != null)
                    m_InfiniteTrackDrawer.DrawTrack(trackContentRect, track, visibleTime, state);
            }

            DrawTrackHeader(trackHeaderRect, state);

            if (hasInlineCurve)
            {
                var curvesHeaderRect = headerRect;
                curvesHeaderRect.yMin = trackHeaderRect.yMax;

                var curvesContentRect = contentRect;
                curvesContentRect.yMin = trackContentRect.yMax;

                DrawInlineCurves(curvesHeaderRect, curvesContentRect, state);
            }

            DrawTrackColorKind(headerRect);

            DrawMuteState(contentRect);
            DrawLockState(track, contentRect);
        }

        void DrawInlineCurves(Rect curvesHeaderRect, Rect curvesContentRect, WindowState state)
        {
            if (!track.GetShowInlineCurves())
                return;

            // Inline curves are not within the editor window -- case 952571
            if (!IsInlineCurvesEditorInBounds(ToWindowSpace(curvesHeaderRect), curvesContentRect.height, state))
            {
                m_InlineCurvesSkipped = true;
                return;
            }

            // If inline curves were skipped during the last event; we want to avoid rendering them until
            // the next Layout event. Otherwise, we still get the RTE prevented above when the user resizes
            // the timeline window very fast. -- case 952571
            if (m_InlineCurvesSkipped && Event.current.type != EventType.Layout)
                return;

            m_InlineCurvesSkipped = false;

            if (inlineCurveEditor == null)
                inlineCurveEditor = new InlineCurveEditor(this);


            curvesHeaderRect.x += DirectorStyles.kBaseIndent;
            curvesHeaderRect.width -= DirectorStyles.kBaseIndent;

            inlineCurveEditor.Draw(curvesHeaderRect, curvesContentRect, state);
        }

        static bool IsInlineCurvesEditorInBounds(Rect windowSpaceTrackRect, float inlineCurveHeight, WindowState state)
        {
            var legalHeight = state.windowHeight;
            var trackTop = windowSpaceTrackRect.y;
            var inlineCurveOffset = windowSpaceTrackRect.height - inlineCurveHeight;
            return legalHeight - trackTop - inlineCurveOffset > 0;
        }

        void DrawErrorIcon(Rect position, WindowState state)
        {
            Rect bindingLabel = position;
            bindingLabel.x = position.xMax + 3;
            bindingLabel.width = state.bindingAreaWidth;
            EditorGUI.LabelField(position, m_ProblemIcon);
        }

        TrackAsset GetTrackForValidation()
        {
            return IsSubTrack() ? ParentTrack() : track;
        }

        bool DetectProblems(WindowState state)
        {
            return state.editSequence.director != null && state.GetWindow().currentMode.ShouldShowTrackBindings(state) && drawer.ValidateBindingForTrack(GetTrackForValidation(), state.editSequence.director, m_Bindings) != null;
        }

        void DrawBackground(Rect trackRect, TrackAsset trackAsset, Vector2 visibleTime, WindowState state)
        {
            bool canDrawRecordBackground = IsRecording(state);
            if (canDrawRecordBackground)
            {
                DrawRecordingTrackBackground(trackRect, trackAsset, visibleTime, state);
            }
            else
            {
                Color trackBackgroundColor;

                if (SelectionManager.Contains(track))
                {
                    trackBackgroundColor = state.IsEditingASubTimeline() ?
                        DirectorStyles.Instance.customSkin.colorTrackSubSequenceBackgroundSelected :
                        DirectorStyles.Instance.customSkin.colorTrackBackgroundSelected;
                }
                else
                {
                    trackBackgroundColor = state.IsEditingASubTimeline() ?
                        DirectorStyles.Instance.customSkin.colorTrackSubSequenceBackground :
                        DirectorStyles.Instance.customSkin.colorTrackBackground;
                }

                EditorGUI.DrawRect(trackRect, trackBackgroundColor);
            }
        }

        float InlineCurveHeight()
        {
            if (!track.GetShowInlineCurves())
                return 0.0f;

            if (!TimelineUtility.TrackHasAnimationCurves(track))
                return 0.0f;

            return TimelineWindowViewPrefs.GetInlineCurveHeight(track);
        }

        public override float GetHeight(WindowState state)
        {
            var height = GetTrackContentHeight(state);

            if (CanDrawInlineCurve())
                height += InlineCurveHeight();

            return height;
        }

        float GetTrackContentHeight(WindowState state)
        {
            float height = drawer.GetHeight(track);
            if (height < 0.0f)
                height = state.trackHeight;

            return height * state.trackScale;
        }

        static bool CanDrawIcon(GUIContent icon)
        {
            return icon != null && icon != GUIContent.none && icon.image != null;
        }

        bool showSceneReference
        {
            get
            {
                if (track == null || IsSubTrack() || m_Bindings.Length == 0)
                    return false;

                var binding = m_Bindings[0];
                return binding.sourceObject != null &&
                    binding.outputTargetType != null &&
                    typeof(Object).IsAssignableFrom(binding.outputTargetType);
            }
        }

        GUIContent headerIcon
        {
            get { return drawer.icon; }
        }

        void DrawTrackHeader(Rect trackHeaderRect, WindowState state)
        {
            using (new GUIViewportScope(trackHeaderRect))
            {
                if (Event.current.type == EventType.Repaint)
                {
                    bool trackHasBindingProblem = DetectProblems(state);
                    RefreshStateIfBindingProblemIsFound(state, trackHasBindingProblem);
                    UpdateBindingProblemValues(trackHasBindingProblem);
                }

                Rect rect = trackHeaderRect;

                DrawHeaderBackground(trackHeaderRect);
                rect.x += m_Styles.trackSwatchStyle.fixedWidth;

                const float buttonSize = WindowConstants.trackHeaderButtonSize;
                const float padding = WindowConstants.trackHeaderButtonPadding;
                var buttonRect = new Rect(trackHeaderRect.xMax - buttonSize - padding, rect.y + ((rect.height - buttonSize) / 2f), buttonSize, buttonSize);

                rect.x += DrawTrackIconKind(rect, state);
                DrawTrackBinding(rect, trackHeaderRect, state);

                if (track is GroupTrack)
                    return;

                buttonRect.x -= DrawTrackDropDownMenu(buttonRect);
                buttonRect.x -= DrawLockMarkersButton(buttonRect, state);
                buttonRect.x -= DrawInlineCurveButton(buttonRect, state);
                buttonRect.x -= DrawMuteButton(buttonRect, state);
                buttonRect.x -= DrawLockButton(buttonRect, state);
                buttonRect.x -= DrawRecordButton(buttonRect, state);
                buttonRect.x -= DrawCustomTrackButton(buttonRect, state);
            }
        }

        void RefreshStateIfBindingProblemIsFound(WindowState state, bool hasProblems)
        {
            if (m_InitHadProblems && m_HadProblems != hasProblems)
            {
                // if the problem state has changed, it may be due to external bindings
                // in that case, we need to tell the graph to rebuild.
                // there is no notification from the engine for some markers that invalidate the graph (addcomponent)
                var message = drawer.ValidateBindingForTrack(GetTrackForValidation(), state.editSequence.director, m_Bindings);
                bool canRefresh = message != null;
                if (canRefresh && !state.playing) // don't rebuild on play
                    state.rebuildGraph = true;
            }
        }

        void UpdateBindingProblemValues(bool hasProblems)
        {
            m_HadProblems = hasProblems;
            m_InitHadProblems = true;
        }

        void DrawHeaderBackground(Rect headerRect)
        {
            Color backgroundColor = SelectionManager.Contains(track)
                ? DirectorStyles.Instance.customSkin.colorSelection
                : DirectorStyles.Instance.customSkin.colorTrackHeaderBackground;

            var bgRect = headerRect;
            bgRect.x += m_Styles.trackSwatchStyle.fixedWidth;
            bgRect.width -= m_Styles.trackSwatchStyle.fixedWidth;

            EditorGUI.DrawRect(bgRect, backgroundColor);
        }

        void DrawTrackColorKind(Rect rect)
        {
            // subtracks don't draw the color, the parent does that.
            if (track != null && track.isSubTrack)
                return;

            using (new GUIColorOverride(drawer.trackColor))
            {
                rect.width = m_Styles.trackSwatchStyle.fixedWidth;
                GUI.Label(rect, GUIContent.none, m_Styles.trackSwatchStyle);
            }
        }

        float DrawTrackIconKind(Rect rect, WindowState state)
        {
            // no icons on subtracks
            if (track != null && track.isSubTrack)
                return 0.0f;

            rect.yMin += (rect.height - 16f) / 2f;
            rect.width = 16.0f;
            rect.height = 16.0f;

            if (m_HadProblems)
            {
                var errorMessage = drawer.ValidateBindingForTrack(GetTrackForValidation(), state.editSequence.director, m_Bindings);
                if (errorMessage != null)
                {
                    m_ProblemIcon.image = DirectorStyles.Instance.warning.normal.background;
                    m_ProblemIcon.tooltip = errorMessage;
                }

                if (CanDrawIcon(m_ProblemIcon))
                    DrawErrorIcon(rect, state);
            }
            else if (CanDrawIcon(headerIcon))
            {
                GUI.Box(rect, headerIcon, GUIStyle.none);
            }

            return rect.width;
        }

        void DrawMuteState(Rect trackRect)
        {
            if (IsMuted())
            {
                var bgRect = trackRect;
                bgRect.x += m_Styles.trackSwatchStyle.fixedWidth;
                bgRect.width -= m_Styles.trackSwatchStyle.fixedWidth;
                EditorGUI.DrawRect(bgRect, DirectorStyles.Instance.customSkin.colorTrackDarken);

                DrawTrackStateBox(trackRect, track);
            }
        }

        void DrawTrackBinding(Rect rect, Rect headerRect, WindowState state)
        {
            if (showSceneReference && state.editSequence.director != null && state.GetWindow().currentMode.ShouldShowTrackBindings(state))
            {
                DoTrackBindingGUI(rect, headerRect, state);
                return;
            }

            var textStyle = m_Styles.trackHeaderFont;
            textStyle.normal.textColor = SelectionManager.Contains(track) ? Color.white : m_Styles.customSkin.colorTrackFont;

            string trackName = track.name;

            EditorGUI.BeginChangeCheck();

            // by default the size is just the width of the string (for selection purposes)
            rect.width = m_Styles.trackHeaderFont.CalcSize(new GUIContent(trackName)).x;

            // if we are editing, supply the entire width of the header
            if (GUIUtility.keyboardControl == track.GetInstanceID())
                rect.width = (headerRect.xMax - rect.xMin) - (5 * WindowConstants.trackHeaderButtonSize);

            trackName = EditorGUI.DelayedTextField(rect, GUIContent.none, track.GetInstanceID(), track.name, textStyle);

            if (EditorGUI.EndChangeCheck())
            {
                TimelineUndo.PushUndo(track, "Rename Track");
                track.name = trackName;
            }
        }

        float DrawTrackDropDownMenu(Rect rect)
        {
            rect.y += 2f;

            if (GUI.Button(rect, GUIContent.none, m_Styles.trackOptions))
            {
                SelectionManager.Clear();
                SelectionManager.Add(track);
                DisplayTrackMenu();
            }

            return WindowConstants.trackHeaderButtonSize;
        }

        float DrawMuteButton(Rect rect, WindowState state)
        {
            if (track.muted)
            {
                if (GUI.Button(rect, GUIContent.none, TimelineWindow.styles.mute))
                {
                    track.muted = false;
                    state.Refresh();
                }
                return WindowConstants.trackHeaderButtonSize;
            }

            return 0.0f;
        }

        bool CanDrawInlineCurve()
        {
            return TimelineUtility.TrackHasAnimationCurves(track);
        }

        float DrawInlineCurveButton(Rect rect, WindowState state)
        {
            if (!CanDrawInlineCurve())
            {
                return 0.0f;
            }

            var newValue = GUI.Toggle(rect, track.GetShowInlineCurves(), GUIContent.none, DirectorStyles.Instance.curves);
            if (newValue != track.GetShowInlineCurves())
            {
                TimelineUndo.PushUndo(track, newValue ? "Show Inline Curves" : "Hide Inline Curves");
                track.SetShowInlineCurves(newValue);
                state.GetWindow().treeView.CalculateRowRects();
            }

            return WindowConstants.trackHeaderButtonSize;
        }

        float DrawRecordButton(Rect rect, WindowState state)
        {
            if (trackAllowsRecording)
            {
                bool isPlayerDisabled = state.editSequence.director != null && !state.editSequence.director.isActiveAndEnabled;
                using (new EditorGUI.DisabledScope(track.lockedInHierarchy || isPlayerDisabled || drawer.ValidateBindingForTrack(GetTrackForValidation(), state.editSequence.director, m_Bindings) != null))
                {
                    if (IsRecording(state))
                    {
                        state.editorWindow.Repaint();
                        float remainder = Time.realtimeSinceStartup % 1;

                        var animatedContent = s_ArmForRecordContentOn;
                        if (remainder < 0.22f)
                        {
                            animatedContent = GUIContent.none;
                        }
                        if (GUI.Button(rect, animatedContent, GUIStyle.none) || isPlayerDisabled)
                        {
                            state.UnarmForRecord(track);
                        }
                    }
                    else
                    {
                        if (GUI.Button(rect, s_ArmForRecordContentOff, GUIStyle.none))
                        {
                            state.ArmForRecord(track);
                        }
                    }
                    return WindowConstants.trackHeaderButtonSize;
                }
            }

            if (showTrackRecordingDisabled)
            {
                using (new EditorGUI.DisabledScope(true))
                    GUI.Button(rect, s_ArmForRecordDisabled, GUIStyle.none);
                return k_ButtonSize;
            }

            return 0.0f;
        }

        float DrawCustomTrackButton(Rect rect, WindowState state)
        {
            if (drawer.DrawTrackHeaderButton(rect, track, state))
            {
                return WindowConstants.trackHeaderButtonSize;
            }
            return 0.0f;
        }

        float DrawLockMarkersButton(Rect rect, WindowState state)
        {
            if (track.GetMarkerCount() == 0)
                return 0.0f;

            var style = TimelineWindow.styles.collapseMarkers;
            if (Event.current.type == EventType.Repaint)
                style.Draw(rect, GUIContent.none, false, false, showMarkers, false);
            if (GUI.Button(rect, DirectorStyles.markerCollapseButton, GUIStyle.none))
            {
                track.SetShowMarkers(!track.GetShowMarkers());
                state.Refresh();
            }
            return WindowConstants.trackHeaderButtonSize;
        }

        static void ObjectBindingField(Rect position, Object obj, PlayableBinding binding)
        {
            bool allowScene =
                typeof(GameObject).IsAssignableFrom(binding.outputTargetType) ||
                typeof(Component).IsAssignableFrom(binding.outputTargetType);

            EditorGUI.BeginChangeCheck();
            // FocusType.Passive so it never gets focused when pressing tab
            int controlId = GUIUtility.GetControlID("s_ObjectFieldHash".GetHashCode(), FocusType.Passive, position);
            var newObject = EditorGUI.DoObjectField(EditorGUI.IndentedRect(position), EditorGUI.IndentedRect(position), controlId, obj, binding.outputTargetType, null, null, allowScene, EditorStyles.objectField);
            if (EditorGUI.EndChangeCheck())
            {
                BindingUtility.Bind(TimelineEditor.inspectedDirector, binding.sourceObject as TrackAsset, newObject);
            }
        }

        void DoTrackBindingGUI(Rect rect, Rect headerRect, WindowState state)
        {
            var spaceUsedByButtons = 2f + ((5 + WindowConstants.trackHeaderButtonPadding) * WindowConstants.trackHeaderButtonSize);

            rect.y += (rect.height - 16.0f) / 2f;
            rect.height = 16f;
            rect.width = (headerRect.xMax - spaceUsedByButtons - rect.xMin);

            var binding = state.editSequence.director.GetGenericBinding(track);
            if (rect.Contains(Event.current.mousePosition) && TimelineDragging.IsDraggingEvent() && DragAndDrop.objectReferences.Length == 1)
            {
                TimelineDragging.HandleBindingDragAndDrop(track, BindingUtility.GetRequiredBindingType(m_Bindings[0]));
            }
            else
            {
                if (m_Bindings[0].outputTargetType != null && typeof(Object).IsAssignableFrom(m_Bindings[0].outputTargetType))
                {
                    ObjectBindingField(rect, binding, m_Bindings[0]);
                }
            }
        }

        bool IsRecording(WindowState state)
        {
            return state.recording && state.IsArmedForRecord(track);
        }

        // background to draw during recording
        void DrawRecordingTrackBackground(Rect trackRect, TrackAsset trackAsset, Vector2 visibleTime, WindowState state)
        {
            if (drawer != null)
                drawer.DrawRecordingBackground(trackRect, trackAsset, visibleTime, state);
        }

        void UpdateClipOverlaps()
        {
            TrackExtensions.ComputeBlendsFromOverlaps(track.clips);
        }

        internal void RebuildGUICacheIfNecessary()
        {
            if (m_TrackHash == track.Hash())
                return;

            m_ItemsDrawer = new TrackItemsDrawer(this);
            m_TrackHash = track.Hash();
        }

        int BlendHash()
        {
            var hash = 0;
            foreach (var clip in track.clips)
            {
                hash = HashUtility.CombineHash(hash,
                    (clip.duration - clip.start).GetHashCode(),
                    ((int)clip.blendInCurveMode).GetHashCode(),
                    ((int)clip.blendOutCurveMode).GetHashCode());
            }
            return hash;
        }

        // callback when the corresponding graph is rebuilt. This can happen, but not have the GUI rebuilt.
        public override void OnGraphRebuilt()
        {
            RefreshCurveEditor();
        }

        void RefreshCurveEditor()
        {
            var animationTrack = track as AnimationTrack;
            var window = TimelineWindow.instance;
            if (animationTrack != null && window != null && window.state != null)
            {
                bool hasEditor = clipCurveEditor != null;
                bool shouldHaveEditor = animationTrack.ShouldShowInfiniteClipEditor();
                if (hasEditor != shouldHaveEditor)
                    window.state.AddEndFrameDelegate((x, currentEvent) =>
                    {
                        x.Refresh();
                        return true;
                    });
            }
        }
    }
}
