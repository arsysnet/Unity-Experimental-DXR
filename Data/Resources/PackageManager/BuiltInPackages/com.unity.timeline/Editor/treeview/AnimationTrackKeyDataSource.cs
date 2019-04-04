using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine.Timeline;

// Data sources for key overlays
namespace UnityEditor.Timeline
{
    // Used for key overlays manipulators
    class AnimationTrackKeyDataSource : IPropertyKeyDataSource
    {
        readonly AnimationTrack m_Track;

        public AnimationTrackKeyDataSource(AnimationTrack track)
        {
            m_Track = track;
        }

        public float[] GetKeys()
        {
            if (m_Track == null || m_Track.infiniteClip == null)
                return null;
            var info = AnimationClipCurveCache.Instance.GetCurveInfo(m_Track.infiniteClip);
            return info.keyTimes.Select(x => x + (float)m_Track.infiniteClipTimeOffset).ToArray();
        }

        public Dictionary<float, string> GetDescriptions()
        {
            var map = new Dictionary<float, string>();
            var info = AnimationClipCurveCache.Instance.GetCurveInfo(m_Track.infiniteClip);
            var processed = new HashSet<string>();

            foreach (var b in info.bindings)
            {
                var groupID = b.GetGroupID();
                if (processed.Contains(groupID))
                    continue;

                var group = info.GetGroupBinding(groupID);
                var prefix = AnimationWindowUtility.GetNicePropertyGroupDisplayName(b.type, b.propertyName);

                foreach (var t in info.keyTimes)
                {
                    var key = t + (float)m_Track.infiniteClipTimeOffset;
                    var result = prefix + " : " + group.GetDescription(key);
                    if (map.ContainsKey(key))
                        map[key] += '\n' + result;
                    else
                        map.Add(key, result);
                }
                processed.Add(groupID);
            }
            return map;
        }
    }
}
