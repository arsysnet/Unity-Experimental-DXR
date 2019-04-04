using JetBrains.Annotations;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    [CustomTrackDrawer(typeof(MarkerTrack))]
    [UsedImplicitly]
    class MarkerTrackDrawer : TrackDrawer
    {
        public override float GetHeight(TrackAsset t)
        {
            return 20;
        }
    }
}
