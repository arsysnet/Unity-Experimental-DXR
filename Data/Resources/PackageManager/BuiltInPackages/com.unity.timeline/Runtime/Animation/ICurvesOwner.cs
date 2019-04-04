namespace UnityEngine.Timeline
{
    interface ICurvesOwner
    {
        AnimationClip curves { get; }
        bool hasCurves { get; }
        void CreateCurves(string curvesClipName);
    }
}
