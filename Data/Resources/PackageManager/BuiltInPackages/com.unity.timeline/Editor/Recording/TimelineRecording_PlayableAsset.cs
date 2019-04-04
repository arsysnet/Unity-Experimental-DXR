using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

namespace UnityEditor.Timeline
{
    // Handles Undo animated properties on PlayableAssets from clips to create parameter animation

    partial class TimelineRecording
    {
        internal static bool HasAnyPlayableAssetModifications(UndoPropertyModification[] modifications)
        {
            return modifications.Any(x => (TimelineRecording.GetTarget(x) as IPlayableAsset) != null);
        }

        internal static UndoPropertyModification[] ProcessPlayableAssetModification(UndoPropertyModification[] modifications, WindowState state)
        {
            // can't record without a director since the asset being modified might be a scene instance
            if (state == null || state.editSequence.director == null)
                return modifications;

            var remaining = new List<UndoPropertyModification>();
            foreach (UndoPropertyModification mod in modifications)
            {
                var clip = FindClipWithAsset(state.editSequence.asset, TimelineRecording.GetTarget(mod) as IPlayableAsset, state.editSequence.director);
                if (clip == null || !IsRecording(clip, state) || !ProcessPlayableAssetRecording(mod, state, clip))
                    remaining.Add(mod);
            }

            if (remaining.Count() != modifications.Length)
            {
                state.rebuildGraph = true;
                state.GetWindow().Repaint();
            }

            return remaining.ToArray();
        }

        internal static TimelineClip FindClipWithAsset(TimelineAsset asset, IPlayableAsset target, PlayableDirector director)
        {
            if (target == null || asset == null || director == null)
                return null;

            var clips = asset.flattenedTracks.SelectMany(x => x.clips);
            return clips.FirstOrDefault(x => x != null && x.asset != null && target == (x.asset as IPlayableAsset));
        }

        internal static bool IsRecording(TimelineClip clip, WindowState state)
        {
            return clip != null &&
                clip.parentTrack != null &&
                state.IsArmedForRecord(clip.parentTrack);
        }

        internal static bool ProcessPlayableAssetRecording(UndoPropertyModification mod, WindowState state, TimelineClip clip)
        {
            if (mod.currentValue == null)
                return false;

            if (!clip.IsParameterAnimatable(mod.currentValue.propertyPath))
                return false;

            // don't use time global to local since it will possibly loop.
            double localTime = clip.ToLocalTimeUnbound(state.editSequence.time);
            if (localTime < 0)
                return false;

            // grab the value from the current modification
            float fValue = 0;
            if (!float.TryParse(mod.currentValue.value, out fValue))
            {
                // case 916913 -- 'Add Key' menu item will passes 'True' or 'False' (instead of 1, 0)
                // so we need a special case to parse the boolean string
                bool bValue = false;
                if (!bool.TryParse(mod.currentValue.value, out bValue))
                {
                    Debug.Assert(false, "Invalid type in PlayableAsset recording");
                    return false;
                }

                fValue = bValue ? 1 : 0;
            }

            bool added = (clip.AddAnimatedParameterValueAt(mod.currentValue.propertyPath, fValue, (float)localTime));
            if (added && AnimationMode.InAnimationMode())
            {
                EditorCurveBinding binding = clip.GetCurveBinding(mod.previousValue.propertyPath);
                AnimationMode.AddPropertyModification(binding, mod.previousValue, true);
                clip.parentTrack.SetShowInlineCurves(true);
                if (state.GetWindow() != null && state.GetWindow().treeView != null)
                    state.GetWindow().treeView.CalculateRowRects();
            }
            return added;
        }

        static bool IsPlayableAssetProperty(SerializedProperty property)
        {
            return (property.serializedObject.targetObject as IPlayableAsset) != null;
        }
    }
}
