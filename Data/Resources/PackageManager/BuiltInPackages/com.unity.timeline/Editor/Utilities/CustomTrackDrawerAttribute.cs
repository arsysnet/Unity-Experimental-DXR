using System;

namespace UnityEditor.Timeline
{
    // Tells a custom [[TrackDrawer]] which [[TrackAsset]] it's a drawer for.
    sealed class CustomTrackDrawerAttribute : Attribute
    {
        public Type assetType;
        public CustomTrackDrawerAttribute(Type type)
        {
            assetType = type;
        }
    }
}
