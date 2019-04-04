using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    class TimelineMarkerHeaderContextMenu : Manipulator
    {
        protected override bool ContextClick(Event evt, WindowState state)
        {
            if (!(state.GetWindow().markerHeaderRect.Contains(evt.mousePosition)
                  || state.GetWindow().markerContentRect.Contains(evt.mousePosition)))
                return false;

            ShowMenu(evt.mousePosition, state);
            return true;
        }

        public static void ShowMenu(Vector2? mousePosition, WindowState state)
        {
            var menu = new GenericMenu();
            ContextMenus.markerHeaderMenu.AddToMenu(menu, state);
            var timeline = state.editSequence.asset;
            var markerTypes = TypeUtility.GetMarkerTypes(); // Marker track supports all Markers for now
            if (markerTypes.Any())
            {
                menu.AddSeparator(string.Empty);
                var time = TimelineHelpers.GetCandidateTime(state, mousePosition);
                var addMarkerCommand = new Func<Type, IMarker>(type => AddMarkerCommand(type, time, state));
                var enabled = timeline.markerTrack == null || !timeline.markerTrack.lockedInHierarchy;
                SequencerContextMenu.AddMarkerMenuCommands(menu, markerTypes, addMarkerCommand, enabled);
            }

            if (timeline.markerTrack != null)
                SequencerContextMenu.RemoveInvalidMarkersMenuItem(menu, timeline.markerTrack);

            menu.ShowAsContext();
        }

        static IMarker AddMarkerCommand(Type markerType, double time, WindowState state)
        {
            var timeline = state.editSequence.asset;
            timeline.CreateMarkerTrack();
            var markerTrack = timeline.markerTrack;

            var marker = SequencerContextMenu.AddMarkerCommand(markerTrack, markerType, time);

            if (typeof(INotification).IsAssignableFrom(markerType))
            {
                // If we have no binding for the Notifications, set it to the director GO
                var director = state.editSequence.director;
                if (director != null && director.GetGenericBinding(markerTrack) == null)
                    director.SetGenericBinding(markerTrack, director.gameObject);
            }

            return marker;
        }
    }
}
