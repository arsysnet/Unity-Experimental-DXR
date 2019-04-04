using UnityEditorInternal;
using UnityEngine;

namespace UnityEditor.Timeline
{
    class TimelineClipHandle : IBounds
    {
        Rect m_Rect;
        readonly TimelineClipGUI m_ClipGUI;
        readonly TrimEdge m_TrimDirection;

        public Rect boundingRect
        {
            get { return m_ClipGUI.parent.ToWindowSpace(m_Rect); }
        }

        public TrimEdge trimDirection
        {
            get { return m_TrimDirection; }
        }

        public TimelineClipGUI clipGUI
        {
            get { return m_ClipGUI; }
        }

        public TimelineClipHandle(TimelineClipGUI theClipGUI, TrimEdge trimDirection)
        {
            m_TrimDirection = trimDirection;
            m_ClipGUI = theClipGUI;
        }

        public void Draw(Rect clientRect, float width)
        {
            Rect handleRect = clientRect;
            handleRect.width = width;

            if (m_TrimDirection == TrimEdge.End)
                handleRect.x = clientRect.xMax - width;

            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.SplitResizeLeftRight);

            m_Rect = handleRect;
        }
    }
}
