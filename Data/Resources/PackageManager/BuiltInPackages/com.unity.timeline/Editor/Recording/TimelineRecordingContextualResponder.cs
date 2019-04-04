using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngineInternal;
using UnityEngine.Playables;

namespace UnityEditor.Timeline
{
    class TimelineRecordingContextualResponder : UnityEditorInternal.IAnimationContextualResponder
    {
        public WindowState state { get; internal set; }

        public TimelineRecordingContextualResponder(WindowState _state)
        {
            state = _state;
        }

        //Unsupported stuff
        public bool HasAnyCandidates() { return false; }
        public bool HasAnyCurves() {return false; }
        public void AddCandidateKeys() {}
        public void AddAnimatedKeys() {}

        public bool IsAnimatable(PropertyModification[] modifications)
        {
            // search playable assets
            for (int i = 0; i < modifications.Length; i++)
            {
                var iAsset = modifications[i].target as IPlayableAsset;
                if (iAsset != null)
                {
                    TimelineClip clip = TimelineRecording.FindClipWithAsset(state.editSequence.asset , iAsset , state.editSequence.director);
                    if (clip != null && clip.IsParameterAnimatable(modifications[i].propertyPath))
                        return true;
                }
            }

            // search recordable game objects
            foreach (var gameObject in TimelineRecording.GetRecordableGameObjects(state))
            {
                for (int i = 0; i < modifications.Length; ++i)
                {
                    var modification = modifications[i];
                    if (AnimationWindowUtility.PropertyIsAnimatable(modification.target, modification.propertyPath, gameObject))
                        return true;
                }
            }

            return false;
        }

        public bool IsEditable(UnityEngine.Object targetObject)
        {
            return true; // i.e. all animatable properties are editable
        }

        public bool KeyExists(PropertyModification[] modifications)
        {
            if (modifications.Length == 0 || modifications[0].target == null)
                return false;

            return TimelineRecording.HasKey(modifications, modifications[0].target, state);
        }

        public bool CandidateExists(PropertyModification[] modifications)
        {
            return true;
        }

        public bool CurveExists(PropertyModification[] modifications)
        {
            if (modifications.Length == 0 || modifications[0].target == null)
                return false;

            return TimelineRecording.HasCurve(modifications, modifications[0].target, state);
        }

        public void AddKey(PropertyModification[] modifications)
        {
            TimelineRecording.AddKey(modifications, state);
            state.Refresh();
        }

        public void RemoveKey(PropertyModification[] modifications)
        {
            if (modifications.Length == 0 || modifications[0].target == null)
                return;

            TimelineRecording.RemoveKey(modifications[0].target, modifications, state);
            state.Refresh();
        }

        public void RemoveCurve(PropertyModification[] modifications)
        {
            if (modifications.Length == 0 || modifications[0].target == null)
                return;

            TimelineRecording.RemoveCurve(modifications[0].target, modifications, state);
            state.Refresh();
        }

        public void GoToNextKeyframe(PropertyModification[] modifications)
        {
            if (modifications.Length == 0 || modifications[0].target == null)
                return;

            TimelineRecording.NextKey(modifications[0].target, modifications, state);
            state.Refresh();
        }

        public void GoToPreviousKeyframe(PropertyModification[] modifications)
        {
            TimelineRecording.PrevKey(modifications[0].target, modifications, state);
            state.Refresh();
        }
    }
}
