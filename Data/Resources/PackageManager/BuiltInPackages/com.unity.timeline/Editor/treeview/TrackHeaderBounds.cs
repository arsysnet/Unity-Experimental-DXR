using UnityEngine;

namespace UnityEditor.Timeline
{
    class TrackHeaderBounds : IBounds
    {
        public TrackHeaderBounds(TimelineTrackBaseGUI track, Rect localRect)
        {
            m_LocalRect = localRect;
            this.track = track;
        }

        Rect m_LocalRect;

        public Rect boundingRect
        {
            get
            {
                var globalRect = m_LocalRect;
                globalRect.position += track.treeViewToWindowTransformation;
                return globalRect;
            }
        }

        public TimelineTrackBaseGUI track { get; }
    }
}
