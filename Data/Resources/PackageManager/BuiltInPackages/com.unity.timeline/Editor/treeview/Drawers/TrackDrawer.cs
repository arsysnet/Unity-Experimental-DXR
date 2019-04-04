using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
namespace UnityEditor.Timeline
{
    class TrackDrawer : GUIDrawer
    {
        static readonly string k_BoundGameObjectDisabled = LocalizationDatabase.GetLocalizedString("The bound GameObject is disabled.");
        static readonly string k_NoValidComponent = LocalizationDatabase.GetLocalizedString("Could not find appropriate component on this gameObject");
        static readonly string k_RequiredComponentIsDisabled = LocalizationDatabase.GetLocalizedString("The component is disabled");
        static readonly string k_InvalidBinding = LocalizationDatabase.GetLocalizedString("The bound object is not the correct type.");
        static readonly string k_PrefabBound = LocalizationDatabase.GetLocalizedString("The bound object is a Prefab");


        public class TrackMenuContext
        {
            public enum ClipTimeCreation
            {
                TimeCursor,
                Mouse
            }

            public ClipTimeCreation clipTimeCreation = ClipTimeCreation.TimeCursor;
            public Vector2? mousePosition = null;
        }

        public float DefaultTrackHeight = -1.0f;
        public TrackMenuContext trackMenuContext = new TrackMenuContext();

        internal WindowState sequencerState { get; set; }


        public static TrackDrawer CreateInstance(TrackAsset trackAsset)
        {
            if (trackAsset == null)
                return Activator.CreateInstance<TrackDrawer>();

            TrackDrawer drawer;

            try
            {
                drawer = (TrackDrawer)Activator.CreateInstance(TimelineHelpers.GetCustomDrawer(trackAsset.GetType()));
            }
            catch (Exception)
            {
                drawer = Activator.CreateInstance<TrackDrawer>();
            }

            drawer.track = trackAsset;
            drawer.trackColor = TrackResourceCache.GetTrackColor(trackAsset);
            drawer.icon = TrackResourceCache.GetTrackIcon(trackAsset);
            return drawer;
        }

        protected TrackAsset track { get; private set; }

        public Color trackColor { get; private set; }
        public GUIContent icon { get; private set; }

        public virtual bool DrawTrackHeaderButton(Rect rect, TrackAsset track, WindowState state)
        {
            return false;
        }

        public virtual bool canDrawExtrapolationIcon
        {
            get { return true; }
        }

        public virtual float GetHeight(TrackAsset t)
        {
            return DefaultTrackHeight;
        }

        public virtual void OnBuildTrackContextMenu(GenericMenu menu, TrackAsset trackAsset, WindowState state)
        {
            var mousePosition = trackMenuContext.mousePosition;
            var candidateTime = TimelineHelpers.GetCandidateTime(state, mousePosition, trackAsset);

            SequencerContextMenu.AddClipMenuCommands(menu, trackAsset, candidateTime);
            SequencerContextMenu.AddMarkerMenuCommands(menu, trackAsset, candidateTime);
        }

        // Override this method for context menus on the clips on the same track
        public virtual void OnBuildClipContextMenu(GenericMenu menu, TimelineClip[] clips, WindowState state) {}

        public virtual bool DrawTrack(Rect trackRect, TrackAsset trackAsset, Vector2 visibleTime, WindowState state)
        {
            return false;
        }

        public virtual void DrawRecordingBackground(Rect trackRect, TrackAsset trackAsset, Vector2 visibleTime, WindowState state)
        {
            EditorGUI.DrawRect(trackRect, DirectorStyles.Instance.customSkin.colorTrackBackgroundRecording);
        }

        // used by subclasses to draw error icons
        protected virtual string GetErrorText(TimelineClip clip)
        {
            if (clip.asset == null)
                return ClipDrawer.Styles.NoPlayableAssetError;
            var playableAsset = clip.asset as ScriptableObject;
            if (playableAsset == null || MonoScript.FromScriptableObject(playableAsset) == null)
                return ClipDrawer.Styles.ScriptLoadError;
            return null;
        }

