using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    class TimelineClipGUI : TimelineItemGUI, IClipCurveEditorOwner, ISnappable, IAttractable
    {
        EditorClip m_EditorItem;

        Rect m_ClipCenterSection;
        readonly List<Rect> m_LoopRects = new List<Rect>();

        int m_ProjectedClipHash;
        readonly TimelineClipHandle m_LeftHandle;
        readonly TimelineClipHandle m_RightHandle;
        ClipDrawData m_ClipDrawData;
        Rect m_MixOutRect = new Rect();
        Rect m_MixInRect = new Rect();
        int m_MinLoopIndex = 1;

        bool supportResize { get; }
        public ClipCurveEditor clipCurveEditor { get; set; }
        public TimelineClipGUI previousClip { get; set; }
        public TimelineClipGUI nextClip { get; set; }

        static readonly float k_MinMixWidth = 2;
        static readonly float k_MaxHandleWidth = 10f;
        static readonly float k_MinHandleWidth = 1f;

        bool? m_ShowDrillIcon;

        static readonly IconData[] k_DiggableClipIcons =
        {
            new IconData
            {
                icon = EditorGUIUtility.LoadIconRequired("TimelineDigIn"),
                tint = Color.white
            }
        };

        public List<Rect> loopRects
        {
            get { return m_LoopRects; }
        }

        bool overlaps
        {
            get { return clip.hasBlendIn; }
        }

        bool isOverlapped
        {
            get { return clip.hasBlendOut; }
        }

        string name
        {
            get
            {
                if (string.IsNullOrEmpty(clip.displayName))
                    return "(Empty)";

                return clip.displayName;
            }
        }

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

        public Rect mixOutRect
        {
            get
            {
                float percent = clip.mixOutPercentage;
                m_MixOutRect.Set(treeViewRect.width * (1 - percent), 0.0f, treeViewRect.width * percent, treeViewRect.height);
                return m_MixOutRect;
            }
        }

        public Rect mixInRect
        {
            get
            {
                m_MixInRect.Set(0.0f, 0.0f, treeViewRect.width * clip.mixInPercentage, treeViewRect.height);
                return m_MixInRect;
            }
        }

        internal BlendKind blendInKind
        {
            get
            {
                if (mixInRect.width > k_MinMixWidth && overlaps)
                    return BlendKind.Mix;

                if (mixInRect.width > k_MinMixWidth)
                    return BlendKind.Ease;

                return BlendKind.None;
            }
        }

        internal BlendKind blendOutKind
        {
            get
            {
                if (mixOutRect.width > k_MinMixWidth && isOverlapped)
                    return BlendKind.Mix;

                if (mixOutRect.width > k_MinMixWidth)
                    return BlendKind.Ease;

                return BlendKind.None;
            }
        }

        public override double start
        {
            get { return clip.start; }
        }

        public override double end
        {
            get { return clip.end; }
        }

        public bool supportsLooping
        {
            get { return clip.SupportsLooping(); }
        }

        // for the inline curve editor, only show loops if we recorded the asset
        bool IClipCurveEditorOwner.showLoops
        {
            get { return clip.SupportsLooping() && (clip.asset is AnimationPlayableAsset);  }
        }

        TrackAsset IClipCurveEditorOwner.owner
        {
            get { return clip.parentTrack; }
        }


        public int minLoopIndex
        {
            get { return m_MinLoopIndex; }
        }

        public Rect clipCenterSection
        {
            get { return m_ClipCenterSection; }
        }

        public TrackDrawer drawer
        {
            get { return ((TimelineTrackGUI)parent).drawer; }
        }

        public Rect clippedRect { get; private set; }

        public override void Select()
        {
            zOrder = zOrderProvider.Next();
            SelectionManager.Add(clip);
        }

        public override bool IsSelected()
        {
            return SelectionManager.Contains(clip);
        }

        public override void Deselect()
        {
            SelectionManager.Remove(clip);
        }

        public override ITimelineItem item
        {
            get { return ItemsUtils.ToItem(clip); }
        }

        IZOrderProvider zOrderProvider { get; }

        public TimelineClipGUI(TimelineClip clip, IRowGUI parent, IZOrderProvider provider) : base(parent)
        {
            zOrderProvider = provider;
            zOrder = provider.Next();

            m_EditorItem = EditorClipFactory.GetEditorClip(clip);

            clip.dirtyHash = 0;

            supportResize = true;

            m_LeftHandle = new TimelineClipHandle(this, TrimEdge.Start);
            m_RightHandle = new TimelineClipHandle(this, TrimEdge.End);

            ItemToItemGui.Add(clip, this);
        }

        void CreateInlineCurveEditor(WindowState state)
        {
            if (clipCurveEditor != null)
                return;

            var animationClip = clip.animationClip;

            if (animationClip != null && animationClip.empty)
                animationClip = null;

            // prune out clips coming from FBX
            if (animationClip != null && !clip.recordable)
                return; // don't show, even if there are curves

            if (clip.curves != null || animationClip != null)
            {
                state.AddEndFrameDelegate((istate, currentEvent) =>
                {
                    clipCurveEditor = new ClipCurveEditor(new TimelineClipCurveDataSource(this), TimelineWindow.instance);
                    return true;
                });
            }
        }

        public TimelineClip clip
        {
            get { return m_EditorItem.clip; }
        }

        int ComputeClipHash()
        {
            return HashUtility.CombineHash(clip.clipAssetDuration.GetHashCode(), clip.duration.GetHashCode() , clip.timeScale.GetHashCode() , clip.start.GetHashCode());
        }

        // Draw the actual clip. Defers to the track drawer for customization
        void DrawClipByDrawer(WindowState state, Rect drawRect, string title, bool selected, float rectXOffset)
        {
            m_ClipDrawData.uiClip = this;
            m_ClipDrawData.clip = clip;
            m_ClipDrawData.targetRect = drawRect;
            m_ClipDrawData.clipCenterSection = m_ClipCenterSection;
            m_ClipDrawData.unclippedRect = treeViewRect;
            m_ClipDrawData.title = title;
            m_ClipDrawData.selected = selected;
            m_ClipDrawData.inlineCurvesSelected = inlineCurvesSelected;
            m_ClipDrawData.state = state;
            m_ClipDrawData.previousClip = previousClip != null ? previousClip.clip : null;

            Vector3 shownAreaTime = state.timeAreaShownRange;
            m_ClipDrawData.localVisibleStartTime = clip.ToLocalTimeUnbound(Math.Max(clip.start, shownAreaTime.x));
            m_ClipDrawData.localVisibleEndTime = clip.ToLocalTimeUnbound(Math.Min(clip.end, shownAreaTime.y));

            m_ClipDrawData.clippedRect = new Rect(clippedRect.x - rectXOffset, 0.0f, clippedRect.width, clippedRect.height);

            m_ClipDrawData.rightIcons = ShowDrillIcon(state.editSequence.director) ? k_DiggableClipIcons : null;

            // temporary workaround for waveforms
            m_ClipDrawData.trackDrawer = drawer;

            drawer.DrawClip(m_ClipDrawData);
        }

        void DrawInto(Rect drawRect, WindowState state)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // create the inline curve editor if not already created
            CreateInlineCurveEditor(state);

            // @todo optimization, most of the calculations (rect, offsets, colors, etc.) could be cached
            // and rebuilt when the hash of the clip changes.

            if (isInvalid)
            {
                drawer.DrawInvalidClip(this, treeViewRect);
                return;
            }

            GUI.BeginClip(drawRect);

            var originRect = new Rect(0.0f, 0.0f, drawRect.width, drawRect.height);
            string clipLabel = name;
            bool selected = SelectionManager.Contains(clip);

            if (selected && !Equals(1.0, clip.timeScale))
                clipLabel += " " + clip.timeScale.ToString("F2") + "x";

            DrawClipByDrawer(state, originRect, clipLabel, selected, drawRect.x);

            GUI.EndClip();

            if (clip.parentTrack != null && !clip.parentTrack.lockedInHierarchy)
            {
                if (selected && supportResize)
                {
                    var cursorRect = boundingRect;
                    cursorRect.xMin += m_LeftHandle.boundingRect.width;
                    cursorRect.xMax -= m_RightHandle.boundingRect.width;
                    EditorGUIUtility.AddCursorRect(cursorRect, MouseCursor.MoveArrow);
                }

                if (supportResize)
                {
                    var handleWidth = Mathf.Clamp(drawRect.width * 0.3f, k_MinHandleWidth, k_MaxHandleWidth);

                    m_LeftHandle.Draw(drawRect, handleWidth);
                    m_RightHandle.Draw(drawRect, handleWidth);

                    state.spacePartitioner.AddBounds(m_LeftHandle);
                    state.spacePartitioner.AddBounds(m_RightHandle);
                }
            }
        }

        void CalculateClipRectangle(Rect trackRect, WindowState state, int projectedClipHash)
        {
            if (m_ProjectedClipHash == projectedClipHash)
            {
                if (Event.current.type == EventType.Repaint && !parent.locked)
                    state.spacePartitioner.AddBounds(this);
                return;
            }

            m_ProjectedClipHash = projectedClipHash;
            var clipRect = RectToTimeline(trackRect, state);

            treeViewRect = clipRect;

            if (Event.current.type == EventType.Repaint && !parent.locked)
                state.spacePartitioner.AddBounds(this);

            // calculate clipped rect
            clipRect.xMin = Mathf.Max(clipRect.xMin, trackRect.xMin);
            clipRect.xMax = Mathf.Min(clipRect.xMax, trackRect.xMax);

            if (clipRect.width > 0 && clipRect.width < 2)
            {
                clipRect.width = 5.0f;
            }

            clippedRect = clipRect;
        }

        void CalculateBlendRect()
        {
            m_ClipCenterSection = treeViewRect;
            m_ClipCenterSection.x = 0;
            m_ClipCenterSection.y = 0;

            m_ClipCenterSection.xMin = treeViewRect.width * clip.mixInPercentage;

            m_ClipCenterSection.width = treeViewRect.width;
            m_ClipCenterSection.xMax -= mixOutRect.width;
            m_ClipCenterSection.xMax -= (treeViewRect.width * clip.mixInPercentage);
        }

        // Entry point to the Clip Drawing...
        public override void Draw(Rect trackRect, TrackDrawer drawer, WindowState state)
        {
            if (SelectionManager.Contains(clip))
                clip.dirtyHash = 0;

            // compute dirty hash, depends on the clip and the timeline
            int dirtyHash = HashUtility.CombineHash(ComputeClipHash(), state.timeAreaTranslation.GetHashCode(), state.timeAreaScale.GetHashCode(), trackRect.GetHashCode());

            // update the clip projected rectangle on the timeline
            CalculateClipRectangle(trackRect, state, dirtyHash);
            // update the blend rects (when clip overlaps with others)
            CalculateBlendRect();
            // update the loop rects (when clip loops)
            CalculateLoopRects(trackRect, state, dirtyHash);

            clip.dirtyHash = dirtyHash;

            if (drawer.canDrawExtrapolationIcon)
                DrawExtrapolation(trackRect, treeViewRect);

            DrawInto(treeViewRect, state);
        }

        GUIStyle GetExtrapolationIcon(TimelineClip.ClipExtrapolation mode)
        {
            GUIStyle extrapolationIcon = null;

            switch (mode)
            {
                case TimelineClip.ClipExtrapolation.None: return null;
                case TimelineClip.ClipExtrapolation.Hold: extrapolationIcon = m_Styles.extrapolationHold; break;
                case TimelineClip.ClipExtrapolation.Loop: extrapolationIcon = m_Styles.extrapolationLoop; break;
                case TimelineClip.ClipExtrapolation.PingPong: extrapolationIcon = m_Styles.extrapolationPingPong; break;
                case TimelineClip.ClipExtrapolation.Continue: extrapolationIcon = m_Styles.extrapolationContinue; break;
            }

            return extrapolationIcon;
        }

        Rect GetPreExtrapolationBounds(Rect trackRect, Rect clipRect, GUIStyle icon)
        {
            float x = clipRect.xMin - (icon.fixedWidth + 10.0f);
            float y = trackRect.yMin + (trackRect.height - icon.fixedHeight) / 2.0f;

            if (previousClip != null)
            {
                float distance = Mathf.Abs(treeViewRect.xMin - previousClip.treeViewRect.xMax);

                if (distance < icon.fixedWidth)
                    return new Rect(0.0f, 0.0f, 0.0f, 0.0f);

                if (distance < icon.fixedWidth + 20.0f)
                {
                    float delta = (distance - icon.fixedWidth) / 2.0f;
                    x = clipRect.xMin - (icon.fixedWidth + delta);
                }
            }

            return new Rect(x, y, icon.fixedWidth, icon.fixedHeight);
        }

        Rect GetPostExtrapolationBounds(Rect trackRect, Rect clipRect, GUIStyle icon)
        {
            float x = clipRect.xMax + 10.0f;
            float y = trackRect.yMin + (trackRect.height - icon.fixedHeight) / 2.0f;

            if (nextClip != null)
            {
                float distance = Mathf.Abs(nextClip.treeViewRect.xMin - treeViewRect.xMax);

                if (distance < icon.fixedWidth)
                    return new Rect(0.0f, 0.0f, 0.0f, 0.0f);

                if (distance < icon.fixedWidth + 20.0f)
                {
                    float delta = (distance - icon.fixedWidth) / 2.0f;
                    x = clipRect.xMax + delta;
                }
            }

            return new Rect(x, y, icon.fixedWidth, icon.fixedHeight);
        }

        static void DrawExtrapolationIcon(Rect rect, GUIStyle icon)
        {
            GUI.Label(rect, GUIContent.none, icon);
        }

        void DrawExtrapolation(Rect trackRect, Rect clipRect)
        {
            if (clip.hasPreExtrapolation)
            {
                GUIStyle icon = GetExtrapolationIcon(clip.preExtrapolationMode);

                if (icon != null)
                {
                    Rect iconBounds = GetPreExtrapolationBounds(trackRect, clipRect, icon);

                    if (iconBounds.width > 1 && iconBounds.height > 1)
                        DrawExtrapolationIcon(iconBounds, icon);
                }
            }

            if (clip.hasPostExtrapolation)
            {
                GUIStyle icon = GetExtrapolationIcon(clip.postExtrapolationMode);

                if (icon != null)
                {
                    Rect iconBounds = GetPostExtrapolationBounds(trackRect, clipRect, icon);

                    if (iconBounds.width > 1 && iconBounds.height > 1)
                        DrawExtrapolationIcon(iconBounds, icon);
                }
            }
        }

        static Rect ProjectRectOnTimeline(Rect rect, Rect trackRect, WindowState state)
        {
            Rect newRect = rect;
            // transform clipRect into pixel-space
            newRect.x *= state.timeAreaScale.x;
            newRect.width *= state.timeAreaScale.x;

            newRect.x += state.timeAreaTranslation.x + trackRect.xMin;

            // adjust clipRect height and vertical centering
            const int clipPadding = 2;
            newRect.y = trackRect.y + clipPadding;
            newRect.height = trackRect.height - (2 * clipPadding);
            return newRect;
        }

        void CalculateLoopRects(Rect trackRect, WindowState state, int currentClipHash)
        {
            if (clip.dirtyHash == currentClipHash)
                return;

            if (clip.duration < WindowState.kTimeEpsilon)
                return;

            m_LoopRects.Clear();

            var times = TimelineHelpers.GetLoopTimes(clip);
            var loopDuration = TimelineHelpers.GetLoopDuration(clip);
            m_MinLoopIndex = -1;

            // we have a hold, no need to compute all loops
            if (!supportsLooping)
            {
                if (times.Length > 1)
                {
                    var t = times[1];
                    float loopTime = (float)(clip.duration - t);
                    m_LoopRects.Add(ProjectRectOnTimeline(new Rect((float)(t + clip.start), 0, loopTime, 0), trackRect, state));
                }
                return;
            }

            var range = state.timeAreaShownRange;
            var visibleStartTime = range.x - clip.start;
            var visibleEndTime = range.y - clip.start;

            for (int i = 1; i < times.Length; i++)
            {
                var t = times[i];

                // don't draw off screen loops
                if (t > visibleEndTime)
                    break;

                float loopTime = Mathf.Min((float)(clip.duration - t), (float)loopDuration);
                var loopEnd = t + loopTime;

                if (loopEnd < visibleStartTime)
                    continue;

                m_LoopRects.Add(ProjectRectOnTimeline(new Rect((float)(t + clip.start), 0, loopTime, 0), trackRect, state));

                if (m_MinLoopIndex == -1)
                    m_MinLoopIndex = i;
            }
        }

        public override Rect RectToTimeline(Rect trackRect, WindowState state)
        {
            var offsetFromTimeSpaceToPixelSpace = state.timeAreaTranslation.x + trackRect.xMin;

            var start = (float)(DiscreteTime)clip.start;
            var end = (float)(DiscreteTime)clip.end;

            return Rect.MinMaxRect(
                Mathf.Round(start * state.timeAreaScale.x + offsetFromTimeSpaceToPixelSpace), Mathf.Round(trackRect.yMin),
                Mathf.Round(end * state.timeAreaScale.x + offsetFromTimeSpaceToPixelSpace), Mathf.Round(trackRect.yMax)
            );
        }

        public IEnumerable<Edge> SnappableEdgesFor(IAttractable attractable, ManipulateEdges manipulateEdges)
        {
            var edges = new List<Edge>();

            bool canAddEdges = !parent.muted;

            if (canAddEdges)
            {
                // Hack: Trim Start in Ripple mode should not have any snap point added
                if (EditMode.editType == EditMode.EditType.Ripple && manipulateEdges == ManipulateEdges.Left)
                    return edges;

                if (attractable != this)
                {
                    if (EditMode.editType == EditMode.EditType.Ripple)
                    {
                        bool skip = false;

                        // Hack: Since Trim End and Move in Ripple mode causes other snap point to move on the same track (which is not supported), disable snapping for this special cases...
                        // TODO Find a proper way to have different snap edges for each edit mode.
                        if (manipulateEdges == ManipulateEdges.Right)
                        {
                            var otherClipGUI = attractable as TimelineClipGUI;
                            skip = otherClipGUI != null && otherClipGUI.parent == parent;
                        }
                        else if (manipulateEdges == ManipulateEdges.Both)
                        {
                            var moveHandler = attractable as MoveItemHandler;
                            skip = moveHandler != null && moveHandler.movingItems.Any(clips => clips.targetTrack == clip.parentTrack && clip.start >= clips.start);
                        }

                        if (skip)
                            return edges;
                    }

                    AddEdge(edges, clip.start);
                    AddEdge(edges, clip.end);
                }
                else
                {
                    if (manipulateEdges == ManipulateEdges.Right)
                    {
                        var d = TimelineHelpers.GetClipAssetEndTime(clip);

                        if (d < double.MaxValue)
                        {
                            if (clip.SupportsLooping())
                            {
                                var l = TimelineHelpers.GetLoopDuration(clip);

                                var shownTime = TimelineWindow.instance.state.timeAreaShownRange;
                                do
                                {
                                    AddEdge(edges, d, false);
                                    d += l;
                                }
                                while (d < shownTime.y);
                            }
                            else
                            {
                                AddEdge(edges, d, false);
                            }
                        }
                    }

                    if (manipulateEdges == ManipulateEdges.Left)
                    {
                        var clipInfo = AnimationClipCurveCache.Instance.GetCurveInfo(clip.animationClip);
                        if (clipInfo != null && clipInfo.keyTimes.Any())
                            AddEdge(edges, clip.FromLocalTimeUnbound(clipInfo.keyTimes.Min()), false);
                    }
                }
            }
            return edges;
        }

        public bool ShouldSnapTo(ISnappable snappable)
        {
            return true;
        }

        bool ShowDrillIcon(IExposedPropertyTable resolver)
        {
            if (!m_ShowDrillIcon.HasValue || TimelineWindow.instance.hierarchyChangedThisFrame)
            {
                var nestable = clip.asset as IDirectorDriver;
                m_ShowDrillIcon = resolver != null &&
                    !resolver.Equals(null) &&              // Testing against Unity null
                    nestable != null &&
                    nestable.GetDrivenDirectors(resolver).Any();
            }

            return m_ShowDrillIcon.Value;
        }

        static void AddEdge(List<Edge> edges, double time, bool showEdgeHint = true)
        {
            var shownTime = TimelineWindow.instance.state.timeAreaShownRange;
            if (time >= shownTime.x && time <= shownTime.y)
                edges.Add(new Edge(time, showEdgeHint));
        }
    }
}
