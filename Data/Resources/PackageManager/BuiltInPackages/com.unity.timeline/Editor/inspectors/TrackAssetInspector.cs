//#define PERF_PROFILE

using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    [CustomEditor(typeof(TrackAsset), true, isFallback = true)]
    [CanEditMultipleObjects]
    class TrackAssetInspector : Editor
    {
        SerializedProperty m_Name;
        bool m_IsBuiltInType;

        protected TimelineWindow timelineWindow
        {
            get
            {
                return TimelineWindow.instance;
            }
        }

        protected bool IsTrackLocked()
        {
            return targets.Any(track => ((TrackAsset)track).lockedInHierarchy);
        }

        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(IsTrackLocked()))
            {
                DrawInspector();
            }
        }

        internal override void OnHeaderTitleGUI(Rect titleRect, string header)
        {
            serializedObject.Update();

            Rect textFieldRect = titleRect;
            textFieldRect.height = 16f;

            EditorGUI.showMixedValue = m_Name.hasMultipleDifferentValues;

            TimelineWindow seqWindow = TimelineWindow.instance;

            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.DelayedTextField(textFieldRect, m_Name.stringValue, EditorStyles.textField);

            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(newName))
            {
                for (int c = 0; c < targets.Length; c++)
                {
                    ObjectNames.SetNameSmart(targets[c], newName);
                }

                if (seqWindow != null)
                    seqWindow.Repaint();
            }
            serializedObject.ApplyModifiedProperties();
        }

        internal override void OnHeaderIconGUI(Rect iconRect)
        {
            if (TimelineWindow.instance == null)
                return;

            //when selecting multiple track types, the default icon will appear by default
            //when selecting only one track type, this will display the track type icon
            TimelineTrackBaseGUI trackGui = TimelineWindow.instance.allTracks.Find((uiTrack => uiTrack.track == target as TrackAsset));

            if (trackGui != null)
                GUI.Label(iconRect, trackGui.drawer.icon);
        }

        internal override void DrawHeaderHelpAndSettingsGUI(Rect r)
        {
            var helpSize = EditorStyles.iconButton.CalcSize(EditorGUI.GUIContents.helpIcon);
            const int kTopMargin = 5;
            // Show Editor Header Items.
            EditorGUIUtility.DrawEditorHeaderItems(new Rect(r.xMax - helpSize.x, r.y + kTopMargin, helpSize.x, helpSize.y), targets);
        }

        public virtual void OnEnable()
        {
            m_IsBuiltInType = target != null && target.GetType().Assembly == typeof(TrackAsset).Assembly;
            m_Name = serializedObject.FindProperty("m_Name");
        }

        public virtual void OnDestroy()
        {
        }

        void DrawInspector()
        {
            if (serializedObject == null)
                return;

            using (var changeScope = new EditorGUI.ChangeCheckScope())
            {
                serializedObject.Update();

                DrawTrackProperties();

                if (changeScope.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    ApplyChanges();
                }
            }
        }

        protected virtual void DrawTrackProperties()
        {
            var property = serializedObject.GetIterator();
            var expanded = true;
            while (property.NextVisible(expanded))
            {
                // Don't draw script field for built-in types
                if (m_IsBuiltInType && "m_Script" == property.propertyPath)
                    continue;

                EditorGUILayout.PropertyField(property, !expanded);
                expanded = false;
            }
        }

        protected virtual void ApplyChanges()
        {
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }
    }
}
