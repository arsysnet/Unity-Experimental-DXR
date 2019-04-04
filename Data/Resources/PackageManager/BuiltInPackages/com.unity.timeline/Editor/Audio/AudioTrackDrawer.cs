using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

namespace UnityEngine.Timeline
{
    [CustomTrackDrawer(typeof(AudioTrack))]
    class AudioTrackDrawer : TrackDrawer
    {
        public static readonly string NoClipAssignedError = "No audio clip assigned";

        readonly Dictionary<TimelineClip, WaveformPreview> k_PersistentPreviews = new Dictionary<TimelineClip, WaveformPreview>();

        ColorSpace m_ColorSpace = ColorSpace.Uninitialized;

        protected override string GetErrorText(TimelineClip clip)
        {
            var audioAsset = clip.asset as AudioPlayableAsset;
            if (audioAsset != null)
            {
                if (audioAsset.clip == null)
                    return NoClipAssignedError;
            }
            return base.GetErrorText(clip);
        }

        static Color GammaCorrect(Color color)
        {
            return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color.gamma : color;
        }

        public override void DrawCustomClipBody(ClipDrawData drawData, Rect rect)
        {
            if (!drawData.state.showAudioWaveform)
                return;

            if (rect.width <= 0)
                return;

            var clip = drawData.clip;
            AudioClip audioClip = clip.asset as AudioClip;

            if (audioClip == null)
            {
                var audioPlayableAsset = drawData.clip.asset as AudioPlayableAsset;

                if (audioPlayableAsset != null)
                {
                    audioClip = audioPlayableAsset.clip;
                }

                if (audioClip == null)
                {
                    return;
                }
            }

            var quantizedRect = new Rect(Mathf.Ceil(rect.x), Mathf.Ceil(rect.y), Mathf.Ceil(rect.width), Mathf.Ceil(rect.height));
            WaveformPreview preview;

            if (QualitySettings.activeColorSpace != m_ColorSpace)
            {
                m_ColorSpace = QualitySettings.activeColorSpace;
                k_PersistentPreviews.Clear();
            }

            if (!k_PersistentPreviews.TryGetValue(drawData.clip, out preview) || audioClip != preview.presentedObject)
            {
                preview = k_PersistentPreviews[drawData.clip] = WaveformPreviewFactory.Create((int)quantizedRect.width, audioClip);
                Color waveColour = GammaCorrect(DirectorStyles.Instance.customSkin.colorAudioWaveform);
                Color transparent = waveColour;
                transparent.a = 0;
                preview.backgroundColor = transparent;
                preview.waveColor = waveColour;
                preview.SetChannelMode(WaveformPreview.ChannelMode.MonoSum);
                preview.updated += drawData.state.editorWindow.Repaint;
            }

            preview.looping = drawData.clip.SupportsLooping();
            preview.SetTimeInfo(drawData.localVisibleStartTime, drawData.localVisibleEndTime - drawData.localVisibleStartTime);
            preview.OptimizeForSize(quantizedRect.size);

            if (Event.current.type == EventType.Repaint)
            {
                preview.ApplyModifications();

                preview.Render(quantizedRect);
            }
        }

#if ENABLE_AUDIO_DSP_PLAYABLES

        internal static TrackType AudioDeckTrackType = new TrackType(typeof(AudioDeckTrack), TimelineAsset.MediaType.Audio);

        public override void OnBuildTrackContextMenu(GenericMenu menu, TrackAsset track, UnityEditor.Timeline.WindowState state)
        {
            base.OnBuildTrackContextMenu(menu, track, state);

            menu.AddItem(EditorGUIUtility.TrTextContent("Add Nested Audio Track"), false, (parentTrack) =>
            {
                AddSubTrack(state, typeof(AudioTrack), "nested track " + track.GetChildTracks().Count().ToString(), track);
            },
                track);
            menu.AddItem(EditorGUIUtility.TrTextContent("Add Effects Track"), false, (parentTrack) =>
            {
                AddSubTrack(state, typeof(AudioDeckTrack), "Effects" + track.GetChildTracks().Count().ToString(), track);
            },
                track);
        }

        private void AddSubTrack(WindowState state, Type trackOfType, string trackName, TrackAsset track)
        {
            track.timelineAsset.CreateTrack(trackOfType, track, trackName);
            track.collapsed = false;
            state.Refresh();
        }

        public override void DrawClip(ClipDrawData drawData)
        {
            var clip = drawData.clip;
            AudioClip audioClip = clip.asset as AudioClip;

            if (audioClip == null)
            {
                var audioPlayableAsset = drawData.clip.asset as AudioPlayableAsset;

                if (audioPlayableAsset != null)
                {
                    audioClip = audioPlayableAsset.clip;
                }

                if (audioClip == null)
                {
                    base.DrawClip(drawData);
                    return;
                }
            }

            float textureWidth = Mathf.Max(Mathf.Pow(2, Mathf.Ceil(Mathf.Log(2.0f * drawData.targetRect.width) / Mathf.Log(2))), 4096.0f);

            if (textureWidth < 1.0f)
            {
                base.DrawClip(drawData);
                return;
            }

            // @todo wayne: GetWaveFormFast removed??
            //Texture2D previewTexture = AudioUtil.GetWaveFormFast(audioClip, audioClip.channels,(int)((float)audioClip.frequency * (float)clip.clipIn), audioClip.samples, textureWidth, 512.0f);
            Texture2D previewTexture = null;
            if (previewTexture == null)
            {
                base.DrawClip(drawData);
                return;
            }

            Rect overlayRect = drawData.targetRect;
            overlayRect.y += 2;
            overlayRect.height -= 4;

            var audioAsset = clip.asset as AudioPlayableAsset;
            var audioDuration = audioAsset.duration;
            if (audioDuration > 0)
            {
                float uvTime = (float)(clip.clipIn / audioDuration);
                Rect uvRect = new Rect(uvTime, 0, 1 - uvTime, 1);
                GUI.DrawTextureWithTexCoords(overlayRect, previewTexture, uvRect);
            }
            base.DrawClip(drawData);
        }

#endif
    }
}
