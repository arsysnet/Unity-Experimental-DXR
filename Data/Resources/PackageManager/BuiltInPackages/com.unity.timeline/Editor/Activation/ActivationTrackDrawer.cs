using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    [CustomTrackDrawer(typeof(ActivationTrack))]
    class ActivationTrackDrawer : TrackDrawer
    {
        static readonly string k_ErrorParentString = LocalizationDatabase.GetLocalizedString("The bound GameObject is a parent of the PlayableDirector.");
        static readonly string k_ErrorString = LocalizationDatabase.GetLocalizedString("The bound GameObject contains the PlayableDirector.");
        internal static class Styles
        {
            public static readonly GUIContent MenuText = EditorGUIUtility.TrTextContent("Add Activation Clip");
            public static readonly GUIContent ClipText = EditorGUIUtility.TrTextContent("Active");
        }

        protected override string DerivedValidateBindingForTrack(PlayableDirector director,
            TrackAsset trackToValidate, PlayableBinding[] bindings)
        {
            var binding = director.GetGenericBinding(bindings.First().sourceObject);
            var gameObject = binding as GameObject;
            if (gameObject != null)
            {
                var dire = gameObject.GetComponent<PlayableDirector>();
                if (dire == director)
                {
                    return k_ErrorString;
                }

                if (director.gameObject.transform.IsChildOf(gameObject.transform))
                {
                    return k_ErrorParentString;
                }
            }

            return null;
        }
    }
}
