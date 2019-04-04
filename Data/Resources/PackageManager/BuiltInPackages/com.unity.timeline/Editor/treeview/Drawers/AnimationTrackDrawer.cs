using System;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    [CustomTrackDrawer(typeof(AnimationTrack))]
    class AnimationTrackDrawer : TrackDrawer
    {
        internal static class Styles
        {
            public static readonly GUIContent AnimationButtonOnTooltip = EditorGUIUtility.TrTextContent("", "Avatar Mask enabled\nClick to disable");
            public static readonly GUIContent AnimationButtonOffTooltip = EditorGUIUtility.TrTextContent("", "Avatar Mask disabled\nClick to enable");
            public static readonly string NoClipAssignedError = L10n.Tr("No animation clip assigned");
            public static readonly string LegacyClipError = L10n.Tr("Legacy animation clips are not supported");
            public static readonly string MotionCurveError = L10n.Tr("You are using motion curves without applyRootMotion enabled on the Animator. The root transform will not be animated");
            public static readonly string RootCurveError = L10n.Tr("You are using root curves without applyRootMotion enabled on the Animator. The root transform will not be animated");
            public static readonly GUIContent ConvertToInfiniteClipMenuItem = EditorGUIUtility.TrTextContent("Convert to Infinite Clip");
            public static readonly GUIContent ConvertToClipTrackMenuItem = EditorGUIUtility.TrTextContent("Convert To Clip Track");
            public static readonly GUIContent AddOverrideTrackMenuItem = EditorGUIUtility.TrTextContent("Add Override Track");

            public static readonly Texture2D s_IconNoRecord = EditorGUIUtility.LoadIcon("console.erroricon.sml");
            public static readonly GUIContent s_ClipNotRecorable = EditorGUIUtility.TrTextContent("", "This clip is not recordable");
            public static readonly GUIContent s_ClipNoRecordInBlend = EditorGUIUtility.TrTextContent("", "Recording in blends in prohibited");

            public static readonly string TrackOffsetMenuPrefix = L10n.Tr("Track Offsets/");
        }

        protected override Color GetClipBaseColor(TimelineClip clip)
        {
            if (clip.recordable)
            {
                return DirectorStyles.Instance.customSkin.colorAnimationRecorded;
            }
            return DirectorStyles.Instance.customSkin.colorAnimation;
        }

        public override void OnBuildTrackContextMenu(GenericMenu menu, TrackAsset track, WindowState state)
        {
            var animTrack = track as AnimationTrack;
            if (animTrack == null)
            {
                base.OnBuildTrackContextMenu(menu, track, state);
                return;
            }

            if (animTrack.CanConvertFromClipMode() || animTrack.CanConvertToClipMode())
            {
                var canConvertToInfinite = animTrack.CanConvertFromClipMode();
                var canConvertToClip = animTrack.CanConvertToClipMode();

                if (canConvertToInfinite)
                {
                    if (track.lockedInHierarchy)
                    {
                        menu.AddDisabledItem(Styles.ConvertToInfiniteClipMenuItem, false);
                    }
                    else
                    {
                        menu.AddItem(Styles.ConvertToInfiniteClipMenuItem, false, parentTrack =>
                        {
                            animTrack.ConvertFromClipMode(state.editSequence.asset);
                            state.Refresh();
                        }, track);
                    }
                }

                if (canConvertToClip)
                {
                    if (track.lockedInHierarchy)
                    {
                        menu.AddDisabledItem(Styles.ConvertToClipTrackMenuItem, false);
                    }
                    else
                    {
                        menu.AddItem(Styles.ConvertToClipTrackMenuItem, false, parentTrack =>
                        {
                            animTrack.ConvertToClipMode();
                            state.Refresh();
                        }, track);
                    }
                }
            }

            if (!track.isSubTrack)
            {
                var items = Enum.GetValues(typeof(TrackOffset));
                foreach (var i in items)
                {
                    var item = (TrackOffset)i;
                    menu.AddItem(
                        new GUIContent(Styles.TrackOffsetMenuPrefix + TypeUtility.GetMenuItemName(item)),
                        animTrack.trackOffset == item,
                        () =>
                        {
                            animTrack.trackOffset = item;
                            state.UnarmForRecord(animTrack);
                            state.rebuildGraph = true;
                        }
                    );
                }
            }

            base.OnBuildTrackContextMenu(menu, track, state);

            if (!track.isSubTrack)
            {
                menu.AddSeparator(string.Empty);
                if (track.lockedInHierarchy)
                {
                    menu.AddDisabledItem(Styles.AddOverrideTrackMenuItem, false);
                }
                else
                {
                    menu.AddItem(Styles.AddOverrideTrackMenuItem, false, parentTrack =>
                    {
                        AddSubTrack(state, typeof(AnimationTrack), "Override " + track.GetChildTracks().Count().ToString(), track);
                    }, track);
                }
            }
        }

        static void AddSubTrack(WindowState state, Type trackOfType, string trackName, TrackAsset track)
        {
            var subAnimationTrack = state.editSequence.asset.CreateTrack(trackOfType, track, trackName);
            TimelineCreateUtilities.SaveAssetIntoObject(subAnimationTrack, track);
            track.SetCollapsed(false);
            state.Refresh();
        }

        protected override string GetErrorText(TimelineClip clip)
        {
            var animationAsset = clip.asset as AnimationPlayableAsset;
            if (animationAsset != null)
            {
                if (animationAsset.clip == null)
                    return Styles.NoClipAssignedError;
                if (animationAsset.clip.legacy)
                    return Styles.LegacyClipError;
                if (animationAsset.clip.hasMotionCurves || animationAsset.clip.hasRootCurves)
                {
                    var animationTrack = clip.parentTrack as AnimationTrack;
                    if (animationTrack != null && animationTrack.trackOffset == TrackOffset.Auto)
                    {
                        var animator = animationTrack.GetBinding(TimelineEditor.inspectedDirector);
                        if (animator != null && !animator.applyRootMotion && !animationAsset.clip.hasGenericRootTransform)
                        {
                            if (animationAsset.clip.hasMotionCurves)
                                return Styles.MotionCurveError;
                            return Styles.RootCurveError;
                        }
                    }
                }
            }

            return base.GetErrorText(clip);
        }

        public override void OnBuildClipContextMenu(GenericMenu menu, TimelineClip[] clips, WindowState state)
        {
            AnimationOffsetMenu.OnClipMenu(state, clips, menu);
        }

        public override bool DrawTrackHeaderButton(Rect rect, TrackAsset track, WindowState state)
        {
            var animTrack = track as AnimationTrack;
            bool hasAvatarMask = animTrack != null && animTrack.avatarMask != null;
            if (hasAvatarMask)
            {
                var style = animTrack.applyAvatarMask
                    ? DirectorStyles.Instance.avatarMaskOn
                    : DirectorStyles.Instance.avatarMaskOff;
                var tooltip = animTrack.applyAvatarMask
                    ? Styles.AnimationButtonOnTooltip
                    : Styles.AnimationButtonOffTooltip;
                if (GUI.Button(rect, tooltip, style))
                {
                    animTrack.applyAvatarMask = !animTrack.applyAvatarMask;
                    if (state != null)
                        state.rebuildGraph = true;
                }
            }
            return hasAvatarMask;
        }

        public override void DrawClip(ClipDrawData drawData)
        {
            base.DrawClip(drawData);

            var state = (WindowState)drawData.state;
            if (drawData.state.recording && state.IsArmedForRecord(drawData.clip.parentTrack))
            {
                DrawAnimationRecordBorder(drawData);
                DrawRecordProhibited(drawData);
            }
        }

        public override void DrawRecordingBackground(Rect trackRect, TrackAsset trackAsset, Vector2 visibleTime, WindowState state)
        {
            base.DrawRecordingBackground(trackRect, trackAsset, visibleTime, state);
            DrawBorderOfAddedRecordingClip(trackRect, trackAsset, visibleTime, (WindowState)state);
        }

        void DrawAnimationRecordBorder(ClipDrawData drawData)
        {
            if (!drawData.clip.parentTrack.IsRecordingToClip(drawData.clip))
                return;

            if (drawData.state.editSequence.time < drawData.clip.start + drawData.clip.mixInDuration || drawData.state.editSequence.time > drawData.clip.end - drawData.clip.mixOutDuration)
                return;

            ClipDrawer.DrawBorder(drawData.clipCenterSection, ClipBorder.kRecording, ClipBlends.kNone);
        }

        void DrawRecordProhibited(ClipDrawData drawData)
        {
            DrawRecordInvalidClip(drawData);
            DrawRecordOnBlend(drawData);
        }

        void DrawRecordOnBlend(ClipDrawData drawData)
        {
            double time = drawData.state.editSequence.time;
            if (time >= drawData.clip.start && time < drawData.clip.start + drawData.clip.mixInDuration)
            {
                Rect r = Rect.MinMaxRect(drawData.clippedRect.xMin, drawData.clippedRect.yMin, drawData.clipCenterSection.xMin, drawData.clippedRect.yMax);
                DrawInvalidRecordIcon(r, Styles.s_ClipNoRecordInBlend);
            }

            if (time <= drawData.clip.end && time > drawData.clip.end - drawData.clip.mixOutDuration)
            {
                Rect r = Rect.MinMaxRect(drawData.clipCenterSection.xMax, drawData.clippedRect.yMin, drawData.clippedRect.xMax, drawData.clippedRect.yMax);
                DrawInvalidRecordIcon(r, Styles.s_ClipNoRecordInBlend);
            }
        }

        void DrawRecordInvalidClip(ClipDrawData drawData)
        {
            if (drawData.clip.recordable)
                return;

            if (drawData.state.editSequence.time < drawData.clip.start + drawData.clip.mixInDuration || drawData.state.editSequence.time > drawData.clip.end - drawData.clip.mixOutDuration)
                return;

            DrawInvalidRecordIcon(drawData.clipCenterSection, Styles.s_ClipNotRecorable);
        }

        void DrawInvalidRecordIcon(Rect rect, GUIContent helpText)
        {
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.30f));

            var icon = Styles.s_IconNoRecord;
            if (rect.width < icon.width || rect.height < icon.height)
                return;

            float x = rect.x + (rect.width - icon.width) * 0.5f;
            float y = rect.y + (rect.height - icon.height) * 0.5f;
            Rect r = new Rect(x, y, icon.width, icon.height);
            GUI.Label(r, helpText);
            GUI.DrawTexture(r, icon, ScaleMode.ScaleAndCrop, true, 0, Color.white, 0, 0);
        }

        void DrawBorderOfAddedRecordingClip(Rect trackRect, TrackAsset trackAsset, Vector2 visibleTime, WindowState state)
        {
            if (!state.IsArmedForRecord(trackAsset))
                return;

            AnimationTrack animTrack = trackAsset as AnimationTrack;
            if (animTrack == null || !animTrack.inClipMode)
                return;

            // make sure there is no clip but we can add one
            TimelineClip clip = null;
            if (track.FindRecordingClipAtTime(state.editSequence.time, out clip) || clip != null)
                return;

            float yMax = trackRect.yMax;
            float yMin = trackRect.yMin;

            double startGap = 0;
            double endGap = 0;

            trackAsset.GetGapAtTime(state.editSequence.time, out startGap, out endGap);
            if (double.IsInfinity(endGap))
                endGap = visibleTime.y;

            if (startGap > visibleTime.y || endGap < visibleTime.x)
                return;


            startGap = Math.Max(startGap, visibleTime.x);
            endGap = Math.Min(endGap, visibleTime.y);

            float xMin = state.TimeToPixel(startGap);
            float xMax = state.TimeToPixel(endGap);

            Rect r = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            ClipDrawer.DrawBorder(r, ClipBorder.kRecording, ClipBlends.kNone);
        }
    }
}
