using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    class MovingItems : ItemsPerTrack
    {
        TrackAsset m_ReferenceTrack;
        readonly bool m_AllowTrackSwitch;

        readonly Rect[] m_ItemsBoundsOnTrack;
        readonly Vector2[] m_ItemsMouseOffsets;

        static readonly Rect s_InvisibleBounds = new Rect(float.MaxValue, float.MaxValue, 0.0f, 0.0f);

        public TrackAsset originalTrack { get; }

        public override TrackAsset targetTrack
        {
            get
            {
                if (m_AllowTrackSwitch)
                    return m_ReferenceTrack;

                return originalTrack;
            }
        }

        public bool canDrop;

        public TrackAsset parentTrack
        {
            get { return m_ItemsGroup.items.First().parentTrack; }
        }

        public double start
        {
            get { return m_ItemsGroup.start; }
            set { m_ItemsGroup.start = value; }
        }

        public double end
        {
            get { return m_ItemsGroup.end; }
        }

        public Rect[] onTrackItemsBounds
        {
            get { return m_ItemsBoundsOnTrack; }
        }

        public MovingItems(WindowState state, TrackAsset parentTrack, ITimelineItem[] items, TrackAsset referenceTrack, Vector2 mousePosition, bool allowTrackSwitch)
            : base(parentTrack, items)
        {
            originalTrack = parentTrack;
            m_ReferenceTrack = referenceTrack;
            m_AllowTrackSwitch = allowTrackSwitch;

            m_ItemsBoundsOnTrack = new Rect[items.Length];
            m_ItemsMouseOffsets = new Vector2[items.Length];

            for (int i = 0; i < items.Length; ++i)
            {
                var itemGUi = items[i].gui;

                if (itemGUi != null)
                {
                    m_ItemsBoundsOnTrack[i] = itemGUi.boundingRect;
                    m_ItemsMouseOffsets[i] = mousePosition - m_ItemsBoundsOnTrack[i].position;
                }
            }

            canDrop = true;
        }

        public void SetReferenceTrack(TrackAsset track)
        {
            m_ReferenceTrack = track;
        }

        public void RefreshBounds(WindowState state, Vector2 mousePosition)
        {
            for (int i = 0; i < m_ItemsGroup.items.Length; ++i)
            {
                var item = m_ItemsGroup.items[i];
                var itemGUI = item.gui;

                if (item.parentTrack != null)
                {
                    m_ItemsBoundsOnTrack[i] = itemGUI.visible ? itemGUI.boundingRect : s_InvisibleBounds;
                }
                else
                {
                    if (targetTrack != null)
                    {
                        var trackGUI = (TimelineTrackGUI)TimelineWindow.instance.allTracks.FirstOrDefault(t => t.track == targetTrack);
                        if (trackGUI == null) return;
                        var trackRect = trackGUI.boundingRect;
                        m_ItemsBoundsOnTrack[i] = itemGUI.RectToTimeline(trackRect, state);
                    }
                    else
                    {
                        m_ItemsBoundsOnTrack[i].position = mousePosition - m_ItemsMouseOffsets[i];
                    }
                }
            }
        }

        public void Draw(bool isValid)
        {
            for (int i = 0; i < m_ItemsBoundsOnTrack.Length; ++i)
            {
                var rect = m_ItemsBoundsOnTrack[i];
                DrawItemInternal(m_ItemsGroup.items[i], rect, isValid);
            }
        }

        static void DrawItemInternal(ITimelineItem item, Rect rect, bool isValid)
        {
            var clipGUI = item.gui as TimelineClipGUI;

            if (clipGUI != null)
            {
                if (isValid)
                    clipGUI.drawer.DrawGhostClip(clipGUI, rect);
                else
                    clipGUI.drawer.DrawInvalidClip(clipGUI, rect);
            }
        }
    }
}
