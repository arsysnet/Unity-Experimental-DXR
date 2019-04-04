using System.Text;
using UnityEngine.Playables;

namespace UnityEditor.Timeline
{
    static class DisplayNameHelper
    {
        static readonly string k_NoAssetDisplayName = L10n.Tr("<No Asset>");
        static readonly StringBuilder k_StringBuilder = new StringBuilder();

        public static string GetDisplayName(ISequenceState sequence)
        {
            return sequence.director != null ? GetDisplayName(sequence.director) : GetDisplayName(sequence.asset);
        }

        public static string GetDisplayName(PlayableAsset asset)
        {
            return asset != null ? asset.name : k_NoAssetDisplayName;
        }

        public static string GetDisplayName(PlayableDirector director)
        {
            k_StringBuilder.Length = 0;
            k_StringBuilder.Append(GetDisplayName(director.playableAsset));
            k_StringBuilder.Append(" (").Append(director.name).Append(')');
            return k_StringBuilder.ToString();
        }
    }
}
