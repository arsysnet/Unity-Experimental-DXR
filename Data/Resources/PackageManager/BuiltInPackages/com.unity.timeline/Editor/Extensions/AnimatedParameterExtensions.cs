using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace UnityEditor.Timeline
{
    static class AnimatedParameterExtensions
    {
        static SerializedObject s_CachedObject;

        static SerializedObject GetSerializedObject(TimelineClip clip)
        {
            if (clip == null)
                return null;

            var asset = clip.asset as IPlayableAsset;
            if (asset == null)
                return null;

            var scriptObject = clip.asset as ScriptableObject;
            if (scriptObject == null)
                return null;

            if (s_CachedObject == null || s_CachedObject.targetObject != clip.asset)
            {
                s_CachedObject = new SerializedObject(scriptObject);
            }

            return s_CachedObject;
        }

        static bool IsKeyable(Type t, string parameterName)
        {
            string basemember = parameterName;
            int index = parameterName.IndexOf('.');
            if (index > 0)
                basemember = parameterName.Substring(0, index);

            // Public | NonPublic doesn't return the field properly
            var field = t.GetField(basemember) ?? t.GetField(basemember, BindingFlags.Instance | BindingFlags.NonPublic);

            // if we can't find the field, treat it as keyable
            return field == null || !field.IsDefined(typeof(NotKeyableAttribute), true);
        }

        public static bool IsAnimatable(SerializedPropertyType t)
        {
            switch (t)
            {
                // Integer not currently supported
                //case SerializedPropertyType.Integer:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.Vector4:
                    // Integers don't work with the animated property system right now
                    // case SerializedPropertyType.Integer:
                    return true;
            }
            return false;
        }

        static bool MatchBinding(EditorCurveBinding binding, string parameterName)
        {
            if (binding.propertyName == parameterName)
                return true;

            var indexOfDot = binding.propertyName.IndexOf('.');
            return indexOfDot > 0 && parameterName.Length == indexOfDot && binding.propertyName.StartsWith(parameterName);
        }

        public static bool HasAnyAnimatableParameters(this TimelineClip clip)
        {
            if (clip.asset == null || Attribute.IsDefined(clip.asset.GetType(), typeof(NotKeyableAttribute)))
                return false;

            if (!clip.HasScriptPlayable())
                return false;

            var serializedObject = GetSerializedObject(clip);
            if (serializedObject == null)
                return false;

            var prop = serializedObject.GetIterator();
            var expand = true;
            bool isRootAnimatable = clip.asset is IPlayableBehaviour;
            bool hasAnimatable = false;
            while (prop.NextVisible(expand))
            {
                if (IsAnimatable(prop.propertyType) && IsKeyable(clip.asset.GetType(), prop.propertyPath))
                {
                    hasAnimatable |= isRootAnimatable || IsAnimatablePath(clip, prop.propertyPath);
                }
            }

            return hasAnimatable;
        }

        public static bool IsParameterAnimatable(this TimelineClip clip, string parameterName)
        {
            if (clip.asset == null || Attribute.IsDefined(clip.asset.GetType(), typeof(NotKeyableAttribute)))
                return false;

            if (!clip.HasScriptPlayable())
                return false;

            var serializedObject = GetSerializedObject(clip);
            if (serializedObject == null)
                return false;

            bool isRootAnimatable = clip.asset is IPlayableBehaviour;
            var prop = serializedObject.FindProperty(parameterName);
            if (prop != null && IsAnimatable(prop.propertyType) && IsKeyable(clip.asset.GetType(), parameterName))
            {
                return isRootAnimatable || IsAnimatablePath(clip, prop.propertyPath);
            }
            return false;
        }

        public static bool IsParameterAnimated(this TimelineClip clip, string parameterName)
        {
            if (clip == null)
                return false;

            if (clip.curves == null)
                return false;

            var binding = GetCurveBinding(clip, parameterName);
            var bindings = AnimationClipCurveCache.Instance.GetCurveInfo(clip.curves).bindings;
            return bindings.Any(x => MatchBinding(x, binding.propertyName));
        }

        // get an animatable curve binding for this parameter
        public static EditorCurveBinding GetCurveBinding(this TimelineClip clip, string parameterName)
        {
            string animationName = GetAnimatedParameterBindingName(clip, parameterName);
            return EditorCurveBinding.FloatCurve(string.Empty, GetAnimationType(clip), animationName);
        }

        static Type GetAnimationType(TimelineClip clip)
        {
            if (clip != null && clip.asset != null && clip.asset is Object)
            {
                return clip.asset.GetType();
            }
            // the animated type must be a non-abstract instantiable object.
            return typeof(TimelineAsset);
        }

        static string GetAnimatedParameterBindingName(TimelineClip clip, string parameterName)
        {
            if (clip == null || clip.asset == null || clip.asset is IPlayableBehaviour)
                return parameterName;

            // strip the IScript playable field name
            var fields = GetScriptPlayableFields(clip.asset as IPlayableAsset);
            foreach (var f in fields)
            {
                if (parameterName.StartsWith(f.Name))
                {
                    if (parameterName.Length > f.Name.Length && parameterName[f.Name.Length] == '.')
                        return parameterName.Substring(f.Name.Length + 1);
                }
            }

            return parameterName;
        }

        public static bool AddAnimatedParameterValueAt(this TimelineClip clip, string parameterName, float value, float time)
        {
            if (!IsParameterAnimatable(clip, parameterName))
                return false;

            // strip the prefix for embedded parameters

            CreateCurvesIfRequired(clip);
            EditorCurveBinding binding = GetCurveBinding(clip, parameterName);
            var curve = AnimationUtility.GetEditorCurve(clip.curves, binding) ?? new AnimationCurve();

            var serializedObject = GetSerializedObject(clip);
            var property = serializedObject.FindProperty(parameterName);

            bool isStepped = property.propertyType == SerializedPropertyType.Boolean ||
                property.propertyType == SerializedPropertyType.Integer ||
                property.propertyType == SerializedPropertyType.Enum;

            CurveEditUtility.AddKeyFrameToCurve(curve, time, clip.curves.frameRate, value, isStepped);
            AnimationUtility.SetEditorCurve(clip.curves, binding, curve);

            return true;
        }

        internal static void CreateCurvesIfRequired(TimelineClip clip, Object owner = null)
        {
            if (owner == null)
            {
                owner = clip.parentTrack;
            }

            if (clip.curves == null)
            {
                if (owner == clip.parentTrack)
                    clip.CreateCurves(AnimationTrackRecorder.GetUniqueRecordedClipName(owner, TimelineClip.kDefaultCurvesName));
                else
                    CreateCurvesOnDifferentOwner(clip, owner);
            }
        }

        static void CreateCurvesOnDifferentOwner(TimelineClip clip, Object owner)
        {
            clip.curves = new AnimationClip
            {
                legacy = true,
                name = AnimationTrackRecorder.GetUniqueRecordedClipName(owner, TimelineClip.kDefaultCurvesName)
            };

            var assetPath = AssetDatabase.GetAssetPath(owner);
            if (!string.IsNullOrEmpty(assetPath))
            {
                TimelineHelpers.SaveAnimClipIntoObject(clip.curves, owner);
                EditorUtility.SetDirty(owner);
                AssetDatabase.ImportAsset(assetPath);
                AssetDatabase.Refresh();
            }
        }

        static bool InternalAddParameter(TimelineClip clip, string parameterName, ref EditorCurveBinding binding, out SerializedProperty property)
        {
            property = null;

            if (IsParameterAnimated(clip, parameterName))
                return false;

            var serializedObject = GetSerializedObject(clip);
            if (serializedObject == null)
                return false;

            property = serializedObject.FindProperty(parameterName);
            if (property == null || !IsAnimatable(property.propertyType))
                return false;

            CreateCurvesIfRequired(clip);
            binding = GetCurveBinding(clip, parameterName);
            return true;
        }

        public static bool AddAnimatedParameter(this TimelineClip clip, string parameterName)
        {
            EditorCurveBinding newBinding = new EditorCurveBinding();
            SerializedProperty property;
            if (!InternalAddParameter(clip, parameterName, ref newBinding, out property))
                return false;
            var duration = (float)clip.duration;
            CurveEditUtility.AddKey(clip.curves, newBinding, property, 0);
            CurveEditUtility.AddKey(clip.curves, newBinding, property, duration);
            return true;
        }

        public static bool RemoveAnimatedParameter(this TimelineClip clip, string parameterName)
        {
            if (!IsParameterAnimated(clip, parameterName) || clip.curves == null)
                return false;

            var binding = GetCurveBinding(clip, parameterName);
            AnimationUtility.SetEditorCurve(clip.curves, binding, null);
            return true;
        }

        // Retrieve an animated parameter curve. parameter name is required to include the appropriate field for
        // vectors
        //  'e.g. position
        public static AnimationCurve GetAnimatedParameter(this TimelineClip clip, string parameterName)
        {
            if (clip == null || clip.curves == null)
                return null;

            var asset = clip.asset as ScriptableObject;
            if (asset == null)
                return null;

            var binding = GetCurveBinding(clip, parameterName);
            return AnimationUtility.GetEditorCurve(clip.curves, binding);
        }

        // Set an animated parameter. Requires the field identifier 'position.x', but will add default curves to all fields
        public static bool SetAnimatedParameter(this TimelineClip clip, string parameterName, AnimationCurve curve)
        {
            // this will add a basic curve for all the related parameters
            if (!IsParameterAnimated(clip, parameterName) && !AddAnimatedParameter(clip, parameterName))
                return false;

            var binding = GetCurveBinding(clip, parameterName);
            AnimationUtility.SetEditorCurve(clip.curves, binding, curve);
            return true;
        }

        internal static bool HasScriptPlayable(this TimelineClip clip)
        {
            if (clip.asset == null)
                return false;

            IPlayableBehaviour scriptPlayable = clip.asset as IPlayableBehaviour;
            if (scriptPlayable != null)
                return true;

            return GetScriptPlayableFields(clip.asset as IPlayableAsset).Any();
        }

        internal static bool IsAnimatablePath(this TimelineClip clip, string path)
        {
            if (clip.asset == null)
                return false;

            return GetScriptPlayableFields(clip.asset as IPlayableAsset).Any(
                f => path.StartsWith(f.Name) &&
                path.Length > f.Name.Length &&
                path[f.Name.Length] == '.'
            );
        }

        internal static IEnumerable<FieldInfo> GetScriptPlayableFields(IPlayableAsset asset)
        {
            if (asset == null)
                return new FieldInfo[0];

            return asset.GetType().GetFields().Where(f => f.IsPublic && !f.IsStatic && typeof(IPlayableBehaviour).IsAssignableFrom(f.FieldType));
        }
    }
}
