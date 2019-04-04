using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Timeline
{
    static class PickerUtils
    {
        public static List<object> pickedElements { get; private set; }

        public static void DoPick(WindowState state, Vector2 mousePosition)
        {
            if (state.GetWindow().sequenceRect.Contains(mousePosition))
            {
                pickedElements = state.spacePartitioner.GetItemsAtPosition<object>(mousePosition).ToList();
            }
            else
            {
                if (pickedElements != null)
                    pickedElements.Clear();
                else
                    pickedElements = new List<object>();
            }
        }

        public static ISelectable PickedSelectable()
        {
            if (pickedElements.OfType<TrackHeaderBounds>().Any())
                return null;

            return PickedSelectableOfType<ISelectable>();
        }

        public static T PickedSelectableOfType<T>() where T : ISelectable
        {
            return pickedElements.OfType<T>().OrderBy(x => x.zOrder).LastOrDefault();
        }

        public static TimelineClipHandle PickedClipGUIHandle()
        {
            if (pickedElements.OfType<TrackHeaderBounds>().Any())
                return null;

            return pickedElements.OfType<TimelineClipHandle>().OrderBy(x => x.clipGUI.zOrder).LastOrDefault();
        }

        public static InlineCurveResizeHandle PickedInlineCurveResizer()
        {
            return pickedElements.FirstOrDefault(e => e is InlineCurveResizeHandle) as InlineCurveResizeHandle;
        }

        public static TimelineTrackBaseGUI PickedTrackBaseGUI()
        {
            var header = pickedElements.FirstOrDefault(e => e is TrackHeaderBounds) as TrackHeaderBounds;
            if (header != null)
                return header.track;

            return pickedElements.FirstOrDefault(e => e is TimelineTrackBaseGUI) as TimelineTrackBaseGUI;
        }
    }
}
