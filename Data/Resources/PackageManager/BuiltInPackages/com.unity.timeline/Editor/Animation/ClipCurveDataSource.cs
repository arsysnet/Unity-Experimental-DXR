using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    abstract class CurveDataSource
    {
        readonly IRowGUI m_TrackGUI;

        protected CurveDataSource(IRowGUI trackGUI)
        {
            m_TrackGUI = trackGUI;
        }

        public Rect GetBackgroundRect(WindowState state)
        {
            var trackRect = m_TrackGUI.boundingRect;
            return new Rect(
                state.timeAreaTranslation.x + trackRect.xMin,
                trackRect.y,
                (float)state.editSequence.asset.duration * state.timeAreaScale.x,
                trackRect.height
            );
        }

        public abstract AnimationClip animationClip { get; }

        public abstract float start { get; }

        public abstract float timeScale { get; }
    }

    // Data source for drawing an inline clip from a GUIClip
    class TimelineClipCurveDataSource : CurveDataSource
    {
        readonly TimelineClipGUI m_ClipGUI;

        public TimelineClipCurveDataSource(TimelineClipGUI clipGUI) : base(clipGUI.parent)
        {
            m_ClipGUI = clipGUI;
        }

        public override AnimationClip animationClip
        {
            get { return m_ClipGUI.clip.animationClip ?? m_ClipGUI.clip.curves; }
        }

        public override float start
        {
            get { return (float)m_ClipGUI.clip.FromLocalTimeUnbound(0.0); }
        }

        public override float timeScale
        {
            get { return (float)m_ClipGUI.clip.timeScale; }
        }
    }

    // Data source for drawing an clip from an infinite clip
    class InfiniteClipCurveDataSource : CurveDataSource
    {
        readonly AnimationTrack m_AnimationTrack;

        public InfiniteClipCurveDataSource(TimelineTrackGUI trackGui) : base(trackGui)
        {
            m_AnimationTrack = trackGui.track as AnimationTrack;
        }

        public override AnimationClip animationClip
        {
            get { return m_AnimationTrack.infiniteClip; }
        }

        public override float start
        {
            get { return 0.0f; }
        }

        public override float timeScale
        {
            get { return 1.0f; }
        }
    }
}