        public virtual void DrawCustomClipBody(ClipDrawData drawData, Rect centerRect)
        {
        }

        public void DrawGhostClip(TimelineClipGUI clipGUI, Rect targetRect)
        {
            DrawSimpleClip(clipGUI, targetRect, ClipBorder.kSelection, new Color(1.0f, 1.0f, 1.0f, 0.5f), GetClipDrawing(clipGUI.clip));
        }

        public void DrawInvalidClip(TimelineClipGUI clipGUI, Rect targetRect)
        {
            DrawSimpleClip(clipGUI, targetRect, ClipBorder.kSelection, DirectorStyles.Instance.customSkin.colorInvalidClipOverlay, GetClipDrawing(clipGUI.clip));
        }

        public static void DrawSimpleClip(TimelineClipGUI clip, Rect targetRect, ClipBorder border, Color overlay, ClipDrawing drawing)
        {
            ClipDrawer.DrawSimpleClip(clip.clip, targetRect, border, overlay, drawing, BlendsFromGUI(clip));
        }

        // TODO, move to clip data?
        public static ClipBlends BlendsFromGUI(TimelineClipGUI clipGUI)
        {
            return new ClipBlends(clipGUI.blendInKind, clipGUI.mixInRect, clipGUI.blendOutKind, clipGUI.mixOutRect);
        }

        // TODO move!
        ClipDrawing GetClipDrawing(TimelineClip clip)
        {
            return new ClipDrawing()
            {
                highlightColor = GetClipBaseColor(clip),
                errorText = GetErrorText(clip)
            };
        }

        protected virtual Color GetClipBaseColor(TimelineClip clip)
        {
            return trackColor;
        }

        public virtual void DrawClip(ClipDrawData drawData)
        {
            ClipDrawer.DrawDefaultClip(drawData, GetClipDrawing(drawData.clip), BlendsFromGUI(drawData.uiClip));
        }

        protected virtual string DerivedValidateBindingForTrack(PlayableDirector director,
            TrackAsset trackToValidate, PlayableBinding[] bindings)
        {
            return null;
        }

        internal string ValidateBindingForTrack(TrackAsset trackToValidate, PlayableDirector director, PlayableBinding[] bindings)
        {
            // no director means no binding
            if (director == null || trackToValidate == null || bindings == null || bindings.Length == 0)
                return null;


            var mainBinding = bindings.First();
            var boundObject = director.GetGenericBinding(bindings.First().sourceObject);
            if (boundObject != null && mainBinding.outputTargetType != null)
            {
                // bound to a prefab asset
                if (PrefabUtility.IsPartOfPrefabAsset(boundObject))
                {
                    return k_PrefabBound;
                }

                // If we are a component, allow for bound game objects (legacy)
                if (typeof(Component).IsAssignableFrom(mainBinding.outputTargetType))
                {
                    var gameObject = boundObject as GameObject;
                    var component = boundObject as Component;
                    if (component != null)
                        gameObject = component.gameObject;

                    // game object is bound with no component
                    if (gameObject != null && component == null)
                    {
                        component = gameObject.GetComponent(mainBinding.outputTargetType);
                        if (component == null)
                        {
                            return k_NoValidComponent;
                        }
                    }

                    // attached gameObject is disables (ignores Activation Track)
                    if (gameObject != null && !gameObject.activeInHierarchy)
                    {
                        return k_BoundGameObjectDisabled;
                    }

                    // component is disabled
                    var behaviour = component as Behaviour;
                    if (behaviour != null && !behaviour.enabled)
                    {
                        return k_RequiredComponentIsDisabled;
                    }

                    // mismatched binding
                    if (component != null && !mainBinding.outputTargetType.IsAssignableFrom(component.GetType()))
                    {
                        return k_InvalidBinding;
                    }
                }
                // Mismatched binding (non-component)
                else if (!mainBinding.outputTargetType.IsAssignableFrom(boundObject.GetType()))
                {
                    return k_InvalidBinding;
                }
            }

            return DerivedValidateBindingForTrack(director, trackToValidate, bindings);
        }
    }
}
