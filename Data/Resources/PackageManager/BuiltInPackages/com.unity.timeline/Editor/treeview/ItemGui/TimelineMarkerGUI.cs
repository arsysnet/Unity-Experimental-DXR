using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.Timeline
{
    class TimelineMarkerGUI : TimelineItemGUI, ISnappable, IAttractable
    {
        int m_ProjectedClipHash;
        int m_MarkerHash;
        bool m_Selectable;
        public event Action onStartDrag;

        IMarker marker { get; }

        bool selectable
        {
            get { return m_Selectable; }
        }

        public double time
        {
            get { return marker.time; }
        }

        public override double start
        {
            get { return time; }
        }

        public override double end
        {
            get { return time; }
        }

        public override void Select()
        {
            zOrder = zOrderProvider.Next();
            SelectionManager.Add(marker);
            TimelineWindowViewPrefs.GetTrackViewModelData(parent.asset).markerTimeStamps[m_MarkerHash] = DateTime.UtcNow.Ticks;
        }

        public override bool IsSelected()
        {
            return SelectionManager.Contains(marker);
        }

        public override void Deselect()
        {
            SelectionManager.Remove(marker);
        }

        public override ITimelineItem item
        {
            get { return ItemsUtils.ToItem(marker); }
        }

        IZOrderProvider zOrderProvider { get; }

        public TimelineMarkerGUI(IMarker theMarker, IRowGUI parent, IZOrderProvider provider) : base(parent)
        {
            marker = theMarker;
            m_Selectable = marker.GetType().IsSubclassOf(typeof(UnityObject));

            m_MarkerHash = 0;
            var o = marker as object;
            if (!o.Equals(null))
                m_MarkerHash = o.GetHashCode();

            zOrderProvider = provider;
            zOrder = zOrderProvider.Next();
            ItemToItemGui.Add(marker, this);
        }

        int ComputeDirtyHash()
        {
            return time.GetHashCode();
        }

        static void DrawMarker(Rect drawRect, Type type, bool isSelected, bool isCollapsed)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var style = StyleManager.UssStyleForType(type);
                style.Draw(drawRect, GUIContent.none, false, false, !isCollapsed, isSelected);
            }
        }

        public override void Draw(Rect trackRect, TrackDrawer drawer, WindowState state)
        {
            // compute marker hash
            var currentMarkerHash = ComputeDirtyHash();

            // compute timeline hash
            var currentTimelineHash = state.timeAreaTranslation.GetHashCode() ^ state.timeAreaScale.GetHashCode() ^ trackRect.GetHashCode();

            // update the clip projected rectangle on the timeline
            CalculateClipRectangle(trackRect, state, currentMarkerHash ^ currentTimelineHash);

            var isSelected = selectable && SelectionManager.Contains(marker);
            var showMarkers = parent.showMarkers;
            DrawMarker(treeViewRect, marker.GetType(), isSelected, !showMarkers);

            if (Event.current.type == EventType.Repaint && showMarkers && !parent.locked)
                state.spacePartitioner.AddBounds(this);
        }

        public override void StartDrag()
        {
            if (onStartDrag != null)
                onStartDrag.Invoke();
        }

        void CalculateClipRectangle(Rect trackRect, WindowState state, int projectedClipHash)
        {
            if (m_ProjectedClipHash == projectedClipHash)
                return;

            m_ProjectedClipHash = projectedClipHash;
            treeViewRect = RectToTimeline(trackRect, state);
        }

        public override Rect RectToTimeline(Rect trackRect, WindowState state)
        {
            var width = StyleManager.UssStyleForType(marker.GetType()).fixedWidth;
            var height = trackRect.height;
            var x = ((float)marker.time * state.timeAreaScale.x) + state.timeAreaTranslation.x + trackRect.xMin;
            x -= 0.5f * width;
            return new Rect(x, trackRect.y, width, height);
        }

        public IEnumerable<Edge> SnappableEdgesFor(IAttractable attractable, ManipulateEdges manipulateEdges)
        {
            var edges = new List<Edge>();
            var attractableGUI = attractable as TimelineMarkerGUI;
            var canAddEdges = !(attractableGUI != null && attractableGUI.parent == parent);
            if (canAddEdges)
                edges.Add(new Edge(time));
            return edges;
        }

        public bool ShouldSnapTo(ISnappable snappable)
        {
            return snappable != this;
        }
    }
}
